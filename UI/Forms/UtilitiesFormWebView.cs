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
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.UI.Controls;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class UtilitiesFormWebView : Form, IUtilitiesView
    {
        private const string AssetHost = "amt-assets";

        private WebView2? _webView;
        private bool _shown;

        private readonly UtilitiesPresenter _presenter;
        private readonly IAppLogger _logger;

        public event EventHandler? OnViewShown;
        public event EventHandler<string>? OnRankApplyRequested;
        public event EventHandler? OnRankResetRequested;
        public event EventHandler<string>? OnBadgeApplyRequested;
        public event EventHandler? OnBadgeResetRequested;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public UtilitiesFormWebView(IRankChangerService rankChanger, IHeroBadgeService heroBadge, IAppLogger logger, string? gamePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            SetupForm();
            _presenter = new UtilitiesPresenter(this, rankChanger, heroBadge, logger, gamePath);

            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new System.Drawing.Size(1700, 1000);
            MinimumSize = new System.Drawing.Size(980, 680);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "UtilitiesFormWebView";
            Text = "Utilities";

            Opacity = 0d;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            Controls.Add(_webView);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            ResumeLayout(false);
        }

        private async Task InitializeAsync()
        {
            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    AssetHost,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images"),
                    CoreWebView2HostResourceAccessKind.DenyCors);

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "utilities.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("utilities.html not found", htmlPath);

                var html = await File.ReadAllTextAsync(htmlPath);

                var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void onContentLoading(object? s, CoreWebView2DOMContentLoadedEventArgs e)
                {
                    _webView.CoreWebView2.DOMContentLoaded -= onContentLoading;
                    ready.TrySetResult(true);
                }
                _webView.CoreWebView2.DOMContentLoaded += onContentLoading;
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));

                await Task.WhenAny(ready.Task, Task.Delay(8000));

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                Opacity = 1d;

                if (_shown) return;
                _shown = true;
                OnViewShown?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UtilitiesFormWebView] Initialization failed: {ex.Message}", ex);
                MessageBox.Show(Loc.T("form.utilities.initFailed", new { error = ex.Message }), Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 is null || IsDisposed) return;
            try { await _webView.CoreWebView2.ExecuteScriptAsync(script); }
            catch (Exception ex) { _logger.LogDebug($"[UtilitiesFormWebView] script failed: {ex.Message}"); }
        }

        public Task LoadStateAsync(string stateJson)
            => ExecuteScriptAsync($"loadState({JsonSerializer.Serialize(stateJson)})");

        public Task ShowToastAsync(string message, string type)
            => ExecuteScriptAsync($"showToast({JsonSerializer.Serialize(message)}, {JsonSerializer.Serialize(type)})");

        public Task SetBusyAsync(bool busy)
            => ExecuteScriptAsync($"setBusy({(busy ? "true" : "false")})");

        public Task ShowProgressAsync(string stage, int percent)
            => ExecuteScriptAsync($"setProgress({JsonSerializer.Serialize(stage)}, {percent})");

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "rankApply":
                        var request = message.TryGetProperty("request", out var reqEl) ? reqEl.GetRawText() : null;
                        if (!string.IsNullOrEmpty(request))
                            BeginInvoke(new Action(() => OnRankApplyRequested?.Invoke(this, request)));
                        break;

                    case "rankReset":
                        BeginInvoke(new Action(() => OnRankResetRequested?.Invoke(this, EventArgs.Empty)));
                        break;

                    case "badgeApply":
                        var badge = message.TryGetProperty("request", out var badgeEl) ? badgeEl.GetRawText() : null;
                        if (!string.IsNullOrEmpty(badge))
                            BeginInvoke(new Action(() => OnBadgeApplyRequested?.Invoke(this, badge)));
                        break;

                    case "badgeReset":
                        BeginInvoke(new Action(() => OnBadgeResetRequested?.Invoke(this, EventArgs.Empty)));
                        break;

                    case "close":
                        BeginInvoke(new Action(Close));
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UtilitiesFormWebView] Failed to handle WebView2 message: {ex.Message}", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
                _webView = null;
            }
            base.Dispose(disposing);
        }
    }
}
