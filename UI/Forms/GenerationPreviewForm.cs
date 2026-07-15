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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class GenerationPreviewForm : Form
    {
        private WebView2? _webView;
        private readonly List<(HeroModel hero, string setName, string? thumbnailUrl)> _selections;

        public bool Confirmed { get; private set; }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public GenerationPreviewForm(List<(HeroModel hero, string setName, string? thumbnailUrl)> selections)
        {
            _selections = selections ?? new List<(HeroModel, string, string?)>();
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new System.Drawing.Size(480, 520);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "GenerationPreviewForm";
            Text = "Confirm Generation";
            Opacity = 0d;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            Controls.Add(_webView);

            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    CloseWith(false);
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
                Helpers.DpiLayout.PinTo100(this, _webView!);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "generation_preview.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("generation_preview.html not found", htmlPath);

                var html = await File.ReadAllTextAsync(htmlPath);

                var tcs = new TaskCompletionSource<bool>();
                void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= handler;
                    tcs.TrySetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += handler;
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                await Task.WhenAny(tcs.Task, Task.Delay(8000));

                var items = _selections.Select(x => new
                {
                    hero = x.hero.DisplayName,
                    setName = x.setName,
                    thumb = x.thumbnailUrl
                });
                var json = JsonSerializer.Serialize(items);
                await _webView.CoreWebView2.ExecuteScriptAsync($"loadItems({json})");

                Opacity = 1d;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerationPreview init failed: {ex.Message}");
                CloseWith(true);
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                switch (message.GetProperty("type").GetString())
                {
                    case "confirm":
                        CloseWith(true);
                        break;
                    case "cancel":
                    case "close":
                        CloseWith(false);
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

        private void CloseWith(bool confirmed)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                Confirmed = confirmed;
                DialogResult = confirmed ? DialogResult.OK : DialogResult.Cancel;
                Close();
            }));
        }
    }
}
