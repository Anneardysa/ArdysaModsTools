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
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class Dota2PerformanceForm : Form, IDota2PerformanceView
    {
        private WebView2? _webView;
        private bool _initialized;
        private readonly IAppLogger _logService;

        private readonly Dota2PerformancePresenter _presenter;

        public event EventHandler? OnViewShown;
        public event EventHandler<string>? OnApplySettingsRequested;
        public event EventHandler<string>? OnExportCfgRequested;
        public event EventHandler? OnDeleteCfgRequested;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public Dota2PerformanceForm(Func<IDota2PerformanceView, string?, Dota2PerformancePresenter> presenterFactory, IAppLogger logService, string? gamePath)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            InitializeComponent();
            SetupForm();

            _presenter = presenterFactory(this, gamePath);

            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);

            if (Loc.Service != null)
                Loc.Service.CultureChanged += OnCultureChanged;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;

            this.ClientSize = new System.Drawing.Size(1320, 780);
            this.MinimumSize = new System.Drawing.Size(1040, 640);

            this.Name = "Dota2PerformanceForm";
            this.Text = "Dota 2 Performance Tweak - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Theme.Canvas;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            this.Controls.Add(_webView);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
        }

        private async Task InitializeAsync()
        {
            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "dota2_performance.html");
                if (File.Exists(htmlPath))
                {
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        Helpers.WebViewTheming.BuildBootstrapScript());
                    _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    throw new FileNotFoundException("dota2_performance.html not found");
                }

                var tcs = new TaskCompletionSource<bool>();
                void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= handler;
                    tcs.SetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += handler;
                await tcs.Task;

                await Task.Delay(100);

                if (_initialized) return;
                _initialized = true;

                if (Loc.Service != null)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));
                    await _webView.CoreWebView2.ExecuteScriptAsync("if(typeof renderSettings==='function'){renderSettings();renderLaunchOptions();}");
                }

                OnViewShown?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("form.perf.initFailed", new { error = ex.Message }), Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 != null)
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        public void InvokeSafeClose()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BeginInvoke(new Action(() => this.Close()));
        }

        public void StartDrag()
        {
            ReleaseCapture();
            SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        public void CopyTextToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        public string? PromptForExportPath()
        {
            using var save = new SaveFileDialog
            {
                Filter = "CFG files (*.cfg)|*.cfg|All files (*.*)|*.*",
                Title = "Export Autoexec Config",
                DefaultExt = "cfg",
                FileName = "autoexec.cfg",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (save.ShowDialog(this) == DialogResult.OK)
            {
                return save.FileName;
            }
            return null;
        }

        public async Task LoadSettingsAsync(string jsonSettings)
        {
            await ExecuteScriptAsync($"loadSettings({JsonSerializer.Serialize(jsonSettings)})");
        }

        public async Task ShowToastAsync(string message, string type)
        {
            await ExecuteScriptAsync($"showToast({JsonSerializer.Serialize(message)}, {JsonSerializer.Serialize(type)})");
        }

        public async Task ShowCfgBannerAsync(string message, string state)
        {
            await ExecuteScriptAsync($"showCfgBanner({JsonSerializer.Serialize(message)}, {JsonSerializer.Serialize(state)})");
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "applySettings":
                        var applyJson = await _webView!.CoreWebView2.ExecuteScriptAsync("getAllSettings()");
                        var applySettingsStr = JsonSerializer.Deserialize<string>(applyJson);
                        if (!string.IsNullOrEmpty(applySettingsStr))
                        {
                            OnApplySettingsRequested?.Invoke(this, applySettingsStr);
                        }
                        break;

                    case "exportCfg":
                        var exportJson = await _webView!.CoreWebView2.ExecuteScriptAsync("getAllSettings()");
                        var exportSettingsStr = JsonSerializer.Deserialize<string>(exportJson);
                        if (!string.IsNullOrEmpty(exportSettingsStr))
                        {
                            OnExportCfgRequested?.Invoke(this, exportSettingsStr);
                        }
                        break;

                    case "deleteCfg":
                        OnDeleteCfgRequested?.Invoke(this, EventArgs.Empty);
                        break;

                    case "close":
                        InvokeSafeClose();
                        break;

                    case "startDrag":
                        StartDrag();
                        break;

                    case "copyText":
                        if (message.TryGetProperty("text", out var textProp))
                        {
                            CopyTextToClipboard(textProp.GetString() ?? string.Empty);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformanceForm] Failed to handle WebView2 message: {ex.Message}", ex);
            }
        }

        private async void OnCultureChanged(object? sender, EventArgs e)
        {
            if (Loc.Service != null && _webView?.CoreWebView2 != null)
            {
                try
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildApplyScript(Loc.Service));
                    await _webView.CoreWebView2.ExecuteScriptAsync("if(typeof renderSettings === 'function') { renderSettings(); renderLaunchOptions(); }");
                }
                catch (Exception ex)
                {
                    _logService.LogError($"[Dota2PerformanceForm] Failed to apply live culture update: {ex.Message}", ex);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Loc.Service != null)
                    Loc.Service.CultureChanged -= OnCultureChanged;
                _webView?.Dispose();
                _webView = null;
            }
            base.Dispose(disposing);
        }
    }
}
