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
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Core.Constants;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-based "Update Available" dialog with manual download links (CDN + GitHub).
    /// Matches the dark theme of the Generation Mode dialog.
    /// </summary>
    public sealed class UpdateAvailableDialogWebView : Form
    {
        private WebView2? _webView;
        private bool _initialized;

        private readonly UpdateInfo _updateInfo;
        private readonly InstallationType _installationType;

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private UpdateAvailableDialogWebView(UpdateInfo updateInfo, InstallationType installationType)
        {
            _updateInfo = updateInfo;
            _installationType = installationType;

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(440, 520);
            this.Name = "UpdateAvailableDialogWebView";
            this.Text = "Update Available - AMT 2.0";
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

            // ESC to close (as "Not Now")
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
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

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "update_available.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("update_available.html not found");
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

                // Pass version info and download links to the HTML
                await SetUpdateInfoAsync();
            }
            catch (Exception)
            {
                // Signal caller to use fallback
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        /// <summary>
        /// Sends version info and direct download URLs to the WebView2 HTML.
        /// Uses actual file download URLs from UpdateInfo based on installation type.
        /// </summary>
        private async Task SetUpdateInfoAsync()
        {
            var currentDisplay = new AppVersion(_updateInfo.CurrentVersion, _updateInfo.CurrentBuildNumber);
            var latestDisplay = new AppVersion(_updateInfo.Version, _updateInfo.BuildNumber);

            bool isInstaller = _installationType == InstallationType.Installer;
            string updateType = isInstaller ? "Installer" : "Portable";

            // CDN: direct file download URL
            string? cdnUrl = isInstaller
                ? _updateInfo.MirrorInstallerUrl
                : _updateInfo.MirrorPortableUrl;
            cdnUrl ??= "https://cdn.ardysamods.my.id/releases/";

            // GitHub: direct file download URL
            string? githubUrl = isInstaller
                ? _updateInfo.InstallerDownloadUrl
                : _updateInfo.PortableDownloadUrl;
            githubUrl ??= "https://ardysamods.my.id/#download";

            // Extract filenames for display
            string cdnFilename = ExtractFilename(cdnUrl) ?? "cdn.ardysamods.my.id";
            string githubFilename = ExtractFilename(githubUrl) ?? "Download Portable / Installer";

            var data = new
            {
                currentVersion = currentDisplay.ToString(),
                latestVersion = latestDisplay.ToString(),
                updateType = updateType,
                cdnUrl = cdnUrl,
                githubUrl = githubUrl,
                cdnFilename = cdnFilename,
                githubFilename = githubFilename
            };

            var json = JsonSerializer.Serialize(data);
            await ExecuteScriptAsync($"setUpdateInfo({json})");
        }

        /// <summary>
        /// Extracts filename from a URL (e.g., "AMT-v2.1.16.zip").
        /// Returns null if URL is a page (no file extension).
        /// </summary>
        private static string? ExtractFilename(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var uri = new Uri(url);
                var filename = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(filename) &&
                    (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                     filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    return filename;
                }
            }
            catch { }

            return null;
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
                    case "openLink":
                        // Open URL in the user's default browser
                        if (message.TryGetProperty("url", out var urlEl) &&
                            urlEl.ValueKind == JsonValueKind.String)
                        {
                            string? url = urlEl.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = url,
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                                }
                            }
                        }
                        break;

                    case "notNow":
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

        /// <summary>
        /// Shows the Update Available dialog with manual download links.
        /// Falls back gracefully if WebView2 is unavailable.
        /// </summary>
        public static void Show(IWin32Window? owner, UpdateInfo updateInfo, InstallationType installationType)
        {
            try
            {
                using var dialog = new UpdateAvailableDialogWebView(updateInfo, installationType);
                var result = dialog.ShowDialog(owner);

                if (result == DialogResult.Abort)
                {
                    ShowFallbackDialog(updateInfo, installationType);
                }
            }
            catch
            {
                ShowFallbackDialog(updateInfo, installationType);
            }
        }

        /// <summary>
        /// Fallback MessageBox if WebView2 is unavailable.
        /// </summary>
        private static void ShowFallbackDialog(UpdateInfo updateInfo, InstallationType installationType)
        {
            var currentVer = new AppVersion(updateInfo.CurrentVersion, updateInfo.CurrentBuildNumber);
            var latestVer = new AppVersion(updateInfo.Version, updateInfo.BuildNumber);

            var result = MessageBox.Show(
                $"A new version is available.\n\n" +
                $"Current: {currentVer}\n" +
                $"Latest:  {latestVer}\n" +
                $"Update type: {InstallationDetector.GetInstallationTypeName(installationType)}\n\n" +
                $"Do you want to open the download page?",
                "Update Available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://ardysamods.my.id/#download",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }
}
