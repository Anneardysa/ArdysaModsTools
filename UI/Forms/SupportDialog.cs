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
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class SupportDialog : Form
    {
        private WebView2? _webView;
        private bool _initialized;

        private readonly int _startupCountdownSeconds;
        private DateTime _closeUnlockAt = DateTime.MinValue;

        public bool SnoozeToday { get; private set; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public SupportDialog(int startupCountdownSeconds = 0)
        {
            _startupCountdownSeconds = startupCountdownSeconds;
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void SetupForm()
        {
            this.Text = "Support";
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Theme.Canvas;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var workArea = screen!.WorkingArea;
            int width = Math.Min(760, (int)(workArea.Width * 0.85));
            int height = Math.Min(460, (int)(workArea.Height * 0.85));
            this.Size = new System.Drawing.Size(width, height);

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
                    if (DateTime.UtcNow < _closeUnlockAt)
                    {
                        e.Handled = true;
                        return;
                    }
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
                _webView.CoreWebView2.WindowCloseRequested += (s, e) => SafeClose();

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "support.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                }
                else
                {
                    throw new FileNotFoundException("support.html not found");
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
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                if (_startupCountdownSeconds > 0)
                {
                    _closeUnlockAt = DateTime.UtcNow.AddSeconds(_startupCountdownSeconds);
                    await ExecuteScriptAsync($"startCountdown({_startupCountdownSeconds})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SupportDialog init failed: {ex.Message}");
                SafeClose();
            }
        }



        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "close":
                        if (message.TryGetProperty("snoozeToday", out var snooze) &&
                            (snooze.ValueKind == JsonValueKind.True || snooze.ValueKind == JsonValueKind.False))
                        {
                            SnoozeToday = snooze.GetBoolean();
                        }
                        SafeClose();
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;

                    case "openUrl":
                        var url = message.GetProperty("url").GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                            }
                            catch { }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void SafeClose()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BeginInvoke(new Action(() => this.Close()));
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 != null)
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
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
