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
using ArdysaModsTools.UI.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-hosted "Disable Options" dialog (temporarily disable vs delete permanently),
    /// styled to match <c>main_shell.html</c> with an explicit close/X (cancel) path.
    /// Reuses <see cref="DisableOptionsDialog.DisableOption"/> so the view-layer mapping is
    /// unchanged. Returns <see cref="DialogResult.Abort"/> on init failure so the caller can
    /// fall back to the classic <see cref="DisableOptionsDialog"/>.
    /// </summary>
    public sealed class DisableOptionsDialogWebView : Form
    {
        private WebView2? _webView;

        /// <summary>Selected option; only meaningful when <see cref="Form.DialogResult"/> is OK.</summary>
        public DisableOptionsDialog.DisableOption SelectedOption { get; private set; }
            = DisableOptionsDialog.DisableOption.Disable;

        // Rounded host window (matches MainFormWebView).
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        // P/Invoke for borderless window dragging.
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
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(480, 384);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = System.Drawing.Color.Black;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "DisableOptionsDialogWebView";
            Text = "Disable Mods";

            // Stay invisible until the window is DPI-scaled AND the HTML has painted, then reveal.
            // Prevents the visible "grow" flash on PerMonitorV2 displays (AutoScaleMode.Font rescales
            // the form after it is shown) and the separate black→content flash.
            Opacity = 0d;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
            };
            Controls.Add(_webView);

            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            // ESC closes as "cancel".
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
                _webView.CoreWebView2.NavigateToString(html);
                // Timeout guard so the window can never stay invisible/stuck if navigation stalls.
                await Task.WhenAny(tcs.Task, Task.Delay(8000));

                // Content is painted at the final (DPI-scaled) size — reveal in one step.
                Opacity = 1d;
            }
            catch (Exception)
            {
                // Signal the caller to fall back to the classic WinForms dialog.
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        // [AMT:PRO] JS↔C# bridge — message 'type'/'option' strings are a contract with
        // Assets/Html/disable_options.html. Changing one side requires the other.
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
                        SelectedOption = option == "delete"
                            ? DisableOptionsDialog.DisableOption.DeletePermanently
                            : DisableOptionsDialog.DisableOption.Disable;
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
