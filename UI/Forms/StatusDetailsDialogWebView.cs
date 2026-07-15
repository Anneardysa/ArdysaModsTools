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
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class StatusDetailsDialogWebView : Form
    {
        #region Fields

        private WebView2? _webView;
        private bool _initialized;
        private bool _disposed;
        private readonly ModStatusInfo _statusInfo;
        private readonly DotaVersionInfo _versionInfo;
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

        public StatusDetailsDialogWebView(
            ModStatusInfo statusInfo,
            DotaVersionInfo versionInfo,
            Action? onPatchRequested = null)
        {
            _statusInfo = statusInfo ?? throw new ArgumentNullException(nameof(statusInfo));
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _onPatchRequested = onPatchRequested;

            InitializeComponent();
            SetupForm();

            this.Shown += async (_, _) => await InitializeWebViewAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        #endregion

        #region Setup

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(440, 460);
            this.Name = "StatusDetailsDialogWebView";
            this.Text = "Status Details - AMT 2.0";
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

        private async Task InitializeWebViewAsync()
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
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "status_details.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("status_details.html not found", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));

            await WaitForNavigationAsync();

            if (_initialized || _disposed) return;
            _initialized = true;

            if (Loc.Service != null)
                await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

            await InjectPayloadAsync();
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

        #region Data Injection

        private async Task InjectPayloadAsync()
        {
            var status = _statusInfo.Status;

            bool showPatchBtn = _onPatchRequested != null &&
                (status == ModStatus.NeedUpdate || status == ModStatus.Disabled);

            bool? digestOk = status switch
            {
                ModStatus.Ready => true,
                ModStatus.NotInstalled => null,
                _ => !_versionInfo.DigestChanged
            };

            bool? gameInfoOk = status switch
            {
                ModStatus.Ready => true,
                ModStatus.NotInstalled => null,
                _ => _versionInfo.GameInfoHasModEntry
            };

            bool versionMismatch = status != ModStatus.Ready &&
                _versionInfo.LastPatchedVersion != null &&
                _versionInfo.LastPatchedVersion != _versionInfo.DotaVersion;

            string patchDate = _versionInfo.LastPatchedDate?.ToString("MMM dd, yyyy")
                ?? (status == ModStatus.Ready ? "Up to date" : "Never");

            string patchedVersion = _versionInfo.LastPatchedVersion
                ?? (status == ModStatus.Ready ? _versionInfo.DotaVersion : "--");

            var payload = new
            {
                status = StatusToKey(status),
                statusText = _statusInfo.StatusText.Length > 0
                    ? _statusInfo.StatusText
                    : StatusToLabel(status),
                description = BuildDescription(),

                showPatchBtn,
                patchBtnText = _statusInfo.ActionButtonText.Length > 0
                    ? _statusInfo.ActionButtonText
                    : "Apply Patches",

                dotaVersion = _versionInfo.DotaVersion,
                buildNumber = _versionInfo.BuildNumber,
                patchedVersion,
                patchDate,
                versionMismatch,

                digestOk,
                gameInfoOk,

                errorMessage = status == ModStatus.Error
                    ? _statusInfo.ErrorMessage
                    : null,
            };

            string json = JsonSerializer.Serialize(payload);
            await _webView!.CoreWebView2.ExecuteScriptAsync($"populate({json})");
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
                System.Diagnostics.Debug.WriteLine($"[StatusDetails] WebMessage error: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private void CloseWithResult(DialogResult result)
        {
            DialogResult = result;
            Close();
        }

        private string BuildDescription()
        {
            if (!string.IsNullOrEmpty(_statusInfo.Description))
                return _statusInfo.Description;

            return _statusInfo.Status switch
            {
                ModStatus.Ready => "your mods are working correctly",
                ModStatus.NeedUpdate => "dota updated, mods need re-patching",
                ModStatus.Disabled => "mods installed but not active",
                ModStatus.NotInstalled => "modspack is not installed yet",
                ModStatus.Error => _statusInfo.ErrorMessage ?? "an error occurred",
                _ => "status unknown"
            };
        }

        private static string StatusToKey(ModStatus status) => status switch
        {
            ModStatus.Ready => "Ready",
            ModStatus.NeedUpdate => "NeedUpdate",
            ModStatus.Disabled => "Disabled",
            ModStatus.NotInstalled => "NotInstalled",
            ModStatus.Error => "Error",
            _ => "NotInstalled"
        };

        private static string StatusToLabel(ModStatus status) => status switch
        {
            ModStatus.Ready => "All Good",
            ModStatus.NeedUpdate => "Update Needed",
            ModStatus.Disabled => "Disabled",
            ModStatus.NotInstalled => "Not Installed",
            ModStatus.Error => "Error",
            _ => "Unknown"
        };

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
            ModStatusInfo statusInfo,
            DotaVersionInfo versionInfo,
            Action? onPatchRequested = null)
        {
            try
            {
                using var dialog = new StatusDetailsDialogWebView(statusInfo, versionInfo, onPatchRequested);
                dialog.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StatusDetails] WebView2 failed: {ex.Message}");
            }
        }

        #endregion
    }
}
