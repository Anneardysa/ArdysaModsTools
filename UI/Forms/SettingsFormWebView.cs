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
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.App;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.UI.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Modern WebView2-based Settings form with dark theme.
    /// </summary>
    public sealed class SettingsFormWebView : Form
    {
        private WebView2? _webView;
        private bool _initialized;

        private readonly IConfigService _configService;
        private readonly AppLifecycleService _lifecycleService;
        private readonly CacheCleaningService _cacheService;
        private readonly UpdaterService _updaterService;
        private readonly TrayService? _trayService;

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public SettingsFormWebView(
            IConfigService configService,
            AppLifecycleService lifecycleService,
            CacheCleaningService cacheService,
            UpdaterService updaterService,
            TrayService? trayService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _updaterService = updaterService ?? throw new ArgumentNullException(nameof(updaterService));
            _trayService = trayService;

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 480);
            this.Name = "SettingsFormWebView";
            this.Text = "Settings - AMT 2.0";
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

            // ESC to close
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
                // Use temp folder for WebView2 user data
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Disable right-click context menu
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "settings_form.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("settings_form.html not found");
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

                // Set version
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await ExecuteScriptAsync($"setVersion('{version}')");

                // Load current settings
                await LoadSettingsAsync();

                // Load cache size
                await LoadCacheSizeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        /// <summary>
        /// Ensures form closes properly even if called from WebView context.
        /// Always defers via BeginInvoke to avoid disposing the WebView2
        /// control while its event handler is still executing.
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

        private async Task LoadSettingsAsync()
        {
            var settings = new
            {
                startup = _lifecycleService.IsRunOnStartupEnabled,
                tray = _configService.MinimizeToTray,
                notifications = _configService.ShowNotifications
            };

            var json = JsonSerializer.Serialize(settings);
            await ExecuteScriptAsync($"initSettings({json})");
        }

        private async Task LoadCacheSizeAsync()
        {
            try
            {
                long cacheSize = await Task.Run(() => _cacheService.GetCacheSizeBytes());
                string formatted = CacheCleaningService.FormatBytes(cacheSize);
                await ExecuteScriptAsync($"setCacheSize('{EscapeJs(formatted)}')");
            }
            catch
            {
                await ExecuteScriptAsync("setCacheSize('Unknown')");
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
                    case "settingChanged":
                        await HandleSettingChanged(message);
                        break;

                    case "checkUpdates":
                        await HandleCheckUpdates();
                        break;

                    case "clearCache":
                        await HandleClearCacheAsync();
                        break;

                    case "close":
                        SafeClose();
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

        private async Task HandleSettingChanged(JsonElement message)
        {
            try
            {
                var key = message.GetProperty("key").GetString();
                var value = message.GetProperty("value").GetBoolean();

                switch (key)
                {
                    case "startup":
                        bool success = _lifecycleService.SetRunOnStartup(value);
                        if (success)
                        {
                            await ExecuteScriptAsync($"showToast('Run on startup {(value ? "enabled" : "disabled")}', 'success')");
                        }
                        else
                        {
                            // Revert the toggle in the HTML UI
                            bool actual = _lifecycleService.IsRunOnStartupEnabled;
                            await ExecuteScriptAsync($"revertSetting('startup', {(actual ? "true" : "false")})");
                            await ExecuteScriptAsync("showToast('Failed to update startup setting', 'error')");
                        }
                        break;

                    case "tray":
                        _configService.MinimizeToTray = value;
                        await ExecuteScriptAsync($"showToast('Minimize to tray {(value ? "enabled" : "disabled")}', 'success')");
                        break;

                    case "notifications":
                        _configService.ShowNotifications = value;
                        _trayService?.SetNotificationsEnabled(value);
                        await ExecuteScriptAsync($"showToast('Notifications {(value ? "enabled" : "disabled")}', 'success')");
                        break;
                }
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Error: {EscapeJs(ex.Message)}', 'error')");
            }
        }

        private async Task HandleCheckUpdates()
        {
            try
            {
                var updateInfo = await _updaterService.GetUpdateInfoAsync();
                if (updateInfo?.IsUpdateAvailable == true)
                {
                    await ExecuteScriptAsync("showToast('Update available! Check main window.', 'success')");
                    _trayService?.ShowNotification("Update Available", "A new version of ArdysaModsTools is available.", System.Windows.Forms.ToolTipIcon.Info);
                }
                else
                {
                    await ExecuteScriptAsync("showToast('You are running the latest version!', 'success')");
                }
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Update check failed: {EscapeJs(ex.Message)}', 'error')");
            }
            finally
            {
                await ExecuteScriptAsync("resetButton('btnCheckUpdates', '🔄', 'Check Updates')");
            }
        }

        private async Task HandleClearCacheAsync()
        {
            try
            {
                var result = await _cacheService.ClearAllCacheAsync();
                if (result.Success)
                {
                    string msg = $"Cache cleared! {CacheCleaningService.FormatBytes(result.BytesFreed)} freed";
                    await ExecuteScriptAsync($"showToast('{EscapeJs(msg)}', 'success')");
                    await ExecuteScriptAsync("setCacheSize('0 B')");
                }
                else
                {
                    await ExecuteScriptAsync($"showToast('Failed to clear cache: {EscapeJs(result.ErrorMessage ?? "Unknown error")}', 'error')");
                }
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Error: {EscapeJs(ex.Message)}', 'error')");
            }
            finally
            {
                await ExecuteScriptAsync("resetClearCacheButton()");
            }
        }

        private static string EscapeJs(string text)
        {
            return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
