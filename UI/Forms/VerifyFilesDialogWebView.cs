/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class VerifyFilesDialogWebView : Form
    {
        #region Types

        private sealed class VerifyCheck
        {
            public required int Index { get; init; }
            public required string Name { get; init; }
            public required Func<Task<(bool Passed, string Detail)>> Execute { get; init; }
        }

        #endregion

        #region Fields

        private WebView2? _webView;
        private bool _initialized;
        private bool _disposed;

        private readonly string _targetPath;
        private readonly DotaVersionService _versionService;
        private readonly Action? _onPatchRequested;

        #endregion

        #region P/Invoke (borderless drag)

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        #endregion

        #region Constructor

        public VerifyFilesDialogWebView(
            string targetPath,
            DotaVersionService versionService,
            Action? onPatchRequested = null)
        {
            _targetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
            _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
            _onPatchRequested = onPatchRequested;

            InitializeComponent();
            SetupForm();

            this.Shown += async (_, _) => await InitializeAndRunAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        #endregion

        #region Setup

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(460, 440);
            this.Name = "VerifyFilesDialogWebView";
            this.Text = "Verify Mod Files - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Theme.Canvas;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            this.Controls.Add(_webView);

            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    CloseWithResult(DialogResult.Cancel);
                    e.Handled = true;
                }
            };
        }

        #endregion

        #region WebView2 Lifecycle

        private async Task InitializeAndRunAsync()
        {
            if (_initialized || _disposed) return;

            var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
            await _webView!.EnsureCoreWebView2Async(env);
            Helpers.DpiLayout.PinTo100(this, _webView!);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var htmlPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "verify_files.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("verify_files.html not found", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));

            await WaitForNavigationAsync();

            if (_initialized || _disposed) return;
            _initialized = true;

            await RunAllChecksAsync();
        }

        private Task WaitForNavigationAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                _webView!.CoreWebView2.NavigationCompleted -= handler;
                if (e.IsSuccess)
                    tcs.SetResult(true);
                else
                    tcs.SetException(new InvalidOperationException(
                        $"WebView2 navigation failed: {e.WebErrorStatus}"));
            }
            _webView!.CoreWebView2.NavigationCompleted += handler;
            return tcs.Task;
        }

        #endregion

        #region Verification Checks

        private async Task RunAllChecksAsync()
        {
            var checks = BuildChecks();
            int passed = 0;

            foreach (var check in checks)
            {
                if (_disposed) return;

                await CallJsAsync($"startCheck({check.Index})");

                await Task.Delay(200);

                bool ok;
                string detail;
                try
                {
                    (ok, detail) = await check.Execute();
                }
                catch (Exception ex)
                {
                    ok = false;
                    detail = $"Error: {ex.Message}";
                }

                if (ok) passed++;

                string safeDetail = JsonSerializer.Serialize(detail);
                await CallJsAsync($"completeCheck({check.Index}, {(ok ? "true" : "false")}, {safeDetail})");

                await Task.Delay(150);
            }

            bool showPatch = _onPatchRequested != null && passed < checks.Count;
            await CallJsAsync($"allDone({passed}, {checks.Count}, {(showPatch ? "true" : "false")})");
        }

        private List<VerifyCheck> BuildChecks()
        {
            return new List<VerifyCheck>
            {
                new VerifyCheck
                {
                    Index = 0,
                    Name = "Mod Package",
                    Execute = async () =>
                    {
                        await Task.Yield();
                        string vpkPath = Path.Combine(_targetPath, DotaPaths.ModsVpk);
                        if (File.Exists(vpkPath))
                        {
                            var info = new FileInfo(vpkPath);
                            string size = FormatFileSize(info.Length);
                            return (true, $"pak01_dir.vpk ({size})");
                        }
                        return (false, "VPK not found — install modspack first");
                    }
                },

                new VerifyCheck
                {
                    Index = 1,
                    Name = "Dota Version",
                    Execute = async () =>
                    {
                        string steamInfPath = Path.Combine(_targetPath, DotaPaths.SteamInf);
                        if (!File.Exists(steamInfPath))
                            return (false, "steam.inf not found — Dota 2 path invalid");

                        string steamInf = await File.ReadAllTextAsync(steamInfPath);

                        var dateMatch = System.Text.RegularExpressions.Regex.Match(
                            steamInf, @"VersionDate=(.+)");
                        var buildMatch = System.Text.RegularExpressions.Regex.Match(
                            steamInf, @"ClientVersion=(\d+)");

                        string currentVersion = dateMatch.Success
                            ? dateMatch.Groups[1].Value.Trim() : "Unknown";
                        string currentBuild = buildMatch.Success
                            ? buildMatch.Groups[1].Value : "Unknown";

                        string versionJsonPath = Path.Combine(_targetPath, DotaPaths.VersionJson);

                        if (File.Exists(versionJsonPath))
                        {
                            try
                            {
                                string json = await File.ReadAllTextAsync(versionJsonPath);
                                using var doc = System.Text.Json.JsonDocument.Parse(json);
                                var root = doc.RootElement;

                                string patchedVer = root.TryGetProperty("VersionDate", out var v)
                                    ? v.GetString() ?? "" : "";
                                string patchedBuild = root.TryGetProperty("Build", out var b)
                                    ? b.GetString() ?? "" : "";

                                bool matches = currentVersion == patchedVer
                                    && currentBuild == patchedBuild;

                                if (matches)
                                    return (true, $"{currentVersion} (Build {currentBuild})");

                                return (false,
                                    $"Dota updated: {currentVersion} ≠ patched {patchedVer}");
                            }
                            catch
                            {
                            }
                        }

                        bool vpkExists = File.Exists(
                            Path.Combine(_targetPath, DotaPaths.ModsVpk));
                        string giPath = Path.Combine(
                            _targetPath, "game", "dota", "gameinfo_branchspecific.gi");
                        bool giPatched = File.Exists(giPath)
                            && (await File.ReadAllTextAsync(giPath))
                                .Contains("_Ardysa", StringComparison.OrdinalIgnoreCase);

                        if (vpkExists && giPatched)
                        {
                            return (true,
                                $"{currentVersion} (Build {currentBuild})");
                        }

                        return (false, "Not patched — run Patch Update");
                    }
                },

                new VerifyCheck
                {
                    Index = 2,
                    Name = "Game Patch",
                    Execute = async () =>
                    {
                        await Task.Yield();
                        string sigPath = Path.Combine(
                            _targetPath, "game", "bin", "win64", "dota.signatures");
                        if (File.Exists(sigPath))
                        {
                            var info = new FileInfo(sigPath);
                            return (true, $"Signatures present ({FormatFileSize(info.Length)})");
                        }
                        return (false, "dota.signatures missing");
                    }
                },

                new VerifyCheck
                {
                    Index = 3,
                    Name = "Mod Integration",
                    Execute = async () =>
                    {
                        string giPath = Path.Combine(
                            _targetPath, "game", "dota", "gameinfo_branchspecific.gi");
                        string sigPath = Path.Combine(
                            _targetPath, "game", "bin", "win64", "dota.signatures");

                        if (!File.Exists(giPath) || !File.Exists(sigPath))
                            return (false, "Core game files missing");

                        string giContent = await File.ReadAllTextAsync(giPath);
                        string sigContent = await File.ReadAllTextAsync(sigPath);

                        bool hasMarker = giContent.Contains(
                            "_Ardysa", StringComparison.OrdinalIgnoreCase);
                        bool hasFormat = sigContent.Contains(
                            ModConstants.ModPatchLine, StringComparison.Ordinal);

                        if (hasMarker && hasFormat)
                            return (true, "GameInfo + Signatures valid");

                        if (hasMarker && !hasFormat)
                            return (false, "Signature format invalid — re-patch");

                        return (false, "Mod entry not in GameInfo");
                    }
                }
            };
        }

        #endregion

        #region Message Handling

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                string? type = msg.GetProperty("type").GetString();

                switch (type)
                {
                    case "patchNow":
                        CloseWithResult(DialogResult.OK);
                        _onPatchRequested?.Invoke();
                        break;

                    case "close":
                        CloseWithResult(DialogResult.Cancel);
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VerifyFiles] WebMessage error: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private void CloseWithResult(DialogResult result)
        {
            DialogResult = result;
            Close();
        }

        private async Task CallJsAsync(string script)
        {
            if (_disposed || _webView?.CoreWebView2 == null) return;
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _webView?.Dispose();
                    _webView = null;
                }
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Static Entry Point

        public static void Show(
            IWin32Window? owner,
            string targetPath,
            DotaVersionService versionService,
            Action? onPatchRequested = null)
        {
            try
            {
                using var dialog = new VerifyFilesDialogWebView(
                    targetPath, versionService, onPatchRequested);
                dialog.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[VerifyFiles] WebView2 failed: {ex.Message}");

                MessageBox.Show(
                    owner,
                    Loc.T("verify.fallback.body", new { error = ex.Message }),
                    Loc.T("shell.patchMenu.verify"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        #endregion
    }
}
