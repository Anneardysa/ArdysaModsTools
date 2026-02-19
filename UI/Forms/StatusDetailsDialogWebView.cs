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
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-based dialog for displaying detailed mod status and version information.
    /// Sends a single JSON payload to the page's <c>populate()</c> function.
    /// Falls back to <see cref="StatusDetailsForm"/> if WebView2 is unavailable.
    /// </summary>
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

        /// <summary>
        /// Creates the WebView2-based status details dialog.
        /// </summary>
        /// <param name="statusInfo">Current mod status information.</param>
        /// <param name="versionInfo">Dota version and patch information.</param>
        /// <param name="onPatchRequested">Callback when user clicks "Apply Patches"; null hides the button.</param>
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
        }

        #endregion

        #region Setup

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(440, 460);
            this.Name = "StatusDetailsDialogWebView";
            this.Text = "Status Details - AMT 2.0";
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
        /// Initializes WebView2, loads HTML, waits for navigation, then injects data.
        /// Any exception here causes the static <see cref="Show"/> to fall back to WinForms.
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            if (_initialized || _disposed) return;

            string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
            await _webView!.EnsureCoreWebView2Async(env);

            // Configure WebView2
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Load HTML
            var htmlPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "status_details.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("status_details.html not found", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            _webView.CoreWebView2.NavigateToString(html);

            // Wait for navigation
            await WaitForNavigationAsync();

            // Guard: only inject once
            if (_initialized || _disposed) return;
            _initialized = true;

            await InjectPayloadAsync();
        }

        /// <summary>
        /// Waits for <see cref="CoreWebView2.NavigationCompleted"/> and throws on failure.
        /// </summary>
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

        /// <summary>
        /// Builds a single JSON payload from status + version info and sends it
        /// to the page's <c>populate(data)</c> JS function.
        /// Diagnostics are derived from the overall <see cref="ModStatus"/>, not raw cache fields,
        /// to avoid false negatives when cache is empty but mods are working.
        /// </summary>
        private async Task InjectPayloadAsync()
        {
            var status = _statusInfo.Status;

            bool showPatchBtn = _onPatchRequested != null &&
                (status == ModStatus.NeedUpdate || status == ModStatus.Disabled);

            // Diagnostics: derive from overall status to avoid cache-empty false negatives
            // - Ready: everything is verified OK by the status service
            // - NeedUpdate/Disabled/Error: use raw version fields for granular feedback
            // - NotInstalled: unknown (null → gray dots)
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

            // Version mismatch: only meaningful when we have a patched version to compare
            bool versionMismatch = status != ModStatus.Ready &&
                _versionInfo.LastPatchedVersion != null &&
                _versionInfo.LastPatchedVersion != _versionInfo.DotaVersion;

            // Patch date: show "Up to date" for Ready when cache is empty
            string patchDate = _versionInfo.LastPatchedDate?.ToString("MMM dd, yyyy")
                ?? (status == ModStatus.Ready ? "Up to date" : "Never");

            // Patched version: for Ready status, default to current version if cache is empty
            string patchedVersion = _versionInfo.LastPatchedVersion
                ?? (status == ModStatus.Ready ? _versionInfo.DotaVersion : "--");

            var payload = new
            {
                // Status header
                status = StatusToKey(status),
                statusText = _statusInfo.StatusText.Length > 0
                    ? _statusInfo.StatusText
                    : StatusToLabel(status),
                description = BuildDescription(),

                // Patch button
                showPatchBtn,
                patchBtnText = _statusInfo.ActionButtonText.Length > 0
                    ? _statusInfo.ActionButtonText
                    : "Apply Patches",

                // Version info
                dotaVersion = _versionInfo.DotaVersion,
                buildNumber = _versionInfo.BuildNumber,
                patchedVersion,
                patchDate,
                versionMismatch,

                // Diagnostics (null = gray/unknown)
                digestOk,
                gameInfoOk,

                // Error block (shown only for Error status)
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
            // Prefer the model's description if set; otherwise derive from status
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

        /// <summary>
        /// Shows the status details dialog. Tries WebView2 first, falls back to
        /// <see cref="StatusDetailsForm"/> on any failure.
        /// </summary>
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

                // Fallback: native WinForms dialog
                using var fallback = new StatusDetailsForm(statusInfo, versionInfo, onPatchRequested);
                fallback.StartPosition = FormStartPosition.CenterParent;
                fallback.ShowDialog(owner);
            }
        }

        #endregion
    }
}
