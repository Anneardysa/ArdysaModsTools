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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Styles;
using ArdysaModsTools.UI.Helpers;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.App;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.UI.Forms;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArdysaModsTools
{
    public partial class MainFormWebView : Form, IMainFormView
    {
        private string? targetPath = null;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly Logger _logger;
        private readonly ModInstallerService _modInstaller;
        private readonly IDetectionService _detection;
        private readonly DotaVersionService _versionService;
        private readonly IConfigService _configService;
        private readonly UI.Presenters.MainFormPresenter _presenter;
        private bool _modFileWarningLogged;

        private TrayService? _trayService;
        private readonly AppLifecycleService _lifecycleService;
        private readonly CacheCleaningService _cacheService;
        private readonly bool _startMinimized;
        private readonly OnboardingService _onboardingService;
        private readonly IServiceProvider _serviceProvider;

        private readonly IAssetPreloadService? _assetPreloadService;
        private readonly ILocalizationService? _loc;
        private EventHandler? _cultureChangedHandler;
        private Action? _themeChangedHandler;
        private readonly CancellationTokenSource _launchCts = new();
        private int _lastPreloadLoggedPercent = -1;

        private WebView2 _webView = null!;
        private ModStatusInfo? _currentStatus;

        private static readonly TimeSpan ShellLoadTimeout = TimeSpan.FromSeconds(30);

        private const string FallbackModspackVersion = "2.6";
        private string _modspackVersion = FallbackModspackVersion;

        private volatile bool _webReady;
        private readonly object _logLock = new();
        private readonly List<string> _logBuffer = new();
        private readonly Dictionary<string, string> _pendingState = new();

        private readonly object _confirmLock = new();
        private int _confirmSeq;
        private readonly Dictionary<int, TaskCompletionSource<bool>> _confirmWaiters = new();

        private bool _bAuto, _bManual, _bInstall, _bDisable, _bPatch, _bMisc, _bHero, _bTweak, _bHighlight;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public MainFormWebView(
            IConfigService configService,
            IDetectionService detectionService,
            IModInstallerService modInstallerService,
            IStatusService statusService,
            IServiceProvider serviceProvider,
            bool startMinimized = false)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _assetPreloadService = serviceProvider.GetService(typeof(IAssetPreloadService)) as IAssetPreloadService;
            _loc = serviceProvider.GetService(typeof(ILocalizationService)) as ILocalizationService;
            _startMinimized = startMinimized;
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _detection = detectionService ?? throw new ArgumentNullException(nameof(detectionService));

            SetupForm();

            _logger = new Logger(
                (msg, cat) => AppendLogWeb(msg, cat),
                (seg, cat) => AppendLogI18nWeb(seg, cat));

            GlobalExceptionHandler.Initialize(_logger);

            _modInstaller = modInstallerService as ModInstallerService
                ?? new ModInstallerService(_logger);
            _modInstaller.SetLogger(_logger);

            _presenter = new UI.Presenters.MainFormPresenter(this, _logger, _configService,
                statusService ?? throw new ArgumentNullException(nameof(statusService)));

            _versionService = new DotaVersionService(_logger);
            _lifecycleService = new AppLifecycleService();
            _cacheService = new CacheCleaningService();
            _onboardingService = new OnboardingService(_configService);

            LoadAppIcon();

            try
            {
                _trayService = new TrayService(this, _configService, this.Icon);
                _trayService.SupportClicked += (s, e) => ShowSupportDialog();
                this.Resize += (s, e) => _trayService?.HandleFormResize();
            }
            catch (Exception ex)
            {
                _logger.Log($"TrayService init failed: {ex.Message}");
            }

            RunPendingUpdateCleanup();

            ArdysaModsTools.Core.Services.FallbackLogger.UserLogger = (msg) =>
            {
                try { _logger.Log(msg); } catch { }
            };

            EnableDetectionButtonsOnly();
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1280, 1000);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainFormWebView";
            Text = "ArdysaModsTools";

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            Controls.Add(_webView);

            this.FormClosing += MainForm_FormClosing;
            this.FormClosed += MainForm_FormClosed;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.Load += OnFormLoad;
            ArdysaModsTools.UI.Helpers.DpiLayout.AttachClamp(this);

            ResumeLayout(false);
        }

        private void LoadAppIcon()
        {
            var icon = AppIconHelper.Load();
            if (icon != null)
                this.Icon = icon;
        }


        private async void OnFormLoad(object? sender, EventArgs e)
        {
            await InitializeWebViewAsync();

            _logger.FlushBufferedLogs();

            _logger.LogLocalized("notice", LogSegment.T("log.notice.regionCdn"));

            try
            {
                await _presenter.InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"Presenter initialization failed: {ex.Message}");
            }

            _ = SmartCdnSelector.Instance.InitializeAsync();

            if (_assetPreloadService != null && _configService.PreloadAssetsOnLaunch)
            {
                var preloadProgress = new Progress<AssetPreloadProgress>(OnPreloadProgress);
                _ = _assetPreloadService.PreloadAllAsync(preloadProgress, _launchCts.Token);
            }


            _ = LoadBannerCarouselAsync();

            _ = LoadLatestUpdatesAsync();

            if (_startMinimized && _trayService != null)
            {
                _trayService.MinimizeToTray();
                return;
            }

            ShowSupportDialogOnStartup();
            ShowOnboardingGuide();
            _trayService?.ShowDonationReminder();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView.EnsureCoreWebView2Async(env);
                ArdysaModsTools.UI.Helpers.DpiLayout.PinTo100(this, _webView);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = Debugger.IsAttached;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                await LoadShellAndWaitReadyAsync();

                await Task.Delay(120);

                MarkWebReadyAndReplay();

                PushInitialAssets();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainFormWebView init failed: {ex.Message}");
                Core.Helpers.StartupLog.Append($"WebView2 init failed: {ex}");
                MessageBox.Show(this,
                    Loc.T("form.webview.initFailed.body", new { error = ex.Message }),
                    Loc.T("form.webview.initFailed.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async Task LoadShellAndWaitReadyAsync()
        {
            var core = _webView.CoreWebView2;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnDomReady(object? s, CoreWebView2DOMContentLoadedEventArgs e) => tcs.TrySetResult(true);
            void OnNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (!e.IsSuccess)
                    tcs.TrySetException(new InvalidOperationException($"WebView2 navigation failed: {e.WebErrorStatus}"));
            }

            core.DOMContentLoaded += OnDomReady;
            core.NavigationCompleted += OnNavCompleted;
            try
            {
                core.NavigateToString(GetShellHtml());

                if (await Task.WhenAny(tcs.Task, Task.Delay(ShellLoadTimeout)) != tcs.Task)
                    throw new TimeoutException($"WebView2 shell did not become ready within {ShellLoadTimeout.TotalSeconds:0}s");

                await tcs.Task;
            }
            finally
            {
                core.DOMContentLoaded -= OnDomReady;
                core.NavigationCompleted -= OnNavCompleted;
            }
        }

        private void MarkWebReadyAndReplay()
        {
            List<string> bufferedLogs;
            lock (_logLock)
            {
                _webReady = true;
                bufferedLogs = new List<string>(_logBuffer);
                _logBuffer.Clear();
            }

            if (_loc != null)
            {
                Exec(WebViewLocalizer.BuildBootstrapScript(_loc));

                _cultureChangedHandler = (s, e) =>
                {
                    if (!_webReady) return;
                    PostExec(WebViewLocalizer.BuildApplyScript(_loc));
                };
                _loc.CultureChanged += _cultureChangedHandler;
            }

            _themeChangedHandler = () =>
            {
                PostExec(UI.Helpers.WebViewTheming.SetThemeScript());
                if (!IsHandleCreated) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        BackColor = Theme.Canvas;
                        if (_webView.CoreWebView2 != null) _webView.DefaultBackgroundColor = Theme.Canvas;
                    }));
                }
                catch {  }
            };
            Theme.ThemeChanged += _themeChangedHandler;

            foreach (var script in _pendingState.Values)
                Exec(script);
            _pendingState.Clear();

            foreach (var call in bufferedLogs)
                Exec(call);
        }

        private void PushInitialAssets()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                if (!string.IsNullOrEmpty(version))
                    Exec($"setVersion({J(version)})");
            }
            catch {  }

            PushNewsVersions();

            try
            {
                string bannerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "banner.jpg");
                if (File.Exists(bannerPath))
                {
                    var dataUri = "data:image/jpeg;base64," + Convert.ToBase64String(File.ReadAllBytes(bannerPath));
                    Exec($"setBanner({J(dataUri)})");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error loading banner: {ex.Message}");
            }
        }

        private void PushNewsVersions()
        {
            var json = JsonSerializer.Serialize(new
            {
                app = FormatCardAppVersion(),
                modspack = "v" + _modspackVersion.TrimStart('v', 'V')
            }, _jsonOptions);
            Js("newsVersions", $"setNewsVersions({json})");
        }

        private static string FormatCardAppVersion()
        {
            var asm = Assembly.GetExecutingAssembly();

            string core = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            int cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) core = core.Substring(0, cut);
            core = core.Trim().TrimStart('v', 'V');

            var asmVersion = asm.GetName().Version;
            if (string.IsNullOrWhiteSpace(core))
                core = asmVersion?.ToString(3) ?? "";

            string label = string.IsNullOrWhiteSpace(core) ? "" : "v" + core;
            if (asmVersion != null && asmVersion.Revision > 0)
                label = $"{label} ({asmVersion.Revision})".Trim();

            return label;
        }

        private async Task LoadBannerCarouselAsync()
        {
            try
            {
                var config = await new BannerService().GetConfigAsync(_launchCts.Token).ConfigureAwait(true);
                if (config == null)
                    return;

                if (!string.IsNullOrWhiteSpace(config.ModspackVersion))
                {
                    _modspackVersion = config.ModspackVersion.Trim();
                    PushNewsVersions();
                }

                if (!string.IsNullOrWhiteSpace(config.InstallCard))
                {
                    string raw = config.InstallCard.Trim();
                    string url = raw.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
                        ? raw
                        : ArdysaModsTools.Core.Constants.CdnConfig.BuildUrl(raw);
                    Js("installCard", $"setInstallCard({J(url)})");
                }

                if (config.Slides == null || config.Slides.Count == 0)
                    return;

                var slides = config.Slides.ConvertAll(s => new
                {
                    image = ArdysaModsTools.Core.Constants.CdnConfig.BuildUrl(s.Image),
                    link = s.Link ?? string.Empty,
                    title = s.Title ?? string.Empty
                });

                string json = JsonSerializer.Serialize(slides, _jsonOptions);
                PostExec($"loadBanners({json})");
            }
            catch (Exception ex)
            {
                _logger.Log($"Banner carousel load skipped: {ex.Message}");
            }
        }

        private async Task LoadLatestUpdatesAsync()
        {
            try
            {
                var heroService = new HeroService(AppDomain.CurrentDomain.BaseDirectory);
                var updatesData = await heroService.LoadSetUpdatesAsync().ConfigureAwait(true);
                var recent = updatesData.GetRecentUpdates(7);
                if (recent.Count == 0)
                    return;

                var heroes = HeroModelMapper.MapFromSummaries(await heroService.LoadHeroesAsync().ConfigureAwait(true));
                var cards = SetUpdateResolver.Resolve(heroes, recent);
                if (cards.Count == 0)
                    return;

                string json = JsonSerializer.Serialize(cards, _jsonOptions);
                Js("latestUpdates", $"loadLatestUpdates({json})");
            }
            catch (Exception ex)
            {
                _logger.Log($"Latest updates strip skipped: {ex.Message}");
            }
        }

        private string GetShellHtml()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "main_shell.html");
            return UI.Helpers.WebViewTheming.Apply(File.ReadAllText(htmlPath));
        }

        private void RunPendingUpdateCleanup()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 6 || args[1] != "--update")
                return;

            string currentExe = args[2];
            string backupExe = args[3];
            string tempArchive = args[4];
            string tempDir = args[5];

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000);
                    if (File.Exists(backupExe)) File.Delete(backupExe);
                    string thisExe = Process.GetCurrentProcess().MainModule!.FileName;
                    File.Move(thisExe, currentExe, true);
                    if (File.Exists(tempArchive)) File.Delete(tempArchive);
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Update cleanup failed: {ex.Message}");
                    InvokeOnUIThread(() =>
                    {
                        MessageBox.Show(this, Loc.T("form.update.cleanupFailed", new { error = ex.Message }), Loc.T("common.error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    });
                }
            });
        }

        private void OnPreloadProgress(AssetPreloadProgress p)
        {
            switch (p.Phase)
            {
                case AssetPreloadPhase.Enumerating:
                    _lastPreloadLoggedPercent = -1;
                    _logger.Log("Launching State: preparing asset cache…");
                    break;

                case AssetPreloadPhase.Downloading:
                    if (p.Total <= 0) break;
                    int percent = (int)(p.Current * 100L / p.Total);
                    if (percent >= _lastPreloadLoggedPercent + 10 || p.Current == p.Total)
                    {
                        _lastPreloadLoggedPercent = percent - (percent % 10);
                        _logger.Log($"Launching State: caching assets… {p.Current}/{p.Total} ({percent}%)");
                    }
                    break;

                case AssetPreloadPhase.Complete:
                    if (p.Failed > 0)
                        _logger.Log($"Launching State: asset cache ready — {p.Total - p.Failed}/{p.Total} ({p.Failed} unavailable; check your connection).");
                    else
                        _logger.Log("Launching State: asset cache ready.");
                    break;
            }
        }

        private void ShowSupportDialogOnStartup()
        {
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (_configService.SupportPromptSnoozeDate == today)
                    return;

                using var supportDialog = new UI.Forms.SupportDialog(startupCountdownSeconds: 5);
                supportDialog.StartPosition = FormStartPosition.CenterParent;
                supportDialog.ShowDialog(this);

                if (supportDialog.SnoozeToday)
                    _configService.SupportPromptSnoozeDate = today;
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to show support dialog: {ex.Message}");
            }
        }

        private static readonly Dictionary<string, string> OnboardingTargetMap = new()
        {
            ["autoDetectButton"] = "btn-autoDetect",
            ["manualDetectButton"] = "btn-manualDetect",
            ["btn_OpenSelectHero"] = "btn-hero",
            ["miscellaneousButton"] = "btn-misc",
            ["installButton"] = "btn-install",
            ["updatePatcherButton"] = "btn-patch",
            ["consolePanel"] = "console-panel",
            ["btnSettings"] = "btn-settings",
        };

        private void ShowOnboardingGuide()
        {
            if (_onboardingService.IsOnboardingCompleted()) return;
            PushOnboardingToWeb();
        }

        private void PushOnboardingToWeb()
        {
            try
            {
                var mapped = new List<object>();
                foreach (var step in _onboardingService.GetSteps())
                {
                    if (!OnboardingTargetMap.TryGetValue(step.ControlName, out var target))
                        continue;
                    mapped.Add(new
                    {
                        target,
                        title = step.Title,
                        desc = step.Description,
                        pad = step.SpotlightPadding
                    });
                }

                if (mapped.Count == 0) return;

                string json = JsonSerializer.Serialize(mapped, _jsonOptions);
                PostExec($"startOnboarding({json})");
            }
            catch (Exception ex)
            {
                _logger.Log($"Onboarding guide error: {ex.Message}");
                _onboardingService.MarkOnboardingCompleted();
            }
        }

        public void ResetAndShowOnboarding()
        {
            _onboardingService.ResetOnboarding();
            PushOnboardingToWeb();
        }


        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_presenter.IsOperationRunning)
                return;

            e.Cancel = true;
            DisableAllButtons();

            Task.Run(async () =>
            {
                await _presenter.ShutdownAsync();
                BeginInvoke(new Action(() => Close()));
            });
        }

        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            try { if (_loc != null && _cultureChangedHandler != null) _loc.CultureChanged -= _cultureChangedHandler; } catch {  }
            try { if (_themeChangedHandler != null) Theme.ThemeChanged -= _themeChangedHandler; } catch {  }
            try { _launchCts.Cancel(); _launchCts.Dispose(); } catch {  }
            try { _presenter.Dispose(); } catch (Exception ex) { _logger.Log($"Presenter dispose failed: {ex.Message}"); }
            try { _trayService?.Dispose(); } catch (Exception ex) { _logger.Log($"TrayService dispose failed: {ex.Message}"); }
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.H)
            {
                _ = _presenter.OpenHeroSelectionAsync();
                e.Handled = true;
            }
        }


        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? type, url = null, copyText = null;
            int? modalId = null;
            bool modalOk = false;
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                type = message.GetProperty("type").GetString();
                if (message.TryGetProperty("url", out var urlEl)) url = urlEl.GetString();
                if (message.TryGetProperty("text", out var textEl)) copyText = textEl.GetString();
                if (message.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    modalId = idEl.GetInt32();
                if (message.TryGetProperty("ok", out var okEl))
                    modalOk = okEl.ValueKind == JsonValueKind.True;
            }
            catch (Exception ex)
            {
                _logger.Log($"Web message parse error: {ex.Message}");
                return;
            }

            try { BeginInvoke(new Action(() => _ = HandleMessageAsync(type, url, copyText, modalId, modalOk))); }
            catch {  }
        }

        private async Task HandleMessageAsync(string? type, string? url, string? copyText, int? modalId, bool modalOk)
        {
            try
            {
                switch (type)
                {
                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;
                    case "minimize":
                        this.WindowState = FormWindowState.Minimized;
                        break;
                    case "close":
                        Close();
                        break;
                    case "settings":
                        OpenSettingsDialog();
                        break;
                    case "about":
                        OpenAboutDialog();
                        break;
                    case "tweak":
                        OpenPerformanceTweak();
                        break;
                    case "whatsnew":
                        OpenWhatsNewDialog();
                        break;
                    case "modspackUpdates":
                        OpenModsPackUpdatesDialog();
                        break;

                    case "autoDetect":
                        await _presenter.AutoDetectAsync();
                        break;
                    case "manualDetect":
                        await _presenter.ManualDetectAsync();
                        break;
                    case "install":
                        await _presenter.InstallAsync();
                        break;
                    case "disable":
                        await _presenter.DisableWithOptionsAsync();
                        break;
                    case "patchClick":
                        await _presenter.HandlePatcherClickAsync();
                        break;
                    case "skinSelector":
                        await _presenter.OpenHeroSelectionAsync();
                        break;
                    case "miscellaneous":
                        await _presenter.OpenMiscellaneousAsync();
                        break;
                    case "refreshStatus":
                        await _presenter.RefreshStatusAsync();
                        break;

                    case "patchApply":
                        await _presenter.ExecutePatchAsync();
                        break;
                    case "patchVerify":
                        await _presenter.VerifyModFilesAsync();
                        break;
                    case "patchViewStatus":
                        _presenter.ShowStatusDetails();
                        break;

                    case "support":
                        ShowSupportDialog();
                        break;
                    case "copyConsole":
                        HandleCopyConsole(copyText);
                        break;
                    case "openUrl":
                        if (!string.IsNullOrEmpty(url))
                            UIHelpers.OpenUrlWithErrorDialog(url, "Link", _logger.Log);
                        break;

                    case "shellModalResult":
                        if (modalId.HasValue)
                            CompleteShellConfirm(modalId.Value, modalOk);
                        break;

                    case "onboardingDone":
                        _onboardingService.MarkOnboardingCompleted();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error handling '{type}': {ex.Message}");
            }
        }

        private void CompleteShellConfirm(int id, bool ok)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_confirmLock)
            {
                _confirmWaiters.TryGetValue(id, out tcs);
                _confirmWaiters.Remove(id);
            }
            tcs?.TrySetResult(ok);
        }

        private void HandleCopyConsole(string? text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return;
                Clipboard.SetText(text);
                _logger.Log("Console text copied to clipboard.");
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to copy: {ex.Message}");
            }
        }

        private void OpenPerformanceTweak()
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                _logger.Log("Detect your Dota 2 path first to use Performance Tweak.");
                return;
            }

            try
            {
                using var perfForm = ActivatorUtilities.CreateInstance<UI.Forms.Dota2PerformanceForm>(
                    _serviceProvider, targetPath);
                perfForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open Performance Tweak: {ex.Message}");
            }
        }

        private void OpenAboutDialog()
        {
            try
            {
                using var about = new AboutDialogWebView { StartPosition = FormStartPosition.CenterParent };
                about.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open About: {ex.Message}");
            }
        }

        private void OpenWhatsNewDialog()
        {
            try
            {
                using var whatsNew = new WhatsNewDialogWebView { StartPosition = FormStartPosition.CenterParent };
                whatsNew.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open What's New: {ex.Message}");
            }
        }

        private void OpenModsPackUpdatesDialog()
        {
            try
            {
                using var dialog = new ModsPackUpdatesDialogWebView(_modspackVersion)
                {
                    StartPosition = FormStartPosition.CenterParent
                };
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open ModsPack updates: {ex.Message}");
            }
        }

        private void OpenSettingsDialog()
        {
            try
            {
                var updaterService = _presenter.GetUpdaterService();
                if (updaterService == null)
                {
                    _logger.Log("UpdaterService not available for Settings.");
                    return;
                }

                using var settingsForm = new SettingsFormWebView(
                    _configService,
                    _lifecycleService,
                    _cacheService,
                    updaterService,
                    _trayService,
                    _assetPreloadService);

                settingsForm.ShowGuideRequested += (s, e) => ResetAndShowOnboarding();
                settingsForm.ChangeDotaPathHandler = () => _presenter.ChangeTargetPathAsync();
                settingsForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open Settings: {ex.Message}");
            }
        }


        private static string J(object? value) => JsonSerializer.Serialize(value, _jsonOptions);

        private void Exec(string script)
        {
            try { _ = _webView.CoreWebView2!.ExecuteScriptAsync(script); }
            catch {  }
        }

        private void Js(string key, string script)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(() => Js(key, script))); } catch { } return; }

            if (_webReady && _webView.CoreWebView2 != null)
                Exec(script);
            else
                _pendingState[key] = script;
        }

        private void PostExec(string script)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(() => PostExec(script))); } catch { } return; }
            if (_webReady && _webView.CoreWebView2 != null) Exec(script);
        }

        private void AppendLogWeb(string msg, string cat)
        {
            var call = $"appendLog({J(msg)},{J(cat)})";
            lock (_logLock)
            {
                if (!_webReady) { _logBuffer.Add(call); return; }
            }
            PostExec(call);
        }

        private void AppendLogI18nWeb(string segmentsJson, string cat)
        {
            var call = $"appendLogI18n({J(segmentsJson)},{J(cat)})";
            lock (_logLock)
            {
                if (!_webReady) { _logBuffer.Add(call); return; }
            }
            PostExec(call);
        }

        private static string ColorHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void PushStatus(Color color, string text, string tooltip)
        {
            var json = JsonSerializer.Serialize(new { color = ColorHex(color), text, tooltip }, _jsonOptions);
            Js("status", $"setStatus({json})");
        }

        private void PushButtons()
        {
            var json = JsonSerializer.Serialize(new
            {
                autoDetect = _bAuto,
                manualDetect = _bManual,
                install = _bInstall,
                disable = _bDisable,
                patch = _bPatch,
                misc = _bMisc,
                hero = _bHero,
                tweak = _bTweak,
                detectHighlight = _bHighlight
            }, _jsonOptions);
            Js("buttons", $"setButtonStates({json})");
        }


        public void SetModsStatus(bool isActive, string statusText)
        {
            var color = isActive ? Color.FromArgb(0, 255, 100) : Color.FromArgb(255, 80, 80);
            PushStatus(color, statusText, string.Empty);
        }

        public void SetModsStatusDetailed(ModStatusInfo statusInfo)
        {
            _currentStatus = statusInfo;

            var tooltip = statusInfo.Description ?? string.Empty;
            if (!string.IsNullOrEmpty(statusInfo.Version))
                tooltip += $"\n\nVersion: {statusInfo.Version}";
            if (statusInfo.LastModified.HasValue)
                tooltip += $"\nLast Modified: {statusInfo.LastModified.Value:g}";
            if (!string.IsNullOrEmpty(statusInfo.ActionButtonText))
                tooltip += $"\n\n-> {statusInfo.ActionButtonText}";

            PushStatus(statusInfo.StatusColor, statusInfo.StatusText, tooltip);
            UpdateButtonsForStatus(statusInfo);
        }

        public void ShowCheckingState()
        {
            Js("status", "showChecking()");
        }

        public void SetVersion(string version)
        {
            Js("version", $"setVersion({J(version)})");
        }

        public void SetDotaRunningState(bool isRunning)
        {
            Js("dota", $"setDotaWarning({(isRunning ? "true" : "false")})");
        }


        public void DisableAllButtons()
        {
            _bAuto = _bManual = _bInstall = _bDisable = _bPatch = _bMisc = _bHero = _bTweak = false;
            _bHighlight = false;
            PushButtons();
        }

        public void EnableDetectionButtonsOnly()
        {
            _bAuto = _bManual = true;
            _bInstall = _bDisable = _bPatch = _bMisc = _bHero = _bTweak = false;
            _bHighlight = true;
            PushButtons();
        }

        public void EnableAllButtons()
        {
            bool hasPath = targetPath != null;
            _bAuto = !hasPath;
            _bManual = true;
            _bInstall = hasPath;
            _bDisable = hasPath;
            _bMisc = hasPath;
            _bHero = hasPath;
            _bTweak = hasPath;
            _bPatch = hasPath && IsRequiredModFilePresent();
            _bHighlight = false;
            PushButtons();
        }

        public void SetButtonEnabled(string buttonName, bool enabled)
        {
            switch (buttonName.ToLowerInvariant())
            {
                case "autodetect": _bAuto = enabled; break;
                case "manualdetect": _bManual = enabled; break;
                case "install": _bInstall = enabled; break;
                case "disable": _bDisable = enabled; break;
                case "updatepatcher":
                case "patch": _bPatch = enabled; break;
                case "miscellaneous":
                case "misc": _bMisc = enabled; break;
                case "selecthero":
                case "hero": _bHero = enabled; break;
                case "performancetweak":
                case "tweak": _bTweak = enabled; break;
                default: return;
            }
            PushButtons();
        }

        private bool IsRequiredModFilePresent()
        {
            if (string.IsNullOrEmpty(targetPath))
                return false;

            string requiredFilePath = Path.Combine(targetPath, ArdysaModsTools.Core.Constants.DotaPaths.ModsVpk);
            bool fileExists = File.Exists(requiredFilePath);

            if (!fileExists && !_modFileWarningLogged)
            {
                _logger.Log("Required mod file 'ArdysaMods/pak01_dir.vpk' not found. Please install mods first.");
                _modFileWarningLogged = true;
            }
            else if (fileExists)
            {
                _modFileWarningLogged = false;
            }

            return fileExists;
        }


        public void UpdatePatchButtonStatus(ModStatus? status, bool isError = false)
        {
            string? key = status switch
            {
                ModStatus.Ready => "ready",
                ModStatus.NeedUpdate => "needUpdate",
                _ => null
            };
            Js("patch", $"setPatchButton({J(key)},{(isError ? "true" : "false")})");
        }

        public void UpdateButtonsForStatus(ModStatusInfo statusInfo)
        {
            UpdatePatchButtonStatus(statusInfo.Status, statusInfo.Status == ModStatus.Error);
        }

        public void SetPatchDetectedStatus()
        {
            PushStatus(Color.FromArgb(255, 165, 0), Loc.T("status.updateDetected.text"), string.Empty);
            UpdatePatchButtonStatus(ModStatus.NeedUpdate);
            SetButtonEnabled("patch", true);
        }

        public void ShowPatchMenu()
        {
            PostExec("showPatchMenu()");
        }


        public void Log(string message) => _logger.Log(message);

        public void ClearLog()
        {
            lock (_logLock)
            {
                if (!_webReady) { _logBuffer.Clear(); return; }
            }
            PostExec("clearLog()");
        }
    }
}
