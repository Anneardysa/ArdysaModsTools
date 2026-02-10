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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Handles patch-related operations including updates, verification, and Dota 2 patch watching.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public class PatchPresenter : IPatchPresenter
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly ModInstallerService _modInstaller;
        private readonly StatusService _status;
        
        private DotaPatchWatcherService? _patchWatcher;
        private CancellationTokenSource? _operationCts;
        private ModStatusInfo? _currentStatus;

        #endregion

        #region Properties

        /// <inheritdoc />
        public string? TargetPath { get; set; }

        #endregion

        #region Events

        /// <inheritdoc />
        public event Action? PatchDetected;

        /// <inheritdoc />
        public event Func<Task>? StatusRefreshRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PatchPresenter.
        /// </summary>
        /// <param name="view">The view interface for UI updates</param>
        /// <param name="logger">Logger instance for logging</param>
        public PatchPresenter(IMainFormView view, Logger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modInstaller = new ModInstallerService(_logger);
            _status = new StatusService(_logger);
        }

        #endregion

        #region Patch Operations

        /// <inheritdoc />
        public async Task UpdatePatcherAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            StartOperation();
            var token = _operationCts?.Token ?? CancellationToken.None;

            try
            {
                _logger.Log("Patching dota.signatures and gameinfo_branchspecific.gi...");

                var result = await _modInstaller.UpdatePatcherAsync(TargetPath, null, token);

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

        /// <inheritdoc />
        public async Task ExecutePatchAsync()
        {
            if (string.IsNullOrEmpty(TargetPath)) return;
            if (!_modInstaller.IsRequiredModFilePresent(TargetPath))
            {
                _view.ShowMessageBox(
                    "Mod VPK file not found. Please install mods first.",
                    "Cannot Patch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            StartOperation();
            var token = _operationCts?.Token ?? CancellationToken.None;

            try
            {
                var result = await _modInstaller.UpdatePatcherAsync(TargetPath, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        await RaiseStatusRefreshAsync();
                        _view.ShowStyledMessage(
                            "Patch Complete",
                            "Patch applied successfully! Your mods are now ready.",
                            Forms.StyledMessageType.Success);
                        break;

                    case PatchResult.AlreadyPatched:
                        await RaiseStatusRefreshAsync();
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

        /// <inheritdoc />
        public async Task HandlePatchButtonClickAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            // Get fresh status before making decisions
            _view.ShowCheckingState();
            var freshStatus = await _status.ForceRefreshAsync(TargetPath);
            _currentStatus = freshStatus;
            _view.SetModsStatusDetailed(freshStatus);

            // Take action based on status
            switch (freshStatus.Status)
            {
                case ModStatus.NeedUpdate:
                    if (_view.ShowPatchRequiredDialog(freshStatus.Description))
                    {
                        await ExecutePatchAsync();
                    }
                    break;

                case ModStatus.Ready:
                    _view.ShowPatchMenu();
                    break;

                case ModStatus.Disabled:
                    if (_view.ShowPatchRequiredDialog("Mods are disabled. Enable and patch now?"))
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
                    _view.ShowPatchMenu();
                    break;
            }
        }

        /// <inheritdoc />
        public async Task VerifyModFilesAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            StartOperation();

            try
            {
                _logger.Log("═══════════════════════════════════════════");
                _logger.Log("Verifying Mod Files...");

                // Check VPK exists
                var vpkPath = Path.Combine(TargetPath, "game", "_ArdysaMods", "pak01_dir.vpk");
                if (!File.Exists(vpkPath))
                {
                    _logger.Log("[VERIFY] VPK file not found!");
                    _view.ShowStyledMessage(
                        "Verification Failed",
                        "Mod VPK file is missing. Please reinstall mods.",
                        Forms.StyledMessageType.Error);
                    return;
                }

                // Check VPK size
                var vpkInfo = new FileInfo(vpkPath);
                _logger.Log($"[VERIFY] VPK Size: {vpkInfo.Length / 1024 / 1024:F2} MB");

                if (vpkInfo.Length < 1024) // Less than 1KB
                {
                    _logger.Log("[VERIFY] VPK file appears corrupted (too small)!");
                    _view.ShowStyledMessage(
                        "Verification Failed",
                        "VPK file appears corrupted. Please reinstall mods.",
                        Forms.StyledMessageType.Error);
                    return;
                }

                // Check gameinfo patched
                var gameinfoPath = Path.Combine(TargetPath, "game", "dota", "gameinfo_branchspecific.gi");
                if (File.Exists(gameinfoPath))
                {
                    var content = await File.ReadAllTextAsync(gameinfoPath);
                    var isPatched = content.Contains("_ArdysaMods");
                    _logger.Log($"[VERIFY] Gameinfo patched: {(isPatched ? "Yes" : "No")}");
                }
                else
                {
                    _logger.Log("[VERIFY] Gameinfo file not found!");
                }

                // Check signatures
                var sigPath = Path.Combine(TargetPath, "game", "dota", "cfg", "dota.signatures");
                if (File.Exists(sigPath))
                {
                    var sigInfo = new FileInfo(sigPath);
                    _logger.Log($"[VERIFY] Signatures file: {sigInfo.Length} bytes");
                }
                else
                {
                    _logger.Log("[VERIFY] Signatures file not found (may be normal)");
                }

                _logger.Log("═══════════════════════════════════════════");
                _logger.Log("Verification complete!");

                _view.ShowStyledMessage(
                    "Verification Complete",
                    "Mod files verified. Check console for details.",
                    Forms.StyledMessageType.Success);
            }
            catch (Exception ex)
            {
                _logger.Log($"[VERIFY] Error: {ex.Message}");
                _view.ShowStyledMessage(
                    "Verification Error",
                    $"Error during verification: {ex.Message}",
                    Forms.StyledMessageType.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        #endregion

        #region Patch Watcher

        /// <inheritdoc />
        public async Task StartPatchWatcherAsync(string targetPath)
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

                await _patchWatcher.StartWatchingAsync(targetPath);
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Failed to start: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void StopPatchWatcher()
        {
            _patchWatcher?.Dispose();
            _patchWatcher = null;
        }

        private void OnPatchDetected(PatchDetectedEventArgs args)
        {
            try
            {
                _logger.Log($"[PatchWatcher] Dota 2 update detected: {args.ChangeSummary}");

                // Show notification
                ShowPatchDetectedNotification(args);

                // Update UI via view interface
                _view.SetPatchDetectedStatus();

                // Raise event for parent coordinator
                PatchDetected?.Invoke();

                // Request status refresh
                _ = RaiseStatusRefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Error handling patch: {ex.Message}");
            }
        }

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

        #endregion

        #region Operation Control

        private void StartOperation()
        {
            try
            {
                _operationCts?.Cancel();
                _operationCts?.Dispose();
            }
            catch { }

            _operationCts = new CancellationTokenSource();
            _view.DisableAllButtons();
        }

        private void EndOperation()
        {
            try
            {
                _operationCts?.Dispose();
            }
            catch { }
            _operationCts = null;

            if (string.IsNullOrEmpty(TargetPath))
                _view.EnableDetectionButtonsOnly();
            else
                _view.EnableAllButtons();
        }

        private async Task RaiseStatusRefreshAsync()
        {
            if (StatusRefreshRequested != null)
            {
                await StatusRefreshRequested.Invoke();
            }
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            StopPatchWatcher();
            _operationCts?.Dispose();
        }

        #endregion
    }
}
