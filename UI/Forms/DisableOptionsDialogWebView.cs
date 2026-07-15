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
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.UI.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class DisableOptionsDialogWebView : Form
    {
        private WebView2? _webView;

        public bool DeletePermanently { get; private set; }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public DisableOptionsDialogWebView()
        {
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new System.Drawing.Size(480, 384);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "DisableOptionsDialogWebView";
            Text = "Disable Mods";

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
                Helpers.DpiLayout.PinTo100(this, _webView!);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "disable_options.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("disable_options.html not found", htmlPath);

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

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                Opacity = 1d;
            }
            catch (Exception)
            {
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "confirm":
                        var option = message.TryGetProperty("option", out var optEl) ? optEl.GetString() : "disable";
                        DeletePermanently = option == "delete";
                        DialogResult = DialogResult.OK;
                        Close();
                        break;

                    case "cancel":
                    case "close":
                        DialogResult = DialogResult.Cancel;
                        Close();
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
    }
}
