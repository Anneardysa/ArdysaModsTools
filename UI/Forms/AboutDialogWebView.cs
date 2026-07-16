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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class AboutDialogWebView : Form
    {
        private WebView2? _webView;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public AboutDialogWebView()
        {
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new System.Drawing.Size(480, 560);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "AboutDialogWebView";
            Text = "About";

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
                    DialogResult = DialogResult.Cancel;
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

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "about.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("about.html not found", htmlPath);

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

                PushVersion();
                Opacity = 1d;
            }
            catch (Exception)
            {
                OpenUrl("https://github.com/Anneardysa/ArdysaModsTools");
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        private void PushVersion()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                if (string.IsNullOrEmpty(version)) return;

                _ = _webView!.CoreWebView2.ExecuteScriptAsync($"setVersion({J(version)})");
            }
            catch {  }
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

        private static string J(object? value) => JsonSerializer.Serialize(value, _jsonOptions);

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return;
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch {  }
        }
    }
}
