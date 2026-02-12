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
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-based dialog shown when a feature is disabled by remote config.
    /// Dark theme with cyan accent, lock icon animation, and smooth entrance.
    /// Follows the same pattern as PatchRequiredDialogWebView.
    /// </summary>
    public sealed class FeatureUnavailableDialog : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private readonly string _featureName;
        private readonly string _message;

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        /// <summary>
        /// Creates the feature unavailable dialog.
        /// </summary>
        /// <param name="featureName">Display name of the feature (e.g. "Skin Selector")</param>
        /// <param name="message">The disabled message to show</param>
        public FeatureUnavailableDialog(string featureName, string message)
        {
            _featureName = featureName;
            _message = message;
            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.Name = "FeatureUnavailableDialog";
            this.Text = "Feature Unavailable - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = System.Drawing.Color.Black;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            // WebView2 control
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
            };
            this.Controls.Add(_webView);

            // ESC or Enter to close
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    e.Handled = true;
                }
            };
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Use temp folder for WebView2 user data
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Disable context menu and dev tools
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "feature_unavailable.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("feature_unavailable.html not found");
                }

                // Wait for navigation to complete
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

                // Set the feature name and message
                await ExecuteScriptAsync($"setFeatureName('{EscapeJs(_featureName)}')");
                await ExecuteScriptAsync($"setMessage('{EscapeJs(_message)}')");
            }
            catch (Exception)
            {
                // If WebView2 fails, fall back to closing the dialog
                // Caller will handle the fallback to a native MessageBox
                throw;
            }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 != null)
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
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
                    case "close":
                        DialogResult = DialogResult.OK;
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

        private static string EscapeJs(string text)
        {
            return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }

        /// <summary>
        /// Shows the feature unavailable dialog with WebView2, falls back to MessageBox if WebView2 fails.
        /// </summary>
        /// <param name="owner">Parent form</param>
        /// <param name="featureName">Display name of the feature</param>
        /// <param name="message">The disabled message</param>
        public static void Show(IWin32Window? owner, string featureName, string message)
        {
            try
            {
                using var dialog = new FeatureUnavailableDialog(featureName, message);
                dialog.ShowDialog(owner);
            }
            catch
            {
                // Fallback to native MessageBox if WebView2 is not available
                MessageBox.Show(owner, message, "Feature Unavailable",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
