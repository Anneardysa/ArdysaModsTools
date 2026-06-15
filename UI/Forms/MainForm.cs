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
using ArdysaModsTools.UI.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools
{
    public partial class MainForm : Form, IMainFormView
    {
        private string? targetPath = null;


        // For rounded form
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);



        // Services - obtained via DI
        private readonly Logger _logger;
        // Note: UpdaterService is now handled by MainFormPresenter
        private readonly ModInstallerService _modInstaller;
        private readonly IDetectionService _detection;
        private readonly DotaVersionService _versionService;
        private readonly IConfigService _configService;
        // Note: Dota 2 process monitoring and mod-status checks are owned solely by
        // MainFormPresenter. The form only reflects state via IMainFormView callbacks.

        // Presenter for MVP pattern
        private readonly UI.Presenters.MainFormPresenter _presenter;
        private bool _modFileWarningLogged; // Prevent duplicate logging
        
        // Tray and Settings Services
        private TrayService? _trayService;
        private readonly AppLifecycleService _lifecycleService;
        private readonly CacheCleaningService _cacheService;
        private readonly bool _startMinimized;

        // Onboarding guide
        private readonly OnboardingService _onboardingService;

        private readonly IServiceProvider _serviceProvider;

        // "Launching State" background asset preload + its cancellation on app close.
        private readonly IAssetPreloadService? _assetPreloadService;
        private readonly System.Threading.CancellationTokenSource _launchCts = new();
        private int _lastPreloadLoggedPercent = -1;

        /// <summary>
        /// Default constructor is disabled. Use MainFormFactory.Create() for proper DI.
        /// </summary>
        [Obsolete("Use MainFormFactory.Create() for proper DI. This constructor throws NotSupportedException.", true)]
        public MainForm()
        {
            throw new NotSupportedException(
                "Default constructor is disabled. Use MainFormFactory.Create() to create MainForm with proper dependency injection.");
        }

        /// <summary>
        /// DI-enabled constructor for proper dependency injection.
        /// </summary>
        /// <param name="configService">Configuration service.</param>
        /// <param name="detectionService">Dota 2 detection service.</param>
        /// <param name="modInstallerService">Mod installation service.</param>
        /// <param name="statusService">Mod status service.</param>
        /// <param name="startMinimized">If true, start minimized to system tray (Windows startup).</param>
        public MainForm(
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
            // Store injected dependencies
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _detection = detectionService ?? throw new ArgumentNullException(nameof(detectionService));

            InitializeComponent();

            // Use embedded JetBrains Mono font for console
            mainConsoleBox.Font = FontHelper.CreateMono(8F, FontStyle.Regular);

            // Apply rounded corners to form
            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            this.FormClosing += MainForm_FormClosing;
            this.FormClosed += MainForm_FormClosed;

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // ═══════════════════════════════════════════════════════════════
            // LOGGER INITIALIZATION
            // ═══════════════════════════════════════════════════════════════
            _logger = new Logger(mainConsoleBox);
            
            // Initialize global exception handler with logger
            GlobalExceptionHandler.Initialize(_logger);

            // ═══════════════════════════════════════════════════════════════
            // SERVICE INITIALIZATION
            // Services that need logger are created here (can't inject logger
            // because it needs the UI control which is created in InitializeComponent)
            // ═══════════════════════════════════════════════════════════════
            // Note: UpdaterService is now initialized in MainFormPresenter
            // Version label updates are handled via IMainFormView.SetVersion()

            // Use injected services where available, create others with logger
            _modInstaller = modInstallerService as ModInstallerService 
                ?? new ModInstallerService(_logger);
            
            // Ensure the service uses the correct UI logger (fix for console logs not showing)
            _modInstaller.SetLogger(_logger);

            // PRESENTER INITIALIZATION (MVP Pattern)
            // The presenter owns the Dota 2 process monitor and the mod-status pipeline.
            _presenter = new UI.Presenters.MainFormPresenter(this, _logger, _configService);

            _versionService = new DotaVersionService(_logger);

            // Initialize services for Settings
            _lifecycleService = new AppLifecycleService();
            _cacheService = new CacheCleaningService();

            // Initialize onboarding service
            _onboardingService = new OnboardingService(_configService);

            // Initialize TrayService (after presenter so we can pass configService)
            try
            {
                _trayService = new TrayService(this, _configService, this.Icon);
                _trayService.SupportClicked += (s, e) => PaypalPictureBox_Click(s, e);
                this.Resize += (s, e) => _trayService?.HandleFormResize();
            }
            catch (Exception ex)
            {
                _logger.Log($"TrayService init failed: {ex.Message}");
            }


            // ═══════════════════════════════════════════════════════════════
            // ICON LOADING - Direct file loading (most reliable)
            // ═══════════════════════════════════════════════════════════════
            Icon? appIcon = null;
            try
            {
                // Primary: Relative path (for deployment)
                string relPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "AppIcon.ico");
                if (File.Exists(relPath))
                {
                    appIcon = new Icon(relPath);
                }
                else
                {
                    // Fallback: Dev path from environment variable (for development only)
                    string? devAssetsPath = Environment.GetEnvironmentVariable("AMT_DEV_ASSETS_PATH");
                    if (!string.IsNullOrEmpty(devAssetsPath))
                    {
                        string devPath = Path.Combine(devAssetsPath, "Icons", "AppIcon.ico");
                        if (File.Exists(devPath))
                        {
                            appIcon = new Icon(devPath);
                        }
                    }
                }
                
                if (appIcon != null)
                {
                    this.Icon = appIcon;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error loading application icon: {ex.Message}");
            }


            // Load banner image with cover mode (fill and crop, maintains aspect ratio)
            try
            {
                string bannerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "banner.jpg");
                if (File.Exists(bannerPath))
                {
                    using var originalImage = Image.FromFile(bannerPath);
                    imagePictureBox.Image = CreateCoverImage(originalImage, imagePictureBox.Width, imagePictureBox.Height);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error loading banner: {ex.Message}");
            }

            RunPendingUpdateCleanup();

            statusModsDotLabel.BackColor = Color.FromArgb(150, 150, 150);
            statusModsTextLabel.Text = "Not Checked";
            statusModsTextLabel.ForeColor = Color.FromArgb(150, 150, 150);

            // Hook fallback logger to UI logger
            ArdysaModsTools.Core.Services.FallbackLogger.UserLogger = (msg) =>
            {
                try { _logger.Log(msg); } catch { }
            };

            // Apply font fallback if JetBrains Mono not installed
            UI.FontHelper.ApplyToForm(this);

            EnableDetectionButtonsOnly();
        }

        /// <summary>
        /// Finalizes a self-update when the app was relaunched with the "--update" handshake.
        /// The freshly downloaded build (running now) replaces the original exe, then removes
        /// the backup and temp artifacts left by the updater. Runs off the UI thread so the
        /// short settle delay does not block form construction.
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
                    // Surface the error and exit on the UI thread.
                    InvokeOnUIThread(() =>
                    {
                        MessageBox.Show(this, $"Update cleanup failed: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    });
                }
            });
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Delegate shutdown check to presenter
            if (!_presenter.IsOperationRunning)
                return;

            // Prevent form from closing immediately
            e.Cancel = true;
            DisableAllButtons();

            // Wait for graceful shutdown in background
            Task.Run(async () =>
            {
                await _presenter.ShutdownAsync();
                BeginInvoke(new Action(() => Close()));
            });
        }

        /// <summary>
        /// Releases long-lived resources once the form has closed.
        /// Disposing the presenter stops the Dota 2 patch watcher, the process monitor,
        /// and any pending operation token. Idempotent — the presenter guards double-dispose.
        /// </summary>
        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            // Cancel the background asset preload so it doesn't outlive the window.
            try { _launchCts.Cancel(); _launchCts.Dispose(); } catch { /* best-effort */ }
            try { _presenter.Dispose(); } catch (Exception ex) { _logger.Log($"Presenter dispose failed: {ex.Message}"); }
            try { _trayService?.Dispose(); } catch (Exception ex) { _logger.Log($"TrayService dispose failed: {ex.Message}"); }
        }



        private void CancelButton_Click(object? sender, EventArgs e)
        {
            CancelOperation();
        }

        private void copyConsoleBtn_Click(object? sender, EventArgs e)
        {
            try
            {
                var text = mainConsoleBox.GetAllText();
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                    _logger.Log("Console text copied to clipboard.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to copy: {ex.Message}");
            }
        }

        private void CancelOperation()
        {
            _presenter.CancelOperation();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            _logger.FlushBufferedLogs();

            // Presenter initialization (MVP). Isolated so a failure here is reported accurately
            // and does not get mislabeled as an asset-loading error.
            try
            {
                await _presenter.InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"Presenter initialization failed: {ex.Message}");
            }

            // Initialize smart CDN selector (tests CDN speeds in background, fire-and-forget)
            _ = SmartCdnSelector.Instance.InitializeAsync();

            // "Launching State": preload all gallery thumbnails into the local cache in the
            // background so Miscellaneous / Skin Selector open instantly. Fire-and-forget; skips
            // already-cached assets, so it's near-instant once warm. Honors the user toggle.
            if (_assetPreloadService != null && _configService.PreloadAssetsOnLaunch)
            {
                var preloadProgress = new Progress<AssetPreloadProgress>(OnPreloadProgress);
                _ = _assetPreloadService.PreloadAllAsync(preloadProgress, _launchCts.Token);
            }

            // Load embedded social media icons (non-critical — a failure must not block startup)
            LoadSocialMediaIcons();

            EnableDetectionButtonsOnly();

            // If launched with --minimized flag (Windows startup), go straight to tray.
            // Skip popups (SupportDialog, donation reminder) for a silent background start.
            if (_startMinimized && _trayService != null)
            {
                _trayService.MinimizeToTray();
                return;
            }

            // Show Support Dialog on startup
            ShowSupportDialogOnStartup();

            // Show onboarding guide for first-time users
            ShowOnboardingGuide();

            // Show donation reminder notification (always on startup)
            _trayService?.ShowDonationReminder();
        }

        /// <summary>
        /// Reports "Launching State" preload progress to the console as concise, throttled lines
        /// (every ~10%). Runs on the UI thread via the Progress&lt;T&gt; synchronization context.
        /// </summary>
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
                    _logger.Log("Launching State: asset cache ready.");
                    break;
            }
        }

        /// <summary>
        /// Loads the embedded Discord/YouTube/PayPal icons. Best-effort: a missing or
        /// unreadable resource is logged but never blocks startup.
        /// </summary>
        private void LoadSocialMediaIcons()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (Stream? discordStream = assembly.GetManifestResourceStream("ArdysaModsTools.dc.png"))
                {
                    if (discordStream != null) discordPictureBox.Image = Image.FromStream(discordStream);
                }

                using (Stream? youtubeStream = assembly.GetManifestResourceStream("ArdysaModsTools.yt.png"))
                {
                    if (youtubeStream != null) youtubePictureBox.Image = Image.FromStream(youtubeStream);
                }

                using (Stream? paypalStream = assembly.GetManifestResourceStream("ArdysaModsTools.paypal.png"))
                {
                    if (paypalStream != null) paypalPictureBox.Image = Image.FromStream(paypalStream);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error loading social media icons: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the Support Dialog when the app starts.
        /// </summary>
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

        /// <summary>
        /// Shows the onboarding guide overlay for first-time users.
        /// Highlights each feature step-by-step with spotlight and tooltips.
        /// </summary>
        private void ShowOnboardingGuide()
        {
            if (_onboardingService.IsOnboardingCompleted()) return;

            try
            {
                var steps = _onboardingService.GetSteps();
                using var overlay = new OnboardingOverlay(this, steps);
                overlay.OnboardingFinished += (s, e) =>
                {
                    _onboardingService.MarkOnboardingCompleted();
                };
                overlay.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Onboarding guide error: {ex.Message}");
                // Don't block the app if guide fails — mark as completed
                _onboardingService.MarkOnboardingCompleted();
            }
        }

        /// <summary>
        /// Resets and re-shows the onboarding guide (called from Settings).
        /// </summary>
        public void ResetAndShowOnboarding()
        {
            _onboardingService.ResetOnboarding();
            ShowOnboardingGuide();
        }


        private void DiscordPictureBox_Click(object? sender, EventArgs e)
        {
            UIHelpers.OpenUrlWithErrorDialog("https://discord.gg/ffXw265Z7e", "Discord", _logger.Log);
        }

        private void YoutubePictureBox_Click(object? sender, EventArgs e)
        {
            UIHelpers.OpenUrlWithErrorDialog("https://youtube.com/@Ardysa", "YouTube", _logger.Log);
        }

        private void PaypalPictureBox_Click(object? sender, EventArgs e)
        {
            using var supportDialog = new UI.Forms.SupportDialog();
            supportDialog.StartPosition = FormStartPosition.CenterParent;
            supportDialog.ShowDialog(this);
        }

        private async void MiscellaneousButton_Click(object? sender, EventArgs e)
        {
            await _presenter.OpenMiscellaneousAsync();
        }



        public void DisableAllButtons()
        {
            autoDetectButton.Enabled = false;
            manualDetectButton.Enabled = false;
            installButton.Enabled = false;
            disableButton.Enabled = false;
            updatePatcherButton.Enabled = false;
            miscellaneousButton.Enabled = false;
            btn_OpenSelectHero.Enabled = false;
        }

        public void EnableDetectionButtonsOnly()
        {
            autoDetectButton.Enabled = true;
            manualDetectButton.Enabled = true;
            installButton.Enabled = false;
            disableButton.Enabled = false;
            updatePatcherButton.Enabled = false;
            miscellaneousButton.Enabled = false;
            btn_OpenSelectHero.Enabled = false;

            // Highlight detection buttons to draw attention
            autoDetectButton.Highlighted = true;
            manualDetectButton.Highlighted = true;
        }

        public void EnableAllButtons()
        {
            autoDetectButton.Enabled = true;
            manualDetectButton.Enabled = true;
            installButton.Enabled = targetPath != null;
            disableButton.Enabled = targetPath != null;
            updatePatcherButton.Enabled = targetPath != null && IsRequiredModFilePresent();
            miscellaneousButton.Enabled = targetPath != null;
            btn_OpenSelectHero.Enabled = targetPath != null;

            // Remove highlight from detection buttons once path is set
            autoDetectButton.Highlighted = false;
            manualDetectButton.Highlighted = false;
        }

        private bool IsRequiredModFilePresent()
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            string requiredFilePath = Path.Combine(targetPath, ArdysaModsTools.Core.Constants.DotaPaths.ModsVpk);
            bool fileExists = File.Exists(requiredFilePath);

            if (!fileExists && !_modFileWarningLogged)
            {
                _logger.Log($"Required mod file 'ArdysaMods/pak01_dir.vpk' not found. Please install mods first.");
                _modFileWarningLogged = true; // Only log once until mods are installed
            }
            else if (fileExists)
            {
                _modFileWarningLogged = false; // Reset when mods are present
            }

            return fileExists;
        }



        /// <summary>
        /// Updates the Patch Update button colors based on mod status.
        /// Default: Black bg, White text
        /// Ready: Black bg, Green border, Green text
        /// NeedUpdate: Black bg, Orange border, Orange text
        /// Error: Red bg, White text
        /// </summary>
        public void UpdatePatchButtonStatus(ModStatus? status, bool isError = false)
        {
            if (updatePatcherButton == null) return;

            // Default colors
            Color bgColor = Color.Black;
            Color borderColor = Color.FromArgb(51, 51, 51);
            Color textColor = Color.White;
            Color hoverBgColor = Color.FromArgb(26, 26, 26);
            Color hoverTextColor = Color.White;

            if (isError)
            {
                // Error: Red background, white text
                bgColor = Color.FromArgb(200, 50, 50);
                borderColor = Color.FromArgb(200, 50, 50);
            }
            else if (status == ModStatus.Ready)
            {
                // Ready: Green border and text
                borderColor = Color.FromArgb(0, 200, 100);
                textColor = Color.FromArgb(0, 200, 100);
                hoverTextColor = Color.FromArgb(0, 255, 120);
            }
            else if (status == ModStatus.NeedUpdate)
            {
                // Need Update: Orange border and text
                borderColor = Color.FromArgb(255, 165, 0);
                textColor = Color.FromArgb(255, 165, 0);
                hoverTextColor = Color.FromArgb(255, 200, 50);
            }
            // else: Default (black bg, white text)

            updatePatcherButton.BackColor = bgColor;
            updatePatcherButton.BorderColor = borderColor;
            updatePatcherButton.ForeColor = textColor;
            updatePatcherButton.HoverBackColor = hoverBgColor;
            updatePatcherButton.HoverForeColor = hoverTextColor;
            updatePatcherButton.Invalidate();
        }

        /// <summary>
        /// Updates UI to show that a Dota 2 patch was detected.
        /// Sets status text and highlights patch button.
        /// </summary>
        public void SetPatchDetectedStatus()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(SetPatchDetectedStatus));
                return;
            }

            // Update status labels
            statusModsTextLabel.Text = "Update Detected";
            statusModsTextLabel.ForeColor = Color.FromArgb(255, 165, 0); // Orange
            statusModsDotLabel.BackColor = Color.FromArgb(255, 165, 0);

            // Highlight patch button
            UpdatePatchButtonStatus(ModStatus.NeedUpdate);
            updatePatcherButton.Enabled = true;
        }

        private async void AutoDetectButton_Click(object? sender, EventArgs e)
        {
            await _presenter.AutoDetectAsync();

        }

        private async void ManualDetectButton_Click(object? sender, EventArgs e)
        {
            await _presenter.ManualDetectAsync();

        }

        // Install button flow — updated to use tuple result
        private async void InstallButton_Click(object? sender, EventArgs e)
        {
            await _presenter.InstallAsync();
        }









        private async void DisableButton_Click(object? sender, EventArgs e)
        {
            await _presenter.DisableWithOptionsAsync();
        }



        #region Patch Update Menu

        private ContextMenuStrip? _patchMenu;

        private void EnsurePatchMenuCreated()
        {
            if (_patchMenu != null) return;

            _patchMenu = new ContextMenuStrip
            {
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("JetBrains Mono", 9F),
                ShowImageMargin = false,
                Padding = new Padding(4),
                DropShadowEnabled = true
            };

            _patchMenu.Renderer = new ModernMenuRenderer();

            // Patch Update - Re-apply all patches
            var fullPatch = new ToolStripMenuItem("Patch Update")
            {
                ToolTipText = "Re-apply all patches (recommended after Dota update)",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            fullPatch.Click += async (s, e) => await _presenter.ExecutePatchAsync();

            var sep1 = new ToolStripSeparator { BackColor = Color.FromArgb(51, 51, 51) };

            // Verify Files
            var verifyFiles = new ToolStripMenuItem("Verify Mod Files")
            {
                ToolTipText = "Check if all mod files are present and valid",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            verifyFiles.Click += async (s, e) => await _presenter.VerifyModFilesAsync();

            // View Status
            var viewStatus = new ToolStripMenuItem("View Status Details")
            {
                ToolTipText = "Show detailed mod status information",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            viewStatus.Click += (s, e) => _presenter.ShowStatusDetails();

            _patchMenu.Items.AddRange(new ToolStripItem[] 
            { 
                fullPatch, 
                sep1, 
                verifyFiles, 
                viewStatus 
            });


        }

        // Ensuring tray icon uses the correct icon even if loaded later


        private async void UpdatePatcherButton_Click(object? sender, EventArgs e)
        {
            await _presenter.HandlePatcherClickAsync();
        }

        public void ShowPatchMenu()
        {
            EnsurePatchMenuCreated();
            
            // Position menu below the button
            var button = updatePatcherButton;
            var location = button.PointToScreen(new Point(0, button.Height));
            _patchMenu!.Show(location);
        }



        #endregion

        /// <summary>
        /// Modern styled menu renderer for context menus with centered text.
        /// </summary>
        private class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernMenuRenderer() : base(new ModernMenuColors()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                
                if (e.Item.Selected)
                {
                    using var brush = new SolidBrush(Color.FromArgb(30, 30, 30));
                    e.Graphics.FillRectangle(brush, rect);
                    
                    // Draw cyan left accent
                    using var accentBrush = new SolidBrush(Color.FromArgb(0, 255, 255));
                    e.Graphics.FillRectangle(accentBrush, 0, 0, 3, rect.Height);
                }
                else
                {
                    using var brush = new SolidBrush(Color.Black);
                    e.Graphics.FillRectangle(brush, rect);
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                // Left-align the text with padding
                e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
                
                // Add left padding
                e.TextRectangle = new Rectangle(12, 0, e.Item.Width - 12, e.Item.Height);
                
                // Cyan color on hover
                if (e.Item.Selected)
                {
                    e.TextColor = Color.FromArgb(0, 255, 255);
                }
                
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51));
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
        }

        private class ModernMenuColors : ProfessionalColorTable
        {
            public override Color MenuBorder => Color.FromArgb(51, 51, 51);
            public override Color MenuItemBorder => Color.FromArgb(51, 51, 51);
            public override Color MenuItemSelected => Color.FromArgb(30, 30, 30);
            public override Color MenuStripGradientBegin => Color.Black;
            public override Color MenuStripGradientEnd => Color.Black;
            public override Color ToolStripDropDownBackground => Color.Black;
            public override Color ImageMarginGradientBegin => Color.Black;
            public override Color ImageMarginGradientMiddle => Color.Black;
            public override Color ImageMarginGradientEnd => Color.Black;
        }

        // Note: UpdateVersionLabelAsync removed - version updates now handled by MainFormPresenter
        // via IMainFormView.SetVersion() and UpdaterService.OnVersionChanged event

        #region Enhanced Status Display

        private ToolTip? _statusTooltip;
        private ModStatusInfo? _currentStatus;

        /// <summary>
        /// Updates the mod status display with detailed information.
        /// </summary>
        public void SetModsStatusDetailed(ModStatusInfo statusInfo)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetModsStatusDetailed(statusInfo)));
                return;
            }

            _currentStatus = statusInfo;

            // Update dot color
            statusModsDotLabel.BackColor = statusInfo.StatusColor;

            // Update text
            statusModsTextLabel.Text = statusInfo.StatusText;
            statusModsTextLabel.ForeColor = statusInfo.StatusColor;

            // Create tooltip with detailed info
            if (_statusTooltip == null)
            {
                _statusTooltip = new ToolTip
                {
                    InitialDelay = 300,
                    AutoPopDelay = 10000,
                    ReshowDelay = 200,
                    ShowAlways = true
                };
            }

            // Build tooltip text
            var tooltipText = statusInfo.Description;
            if (!string.IsNullOrEmpty(statusInfo.Version))
            {
                tooltipText += $"\n\nVersion: {statusInfo.Version}";
            }
            if (statusInfo.LastModified.HasValue)
            {
                tooltipText += $"\nLast Modified: {statusInfo.LastModified.Value:g}";
            }
            if (!string.IsNullOrEmpty(statusInfo.ActionButtonText))
            {
                tooltipText += $"\n\n-> {statusInfo.ActionButtonText}";
            }

            _statusTooltip.SetToolTip(statusModsDotLabel, tooltipText);
            _statusTooltip.SetToolTip(statusModsTextLabel, tooltipText);

            // Update button states based on status
            UpdateButtonsForStatus(statusInfo);
            
            // Re-enable refresh button after status update
            statusRefreshButton.Enabled = true;
            statusRefreshButton.ForeColor = Color.FromArgb(100, 100, 100);
        }

        /// <summary>
        /// Shows the "Checking..." loading state for the status indicator.
        /// </summary>
        public void ShowCheckingState()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowCheckingState()));
                return;
            }

            StatusUIHelper.ShowCheckingState(statusModsDotLabel, statusModsTextLabel, statusRefreshButton);
        }

        /// <summary>
        /// Updates action buttons based on current mod status.
        /// </summary>
        public void UpdateButtonsForStatus(ModStatusInfo statusInfo)
        {
            // Always keep SpringGreen text for consistency
            updatePatcherButton.ForeColor = Color.SpringGreen;

            // Use centralized status colors for button backgrounds
            updatePatcherButton.BackColor = StatusColors.ButtonForStatus(statusInfo.Status);
        }

        #endregion


        
        /// <summary>
        /// Handles click on the refresh icon to manually refresh mod status.
        /// </summary>
        private async void StatusRefreshButton_Click(object? sender, EventArgs e)
        {
            await _presenter.RefreshStatusAsync();
        }

        private void StatusRefreshButton_MouseEnter(object? sender, EventArgs e)
        {
            statusRefreshButton.ForeColor = Color.White;
        }

        private void StatusRefreshButton_MouseLeave(object? sender, EventArgs e)
        {
            statusRefreshButton.ForeColor = Color.FromArgb(100, 100, 100);
        }
        
        // progressBar removed - using ProgressOverlay


        private async void Btn_OpenSelectHero_Click(object? sender, EventArgs e)
        {
            await _presenter.OpenHeroSelectionAsync();
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.H)
            {
                Btn_OpenSelectHero_Click(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Create a cover-style image that fills the target size while maintaining aspect ratio (crops excess).
        /// </summary>
        private static Image CreateCoverImage(Image source, int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
                return new Bitmap(source);

            float sourceRatio = (float)source.Width / source.Height;
            float targetRatio = (float)targetWidth / targetHeight;

            int srcX = 0, srcY = 0, srcW = source.Width, srcH = source.Height;

            if (sourceRatio > targetRatio)
            {
                // Source is wider - crop horizontally
                srcW = (int)(source.Height * targetRatio);
                srcX = (source.Width - srcW) / 2;
            }
            else if (sourceRatio < targetRatio)
            {
                // Source is taller - crop vertically  
                srcH = (int)(source.Width / targetRatio);
                srcY = (source.Height - srcH) / 2;
            }

            var result = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source,
                    new Rectangle(0, 0, targetWidth, targetHeight),
                    new Rectangle(srcX, srcY, srcW, srcH),
                    GraphicsUnit.Pixel);
            }
            return result;
        }



        #region Custom Borderless Window

        /// <summary>
        /// Apply rounded corners to form
        /// </summary>
        private void ApplyRoundedForm()
        {
            int radius = 20;
            this.Region = Region.FromHrgn(
                CreateRoundRectRgn(0, 0, Width, Height, radius, radius));
        }

        // P/Invoke for reliable window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // Window dragging - using Windows API for reliable behavior
        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        }

        private void HeaderPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            // No longer needed - handled by Windows API
        }

        private void HeaderPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            // No longer needed - handled by Windows API
        }

        // Close button
        private void BtnClose_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnClose_MouseEnter(object? sender, EventArgs e)
        {
            btnClose.ForeColor = Color.FromArgb(255, 80, 80);
        }

        private void BtnClose_MouseLeave(object? sender, EventArgs e)
        {
            btnClose.ForeColor = Color.FromArgb(68, 68, 68);
        }

        // Minimize button
        private void BtnMinimize_Click(object? sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void BtnMinimize_MouseEnter(object? sender, EventArgs e)
        {
            btnMinimize.ForeColor = Color.White;
        }

        private void BtnMinimize_MouseLeave(object? sender, EventArgs e)
        {
            btnMinimize.ForeColor = Color.FromArgb(68, 68, 68);
        }

        // Settings button
        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            OpenSettingsDialog();
        }

        private void BtnSettings_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is Label lbl) lbl.ForeColor = Color.FromArgb(0, 200, 150);
        }

        private void BtnSettings_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is Label lbl) lbl.ForeColor = Color.FromArgb(68, 68, 68);
        }

        /// <summary>
        /// Opens the Settings dialog.
        /// </summary>
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
                    _assetPreloadService
                );

                settingsForm.ShowGuideRequested += (s, e) =>
                {
                    ResetAndShowOnboarding();
                };

                settingsForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open Settings: {ex.Message}");
            }
        }

        #region Tweak Button

        private void BtnTweak_Click(object? sender, EventArgs e)
        {
            // Performance Tweak writes autoexec.cfg into the Dota 2 cfg folder, which can only be
            // resolved from a known install path — require detection first (parity with MainFormWebView).
            if (string.IsNullOrEmpty(targetPath))
            {
                _logger.Log("Detect your Dota 2 path first to use Performance Tweak.");
                return;
            }

            try
            {
                // targetPath is typically "...\dota 2 beta\game"
                using var perfForm = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<UI.Forms.Dota2PerformanceForm>(_serviceProvider, targetPath);
                perfForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open Performance Tweak: {ex.Message}");
            }
        }

        private void BtnTweak_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is Label lbl) lbl.ForeColor = Color.FromArgb(147, 51, 234); // purple on hover
        }

        private void BtnTweak_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is Label lbl) lbl.ForeColor = Color.FromArgb(68, 68, 68); // grey on leave
        }

        private void BtnTweak_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Label lbl) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color iconColor = lbl.ForeColor;
            
            // Draw 3 minimalist horizontal sliders (Tweak Icon)
            using var linePen = new Pen(Color.FromArgb(100, iconColor), 2f); // semi-transparent tracks
            using var knobBrush = new SolidBrush(iconColor); // solid knobs
            using var knobPen = new Pen(lbl.Parent?.BackColor ?? Color.Black, 1.5f); // outline separator

            // Slider 1: y = 9
            g.DrawLine(linePen, 6.5f, 9f, 22.5f, 9f);
            g.FillEllipse(knobBrush, 11f - 3f, 9f - 3f, 6f, 6f);
            g.DrawEllipse(knobPen, 11f - 3f, 9f - 3f, 6f, 6f);

            // Slider 2: y = 14.5 (Center)
            g.DrawLine(linePen, 6.5f, 14.5f, 22.5f, 14.5f);
            g.FillEllipse(knobBrush, 18f - 3f, 14.5f - 3f, 6f, 6f);
            g.DrawEllipse(knobPen, 18f - 3f, 14.5f - 3f, 6f, 6f);

            // Slider 3: y = 20
            g.DrawLine(linePen, 6.5f, 20f, 22.5f, 20f);
            g.FillEllipse(knobBrush, 13f - 3f, 20f - 3f, 6f, 6f);
            g.DrawEllipse(knobPen, 13f - 3f, 20f - 3f, 6f, 6f);
        }

        private void BtnSettings_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Label lbl) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float cx = lbl.Width / 2f;
            float cy = lbl.Height / 2f;
            Color iconColor = lbl.ForeColor;

            using var pen = new Pen(iconColor, 2f);
            
            // Draw core ring
            g.DrawEllipse(pen, cx - 5.5f, cy - 5.5f, 11f, 11f);
            
            // Draw 6 spokes/teeth
            int teethCount = 6;
            for (int i = 0; i < teethCount; i++)
            {
                float angle = i * 60f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);
                
                // Draw tooth at the top
                using var brush = new SolidBrush(iconColor);
                g.FillRectangle(brush, -2f, -10f, 4f, 4f);
                
                g.ResetTransform();
            }
            
            // Draw inner small hole
            g.FillEllipse(new SolidBrush(iconColor), cx - 2f, cy - 2f, 4f, 4f);
        }

        #endregion

        #endregion




    }
}

