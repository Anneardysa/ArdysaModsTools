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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Update;
using System.Net.Http;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Handles all business logic for MainForm.
    /// Coordinates between services and the view.
    /// Single Responsibility: Orchestrate main form operations.
    /// </summary>
    public class MainFormPresenter : IDisposable
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly UpdaterService _updater;
        private readonly ModInstallerService _modInstaller;
        private readonly DetectionService _detection;
        private readonly StatusService _status;
        private readonly IConfigService _config;
        private readonly Dota2Monitor _dotaMonitor;
        private readonly DotaVersionService _versionService;
        
        private bool _patchDialogDismissedByUser;
        private DotaPatchWatcherService? _patchWatcher;

        private string? _targetPath;
        private CancellationTokenSource? _operationCts;
        private Task<(bool Success, bool IsUpToDate)>? _ongoingOperationTask;
        private bool _disposed;

        private const string RequiredModFilePath = DotaPaths.ModsVpk;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether an operation is currently running.
        /// </summary>
        public bool IsOperationRunning => _ongoingOperationTask != null;

        #endregion



        #region Constructor & Initialization

        /// <summary>
        /// Creates a new MainFormPresenter with the specified view.
        /// </summary>
        /// <param name="view">The view to control</param>
        /// <param name="logger">Logger instance for UI logging</param>
        public MainFormPresenter(IMainFormView view, Logger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _updater = new UpdaterService(_logger);
            _updater.OnVersionChanged += version =>
            {
                _view.InvokeOnUIThread(() => _view.SetVersion($"Version: {version}"));
            };

            _modInstaller = new ModInstallerService(_logger);
            _detection = new DetectionService(_logger);
            _status = new StatusService(_logger);
            _config = ServiceLocator.GetRequired<IConfigService>();
            _versionService = new DotaVersionService(_logger);

            _dotaMonitor = new Dota2Monitor();
            _dotaMonitor.OnDota2StateChanged += OnDotaStateChanged;
            _dotaMonitor.Start();

            // Load last target path
            LoadLastTargetPath();
        }

        /// <summary>
        /// Initializes the presenter and performs startup tasks.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Check for updates
                await _updater.CheckForUpdatesAsync();
                await UpdateVersionAsync();

                // Update UI based on target path
                if (!string.IsNullOrEmpty(_targetPath))
                {
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    _view.EnableAllButtons();
                }
                else
                {
                    _view.EnableDetectionButtonsOnly();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Initialization error: {ex.Message}");
                _view.EnableDetectionButtonsOnly();
            }
        }

        private void LoadLastTargetPath()
        {
            var last = _config.GetLastTargetPath();
            if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
            {
                _targetPath = last;
                _view.TargetPath = last;
            }
            else if (!string.IsNullOrEmpty(last))
            {
                _config.SetLastTargetPath(null);
            }
        }

        #endregion

        #region Detection Commands

        /// <summary>
        /// Attempts to auto-detect the Dota 2 installation path.
        /// </summary>
        public async Task AutoDetectAsync()
        {
            _view.DisableAllButtons();

            try
            {
                var detectedPath = await _detection.AutoDetectAsync();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _targetPath = detectedPath;
                    _view.TargetPath = detectedPath;
                    _config.SetLastTargetPath(_targetPath);
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    await ShowInstallDialogIfNeededAsync();
                    _view.EnableAllButtons();
                }
                else
                {
                    _view.EnableDetectionButtonsOnly();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Auto-detect failed: {ex.Message}");
                _view.EnableDetectionButtonsOnly();
            }
        }

        /// <summary>
        /// Shows a dialog for manual Dota 2 path selection.
        /// </summary>
        public async Task ManualDetectAsync()
        {
            _view.DisableAllButtons();

            try
            {
                var selectedPath = _detection.ManualDetect();
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _targetPath = selectedPath;
                    _view.TargetPath = selectedPath;
                    _config.SetLastTargetPath(_targetPath);
                    _logger.Log($"Dota 2 path set: {_targetPath}");
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    await ShowInstallDialogIfNeededAsync();
                    _view.EnableAllButtons();
                }
                else
                {
                    _view.EnableDetectionButtonsOnly();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Manual detect failed: {ex.Message}");
                _view.EnableDetectionButtonsOnly();
            }
        }

        #endregion

        #region Install Commands

        /// <summary>
        /// Initiates the mod installation process.
        /// </summary>
        public async Task<bool> InstallAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.ShowMessageBox("Please set Dota 2 path first.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Show install method dialog
            var method = _view.ShowInstallMethodDialog();
            if (method == null)
                return false; // Cancelled

            if (method == false)
            {
                // Manual install
                return await HandleManualInstallAsync();
            }

            // Auto install
            
            // Limit check to "CheckForNewerModsPackAsync" to match MainForm logic
            try
            {
                var (hasNewer, hasLocalInstall) = await _modInstaller.CheckForNewerModsPackAsync(_targetPath);
                
                if (hasNewer && hasLocalInstall)
                {
                    // Show dialog for update
                    var result = _view.ShowMessageBox(
                        "A newer ModsPack is available!\n\nDownload and install the update?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );
                    
                    if (result != DialogResult.Yes)
                    {
                        // User chose NOT to update
                        // In MainForm this just returned, effectively cancelling the install if it was an "Update" intent?
                        // Or maybe it proceeds with existing? 
                        // MainForm logic: "if (result != DialogResult.Yes) return;"
                        // So we cancel.
                        return false; 
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Update check failed: {ex.Message}");
                // Proceed anyway as fallback
            }

            return await RunAutoInstallAsync();
        }

        private async Task<bool> RunAutoInstallAsync()
        {
            // Lock UI
            StartOperation();
            var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

            bool success = false;
            bool isUpToDate = false;

            try
            {
                var result = await _view.RunWithProgressOverlayAsync("Initializing...", async (ctx) =>
                {
                    // Run installation using context tokens and progress
                    var (opSuccess, opIsUpToDate) = await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, ctx.Progress, ctx.Token, force: false, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    success = opSuccess;
                    isUpToDate = opIsUpToDate;

                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Installation failed");
                });

                if (!result.Success)
                {
                    // Service already logs detailed failure - just set flag
                    success = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Installation error: {ex.Message}");
                success = false;
            }
            finally
            {
                EndOperation();
            }

            // Handle result
            await HandleInstallResultAsync(success, isUpToDate, appPath);
            return success;
        }

        private async Task HandleInstallResultAsync(bool success, bool isUpToDate, string appPath)
        {
            if (isUpToDate)
            {
                var result = _view.ShowMessageBox(
                    "ModsPack already up to date.\nReinstall anyway?",
                    "Up to Date",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await ReinstallAsync(appPath);
                }
            }
            else if (success)
            {
                await CheckModsStatusAsync();
                _view.ShowStyledMessage(
                    "Installation Complete",
                    "ModsPack installed successfully!",
                    Forms.StyledMessageType.Success);
                
                // Check if patch is required after install
                await ShowPatchRequiredIfNeededAsync();
            }
            else
            {
                _view.ShowStyledMessage(
                    "Installation Failed",
                    "Installation failed. Check the console for details.",
                    Forms.StyledMessageType.Error);
            }
        }

        private async Task ReinstallAsync(string appPath)
        {
            StartOperation();
            
            try
            {
                var result = await _view.RunWithProgressOverlayAsync("Reinstalling ModsPack...", async (ctx) =>
                {
                    var (opSuccess, _) = await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, ctx.Progress, ctx.Token, force: true, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Reinstallation failed");
                });

                if (result.Success)
                {
                    await CheckModsStatusAsync();
                    _view.ShowStyledMessage(
                        "Reinstallation Complete",
                        "ModsPack reinstalled successfully!",
                        Forms.StyledMessageType.Success);
                }
                else
                {
                    // Service already logs detailed failure
                    _view.ShowStyledMessage(
                        "Reinstallation Failed",
                        "Reinstallation failed. Check the console for details.",
                        Forms.StyledMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Reinstallation error: {ex.Message}");
                _view.ShowStyledMessage(
                    "Reinstallation Error",
                    $"Error: {ex.Message}",
                    Forms.StyledMessageType.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task<bool> HandleManualInstallAsync()
        {
            var vpkPath = _view.ShowFileDialog("Select VPK File", "VPK Files (*.vpk)|*.vpk");
            if (string.IsNullOrEmpty(vpkPath))
                return false;

            var fileName = Path.GetFileName(vpkPath);
            if (!fileName.Equals("pak01_dir.vpk", StringComparison.OrdinalIgnoreCase))
            {
                _view.ShowStyledMessage(
                    "Invalid File",
                    "Please select pak01_dir.vpk",
                    Forms.StyledMessageType.Warning);
                return false;
            }

            var (isValid, errorMessage) = await _modInstaller.ValidateVpkAsync(vpkPath);
            if (!isValid)
            {
                _view.ShowStyledMessage(
                    "Invalid VPK",
                    "ModsPack Error.\nPlease contact developer if this is unexpected.",
                    Forms.StyledMessageType.Error);
                return false;
            }

            // Confirmation dialog - using standard MessageBox for Yes/No is acceptable
            var confirmResult = _view.ShowMessageBox(
                $"Install VPK:\n{fileName}\n\nContinue?",
                "Confirm Install",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return false;

            StartOperation();
            bool success = false;

            try
            {
                var result = await _view.RunWithProgressOverlayAsync("Installing ModsPack...", async (ctx) =>
                {
                    var opSuccess = await _modInstaller.ManualInstallModsAsync(
                        _targetPath!, vpkPath, ctx.Progress, ctx.Token, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    if (opSuccess) success = true;

                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Installation failed");
                });

                if (result.Success)
                {
                    await CheckModsStatusAsync();
                    _view.ShowStyledMessage(
                        "Installation Complete",
                        "ModsPack installed successfully! Your mods are now ready.",
                        Forms.StyledMessageType.Success);
                }
                else
                {
                    _logger.Log(result.Message ?? "Installation failed");
                    _view.ShowStyledMessage(
                        "Installation Failed",
                        "Installation failed. Check the console for details.",
                        Forms.StyledMessageType.Error);
                    success = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error installing VPK: {ex.Message}");
                _view.ShowStyledMessage(
                    "Installation Error",
                    $"Error: {ex.Message}",
                    Forms.StyledMessageType.Error);
                success = false;
            }
            finally
            {
                EndOperation();
            }

            return success;
        }

        #endregion

        #region Disable Command

        /// <summary>
        /// Disables all mods by removing the VPK file.
        /// Simple version that just removes the VPK.
        /// </summary>
        public async Task DisableAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
                return;

            var cts = StartOperation();

            try
            {
                var vpkPath = Path.Combine(_targetPath, RequiredModFilePath);
                if (File.Exists(vpkPath))
                {
                    File.Delete(vpkPath);
                    _logger.Log("Mods disabled - VPK file removed.");
                }

                await CheckModsStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to disable mods: {ex.Message}");
                _view.ShowMessageBox($"Failed to disable mods: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Disables mods with user options (simple disable or permanent delete).
        /// Shows options dialog and handles both cases including temp folder cleanup.
        /// </summary>
        public async Task DisableWithOptionsAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _logger.Log("No Dota 2 folder selected.");
                return;
            }

            // Show options dialog
            var (shouldProceed, deletePermanently) = _view.ShowDisableOptionsDialog();
            if (!shouldProceed)
                return;

            // Confirm if deleting permanently
            if (deletePermanently)
            {
                var confirm = _view.ShowMessageBox(
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
                bool success = await _modInstaller.DisableModsAsync(_targetPath, token);

                // If delete permanently, also remove _ArdysaMods folder and clear temp
                if (deletePermanently && success)
                {
                    string modsFolder = Path.Combine(_targetPath, "game", "_ArdysaMods");
                    if (Directory.Exists(modsFolder))
                    {
                        try
                        {
                            Directory.Delete(modsFolder, true);
                            _logger.Log("Mod folder deleted permanently.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Failed to delete mod folder: {ex.Message}");
                        }
                    }

                    // Clear temp folder
                    ClearTempFolder();

                    // Show restart dialog
                    if (_view.ShowRestartAppDialog("All mod files have been deleted.\nPlease restart the application."))
                    {
                        _view.RestartApplication();
                    }
                }

                await CheckModsStatusAsync();
            }
            catch (OperationCanceledException)
            {
                // Silent cancel
            }
            catch (Exception ex)
            {
                _logger.Log($"Disable operation failed: {ex.Message}");
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

        #endregion

        #region Update Patcher Command

        /// <summary>
        /// Updates the patcher (signatures and gameinfo) by reinstalling patch files.
        /// </summary>
        public async Task UpdatePatcherAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
                return;

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                _logger.Log("Patching dota.signatures and gameinfo_branchspecific.gi...");

                // Use ModInstallerService to patch the signatures with new PatchResult
                var result = await _modInstaller.UpdatePatcherAsync(_targetPath, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        _logger.Log("Patch update completed successfully.");
                        _view.ShowMessageBox("Patch update completed!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    
                    case PatchResult.AlreadyPatched:
                        _logger.Log("Already patched - no action needed.");
                        _view.ShowMessageBox("Already patched! No action needed.", "Up To Date",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    
                    case PatchResult.Failed:
                        _logger.Log("Patch update failed.");
                        _view.ShowMessageBox("Patch update failed. Check the log for details.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    
                    case PatchResult.Cancelled:
                        _logger.Log("Patch update cancelled.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Patch update error: {ex.Message}");
                _view.ShowMessageBox($"Patch update failed: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        #endregion

        #region Status & Utilities

        private async Task CheckModsStatusAsync(bool force = false)
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.SetModsStatus(false, "Not Detected");
                return;
            }

            try
            {
                ModStatusInfo statusInfo;
                if (force)
                {
                    statusInfo = await _status.ForceRefreshAsync(_targetPath);
                }
                else
                {
                    statusInfo = await _status.GetDetailedStatusAsync(_targetPath);
                }

                // Check for duplicates to avoid log spam
                bool isSameStatus = _currentStatus != null && 
                                   _currentStatus.Status == statusInfo.Status && 
                                   _currentStatus.Description == statusInfo.Description;

                _currentStatus = statusInfo; // Store for ShowStatusDetails
                
                _view.InvokeOnUIThread(() =>
                {
                    // Use detailed status update if available
                    _view.SetModsStatusDetailed(statusInfo);
                    
                    // Log status change only if changed or forced
                    if (!isSameStatus || force)
                    {
                        _logger.Log($"[STATUS] {statusInfo.StatusText}: {statusInfo.Description}");
                    }
                    
                    // Auto-enable/disable buttons based on status
                    if (statusInfo.Status == ModStatus.Error)
                    {
                        _view.EnableDetectionButtonsOnly();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Error checking status: {ex.Message}");
                _view.SetModsStatus(false, "Error");
            }
        }

        /// <summary>
        /// Manually refresh the mod status.
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            await CheckModsStatusAsync();
        }

        /// <summary>
        /// Start auto-refresh of status (every 30 seconds + real-time file watching).
        /// </summary>
        public void StartAutoRefresh()
        {
            if (!string.IsNullOrEmpty(_targetPath))
            {
                _status.OnStatusChanged += OnStatusChanged;
                _status.OnCheckingStarted += OnCheckingStarted;
                _status.StartAutoRefresh(_targetPath);
            }
        }

        /// <summary>
        /// Stop auto-refresh of status.
        /// </summary>
        public void StopAutoRefresh()
        {
            _status.OnStatusChanged -= OnStatusChanged;
            _status.OnCheckingStarted -= OnCheckingStarted;
            _status.StopAutoRefresh();
        }

        private void OnStatusChanged(ModStatusInfo statusInfo)
        {
            _currentStatus = statusInfo; // Store for ShowStatusDetails
            _view.InvokeOnUIThread(() =>
            {
                _view.SetModsStatusDetailed(statusInfo);
            });
        }

        private void OnCheckingStarted()
        {
            _view.InvokeOnUIThread(() =>
            {
                _view.ShowCheckingState();
            });
        }

        private async Task UpdateVersionAsync()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "Unknown";
                _view.SetVersion($"Version: {version}");
            }
            catch
            {
                _view.SetVersion("Version: Unknown");
            }
        }

        private void OnDotaStateChanged(bool isRunning)
        {
            _view.InvokeOnUIThread(() =>
            {
                if (isRunning)
                {
                    _view.DisableAllButtons();
                    _view.ShowMessageBox(
                        "Dota 2 is now running. Operations are disabled while the game is active.",
                        "Dota 2 Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_targetPath))
                        _view.EnableAllButtons();
                    else
                        _view.EnableDetectionButtonsOnly();
                }
            });
        }

        #endregion

        #region Dota Patch Watcher

        /// <summary>
        /// Starts monitoring for Dota 2 updates.
        /// Will skip if already watching the same path.
        /// </summary>
        public async Task StartPatchWatcherAsync(string dotaPath)
        {
            try
            {
                // Skip if already watching
                if (_patchWatcher != null && _patchWatcher.IsWatching)
                {
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
        /// Shows notification and updates UI.
        /// </summary>
        private void OnPatchDetected(PatchDetectedEventArgs args)
        {
            try
            {
                _logger.Log($"[PatchWatcher] Dota 2 update detected: {args.ChangeSummary}");

                // Show notification
                ShowPatchDetectedNotification(args);

                // Update UI via view interface
                _view.SetPatchDetectedStatus();

                // Refresh status to get accurate state
                _ = CheckModsStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Error handling patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows notification for detected patch.
        /// Logs to console and plays system sound.
        /// </summary>
        private void ShowPatchDetectedNotification(PatchDetectedEventArgs args)
        {
            try
            {
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
        /// Stops the patch watcher and cleans up resources.
        /// </summary>
        public void StopPatchWatcher()
        {
            _patchWatcher?.Dispose();
            _patchWatcher = null;
        }

        #endregion

        #region Operation Management

        private CancellationTokenSource StartOperation()
        {
            try
            {
                _operationCts?.Cancel();
                _operationCts?.Dispose();
            }
            catch { }

            _operationCts = new CancellationTokenSource();
            _view.DisableAllButtons();
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

            if (string.IsNullOrEmpty(_targetPath))
                _view.EnableDetectionButtonsOnly();
            else
                _view.EnableAllButtons();
        }

        /// <summary>
        /// Cancels the current operation.
        /// </summary>
        public void CancelOperation()
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


        /// <summary>
        /// Gets the current target path.
        /// </summary>
        public string? TargetPath => _targetPath;

        /// <summary>
        /// Gets whether the required mod file is present.
        /// </summary>
        public bool IsModFilePresent =>
            !string.IsNullOrEmpty(_targetPath) &&
            File.Exists(Path.Combine(_targetPath, RequiredModFilePath));

        #endregion





        #region Post-Install Dialogs

        /// <summary>
        /// Shows install dialog if mods are not installed.
        /// Called after successful path detection for better UX.
        /// </summary>
        public async Task ShowInstallDialogIfNeededAsync()
        {
            // Only show dialog if mods are not installed
            if (IsModFilePresent)
                return;

            if (_view.ShowInstallRequiredDialog())
            {
                // User clicked Install Now - trigger install
                await InstallAsync();
            }
        }

        /// <summary>
        /// Check status after install and show PatchRequiredDialog if not Ready.
        /// </summary>
        /// <param name="successMessage">Message to show in dialog</param>
        /// <param name="fromDetection">If true, respects user's previous "Later" dismissal</param>
        public async Task ShowPatchRequiredIfNeededAsync(string successMessage = "ModsPack installed successfully!", bool fromDetection = false)
        {
            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(_targetPath);

                // If status is Ready, no action needed - also reset dismissed flag
                if (statusInfo.Status == ModStatus.Ready)
                {
                    _patchDialogDismissedByUser = false;
                    return;
                }

                // If from detection and user already dismissed with "Later", skip showing again
                if (fromDetection && _patchDialogDismissedByUser)
                {
                    return;
                }

                // If status is NeedUpdate or Disabled - show dialog
                if (statusInfo.Status == ModStatus.NeedUpdate ||
                    statusInfo.Status == ModStatus.Disabled)
                {
                    // If user clicked "Patch Now", execute patch directly
                    if (_view.ShowPatchRequiredDialog(successMessage))
                    {
                        _patchDialogDismissedByUser = false; // Reset on successful patch
                        await ExecutePatchAsync();
                    }
                    else
                    {
                        // User clicked "Later" - remember this choice for detection-triggered dialogs
                        _patchDialogDismissedByUser = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Post-install check failed: {ex.Message}");
            }
        }




        /// <summary>
        /// Executes the patch update operation.
        /// </summary>
        public async Task ExecutePatchAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;
            if (!_modInstaller.IsRequiredModFilePresent(_targetPath))
            {
                _view.ShowMessageBox(
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
                // Always use full patch mode
                var result = await _modInstaller.UpdatePatcherAsync(_targetPath, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        // Refresh status
                        await CheckModsStatusAsync();

                        _view.ShowStyledMessage(
                            "Patch Complete",
                            "Patch applied successfully! Your mods are now ready.",
                            Forms.StyledMessageType.Success);
                        break;

                    case PatchResult.AlreadyPatched:
                        await CheckModsStatusAsync();
                        _view.ShowStyledMessage(
                            "Already Up To Date",
                            "Already patched! No action needed.",
                            Forms.StyledMessageType.Info);
                        break;

                    case PatchResult.Failed:
                        _view.ShowStyledMessage(
                            "Patch Failed",
                            "Patch failed. Check the console for details.",
                            Forms.StyledMessageType.Error);
                        break;

                    case PatchResult.Cancelled:
                        // Silently handled
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[PATCH] Error: {ex.Message}");
                _view.ShowMessageBox(
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

        /// <summary>
        /// Handles the patch button click with status-aware behavior.
        /// Shows menu or takes direct action based on current status.
        /// </summary>
        public async Task HandlePatchButtonClickAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
                return;

            // Get fresh status before making decisions
            _view.ShowCheckingState();
            var freshStatus = await _status.ForceRefreshAsync(_targetPath);
            _view.SetModsStatusDetailed(freshStatus);

            // Take action based on status
            switch (freshStatus.Status)
            {
                case ModStatus.NeedUpdate:
                case ModStatus.Ready:
                    // Show options menu
                    _view.ShowPatchMenu();
                    break;

                case ModStatus.Disabled:
                    // Offer to enable
                    var result = _view.ShowMessageBox(
                        "Mods are currently disabled. Would you like to enable them?",
                        "Enable Mods",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        await ExecutePatchAsync();
                    }
                    break;

                case ModStatus.NotInstalled:
                    _view.ShowMessageBox(
                        "Please install mods first using the 'Skin Selector' or 'Miscellaneous' buttons.",
                        "Mods Not Installed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                default:
                    // Default: show menu
                    _view.ShowPatchMenu();
                    break;
            }
        }

        /// <summary>
        /// Shows status details in a dedicated form.
        /// </summary>
        public void ShowStatusDetails()
        {
            if (_currentStatus == null)
            {
                _view.ShowMessageBox("Status not checked yet. Please wait...", "Loading", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.ShowMessageBox("Please detect Dota 2 path first.", "Path Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _view.ShowStatusDetails(_currentStatus, ExecutePatchAsync);
        }

        private ModStatusInfo? _currentStatus;

        #endregion


        /// <summary>
        /// Verifies mod files integrity.
        /// </summary>
        public async Task VerifyModFilesAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                 _view.ShowMessageBox("Please detect Dota 2 path first.", "Path Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            _logger.Log("[VERIFY] Starting file verification...");

            var issues = new System.Collections.Generic.List<string>();
            int checksPassed = 0;
            const int totalChecks = 4;

            // Check 1: Mod Package (VPK)
            string vpkPath = Path.Combine(_targetPath, DotaPaths.ModsVpk); 
            if (File.Exists(vpkPath))
            {
                checksPassed++;
                _logger.Log("[VERIFY] ✓ Mod Package exists");
            }
            else
            {
                issues.Add("Mod Package not installed");
            }

            // Check 2: Dota Version Match
            var (versionMatches, currentVer, patchedVer) = await _versionService.ComparePatchedVersionAsync(_targetPath);
            if (versionMatches)
            {
                checksPassed++;
                _logger.Log($"[VERIFY] ✓ Version match: {currentVer}");
            }
            else
            {
                if (patchedVer == "Not patched yet")
                {
                    issues.Add("Never patched - run Patch Update first");
                }
                else
                {
                    issues.Add($"Dota updated: {currentVer} (patched: {patchedVer})");
                }
                _logger.Log($"[VERIFY] ✗ Version mismatch: current={currentVer}, patched={patchedVer}");
            }

            // Check 3: Game Compatibility (signatures)
            string sigPath = Path.Combine(_targetPath, "game/bin/win64/dota.signatures"); 
            
            if (File.Exists(sigPath))
            {
                checksPassed++;
                _logger.Log("[VERIFY] ✓ Game compatibility verified");
            }
            else
            {
                issues.Add("Game compatibility issue detected");
            }

            // Check 4: Mod Integration (gameinfo + signatures format)
            string giPath = Path.Combine(_targetPath, "game/dota/gameinfo_branchspecific.gi");
            if (File.Exists(giPath) && File.Exists(sigPath))
            {
                var giContent = await File.ReadAllTextAsync(giPath);
                var sigContent = await File.ReadAllTextAsync(sigPath);
                
                bool hasGameInfoMarker = giContent.Contains("_Ardysa", StringComparison.OrdinalIgnoreCase);
                bool hasCorrectSigFormat = sigContent.Contains(
                    ModConstants.ModPatchLine,
                    StringComparison.Ordinal);
                
                if (hasGameInfoMarker && hasCorrectSigFormat)
                {
                    checksPassed++;
                    _logger.Log("[VERIFY] ✓ Mod integration active");
                }
                else if (hasGameInfoMarker && !hasCorrectSigFormat)
                {
                    issues.Add("Mod integration format invalid - run Patch Update");
                    _logger.Log("[VERIFY] ✗ Mod integration format invalid");
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
                _view.ShowMessageBox(
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
                    $"1. Run 'Patch Update' from the menu\n" +
                    $"2. Verify Dota 2 game files in Steam:\n" +
                    $"   Steam → Dota 2 → Properties → Verify\n" +
                    $"3. Contact developer if issue persists";

                _view.ShowMessageBox(
                    message,
                    "Verification Issues",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Refresh status indicator
            await CheckModsStatusAsync();
        }

        #region UI Commands
        

        /// <summary>
        /// Handles the Patcher button click.
        /// Refreshes status and decides whether to prompt for update or show menu.
        /// </summary>
        public async Task HandlePatcherClickAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;

            _view.ShowCheckingState();
            // Force refresh status
            await CheckModsStatusAsync();
            
            if (_currentStatus?.Status == ModStatus.NeedUpdate)
            {
                 var result = _view.ShowMessageBox(
                    $"Mods Status: {_currentStatus.StatusText}\n\nDo you want to run the Patch Update now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await ExecutePatchAsync();
                }
            }
            else if (_currentStatus?.Status == ModStatus.NotInstalled)
            {
                 _view.ShowMessageBox(
                    "Please install mods first using the 'Skin Selector' or 'Miscellaneous' buttons.",
                    "Mods Not Installed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                // Default: show menu
                _view.ShowPatchMenu();
            }
        }

        /// <summary>
        /// Opens the Miscellaneous options form.
        /// </summary>
        public async Task OpenMiscellaneousAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;

            // Show Misc Form and get result
            var (result, generationResult) = _view.ShowMiscForm(_targetPath);
            
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }

            // Refresh status if valid result
            // Refresh status if valid result
            if (result == DialogResult.OK)
            {
                await CheckModsStatusAsync(force: true);
                
                 // If generation was successful, check if patching needed
                if (generationResult != null && generationResult.Success)
                {
                    // Delay slightly to let status settle
                    await Task.Delay(500);
                    await ShowPatchRequiredIfNeededAsync("Custom ModsPack (Misc) installed successfully!");
                }
            }
        }

        /// <summary>
        /// Opens the Hero Selection gallery.
        /// </summary>
        public async Task OpenHeroSelectionAsync()
        {
             // If app not ready, ignore (defensive)
            if (string.IsNullOrEmpty(_targetPath))
            {
                return;
            }

            // Check GitHub access before opening form
            var hasAccess = await CheckHeroesJsonAccessAsync();
            if (!hasAccess)
            {
                _view.ShowMessageBox(
                    "Unable to access this feature.\n\n" +
                    "Please check your internet connection and try again.\n" +
                    "The Select Hero feature requires online access to load hero data.",
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _logger.Log("Connection to server failed.");
                return;
            }

            // Show beta warning dialog
            var betaResult = _view.ShowMessageBox(
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

            // Try modern WebView2 gallery first, fallback to classic if it fails
            DialogResult dialogResult;
            ModGenerationResult? generationResult = null;
            
            var (res, gen) = _view.ShowHeroGallery();
            dialogResult = res;
            generationResult = gen;
            
            // If WebView2 failed (Abort), fallback to classic SelectHero
            if (dialogResult == DialogResult.Abort)
            {
                _logger.Log("WebView2 failed, falling back to classic SelectHero...");
                var (resClassic, genClassic) = _view.ShowClassicHeroSelector();
                dialogResult = resClassic;
                generationResult = genClassic;
            }
            
            // Log generation result
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }
            
            // Always refresh status after hero gallery closes (force refresh)
            await CheckModsStatusAsync(force: true);
            
            // If generation was successful, check if patching needed
            if (dialogResult == DialogResult.OK)
            {
                await Task.Delay(500);
                
                // If NOT NeedUpdate (e.g. just replaced files without requiring patch), show success
                if (_currentStatus?.Status == ModStatus.Ready)
                {
                     _view.ShowMessageBox("Custom ModsPack installed successfully!", 
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    await ShowPatchRequiredIfNeededAsync("Custom ModsPack installed successfully!");
                }
            }
        }

        private async Task<bool> CheckHeroesJsonAccessAsync()
        {
            // Use EnvironmentConfig from Core
            string heroesJsonUrl = EnvironmentConfig.BuildRawUrl("Assets/heroes.json");
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var request = new HttpRequestMessage(HttpMethod.Head, heroesJsonUrl);
                using var response = await client.SendAsync(request, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        private void LogGenerationResult(ModGenerationResult result)
        {
            if (result.Success)
            {
                _logger.Log($"[GEN] Generation successful: {result.Details ?? "Mod generation completed."}");
                _logger.Log($"[GEN] Total items: {result.OptionsCount}");
                _logger.Log($"[GEN] Time taken: {result.Duration.TotalSeconds:F2}s");
            }
            else
            {
                _logger.Log($"[GEN] Generation failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }

        private async Task ShowPatchRequiredIfNeededAsync()
        {
            if (_currentStatus.Status == ModStatus.NeedUpdate || _currentStatus.Status == ModStatus.Disabled)
            {
                var shouldPatch = _view.ShowPatchRequiredDialog(
                    "Operation complete! But a patch is required for the mods to work correctly.");

                if (shouldPatch)
                {
                    await UpdatePatcherAsync();
                }
            }
        }

        #endregion

        #region Shutdown & Disposal

        /// <summary>
        /// Gracefully shuts down the presenter and cancels ongoing operations.
        /// Returns a task that completes when the ongoing operation (if any) has finished.
        /// </summary>
        public async Task ShutdownAsync()
        {
            // Signal cancellation
            _operationCts?.Cancel();
            
            // Wait for ongoing operation to complete (with timeout)
            if (_ongoingOperationTask != null)
            {
                try
                {
                    await Task.WhenAny(_ongoingOperationTask, Task.Delay(5000));
                }
                catch { /* Ignore errors during shutdown */ }
            }

            Dispose();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes of resources used by the presenter.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            StopPatchWatcher();
            _dotaMonitor?.Stop();
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}

