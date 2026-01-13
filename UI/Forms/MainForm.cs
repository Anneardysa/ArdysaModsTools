using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.UI.Forms;
using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArdysaModsTools
{
    public partial class MainForm : Form
    {
        private string? targetPath = null;
        private CancellationTokenSource? _operationCts;
        private Task<(bool Success, bool IsUpToDate)>? _ongoingOperationTask;

        // Window dragging
        private bool _dragging = false;
        private Point _dragStart;

        // For rounded form
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        private const string RequiredModFilePath = "game/_ArdysaMods/pak01_dir.vpk";

        private readonly Logger _logger;
        private readonly UpdaterService _updater = null!;
        private readonly ModInstallerService _modInstaller;
        private readonly DetectionService _detection;
        private readonly StatusService _status;
        private readonly DotaVersionService _versionService;
        private readonly MainConfigService _configService;
        private Dota2Monitor _dotaMonitor;
        private DotaPatchWatcherService? _patchWatcher;

        public MainForm()
        {
            InitializeComponent();

            // Use embedded JetBrains Mono font for console
            mainConsoleBox.Font = FontHelper.CreateMono(8F, FontStyle.Bold);

            // Apply rounded corners to form
            ApplyRoundedForm();
            this.Resize += (s, e) => ApplyRoundedForm();

            this.FormClosing += MainForm_FormClosing;

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            _logger = new Logger(mainConsoleBox);
            _updater = new UpdaterService(_logger);
            _updater.OnVersionChanged += version =>
            {
                if (InvokeRequired)
                    Invoke(new Action(() => versionLabel.Text = $"Version: {version}"));
                else
                    versionLabel.Text = $"Version: {version}";
            };

            // Progress now handled by ProgressOverlay, not MainForm progressBar

            _modInstaller = new ModInstallerService(_logger);

            _detection = new DetectionService(_logger);

            _status = new StatusService(_logger);
            
            _versionService = new DotaVersionService(_logger);

            _configService = new MainConfigService();

            _dotaMonitor = new Dota2Monitor();
            _dotaMonitor.OnDota2StateChanged += DotaStateChanged;
            _dotaMonitor.Start();

            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            ServicePointManager.DefaultConnectionLimit = 10;

            // ═══════════════════════════════════════════════════════════════
            // ICON LOADING - Direct file loading (most reliable)
            // ═══════════════════════════════════════════════════════════════
            Icon? appIcon = null;
            try
            {
                // Primary: Absolute path (for development)
                string devPath = @"D:\Projects\AMT2.0\Assets\Icons\AppIcon.ico";
                if (File.Exists(devPath))
                {
                    appIcon = new Icon(devPath);
                }
                else
                {
                    // Fallback: Relative path (for deployment)
                    string relPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "AppIcon.ico");
                    if (File.Exists(relPath))
                    {
                        appIcon = new Icon(relPath);
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

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length >= 6 && args[1] == "--update")
            {
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
                        MessageBox.Show($"Update cleanup failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }
                });
            }

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

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Dispose resources
            _patchWatcher?.Dispose();
            _patchWatcher = null;

            // If no running task → allow closing
            if (_ongoingOperationTask == null || _operationCts == null)
                return;

            // Request cancellation silently
            _operationCts.Cancel();

            // prevent form from closing immediately
            e.Cancel = true;
            DisableAllButtons();

            // wait in background
            Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAny(_ongoingOperationTask, Task.Delay(5000)); // wait max 5 seconds
                }
                catch { }

                try
                {
                    BeginInvoke(new Action(() => Close()));   // try closing again
                }
                catch { }
            });
        }

        private CancellationTokenSource StartOperation()
        {
            try
            {
                try { _operationCts?.Cancel(); } catch { }

                _operationCts?.Dispose();
            }
            catch { /* ignore disposal issues */ }

            _operationCts = new CancellationTokenSource();

            // Disable all buttons while operation is running
            DisableAllButtons();

            return _operationCts;
        }

        private void EndOperation()
        {
            try
            {
                _operationCts?.Dispose();
            }
            catch { }
            _operationCts = null;

            if (string.IsNullOrEmpty(targetPath))
                EnableDetectionButtonsOnly();
            else
                EnableAllButtons();
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
            try
            {
                if (_operationCts != null && !_operationCts.IsCancellationRequested)
                {
                    _operationCts.Cancel();
                }
            }
            catch { }
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            _logger.FlushBufferedLogs();
            try
            {
                var last = _configService.GetLastTargetPath();
                if (!string.IsNullOrEmpty(last))
                {
                    if (Directory.Exists(last))
                    {
                        targetPath = last;
                        // Don't start watcher here - will start on first user interaction
                        // This prevents duplicate logging when user clicks Auto-Detect
                        EnableAllButtons();
                    }
                    else
                    {
                        _configService.SetLastTargetPath(null);
                    }
                }

                await _updater.CheckForUpdatesAsync();
                await UpdateVersionLabelAsync();

                using (Stream? discordStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArdysaModsTools.dc.png"))
                {
                    if (discordStream == null) { }
                    else { discordPictureBox.Image = Image.FromStream(discordStream); }
                }

                using (Stream? youtubeStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArdysaModsTools.yt.png"))
                {
                    if (youtubeStream == null) { }
                    else { youtubePictureBox.Image = Image.FromStream(youtubeStream); }
                }

                using (Stream? paypalStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArdysaModsTools.paypal.png"))
                {
                    if (paypalStream == null) { }
                    else { paypalPictureBox.Image = Image.FromStream(paypalStream); }
                }

                EnableDetectionButtonsOnly();

                // Show Support Dialog on startup
                ShowSupportDialogOnStartup();
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

        private void MiscellaneousButton_Click(object? sender, EventArgs e)
        {
            using (var miscForm = new MiscForm(targetPath, _logger.Log, DisableAllButtons, EnableAllButtons))
            {
                miscForm.ShowDialog(this);
            }
        }

        /// <summary>
        /// Adjusts Install and Disable button sizes when Cancel visibility changes
        /// </summary>
        private void UpdateActionButtonLayout(bool showCancel)
        {
            const int startX = 216;
            const int buttonY = 252;
            const int buttonHeight = 50;
            const int totalWidth = 600;
            const int spacing = 12;

            // Always 2 buttons: Install, Disable (50/50 split)
            int buttonWidth = (totalWidth - spacing) / 2;
            installButton.Location = new Point(startX, buttonY);
            installButton.Size = new Size(buttonWidth, buttonHeight);
            installButton.Visible = true;

            disableButton.Location = new Point(startX + buttonWidth + spacing, buttonY);
            disableButton.Size = new Size(buttonWidth, buttonHeight);
            disableButton.Visible = true;
        }

        private void DisableAllButtons()
        {
            autoDetectButton.Enabled = false;
            manualDetectButton.Enabled = false;
            installButton.Enabled = false;
            disableButton.Enabled = false;
            updatePatcherButton.Enabled = false;
            miscellaneousButton.Enabled = false;
            btn_OpenSelectHero.Enabled = false;
        }

        private void EnableDetectionButtonsOnly()
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

        private void EnableAllButtons()
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

            string requiredFilePath = Path.Combine(targetPath, RequiredModFilePath);
            bool fileExists = File.Exists(requiredFilePath);

            if (!fileExists)
            {
                _logger.Log($"Required mod file '{RequiredModFilePath}' not found. Please install mods first.");
            }

            return fileExists;
        }

        private async Task CheckModsStatus()
        {
            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(targetPath);
                SetModsStatusDetailed(statusInfo);
                UpdatePatchButtonStatus(statusInfo?.Status);
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Error: {ex.Message}");
                statusModsDotLabel.BackColor = Color.FromArgb(255, 80, 80);
                statusModsTextLabel.Text = "Error";
                statusModsTextLabel.ForeColor = Color.FromArgb(255, 80, 80);
                UpdatePatchButtonStatus(null, isError: true);
            }
        }

        #region Dota Patch Auto-Update Watcher

        /// <summary>
        /// Start monitoring for Dota 2 updates.
        /// Will skip if already watching the same path.
        /// </summary>
        private async Task StartPatchWatcherAsync(string dotaPath)
        {
            try
            {
                // Skip if already watching this path
                if (_patchWatcher != null && _patchWatcher.IsWatching)
                {
                    // Already watching - no need to restart
                    return;
                }

                // Dispose existing watcher if any
                _patchWatcher?.Dispose();

                // Create new watcher
                _patchWatcher = new DotaPatchWatcherService(_logger);
                _patchWatcher.OnPatchDetected += OnPatchDetected;

                await _patchWatcher.StartWatchingAsync(dotaPath);
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Failed to start: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when Dota 2 patch is detected by the watcher.
        /// Shows toast notification and updates UI.
        /// </summary>
        private void OnPatchDetected(PatchDetectedEventArgs args)
        {
            try
            {
                _logger.Log($"[PatchWatcher] Dota 2 update detected: {args.ChangeSummary}");

                // Show Windows toast notification
                ShowPatchDetectedToast(args);

                // Update UI on main thread
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => HandlePatchDetectedUI(args)));
                }
                else
                {
                    HandlePatchDetectedUI(args);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Error handling patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Show notification for detected patch.
        /// Currently logs to console - Windows toast can be added as future enhancement.
        /// </summary>
        private void ShowPatchDetectedToast(PatchDetectedEventArgs args)
        {
            try
            {
                // Log notification to console (always works)
                _logger.Log("═══════════════════════════════════════════");
                _logger.Log("🎮 DOTA 2 UPDATE DETECTED!");
                _logger.Log(args.RequiresRepatch 
                    ? "Action Required: Click 'Patch Update' to fix your mods." 
                    : "Your mods may need re-patching.");
                _logger.Log($"New Version: {args.NewVersion}");
                _logger.Log("═══════════════════════════════════════════");
                
                // Play system notification sound
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update UI elements when patch is detected.
        /// </summary>
        private void HandlePatchDetectedUI(PatchDetectedEventArgs args)
        {
            // Update status
            statusModsTextLabel.Text = "Update Detected";
            statusModsTextLabel.ForeColor = Color.FromArgb(255, 165, 0); // Orange
            statusModsDotLabel.BackColor = Color.FromArgb(255, 165, 0);

            // Highlight patch button
            UpdatePatchButtonStatus(ModStatus.NeedUpdate);
            updatePatcherButton.Enabled = true;

            // Refresh status to get accurate state
            _ = CheckModsStatus();
        }

        #endregion

        /// <summary>
        /// Updates the Patch Update button colors based on mod status.
        /// Default: Black bg, White text
        /// Ready: Black bg, Green border, Green text
        /// NeedUpdate: Black bg, Orange border, Orange text
        /// Error: Red bg, White text
        /// </summary>
        private void UpdatePatchButtonStatus(ModStatus? status, bool isError = false)
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

        private async void AutoDetectButton_Click(object? sender, EventArgs e)
        {
            DisableAllButtons();
            // Progress handled by overlay

            string? detectedPath = await _detection.AutoDetectAsync();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                targetPath = detectedPath;
                _configService.SetLastTargetPath(targetPath);
                await CheckModsStatus();
                await StartPatchWatcherAsync(targetPath);
                EnableAllButtons();
                return;
            }
        }

        private async void ManualDetectButton_Click(object? sender, EventArgs e)
        {
            DisableAllButtons();

            string? selectedPath = _detection.ManualDetect();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                targetPath = selectedPath;
                _configService.SetLastTargetPath(targetPath);
                _logger.Log($"Dota 2 path set: {targetPath}");
                await CheckModsStatus();
                await StartPatchWatcherAsync(targetPath);
                EnableAllButtons();
            }
            else
            {
                EnableDetectionButtonsOnly();
            }
        }

        // Install button flow — updated to use tuple result
        private async void InstallButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(targetPath))
                return;

            // Show install method selection dialog
            using (var methodDialog = new UI.Forms.InstallMethodDialog())
            {
                methodDialog.StartPosition = FormStartPosition.CenterParent;
                if (methodDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (methodDialog.SelectedMethod == UI.Forms.InstallMethod.ManualInstall)
                {
                    // Manual Install flow
                    await HandleManualInstallAsync();
                    return;
                }
                // else: Auto-Install continues below
            }

            try
            {
                var (hasNewer, hasLocalInstall) = await _modInstaller.CheckForNewerModsPackAsync(targetPath);
                
                if (hasNewer && hasLocalInstall)
                {
                    // Show dialog for update
                    var result = MessageBox.Show(
                        "A newer ModsPack is available!\n\nDownload and install the update?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );
                    
                    if (result != DialogResult.Yes)
                        return;
                }
                else if (hasNewer && !hasLocalInstall)
                {
                    // First install - just proceed
                }
                // If not newer, InstallModsAsync will handle the "up to date" case
            }
            catch (Exception ex)
            {
                _logger.Log($"Version check failed: {ex.Message}");
                // Continue anyway - the install will handle errors
            }

            // Auto-Install flow with WebView2 progress overlay
            await RunAutoInstallWithProgressOverlayAsync();
        }

        /// <summary>
        /// Run auto-install with animated WebView2 progress overlay.
        /// </summary>
        private async Task RunAutoInstallWithProgressOverlayAsync()
        {
            string appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            bool success = false;
            bool isUpToDate = false;

            var result = await ProgressOperationRunner.RunAsync(
                this,
                "Preparing installation...",
                async (context) =>
                {
                    var installResult = await _modInstaller.InstallModsAsync(
                        targetPath!,
                        appPath,
                        context.Progress,
                        context.Token,
                        force: false,
                        context.Speed,
                        s => context.Status.Report(s)
                    ).ConfigureAwait(false);

                    success = installResult.Success;
                    isUpToDate = installResult.IsUpToDate;
                    return new OperationResult { Success = success };
                });

            await HandleInstallResultOnUiThread(success, isUpToDate, targetPath!, appPath, null);
        }

        /// <summary>
        /// Fallback: classic auto-install without overlay.
        /// </summary>
        private async Task RunClassicAutoInstallAsync(CancellationToken token, string appPath)
        {
            var progress = new Progress<int>(value =>
            {
                int v = Math.Clamp(value, 0, 100);
                // Progress handled by overlay
            });

            _ongoingOperationTask = Task.Run(async () =>
            {
                try
                {
                    return await _modInstaller.InstallModsAsync(
                        targetPath!, appPath, progress, token, force: false, speedProgress: null
                    ).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return (false, false);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Install task exception: {ex.Message}");
                    return (false, false);
                }
            }, token);

            try
            {
                var (success, isUpToDate) = await _ongoingOperationTask.ConfigureAwait(false);

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(async () => await HandleInstallResultOnUiThread(success, isUpToDate, targetPath!, appPath, progress)));
                }
                else
                {
                    await HandleInstallResultOnUiThread(success, isUpToDate, targetPath!, appPath, progress);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancel
            }
            catch (Exception ex)
            {
                _logger?.Log($"Install operation failed: {ex.Message}");
            }
            finally
            {
                _ongoingOperationTask = null;
                EndOperation();
            }
        }

        private async Task HandleManualInstallAsync()
        {
            // Open file dialog directly for VPK selection
            using var ofd = new OpenFileDialog
            {
                Title = "Select VPK File",
                Filter = "VPK Files (*.vpk)|*.vpk",
                RestoreDirectory = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(ofd.FileName))
                return;

            string vpkPath = ofd.FileName;

            // Check filename must be pak01_dir.vpk
            string fileName = Path.GetFileName(vpkPath);
            if (!fileName.Equals("pak01_dir.vpk", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please select pak01_dir.vpk",
                    "Invalid File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            var (isValid, errorMessage) = await _modInstaller.ValidateVpkAsync(vpkPath);

            if (!isValid)
            {
                MessageBox.Show(
                    $"VPK is invalid, please contact developer.",
                    "Invalid VPK",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            // Confirm before proceeding
            var confirmResult = MessageBox.Show(
                $"Install VPK:\n{Path.GetFileName(vpkPath)}\n\nContinue?",
                "Confirm Install",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirmResult != DialogResult.Yes)
                return;

            var cts = StartOperation();
            var token = cts.Token;

            // Progress handled by overlay

            var progress = new Progress<int>(value =>
            {
                int v = Math.Clamp(value, 0, 100);
                // Progress handled by overlay
            });

            try
            {
                bool success = await Task.Run(async () =>
                {
                    return await _modInstaller.ManualInstallModsAsync(targetPath!, vpkPath, progress, token).ConfigureAwait(false);
                }, token);

                if (success)
                {
                    // Progress handled by overlay
                    await _status.CheckAndUpdateUIAsync(targetPath!, statusModsDotLabel, statusModsTextLabel);
                }
            }
            catch (OperationCanceledException)
            {
                // Silent cancel
            }
            catch (Exception)
            {
                // Errors logged by ModInstallerService
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task HandleInstallResultOnUiThread(bool success, bool isUpToDate, string targetPath, string appPath, IProgress<int> progress)
        {
            if (isUpToDate)
            {
                var ask = MessageBox.Show(
                    "ModsPack already up to date.\nReinstall anyway?",
                    "Installation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (ask == DialogResult.Yes)
                {
                    // Run reinstall with progress overlay
                    _ = RunReinstallWithProgressOverlayAsync(targetPath, appPath);
                }
            }
            else if (success)
            {
                _logger.Log("Install done.");
                
                // Check and update mods status after successful install
                await CheckModsStatus();
                
                await ShowPatchRequiredIfNeededAsync();
            }
            else
            {
                _logger.Log("Install failed.");
            }
        }

        /// <summary>
        /// Check status after install and show PatchRequiredDialog if not Ready.
        /// </summary>
        /// <param name="successMessage">Message to show in dialog (e.g. "ModsPack installed successfully!" or "Custom ModsPack installed successfully!")</param>
        private async Task ShowPatchRequiredIfNeededAsync(string successMessage = "ModsPack installed successfully!")
        {
            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(targetPath);
                
                // If status is Ready, no action needed
                if (statusInfo.Status == ModStatus.Ready)
                {
                    return;
                }
                
                // If status is NeedUpdate, Disabled, or Error - show dialog
                if (statusInfo.Status == ModStatus.NeedUpdate || 
                    statusInfo.Status == ModStatus.Disabled)
                {
                    using var dialog = new PatchRequiredDialog(successMessage);
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ShowDialog(this);
                    
                    // If user clicked "Patch Now", trigger patch update
                    if (dialog.ShouldPatch && !string.IsNullOrEmpty(targetPath))
                    {
                        // Simulate clicking the Patch Update button
                        UpdatePatcherButton_Click(null, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Post-install check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Reinstall with progress overlay.
        /// </summary>
        private async Task RunReinstallWithProgressOverlayAsync(string targetPath, string appPath)
        {
            bool success = false;

            await ProgressOperationRunner.RunAsync(
                this,
                "Preparing reinstallation...",
                async (context) =>
                {
                    var result = await _modInstaller.InstallModsAsync(
                        targetPath,
                        appPath,
                        context.Progress,
                        context.Token,
                        force: true,
                        context.Speed,
                        s => context.Status.Report(s)
                    ).ConfigureAwait(false);

                    success = result.Success;
                    return new OperationResult { Success = success };
                });

            if (success)
            {
                _logger.Log("Reinstall done.");
                await CheckModsStatus();
                
                await ShowPatchRequiredIfNeededAsync();
            }
        }

        /// <summary>
        /// Classic reinstall without overlay.
        /// </summary>
        private async Task RunReinstallClassicAsync(string targetPath, string appPath, CancellationToken token)
        {
            var progress = new Progress<int>(v =>
            {
                // Progress handled by overlay
            });

            try
            {
                var (forcedSuccess, _) = await _modInstaller.InstallModsAsync(
                    targetPath, appPath, progress, token, force: true
                ).ConfigureAwait(false);

                if (forcedSuccess)
                {
                    _logger.Log("Reinstall done.");
                    
                    // Check and update mods status after successful reinstall
                    if (InvokeRequired)
                        BeginInvoke(new Action(async () => 
                        {
                            await CheckModsStatus();
                            await ShowPatchRequiredIfNeededAsync();
                        }));
                    else
                    {
                        await CheckModsStatus();
                        await ShowPatchRequiredIfNeededAsync();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Log($"Reinstall error: {ex.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async void DisableButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                _logger?.Log("No Dota 2 folder selected.");
                return;
            }

            // Show choice dialog
            using var dialog = new UI.Controls.DisableOptionsDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var selectedOption = dialog.SelectedOption;
            bool deletePermanently = selectedOption == UI.Controls.DisableOptionsDialog.DisableOption.DeletePermanently;

            // Confirm if deleting permanently
            if (deletePermanently)
            {
                var confirm = MessageBox.Show(
                    "This will permanently delete all mod files.\n\nAre you sure?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (confirm != DialogResult.Yes)
                    return;
            }

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                // Disable mods (remove VPK, revert signatures)
                bool success = await _modInstaller.DisableModsAsync(targetPath, token);

                // If delete permanently, also remove _ArdysaMods folder and clear temp
                if (deletePermanently && success)
                {
                    string modsFolder = Path.Combine(targetPath, "game", "_ArdysaMods");
                    if (Directory.Exists(modsFolder))
                    {
                        try
                        {
                            Directory.Delete(modsFolder, true);
                            _logger?.Log("Mod folder deleted permanently.");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Log($"Failed to delete mod folder: {ex.Message}");
                        }
                    }

                    // Clear %temp% folder
                    ClearTempFolder();

                    // Show restart dialog
                    using var restartDialog = new UI.Forms.RestartAppDialog("All mod files have been deleted.\nPlease restart the application.");
                    restartDialog.StartPosition = FormStartPosition.CenterParent;
                    if (restartDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // Restart the application
                        var currentProcess = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(currentProcess))
                        {
                            Process.Start(currentProcess);
                            Application.Exit();
                        }
                    }
                }

                await _status.CheckAndUpdateUIAsync(targetPath, statusModsDotLabel, statusModsTextLabel);
            }
            catch (OperationCanceledException)
            {
                // Silent cancel
            }
            catch (Exception ex)
            {
                _logger?.Log($"Disable operation failed: {ex.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Clears all files and subdirectories in the Windows temp folder (%temp%).
        /// </summary>
        private void ClearTempFolder()
        {
            try
            {
                string tempPath = Path.GetTempPath();

                // Delete files
                foreach (var file in Directory.GetFiles(tempPath))
                {
                    try { File.Delete(file); } catch { }
                }

                // Delete subdirectories
                foreach (var dir in Directory.GetDirectories(tempPath))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
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

            // Quick Patch - Just update signatures
            var quickPatch = new ToolStripMenuItem("Quick Patch")
            {
                ToolTipText = "Update signatures only (fastest)",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            quickPatch.Click += async (s, e) => await ExecutePatchAsync(PatchMode.Quick);

            // Full Re-patch - Re-apply all patches
            var fullPatch = new ToolStripMenuItem("Full Re-patch")
            {
                ToolTipText = "Re-apply all patches (recommended after Dota update)",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            fullPatch.Click += async (s, e) => await ExecutePatchAsync(PatchMode.Full);

            var sep1 = new ToolStripSeparator { BackColor = Color.FromArgb(51, 51, 51) };

            // Verify Files
            var verifyFiles = new ToolStripMenuItem("Verify Mod Files")
            {
                ToolTipText = "Check if all mod files are present and valid",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            verifyFiles.Click += async (s, e) => await VerifyModFilesAsync();

            // View Status
            var viewStatus = new ToolStripMenuItem("View Status Details")
            {
                ToolTipText = "Show detailed mod status information",
                Padding = new Padding(12, 8, 12, 8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            viewStatus.Click += (s, e) => ShowStatusDetails();

            _patchMenu.Items.AddRange(new ToolStripItem[] 
            { 
                quickPatch, 
                fullPatch, 
                sep1, 
                verifyFiles, 
                viewStatus 
            });

        }

        // Ensuring tray icon uses the correct icon even if loaded later


        private async void UpdatePatcherButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                return;
            }

            // Check current status to determine action
            if (_currentStatus?.Status == ModStatus.NeedUpdate)
            {
                // Direct action when update is needed - show menu
                ShowPatchMenu();
            }
            else if (_currentStatus?.Status == ModStatus.Ready)
            {
                // If already ready, show options menu
                ShowPatchMenu();
            }
            else if (_currentStatus?.Status == ModStatus.Disabled)
            {
                // If disabled, offer to enable
                var result = MessageBox.Show(
                    "Mods are currently disabled. Would you like to enable them?",
                    "Enable Mods",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await ExecutePatchAsync(PatchMode.Full);
                }
            }
            else if (_currentStatus?.Status == ModStatus.NotInstalled)
            {
                MessageBox.Show(
                    "Please install mods first using the 'Skin Selector' or 'Miscellaneous' buttons.",
                    "Mods Not Installed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                // Default: show menu
                ShowPatchMenu();
            }
        }

        private void ShowPatchMenu()
        {
            EnsurePatchMenuCreated();
            
            // Position menu below the button
            var button = updatePatcherButton;
            var location = button.PointToScreen(new Point(0, button.Height));
            _patchMenu!.Show(location);
        }

        private async Task ExecutePatchAsync(PatchMode mode)
        {
            if (string.IsNullOrEmpty(targetPath)) return;
            if (!_modInstaller.IsRequiredModFilePresent(targetPath))
            {
                MessageBox.Show(
                    "Mod VPK file not found. Please install mods first.",
                    "Cannot Patch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                // Use the new UpdatePatcherAsync with PatchResult
                var result = await _modInstaller.UpdatePatcherAsync(targetPath, mode, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        // Save version cache (track when we last patched)
                        var versionInfo = await _versionService.GetVersionInfoAsync(targetPath);
                        await _versionService.SaveVersionCacheAsync(targetPath, versionInfo);
                        
                        // Save patched version.json for verification
                        await _versionService.SavePatchedVersionJsonAsync(targetPath);
                        
                        // Refresh status
                        await CheckModsStatus();

                        MessageBox.Show(
                            "Patch applied successfully! Your mods are now ready.",
                            "Patch Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        break;

                    case PatchResult.AlreadyPatched:
                        await CheckModsStatus();
                        MessageBox.Show(
                            "Already patched! No action needed.",
                            "Already Up To Date",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        break;

                    case PatchResult.Failed:
                        MessageBox.Show(
                            "Patch failed. Check the console for details.",
                            "Patch Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        break;

                    case PatchResult.Cancelled:
                        // Silently handled
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[PATCH] Error: {ex.Message}");
                MessageBox.Show(
                    $"Patch error: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task VerifyModFilesAsync()
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                MessageBox.Show("Please detect Dota 2 path first.", "Path Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _logger.Log("[VERIFY] Starting file verification...");

            var issues = new List<string>();
            int checksPassed = 0;
            const int totalChecks = 4;

            // Check 1: Mod Package (VPK)
            string vpkPath = Path.Combine(targetPath, "game/_ArdysaMods/pak01_dir.vpk");
            if (File.Exists(vpkPath))
            {
                checksPassed++;
                _logger.Log("[VERIFY] ✓ Mod Package exists");
            }
            else
            {
                issues.Add("Mod Package not installed");
            }

            // Check 2: Dota Version Match (compare steam.inf with saved version.json)
            var (versionMatches, currentVer, patchedVer) = await _versionService.ComparePatchedVersionAsync(targetPath);
            if (versionMatches)
            {
                checksPassed++;
                _logger.Log($"[VERIFY] ✓ Version match: {currentVer}");
            }
            else
            {
                if (patchedVer == "Not patched yet")
                {
                    issues.Add("Never patched - run Full Re-patch first");
                }
                else
                {
                    issues.Add($"Dota updated: {currentVer} (patched: {patchedVer})");
                }
                _logger.Log($"[VERIFY] ✗ Version mismatch: current={currentVer}, patched={patchedVer}");
            }

            // Check 3: Game Compatibility (signatures - hidden name)
            string sigPath = Path.Combine(targetPath, "game/bin/win64/dota.signatures");
            if (File.Exists(sigPath))
            {
                checksPassed++;
                _logger.Log("[VERIFY] ✓ Game compatibility verified");
            }
            else
            {
                issues.Add("Game compatibility issue detected");
            }

            // Check 4: Mod Integration (gameinfo - hidden name)
            string giPath = Path.Combine(targetPath, "game/dota/gameinfo_branchspecific.gi");
            if (File.Exists(giPath))
            {
                var content = await File.ReadAllTextAsync(giPath);
                if (content.Contains("_Ardysa", StringComparison.OrdinalIgnoreCase))
                {
                    checksPassed++;
                    _logger.Log("[VERIFY] ✓ Mod integration active");
                }
                else
                {
                    issues.Add("Mod integration not active");
                }
            }
            else
            {
                issues.Add("Core game file missing");
            }

            // Show results
            if (checksPassed == totalChecks)
            {
                _logger.Log("[VERIFY] All files verified successfully!");
                MessageBox.Show(
                    $"✅ Verification Complete\n\n" +
                    $"All {totalChecks} checks passed!\n\n" +
                    $"• Mod Package: OK\n" +
                    $"• Dota Version: OK\n" +
                    $"• Game Compatibility: OK\n" +
                    $"• Mod Integration: OK\n\n" +
                    $"Your mods are properly installed.",
                    "Verification Passed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _logger.Log($"[VERIFY] Issues found: {string.Join(", ", issues)}");
                
                var message = $"⚠️ Verification Found Issues\n\n" +
                    $"Passed: {checksPassed}/{totalChecks} checks\n\n" +
                    $"Issues:\n• " + string.Join("\n• ", issues) + "\n\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    $"Recommended Actions:\n" +
                    $"1. Run 'Full Re-patch' from the menu\n" +
                    $"2. Verify Dota 2 game files in Steam:\n" +
                    $"   Steam → Dota 2 → Properties → Verify\n" +
                    $"3. Contact developer if issue persists";

                MessageBox.Show(
                    message,
                    "Verification Issues",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private async void ShowStatusDetails()
        {
            if (_currentStatus == null)
            {
                MessageBox.Show("Status not checked yet. Please wait...", "Loading", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                MessageBox.Show("Please detect Dota 2 path first.", "Path Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Get detailed version info
                var versionInfo = await _versionService.GetVersionInfoAsync(targetPath);
                
                // Show the detailed status form
                using var form = new StatusDetailsForm(
                    _currentStatus, 
                    versionInfo,
                    async () => await ExecutePatchAsync(PatchMode.Full)
                );
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Error showing details: {ex.Message}");
                MessageBox.Show($"Error loading status details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private async Task UpdateVersionLabelAsync()
        {
            try
            {
                await Task.Delay(100); // Small async buffer

                string versionText = _updater?.CurrentVersion ?? "Unknown";

                if (versionLabel.InvokeRequired)
                {
                    versionLabel.Invoke(new Action(() =>
                        versionLabel.Text = $"Version: {versionText}"));
                }
                else
                {
                    versionLabel.Text = $"Version: {versionText}";
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to update version label: {ex.Message}");
            }
        }

        private void DotaStateChanged(bool isRunning)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => DotaStateChanged(isRunning)));
                return;
            }

            lblDotaWarning.Visible = isRunning;

            // Tray functionality removed - no longer minimize to tray when Dota runs

            // Disable/enable all action buttons when Dota 2 is running
            installButton.Enabled = !isRunning;
            disableButton.Enabled = !isRunning;
            autoDetectButton.Enabled = !isRunning;
            manualDetectButton.Enabled = !isRunning;
            updatePatcherButton.Enabled = !isRunning;
            miscellaneousButton.Enabled = !isRunning;
            btn_OpenSelectHero.Enabled = !isRunning;

            lblDotaWarning.Enabled = true;
            
            // Refresh status when Dota closes (might have updated)
            if (!isRunning && !string.IsNullOrEmpty(targetPath))
            {
                _ = CheckModsStatus();
            }
        }

        #region Enhanced Status Display

        private ToolTip? _statusTooltip;
        private ModStatusInfo? _currentStatus;

        /// <summary>
        /// Updates the mod status display with detailed information.
        /// </summary>
        private void SetModsStatusDetailed(ModStatusInfo statusInfo)
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
                tooltipText += $"\n\n→ {statusInfo.ActionButtonText}";
            }

            _statusTooltip.SetToolTip(statusModsDotLabel, tooltipText);
            _statusTooltip.SetToolTip(statusModsTextLabel, tooltipText);

            // Update button states based on status
            UpdateButtonsForStatus(statusInfo);
        }

        /// <summary>
        /// Updates action buttons based on current mod status.
        /// </summary>
        private void UpdateButtonsForStatus(ModStatusInfo statusInfo)
        {
            // Always keep SpringGreen text for consistency
            updatePatcherButton.ForeColor = Color.SpringGreen;

            switch (statusInfo.Status)
            {
                case ModStatus.Ready:
                    // Normal state - mods are working fine
                    updatePatcherButton.BackColor = Color.FromArgb(42, 42, 55);
                    break;

                case ModStatus.NeedUpdate:
                    // Highlight - needs attention
                    updatePatcherButton.BackColor = Color.FromArgb(60, 80, 60);
                    break;

                case ModStatus.Disabled:
                    // User needs to re-enable
                    updatePatcherButton.BackColor = Color.FromArgb(50, 60, 70);
                    break;

                case ModStatus.NotInstalled:
                    // Dimmed - button less relevant
                    updatePatcherButton.BackColor = Color.FromArgb(35, 35, 45);
                    break;

                case ModStatus.Error:
                    // Error state
                    updatePatcherButton.BackColor = Color.FromArgb(60, 40, 40);
                    break;

                default:
                    // Default state
                    updatePatcherButton.BackColor = Color.FromArgb(42, 42, 55);
                    break;
            }
        }

        #endregion

        private void label1_Click(object sender, EventArgs e) { }
        private void label2_Click(object sender, EventArgs e) { }
        private void button1_Click(object sender, EventArgs e) { }
        
        /// <summary>
        /// Handles click on the refresh icon to manually refresh mod status.
        /// </summary>
        private async void StatusRefreshButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                _logger?.Log("Cannot refresh status: No path detected.");
                return;
            }
            
            _logger?.Log("Refreshing mod status...");
            statusRefreshButton.Enabled = false;
            statusRefreshButton.ForeColor = Color.FromArgb(60, 60, 60);
            
            try
            {
                // Re-enable Patch Update button if Dota 2 is not running
                bool isDotaRunning = ProcessChecker.IsProcessRunning("dota2");
                if (!isDotaRunning)
                {
                    updatePatcherButton.Enabled = true;
                }
                
                await _status.CheckAndUpdateUIAsync(targetPath, statusModsDotLabel, statusModsTextLabel);
                _logger?.Log("Status refreshed.");
            }
            catch (Exception ex)
            {
                _logger?.Log($"Status refresh failed: {ex.Message}");
            }
            finally
            {
                statusRefreshButton.Enabled = true;
                statusRefreshButton.ForeColor = Color.FromArgb(100, 100, 100);
            }
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
        private void mainConsoleBox_TextChanged(object sender, EventArgs e)
        {

        }

        private async void Btn_OpenSelectHero_Click(object? sender, EventArgs e)
        {
            // If app not ready, ignore (defensive)
            if (string.IsNullOrEmpty(targetPath))
            {
                return;
            }

            // Check GitHub access before opening form
            // _logger?.Log("Accessing Database Server...");
            var hasAccess = await CheckHeroesJsonAccessAsync();
            if (!hasAccess)
            {
                MessageBox.Show(
                    "Unable to access this feature.\n\n" +
                    "Please check your internet connection and try again.\n" +
                    "The Select Hero feature requires online access to load hero data.",
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _logger?.Log("Connection to server failed.");
                return;
            }

            // Show beta warning dialog
            var betaResult = MessageBox.Show(
                "[BETA VERSION] - Skin Selector\n\n" +
                "This feature is currently in beta. Not all hero sets are available yet.\n\n" +
                "More hero sets will be added soon!\n\n" +
                "IMPORTANT:\n" +
                "Using this feature will remove your ModsPack.\n" +
                "Custom sets are built independently and are NOT linked to the standard ModsPack.\n\n" +
                "Do you want to continue?",
                "Beta Feature Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (betaResult != DialogResult.Yes)
            {
                return;
            }

            // Show modal, similar to MiscForm pattern
            using (var f = new ArdysaModsTools.UI.Forms.SelectHero())
            {
                f.StartPosition = FormStartPosition.CenterParent;
                var dialogResult = f.ShowDialog(this);
                
                // If generation was successful, refresh mod status and check if patching needed
                if (dialogResult == DialogResult.OK)
                {
                    await CheckModsStatus();
                    
                    await ShowPatchRequiredIfNeededAsync("Custom ModsPack installed successfully!");
                }
            }
        }

        /// <summary>
        /// Check if heroes.json is accessible from GitHub.
        /// </summary>
        private async Task<bool> CheckHeroesJsonAccessAsync()
        {
            string heroesJsonUrl = EnvironmentConfig.BuildRawUrl("Assets/heroes.json");
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, heroesJsonUrl);
                using var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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

        private void imageContainer_Click(object sender, EventArgs e)
        {
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

        /// <summary>
        /// Draw cyan border on top of everything
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }

        /// <summary>
        /// Override WndProc for custom handling
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Border removed - no custom drawing
        }

        // Window dragging
        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
            }
        }

        private void HeaderPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point newLocation = PointToScreen(e.Location);
                Location = new Point(newLocation.X - _dragStart.X - headerPanel.Left,
                                     newLocation.Y - _dragStart.Y - headerPanel.Top);
            }
        }

        private void HeaderPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
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

        #endregion

        private void sidebarPanel_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
    }
}
