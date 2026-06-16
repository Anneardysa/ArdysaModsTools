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
    /// <summary>
    /// WebView2-based main window shell. Functionally equivalent to <see cref="MainForm"/> but
    /// renders its entire chrome (title bar, sidebar, content, console) as a hybrid web UI defined
    /// in <c>Assets/Html/main_shell.html</c>. It is a thin host: it owns the same
    /// <see cref="UI.Presenters.MainFormPresenter"/> and implements the identical
    /// <see cref="IMainFormView"/> contract, reflecting state into the page via
    /// <c>ExecuteScriptAsync</c> and routing user intents back through <c>WebMessageReceived</c>.
    ///
    /// The classic <see cref="MainForm"/> remains the fallback when WebView2 is unavailable
    /// (selection happens in <see cref="UI.Factories.MainFormFactory"/>).
    /// </summary>
    public partial class MainFormWebView : Form, IMainFormView
    {
        private string? targetPath = null;

        // For rounded form
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        // P/Invoke for reliable window dragging
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // Services - obtained via DI (mirrors MainForm)
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
        private readonly CancellationTokenSource _launchCts = new();
        private int _lastPreloadLoggedPercent = -1;

        // ── WebView host state ──────────────────────────────────────────────────────────
        private WebView2 _webView = null!;
        private ModStatusInfo? _currentStatus;

        // Bundled fallback ModsPack version shown on the What's New card until the R2
        // banner manifest (config/banner.json → "modspackVersion") supplies the live value.
        private const string FallbackModspackVersion = "2.6";
        private string _modspackVersion = FallbackModspackVersion;

        // [AMT:PRO] Bridge state. _webReady gates whether interop calls execute immediately or are
        // deferred. The JS function names below are a contract with main_shell.html — keep in sync.
        private volatile bool _webReady;
        private readonly object _logLock = new();
        private readonly List<(string msg, string cat)> _logBuffer = new();
        // Latest idempotent state scripts, keyed by JS function, replayed once the page is ready.
        private readonly Dictionary<string, string> _pendingState = new();

        // Button enabled snapshot (reflected into setButtonStates).
        private bool _bAuto, _bManual, _bInstall, _bDisable, _bPatch, _bMisc, _bHero, _bTweak, _bHighlight;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// DI-enabled constructor. Same signature as <see cref="MainForm"/> so the factory can build
        /// either shell from the identical dependency set.
        /// </summary>
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
            _startMinimized = startMinimized;
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _detection = detectionService ?? throw new ArgumentNullException(nameof(detectionService));

            SetupForm();

            // LOGGER: route every line to the WebView console (buffered until the page is ready).
            _logger = new Logger((msg, cat) => AppendLogWeb(msg, cat));

            GlobalExceptionHandler.Initialize(_logger);

            _modInstaller = modInstallerService as ModInstallerService
                ?? new ModInstallerService(_logger);
            _modInstaller.SetLogger(_logger);

            // PRESENTER (MVP). Owns the Dota 2 monitor + mod-status pipeline.
            _presenter = new UI.Presenters.MainFormPresenter(this, _logger, _configService);

            _versionService = new DotaVersionService(_logger);
            _lifecycleService = new AppLifecycleService();
            _cacheService = new CacheCleaningService();
            _onboardingService = new OnboardingService(_configService);

            // Tray
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

            LoadAppIcon();
            RunPendingUpdateCleanup();

            // Hook fallback logger to UI logger
            ArdysaModsTools.Core.Services.FallbackLogger.UserLogger = (msg) =>
            {
                try { _logger.Log(msg); } catch { }
            };

            EnableDetectionButtonsOnly();
        }

        /// <summary>
        /// Builds the borderless rounded host window and the single docked WebView2 control.
        /// </summary>
        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1040, 780);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            BackColor = Color.Black;
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainFormWebView";
            Text = "ArdysaModsTools";

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            Controls.Add(_webView);

            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            this.FormClosing += MainForm_FormClosing;
            this.FormClosed += MainForm_FormClosed;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.Load += OnFormLoad;

            ResumeLayout(false);
        }

        /// <summary>
        /// Loads the application icon from disk (used by the window + tray). Best-effort.
        /// </summary>
        private void LoadAppIcon()
        {
            try
            {
                string relPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "AppIcon.ico");
                if (File.Exists(relPath))
                {
                    this.Icon = new Icon(relPath);
                    return;
                }

                string? devAssetsPath = Environment.GetEnvironmentVariable("AMT_DEV_ASSETS_PATH");
                if (!string.IsNullOrEmpty(devAssetsPath))
                {
                    string devPath = Path.Combine(devAssetsPath, "Icons", "AppIcon.ico");
                    if (File.Exists(devPath))
                        this.Icon = new Icon(devPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error loading application icon: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // STARTUP
        // ══════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes the WebView first (so the shell is interactive and view callbacks render),
        /// then runs the presenter init + the same startup sequence as the classic form.
        /// </summary>
        private async void OnFormLoad(object? sender, EventArgs e)
        {
            await InitializeWebViewAsync();

            _logger.FlushBufferedLogs();

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

            EnableDetectionButtonsOnly();

            // Replace the bundled banner with the R2 carousel when a manifest is available (non-blocking).
            _ = LoadBannerCarouselAsync();

            // If launched with --minimized (Windows startup), go straight to tray; skip popups.
            if (_startMinimized && _trayService != null)
            {
                _trayService.MinimizeToTray();
                return;
            }

            ShowSupportDialogOnStartup();
            ShowOnboardingGuide();
            _trayService?.ShowDonationReminder();
        }

        /// <summary>
        /// Boots the WebView2 environment, wires interop, loads the shell HTML and pushes initial
        /// state. On failure the shell cannot render — surfaces an error and closes (the factory
        /// gate already confirmed a runtime, so this path is rare).
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = Debugger.IsAttached;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Serve CDN images (e.g. simpleicons fallbacks) from the persistent asset cache.
                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                var tcs = new TaskCompletionSource<bool>();
                void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs ev)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNav;
                    tcs.TrySetResult(ev.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += OnNav;

                _webView.CoreWebView2.NavigateToString(GetShellHtml());

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000));
                if (completed != tcs.Task)
                    throw new TimeoutException("WebView2 navigation timeout");

                await Task.Delay(120); // let the DOM settle

                MarkWebReadyAndReplay();

                PushInitialAssets();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainFormWebView init failed: {ex.Message}");
                MessageBox.Show(this,
                    "Failed to initialize the WebView2 interface.\n\n" + ex.Message +
                    "\n\nPlease reinstall the Microsoft Edge WebView2 Runtime.",
                    "Interface Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        /// <summary>
        /// Flips the ready flag, replays the latest idempotent state and drains the log buffer.
        /// Runs on the UI thread (continuation of <see cref="InitializeWebViewAsync"/>).
        /// </summary>
        private void MarkWebReadyAndReplay()
        {
            List<(string msg, string cat)> bufferedLogs;
            lock (_logLock)
            {
                _webReady = true;
                bufferedLogs = new List<(string, string)>(_logBuffer);
                _logBuffer.Clear();
            }

            foreach (var script in _pendingState.Values)
                Exec(script);
            _pendingState.Clear();

            foreach (var (msg, cat) in bufferedLogs)
                Exec($"appendLog({J(msg)},{J(cat)})");
        }

        /// <summary>
        /// Pushes the assembly version (placeholder until the presenter sets the live one) and the
        /// banner image as a base64 data URI.
        /// </summary>
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
            catch { /* non-critical */ }

            // What's New card version badges (app from the assembly, ModsPack fallback until R2 loads).
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

        /// <summary>
        /// Pushes the What's New card version badges: the app version (clean semver + build number,
        /// e.g. <c>v2.1.27 (2138)</c>) and the current ModsPack version. Idempotent and replay-safe.
        /// </summary>
        private void PushNewsVersions()
        {
            var json = JsonSerializer.Serialize(new
            {
                app = FormatCardAppVersion(),
                modspack = "v" + _modspackVersion.TrimStart('v', 'V')
            }, _jsonOptions);
            Js("newsVersions", $"setNewsVersions({json})");
        }

        /// <summary>
        /// Builds the card's app-version label from assembly metadata: the clean semver core from the
        /// informational version (pre-release/build metadata stripped) plus the build number taken from
        /// the assembly version's revision component. Falls back gracefully when parts are missing.
        /// </summary>
        private static string FormatCardAppVersion()
        {
            var asm = Assembly.GetExecutingAssembly();

            string core = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            int cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) core = core.Substring(0, cut);
            core = core.Trim().TrimStart('v', 'V');

            var asmVersion = asm.GetName().Version; // AssemblyVersion, e.g. 2.1.27.2138
            if (string.IsNullOrWhiteSpace(core))
                core = asmVersion?.ToString(3) ?? "";

            string label = string.IsNullOrWhiteSpace(core) ? "" : "v" + core;
            if (asmVersion != null && asmVersion.Revision > 0)
                label = $"{label} ({asmVersion.Revision})".Trim();

            return label;
        }

        // [AMT:PRO] Bridge push — paired with loadBanners(...) in main_shell.html. The slide JSON shape
        // (image/link/title) is a contract with the carousel renderer; keep both sides in sync.
        /// <summary>
        /// Fetches the banner carousel manifest from R2 (with CDN fallback) and pushes the slides to the
        /// page. Best-effort: on any failure the bundled banner pushed by <see cref="PushInitialAssets"/>
        /// stays visible.
        /// </summary>
        private async Task LoadBannerCarouselAsync()
        {
            try
            {
                var config = await new BannerService().GetConfigAsync(_launchCts.Token).ConfigureAwait(true);
                if (config == null)
                    return;

                // Refresh the ModsPack version badge from R2 (overrides the bundled fallback).
                if (!string.IsNullOrWhiteSpace(config.ModspackVersion))
                {
                    _modspackVersion = config.ModspackVersion.Trim();
                    PushNewsVersions();
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

        private string GetShellHtml()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "main_shell.html");
            return File.ReadAllText(htmlPath);
        }

        /// <summary>
        /// Finalizes a self-update when relaunched with the "--update" handshake. Ported verbatim
        /// from <see cref="MainForm"/>.
        /// </summary>
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
                        MessageBox.Show(this, $"Update cleanup failed: {ex.Message}", "Error",
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
                using var supportDialog = new UI.Forms.SupportDialog();
                supportDialog.StartPosition = FormStartPosition.CenterParent;
                supportDialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to show support dialog: {ex.Message}");
            }
        }

        // [AMT:PRO] Maps OnboardingService control names (authored against the classic WinForms
        // MainForm) onto the DOM element ids in main_shell.html. The GDI OnboardingOverlay cannot be
        // used on the WebView shell: DrawToBitmap does not capture WebView2 content and the buttons
        // are HTML elements, not WinForms controls. Keep these ids in sync with main_shell.html.
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

        /// <summary>
        /// Projects the onboarding steps onto their DOM targets and starts the in-page guide overlay.
        /// Completion is persisted when the page posts back "onboardingDone".
        /// </summary>
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

        /// <summary>
        /// Resets and re-shows the onboarding guide (called from Settings).
        /// </summary>
        public void ResetAndShowOnboarding()
        {
            _onboardingService.ResetOnboarding();
            PushOnboardingToWeb();
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════════════════

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
            try { _launchCts.Cancel(); _launchCts.Dispose(); } catch { /* best-effort */ }
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

        private void ApplyRoundedForm()
        {
            int radius = 16;
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, radius, radius));
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // C# ← JS BRIDGE
        // ══════════════════════════════════════════════════════════════════════════════════

        // [AMT:PRO] Message dispatch — paired with main_shell.html post()/send() callers.
        // The 'type' strings are a shared contract; changing one side requires the other.
        // Handling is deferred onto a fresh message-loop turn via BeginInvoke: opening a modal
        // dialog (ShowDialog) or creating a child WebView2 *synchronously inside* this WebView2
        // event callback reenters the WebView2 message loop and hard-crashes the process
        // (STATUS_BREAKPOINT 0x80000003). Only primitive payload values are captured so the
        // JsonElement does not escape the callback.
        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? type, url = null, copyText = null;
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                type = message.GetProperty("type").GetString();
                if (message.TryGetProperty("url", out var urlEl)) url = urlEl.GetString();
                if (message.TryGetProperty("text", out var textEl)) copyText = textEl.GetString();
            }
            catch (Exception ex)
            {
                _logger.Log($"Web message parse error: {ex.Message}");
                return;
            }

            try { BeginInvoke(new Action(() => _ = HandleMessageAsync(type, url, copyText))); }
            catch { /* form tearing down */ }
        }

        private async Task HandleMessageAsync(string? type, string? url, string? copyText)
        {
            try
            {
                switch (type)
                {
                    // ── window chrome ──
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

                    // ── primary actions ──
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

                    // ── patch dropdown ──
                    case "patchApply":
                        await _presenter.ExecutePatchAsync();
                        break;
                    case "patchVerify":
                        await _presenter.VerifyModFilesAsync();
                        break;
                    case "patchViewStatus":
                        _presenter.ShowStatusDetails();
                        break;

                    // ── misc ──
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

                    // ── onboarding guide (DOM overlay finished/skipped) ──
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
            // Gated behind path detection (see EnableAllButtons): autoexec.cfg cannot be resolved
            // without a known install path. Defensive guard in case the action arrives out-of-band.
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
                settingsForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open Settings: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // C# → JS BRIDGE (interop plumbing)
        // ══════════════════════════════════════════════════════════════════════════════════

        /// <summary>Serializes a value to a safe JS literal (string → quoted/escaped, etc.).</summary>
        private static string J(object? value) => JsonSerializer.Serialize(value, _jsonOptions);

        /// <summary>Executes a script immediately (must be on the UI thread, page must be ready).</summary>
        private void Exec(string script)
        {
            try { _ = _webView.CoreWebView2!.ExecuteScriptAsync(script); }
            catch { /* control may be tearing down */ }
        }

        /// <summary>
        /// Pushes an idempotent state script. If the page is not ready it is stored under
        /// <paramref name="key"/> (latest wins) and replayed on ready. Marshals to the UI thread.
        /// </summary>
        private void Js(string key, string script)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(() => Js(key, script))); } catch { } return; }

            if (_webReady && _webView.CoreWebView2 != null)
                Exec(script);
            else
                _pendingState[key] = script;
        }

        /// <summary>Fires a one-shot script if the page is ready (no deferral). Marshals to UI thread.</summary>
        private void PostExec(string script)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(() => PostExec(script))); } catch { } return; }
            if (_webReady && _webView.CoreWebView2 != null) Exec(script);
        }

        private void AppendLogWeb(string msg, string cat)
        {
            lock (_logLock)
            {
                if (!_webReady) { _logBuffer.Add((msg, cat)); return; }
            }
            PostExec($"appendLog({J(msg)},{J(cat)})");
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

        // ══════════════════════════════════════════════════════════════════════════════════
        // IMainFormView — Status Updates
        // ══════════════════════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════════════════════
        // IMainFormView — Button States
        // ══════════════════════════════════════════════════════════════════════════════════

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
            _bAuto = _bManual = true;
            bool hasPath = targetPath != null;
            _bInstall = hasPath;
            _bDisable = hasPath;
            _bMisc = hasPath;
            _bHero = hasPath;
            // Performance Tweak writes autoexec.cfg into the Dota 2 cfg folder, which can only be
            // resolved from a known install path — gate it behind detection like the other mod tools.
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

        // ══════════════════════════════════════════════════════════════════════════════════
        // IMainFormView — Extended Status Updates
        // ══════════════════════════════════════════════════════════════════════════════════

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
            UpdatePatchButtonStatus(statusInfo.Status);
        }

        public void SetPatchDetectedStatus()
        {
            PushStatus(Color.FromArgb(255, 165, 0), "Update Detected", string.Empty);
            UpdatePatchButtonStatus(ModStatus.NeedUpdate);
            SetButtonEnabled("patch", true);
        }

        public void ShowPatchMenu()
        {
            PostExec("showPatchMenu()");
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // IMainFormView — Logging
        // ══════════════════════════════════════════════════════════════════════════════════

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
