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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Update;
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
        private readonly MainConfigService _config;
        private readonly Dota2Monitor _dotaMonitor;

        private string? _targetPath;
        private CancellationTokenSource? _operationCts;
        private Task<(bool Success, bool IsUpToDate)>? _ongoingOperationTask;
        private bool _disposed;

        private const string RequiredModFilePath = DotaPaths.ModsVpk;

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
            _config = new MainConfigService();

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
                    _config.SetLastTargetPath(_targetPath);
                    await CheckModsStatusAsync();
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
                    _config.SetLastTargetPath(_targetPath);
                    _logger.Log($"Dota 2 path set: {_targetPath}");
                    await CheckModsStatusAsync();
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
            return await RunAutoInstallAsync();
        }

        private async Task<bool> RunAutoInstallAsync()
        {
            var cts = StartOperation();
            var token = cts.Token;
            var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

            bool success = false;
            bool isUpToDate = false;

            try
            {
                await _view.ShowProgressOverlayAsync();
                await _view.UpdateProgressAsync(0, "Initializing...");

                // Create progress reporter
                var speedTracker = new Stopwatch();
                long lastBytes = 0;
                double lastSpeed = 0;
                speedTracker.Start();

                Action<long, long> byteCallback = async (downloaded, total) =>
                {
                    double elapsed = speedTracker.Elapsed.TotalSeconds;
                    if (elapsed >= 0.5)
                    {
                        long deltaBytes = downloaded - lastBytes;
                        lastSpeed = deltaBytes / elapsed / (1024 * 1024);
                        lastBytes = downloaded;
                        speedTracker.Restart();
                    }

                    string speedText = lastSpeed > 0 ? $" @ {lastSpeed:F1} MB/s" : "";
                    string text = total > 0
                        ? $"{downloaded / (1024 * 1024)} / {total / (1024 * 1024)} MB{speedText}"
                        : $"{downloaded / (1024 * 1024)} MB{speedText}";

                    _view.InvokeOnUIThread(async () =>
                        await _view.UpdateProgressAsync(-1, "Downloading...", text));
                };

                var progress = new Progress<int>(async percent =>
                {
                    await _view.UpdateProgressAsync(Math.Clamp(percent, 0, 100), "Installing...");
                });

                // Run installation
                _ongoingOperationTask = Task.Run(async () =>
                {
                    return await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, progress, token, force: false, speedProgress: null
                    ).ConfigureAwait(false);
                }, token);

                (success, isUpToDate) = await _ongoingOperationTask;
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Installation cancelled.");
                success = false;
            }
            catch (Exception ex)
            {
                _logger.Log($"Installation error: {ex.Message}");
                success = false;
            }
            finally
            {
                _view.HideProgressOverlay();
                _ongoingOperationTask = null;
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
                _view.ShowMessageBox("Installation complete!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _view.ShowMessageBox("Installation failed. Check the log for details.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReinstallAsync(string appPath)
        {
            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                await _view.ShowProgressOverlayAsync();

                var progress = new Progress<int>(async percent =>
                {
                    await _view.UpdateProgressAsync(Math.Clamp(percent, 0, 100), "Reinstalling...");
                });

                var (success, _) = await Task.Run(async () =>
                {
                    return await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, progress, token, force: true, speedProgress: null
                    ).ConfigureAwait(false);
                }, token);

                if (success)
                {
                    await CheckModsStatusAsync();
                    _view.ShowMessageBox("Reinstallation complete!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Reinstallation cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Log($"Reinstallation error: {ex.Message}");
            }
            finally
            {
                _view.HideProgressOverlay();
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
                _view.ShowMessageBox("Please select pak01_dir.vpk", "Invalid File",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var (isValid, errorMessage) = await _modInstaller.ValidateVpkAsync(vpkPath);
            if (!isValid)
            {
                _view.ShowMessageBox("VPK is invalid, please contact developer.",
                    "Invalid VPK", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var confirmResult = _view.ShowMessageBox(
                $"Install VPK:\n{fileName}\n\nContinue?",
                "Confirm Install",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return false;

            var cts = StartOperation();
            var token = cts.Token;
            bool success = false;

            try
            {
                var progress = new Progress<int>(async percent =>
                {
                    await _view.UpdateProgressAsync(Math.Clamp(percent, 0, 100), "Installing...");
                });

                success = await Task.Run(async () =>
                {
                    return await _modInstaller.ManualInstallModsAsync(
                        _targetPath!, vpkPath, progress, token, speedProgress: null
                    ).ConfigureAwait(false);
                }, token);

                if (success)
                {
                    await CheckModsStatusAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Manual installation cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Log($"Manual installation error: {ex.Message}");
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

        private async Task CheckModsStatusAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.SetModsStatus(false, "Not Detected");
                return;
            }

            try
            {
                // Get detailed status from enhanced StatusService
                var statusInfo = await _status.GetDetailedStatusAsync(_targetPath);
                
                _view.InvokeOnUIThread(() =>
                {
                    // Use detailed status update if available
                    _view.SetModsStatusDetailed(statusInfo);
                    
                    // Log status change
                    _logger.Log($"[STATUS] {statusInfo.StatusText}: {statusInfo.Description}");
                    
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
        /// Gets whether an operation is currently running.
        /// </summary>
        public bool IsOperationRunning => _ongoingOperationTask != null;

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

        #region IDisposable

        /// <summary>
        /// Disposes of resources used by the presenter.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _dotaMonitor?.Stop();
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}

