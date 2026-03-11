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
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-based Support dialog with animated donation cards,
    /// Ko-fi goal progress bar, and YouTube subscriber goal.
    /// </summary>
    public sealed class SupportDialog : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private readonly SupportGoalsService _goalsService = new();

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public SupportDialog()
        {
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void SetupForm()
        {
            this.Text = "Support";
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = System.Drawing.Color.Black;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            // Responsive sizing: cap at 820×620 but scale down on small monitors
            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var workArea = screen!.WorkingArea;
            int width = Math.Min(820, (int)(workArea.Width * 0.85));
            int height = Math.Min(620, (int)(workArea.Height * 0.85));
            this.Size = new System.Drawing.Size(width, height);

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
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
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webView.CoreWebView2.WindowCloseRequested += (s, e) => SafeClose();

                // Disable right-click context menu
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "support.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("support.html not found");
                }

                // Wait for navigation
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

                // Load goal data from R2 CDN
                await LoadGoalsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SupportDialog init failed: {ex.Message}");
                SafeClose();
            }
        }

        private async Task LoadGoalsAsync()
        {
            try
            {
                var config = await _goalsService.GetConfigAsync();
                if (config != null)
                {
                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    var escaped = json.Replace("'", "\\'");
                    await ExecuteScriptAsync($"loadGoals('{escaped}')");
                }
            }
            catch
            {
                // Silent fail — goals section stays hidden
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

        /// <summary>
        /// Ensures form closes properly even if called from WebView context.
        /// </summary>
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
