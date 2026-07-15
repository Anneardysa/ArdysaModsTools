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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Helpers;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class UpdateAvailableDialogWebView : Form
    {
        private WebView2? _webView;
        private bool _initialized;

        private readonly UpdateInfo _updateInfo;
        private readonly InstallationType _installationType;
        private readonly DeltaUpdateService? _delta;
        private readonly CancellationTokenSource _cts = new();

        private DeltaPlan? _plan;
        private bool _applying;

        public bool ApplierStarted { get; private set; }

        private static string DownloadPageUrl => $"{EnvironmentConfig.WebsiteBase}/#download";

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private UpdateAvailableDialogWebView(
            UpdateInfo updateInfo, InstallationType installationType, DeltaUpdateService? delta)
        {
            _updateInfo = updateInfo;
            _installationType = installationType;
            _delta = delta;

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
            this.FormClosing += (s, e) => _cts.Cancel();
            this.Disposed += (s, e) => _cts.Dispose();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(440, 520);
            this.Name = "UpdateAvailableDialogWebView";
            this.Text = "Update Available - AMT 2.0";
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
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "update_available.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                }
                else
                {
                    throw new FileNotFoundException("update_available.html not found");
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

                await SetUpdateInfoAsync();

                _ = PrepareDeltaAsync();
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"UpdateAvailableDialog init failed, using fallback: {ex.Message}");
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        private async Task SetUpdateInfoAsync()
        {
            var currentDisplay = new AppVersion(_updateInfo.CurrentVersion, _updateInfo.CurrentBuildNumber);
            var latestDisplay = new AppVersion(_updateInfo.Version, _updateInfo.BuildNumber);

            bool isInstaller = _installationType == InstallationType.Installer;
            string updateType = isInstaller ? "Installer" : "Portable";

            string? cdnUrl = isInstaller
                ? _updateInfo.MirrorInstallerUrl
                : _updateInfo.MirrorPortableUrl;
            cdnUrl ??= $"{CdnConfig.R2BaseUrl}/releases/";

            string? githubUrl = isInstaller
                ? _updateInfo.InstallerDownloadUrl
                : _updateInfo.PortableDownloadUrl;
            githubUrl ??= DownloadPageUrl;

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

        #region Incremental update

        private async Task PrepareDeltaAsync()
        {
            if (_delta == null || string.IsNullOrEmpty(_updateInfo.FilesManifestUrl))
                return;

            if (_installationType != InstallationType.Installer)
                return;

            try
            {
                var plan = await _delta.PrepareAsync(_updateInfo, _cts.Token).ConfigureAwait(true);
                if (plan == null || IsDisposed || _cts.IsCancellationRequested)
                    return;

                _plan = plan;

                await CallJsAsync("setDeltaOffer",
                    Loc.T("updateAvail.updateNow"),
                    Loc.T("updateAvail.updateDesc", new { size = FormatSize(plan.TotalDownloadBytes) }));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"Delta prepare failed, offering full download only: {ex.Message}");
            }
        }

        private async Task ApplyDeltaAsync()
        {
            if (_delta == null || _plan == null || _applying)
                return;

            _applying = true;

            var progress = new Progress<int>(percent => _ = CallJsAsync("setUpdateProgress", percent));
            IProgress<string> status = new Progress<string>(text => _ = CallJsAsync("setUpdateStatus", text));

            try
            {
                await CallJsAsync("setUpdateBusy", Loc.T("updateAvail.downloading"));

                await _delta.StageAsync(_plan, status.Report, progress, _cts.Token)
                    .ConfigureAwait(true);

                if (IsDisposed || _cts.IsCancellationRequested)
                    return;

                await CallJsAsync("setUpdateStatus", Loc.T("updateAvail.restarting"));

                if (!await _delta.LaunchApplierAsync(_plan, _cts.Token).ConfigureAwait(true))
                {
                    _applying = false;
                    await CallJsAsync("setUpdateFailed", Loc.T("updateAvail.failed"));
                    return;
                }

                ApplierStarted = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                _applying = false;
            }
            catch (Exception ex)
            {
                _applying = false;
                FallbackLogger.Log($"Incremental update failed: {ex.Message}");
                await CallJsAsync("setUpdateFailed", Loc.T("updateAvail.failed"));
            }
        }

        private Task CallJsAsync(string function, params object[] args)
        {
            var encoded = string.Join(",", Array.ConvertAll(args, a => JsonSerializer.Serialize(a, a.GetType())));
            return ExecuteScriptAsync($"{function}({encoded})");
        }

        private static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        #endregion

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
                    case "applyUpdate":
                        _ = ApplyDeltaAsync();
                        break;

                    case "openLink":
                        if (message.TryGetProperty("url", out var urlEl) &&
                            urlEl.ValueKind == JsonValueKind.String)
                        {
                            string? url = urlEl.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                UIHelpers.OpenUrlWithErrorDialog(url, "download", FallbackLogger.Log);
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

        public static bool Show(
            IWin32Window? owner,
            UpdateInfo updateInfo,
            InstallationType installationType,
            DeltaUpdateService? delta = null)
        {
            bool applierStarted;

            try
            {
                using var dialog = new UpdateAvailableDialogWebView(updateInfo, installationType, delta);
                var result = dialog.ShowDialog(owner);

                if (result == DialogResult.Abort)
                {
                    ShowFallbackDialog(updateInfo, installationType);
                    return false;
                }

                applierStarted = dialog.ApplierStarted;
            }
            catch
            {
                ShowFallbackDialog(updateInfo, installationType);
                return false;
            }

            if (applierStarted)
                Application.Exit();

            return applierStarted;
        }

        private static void ShowFallbackDialog(UpdateInfo updateInfo, InstallationType installationType)
        {
            var currentVer = new AppVersion(updateInfo.CurrentVersion, updateInfo.CurrentBuildNumber);
            var latestVer = new AppVersion(updateInfo.Version, updateInfo.BuildNumber);

            var result = MessageBox.Show(
                Loc.T("update.fallback.body", new
                {
                    current = currentVer.ToString(),
                    latest = latestVer.ToString(),
                    type = InstallationDetector.GetInstallationTypeName(installationType)
                }),
                Loc.T("update.available.title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                UIHelpers.OpenUrlWithErrorDialog(DownloadPageUrl, "download", FallbackLogger.Log);
            }
        }
    }
}
