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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.App;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.UI.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class SettingsFormWebView : Form
    {
        private WebView2? _webView;
        private bool _initialized;

        private readonly IConfigService _configService;
        private readonly AppLifecycleService _lifecycleService;
        private readonly CacheCleaningService _cacheService;
        private readonly UpdaterService _updaterService;
        private readonly TrayService? _trayService;
        private readonly IAssetPreloadService? _assetPreloadService;

        private readonly ILocalizationService? _loc = Loc.Service;

        public event EventHandler? ShowGuideRequested;

        public Func<Task<(string? Path, bool Changed)>>? ChangeDotaPathHandler { get; set; }

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
            TrayService? trayService,
            IAssetPreloadService? assetPreloadService = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _updaterService = updaterService ?? throw new ArgumentNullException(nameof(updaterService));
            _trayService = trayService;
            _assetPreloadService = assetPreloadService;

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(960, 650);
            this.Name = "SettingsFormWebView";
            this.Text = "Settings - AMT 2.0";
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
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "settings_form.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                }
                else
                {
                    throw new FileNotFoundException("settings_form.html not found");
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

                if (_loc != null)
                    await ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(_loc));

                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await ExecuteScriptAsync($"setVersion('{version}')");

                await LoadSettingsAsync();

                await LoadCacheSizeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("form.settings.initFailed", new { error = ex.Message }), Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

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
                notifications = _configService.ShowNotifications,
                preloadAssets = _configService.PreloadAssetsOnLaunch,
                autoDetectPath = _configService.AutoDetectOnStartup,
                dotaPath = _configService.GetLastTargetPath() ?? "",
                language = _loc?.CurrentCode ?? "en",
                uiScale = _configService.GetValue("uiScale", 1.0),
                theme = _configService.GetValue("theme", "dark"),
                cdnServer = _configService.CdnServerPreference
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
                await ExecuteScriptAsync($"setCacheSize('{EscapeJs(_loc?.T("common.unknown") ?? "Unknown")}')");
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

                    case "languageChanged":
                        await HandleLanguageChanged(message);
                        break;

                    case "cdnServerChanged":
                        await HandleCdnServerChanged(message);
                        break;

                    case "uiScaleChanged":
                        await HandleUiScaleChanged(message);
                        break;

                    case "themeChanged":
                        await HandleThemeChanged(message);
                        break;

                    case "changeDotaPath":
                        await HandleChangeDotaPathAsync();
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

                    case "showGuide":
                        SafeClose();
                        ShowGuideRequested?.Invoke(this, EventArgs.Empty);
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
                            await ToastAsync(value ? "toast.startup.enabled" : "toast.startup.disabled", "success");
                        }
                        else
                        {
                            bool actual = _lifecycleService.IsRunOnStartupEnabled;
                            await ExecuteScriptAsync($"revertSetting('startup', {(actual ? "true" : "false")})");
                            await ToastAsync("toast.startup.failed", "error");
                        }
                        break;

                    case "tray":
                        _configService.MinimizeToTray = value;
                        await ToastAsync(value ? "toast.tray.enabled" : "toast.tray.disabled", "success");
                        break;

                    case "notifications":
                        _configService.ShowNotifications = value;
                        _trayService?.SetNotificationsEnabled(value);
                        await ToastAsync(value ? "toast.notifications.enabled" : "toast.notifications.disabled", "success");
                        break;

                    case "preloadAssets":
                        _configService.PreloadAssetsOnLaunch = value;
                        await ToastAsync(value ? "toast.preload.enabled" : "toast.preload.disabled", "success");
                        if (value && _assetPreloadService != null)
                            _ = _assetPreloadService.PreloadAllAsync();
                        break;

                    case "autoDetectPath":
                        _configService.AutoDetectOnStartup = value;
                        await ToastAsync(value ? "toast.autoDetect.enabled" : "toast.autoDetect.disabled", "success");
                        break;
                }
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private async Task HandleLanguageChanged(JsonElement message)
        {
            try
            {
                var code = message.GetProperty("value").GetString();
                if (string.IsNullOrEmpty(code) || _loc == null) return;

                _configService.Language = code;
                _loc.SetCulture(code);

                await ExecuteScriptAsync(WebViewLocalizer.BuildApplyScript(_loc));
                await ToastAsync("toast.language.changed", "success");
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private async Task HandleCdnServerChanged(JsonElement message)
        {
            try
            {
                var value = message.GetProperty("value").GetString() ?? "auto";
                _configService.CdnServerPreference = value;
                CdnConfig.CdnServerPreference = value;
                await ToastAsync("toast.cdnServer.changed", "success");
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private async Task HandleUiScaleChanged(JsonElement message)
        {
            try
            {
                double scale = message.GetProperty("value").GetDouble();
                _configService.SetValue("uiScale", scale);
                _configService.Save();

                Helpers.DpiLayout.UiScale = scale;
                Helpers.DpiLayout.ReapplyAll();

                await ToastAsync("toast.uisize.changed", "success");
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private async Task HandleThemeChanged(JsonElement message)
        {
            try
            {
                var value = message.GetProperty("value").GetString() == "light" ? "light" : "dark";
                _configService.SetValue("theme", value);
                _configService.Save();

                Theme.SetTheme(value != "light");

                this.BackColor = Theme.Canvas;
                if (_webView != null) _webView.DefaultBackgroundColor = Theme.Canvas;
                await ExecuteScriptAsync(Helpers.WebViewTheming.SetThemeScript());

                await ToastAsync("toast.theme.changed", "success");
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private async Task HandleChangeDotaPathAsync()
        {
            if (ChangeDotaPathHandler == null) return;
            try
            {
                var (newPath, changed) = await ChangeDotaPathHandler();
                if (string.IsNullOrEmpty(newPath))
                    return;

                if (changed)
                {
                    await ExecuteScriptAsync($"setDotaPath('{EscapeJs(newPath)}')");
                    await ToastAsync("toast.dotaPath.changed", "success");
                }
                else
                {
                    await ToastAsync("toast.dotaPath.unchanged", "success");
                }
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
            }
        }

        private Task ToastAsync(string key, string variant)
            => ExecuteScriptAsync($"showToast('{EscapeJs(_loc?.T(key) ?? key)}', '{variant}')");

        private Task ToastAsync(string key, string variant, object values)
            => ExecuteScriptAsync($"showToast('{EscapeJs(_loc?.T(key, values) ?? key)}', '{variant}')");

        private async Task HandleCheckUpdates()
        {
            bool applierStarted = false;
            try
            {
                var updateInfo = await _updaterService.GetUpdateInfoAsync();
                if (updateInfo?.IsUpdateAvailable == true)
                {
                    applierStarted = UpdateAvailableDialogWebView.Show(
                        this, updateInfo, _updaterService.InstallationType, _updaterService.Delta);
                }
                else
                {
                    await ToastAsync("toast.update.latest", "success");
                }
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.update.checkFailed", "error", new { error = ex.Message });
            }
            finally
            {
                if (!applierStarted)
                    await ExecuteScriptAsync("resetCheckUpdatesButton()");
            }
        }

        private async Task HandleClearCacheAsync()
        {
            try
            {
                var result = await _cacheService.ClearAllCacheAsync();
                if (result.Success)
                {
                    await ToastAsync("toast.cache.cleared", "success",
                        new { size = CacheCleaningService.FormatBytes(result.BytesFreed) });
                    await LoadCacheSizeAsync();
                    if (_configService.PreloadAssetsOnLaunch && _assetPreloadService != null)
                        _ = _assetPreloadService.PreloadAllAsync();
                }
                else
                {
                    await ToastAsync("toast.cache.clearFailed", "error",
                        new { error = result.ErrorMessage ?? (_loc?.T("common.unknown") ?? "Unknown error") });
                }
            }
            catch (Exception ex)
            {
                await ToastAsync("toast.error", "error", new { error = ex.Message });
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
