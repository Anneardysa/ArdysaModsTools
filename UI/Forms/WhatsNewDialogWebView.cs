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
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-hosted "What's New" dialog. Renders the GitHub releases changelog (same source as the
    /// website's whatsnew page) natively, styled to match <c>main_shell</c>. Self-contained: it fetches
    /// the releases via <see cref="WhatsNewService"/> and pushes them to <c>whatsnew.html</c>; on failure
    /// the page shows an offline fallback with an "open on the web" action.
    /// </summary>
    public sealed class WhatsNewDialogWebView : Form
    {
        private WebView2? _webView;
        private readonly CancellationTokenSource _cts = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public WhatsNewDialogWebView()
        {
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
            this.FormClosed += (s, e) =>
            {
                try { _cts.Cancel(); _cts.Dispose(); } catch { /* best-effort */ }
            };
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(580, 640);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = System.Drawing.Color.Black;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "WhatsNewDialogWebView";
            Text = "What's New";

            // Hidden until DPI-scaled + content painted (prevents the open "grow"/black flash).
            Opacity = 0d;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
            };
            Controls.Add(_webView);

            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    e.Handled = true;
                }
            };

            ResumeLayout(false);
        }

        private void ApplyRoundedForm()
        {
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));
        }

        private async Task InitializeAsync()
        {
            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView!.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "whatsnew.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("whatsnew.html not found", htmlPath);

                var html = await File.ReadAllTextAsync(htmlPath);

                var tcs = new TaskCompletionSource<bool>();
                void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= handler;
                    tcs.TrySetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += handler;
                _webView.CoreWebView2.NavigateToString(html);
                await Task.WhenAny(tcs.Task, Task.Delay(8000));

                // Reveal with the loading state visible, then populate asynchronously.
                Opacity = 1d;

                await PopulateAsync();
            }
            catch (Exception)
            {
                // Without WebView2 there is no native fallback view; open the website instead.
                OpenUrl("https://ardysamods.my.id/whatsnew.html");
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        // [AMT:PRO] Bridge push — paired with loadReleases(...) in Assets/Html/whatsnew.html.
        private async Task PopulateAsync()
        {
            string json = "[]";
            try
            {
                var releases = await new WhatsNewService().GetReleasesAsync(_cts.Token);
                if (releases != null && releases.Count > 0)
                {
                    var data = releases.ConvertAll(r => new
                    {
                        tag = r.Tag,
                        name = r.Name,
                        date = r.Date?.ToString("o"),
                        body = r.Body,
                        htmlUrl = r.HtmlUrl
                    });
                    json = JsonSerializer.Serialize(data, _jsonOptions);
                }
            }
            catch
            {
                json = "[]"; // page shows the offline fallback
            }

            try { await _webView!.CoreWebView2.ExecuteScriptAsync($"loadReleases({json})"); }
            catch { /* control tearing down */ }
        }

        // [AMT:PRO] JS↔C# bridge — message 'type' strings are a contract with whatsnew.html.
        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "close":
                        DialogResult = DialogResult.OK;
                        Close();
                        break;

                    case "openUrl":
                        if (message.TryGetProperty("url", out var urlEl))
                            OpenUrl(urlEl.GetString());
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return;
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { /* best-effort */ }
        }
    }
}
