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
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2 dialog that runs mod file verification checks sequentially,
    /// animating each result in real-time. Falls back to a plain MessageBox
    /// if WebView2 is unavailable.
    /// </summary>
    public sealed class VerifyFilesDialogWebView : Form
    {
        #region Types

        /// <summary>
        /// Encapsulates one verification check: a name, an async function that
        /// returns (passed, detail), and the index for UI mapping.
        /// </summary>
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

        /// <summary>
        /// Creates the verification dialog.
        /// </summary>
        /// <param name="targetPath">Dota 2 game directory path.</param>
        /// <param name="versionService">Version service for patch comparison.</param>
        /// <param name="onPatchRequested">Callback when user clicks "Patch Update".</param>
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
        }

        #endregion

        #region Setup

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 440);
            this.Name = "VerifyFilesDialogWebView";
            this.Text = "Verify Mod Files - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = System.Drawing.Color.Black;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
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

        /// <summary>
        /// Initializes WebView2, loads HTML, then runs all checks sequentially.
        /// </summary>
        private async Task InitializeAndRunAsync()
        {
            if (_initialized || _disposed) return;

            string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
            await _webView!.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var htmlPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "verify_files.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("verify_files.html not found", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            _webView.CoreWebView2.NavigateToString(html);

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

        /// <summary>
        /// Defines and runs all verification checks sequentially, updating the UI
        /// in real-time after each check completes.
        /// </summary>
        private async Task RunAllChecksAsync()
        {
            var checks = BuildChecks();
            int passed = 0;

            foreach (var check in checks)
            {
                if (_disposed) return;

                // Signal: "this check is starting" → spinner
                await CallJsAsync($"startCheck({check.Index})");

                // Small delay for visual feedback
                await Task.Delay(200);

                // Run the actual check
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

                // Signal: "this check is done" → green/red
                string safeDetail = JsonSerializer.Serialize(detail); // escapes properly
                await CallJsAsync($"completeCheck({check.Index}, {(ok ? "true" : "false")}, {safeDetail})");

                // Brief pause between checks for visual stagger
                await Task.Delay(150);
            }

            // All done
            bool showPatch = _onPatchRequested != null && passed < checks.Count;
            await CallJsAsync($"allDone({passed}, {checks.Count}, {(showPatch ? "true" : "false")})");
        }

        /// <summary>
        /// Builds the ordered list of verification checks.
        /// Each check is a self-contained async function that returns (passed, detail).
        /// </summary>
        private List<VerifyCheck> BuildChecks()
        {
            return new List<VerifyCheck>
            {
                // Check 0: Mod Package (VPK exists)
                new VerifyCheck
                {
                    Index = 0,
                    Name = "Mod Package",
                    Execute = async () =>
                    {
                        await Task.Yield(); // ensure async context
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

                // Check 1: Dota Version
                // Reads steam.inf directly for version info, then compares
                // against version.json if it exists. Does NOT fail when
                // version.json is absent but patches are clearly active.
                new VerifyCheck
                {
                    Index = 1,
                    Name = "Dota Version",
                    Execute = async () =>
                    {
                        // Step 1: Read steam.inf — this is the ground truth
                        string steamInfPath = Path.Combine(_targetPath, DotaPaths.SteamInf);
                        if (!File.Exists(steamInfPath))
                            return (false, "steam.inf not found — Dota 2 path invalid");

                        string steamInf = await File.ReadAllTextAsync(steamInfPath);

                        // Extract version date and build number
                        var dateMatch = System.Text.RegularExpressions.Regex.Match(
                            steamInf, @"VersionDate=(.+)");
                        var buildMatch = System.Text.RegularExpressions.Regex.Match(
                            steamInf, @"ClientVersion=(\d+)");

                        string currentVersion = dateMatch.Success
                            ? dateMatch.Groups[1].Value.Trim() : "Unknown";
                        string currentBuild = buildMatch.Success
                            ? buildMatch.Groups[1].Value : "Unknown";

                        // Step 2: Check version.json for patch comparison
                        string versionJsonPath = Path.Combine(_targetPath, DotaPaths.VersionJson);

                        if (File.Exists(versionJsonPath))
                        {
                            // version.json exists — compare against patched version
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

                                // Version mismatch — Dota was updated since last patch
                                return (false,
                                    $"Dota updated: {currentVersion} ≠ patched {patchedVer}");
                            }
                            catch
                            {
                                // Corrupt version.json — treat as missing
                            }
                        }

                        // Step 3: version.json missing or corrupt
                        // Check if patches are active despite no version tracking
                        bool vpkExists = File.Exists(
                            Path.Combine(_targetPath, DotaPaths.ModsVpk));
                        string giPath = Path.Combine(
                            _targetPath, "game", "dota", "gameinfo_branchspecific.gi");
                        bool giPatched = File.Exists(giPath)
                            && (await File.ReadAllTextAsync(giPath))
                                .Contains("_Ardysa", StringComparison.OrdinalIgnoreCase);

                        if (vpkExists && giPatched)
                        {
                            // Mods are active — version.json just wasn't created
                            return (true,
                                $"{currentVersion} (Build {currentBuild})");
                        }

                        // Nothing is patched at all
                        return (false, "Not patched — run Patch Update");
                    }
                },

                // Check 2: Game Compatibility (signatures file)
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

                // Check 3: Mod Integration (gameinfo + signature content)
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

        /// <summary>
        /// Shows the verify files dialog. WebView2-first with MessageBox fallback.
        /// </summary>
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

                // Fallback: plain message indicating WebView2 failure
                MessageBox.Show(
                    owner,
                    "Verification dialog could not be displayed.\n\n" +
                    "Please run Patch Update from the menu instead.\n\n" +
                    $"Technical detail: {ex.Message}",
                    "Verify Mod Files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        #endregion
    }
}
