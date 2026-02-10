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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Handles mod installation and management operations.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public class ModOperationsPresenter : IModOperationsPresenter
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly ModInstallerService _modInstaller;
        
        private CancellationTokenSource? _operationCts;
        private Task<(bool Success, bool IsUpToDate)>? _ongoingOperationTask;
        
        private const string RequiredModFilePath = DotaPaths.ModsVpk;

        #endregion

        #region Properties

        /// <inheritdoc />
        public bool IsOperationRunning => _ongoingOperationTask != null;

        /// <inheritdoc />
        public string? TargetPath { get; set; }

        #endregion

        #region Events

        /// <inheritdoc />
        public event Action? OperationStarted;

        /// <inheritdoc />
        public event Action? OperationEnded;

        /// <inheritdoc />
        public event Func<Task>? StatusRefreshRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new ModOperationsPresenter.
        /// </summary>
        /// <param name="view">The view interface for UI updates</param>
        /// <param name="logger">Logger instance for logging</param>
        public ModOperationsPresenter(IMainFormView view, Logger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modInstaller = new ModInstallerService(_logger);
        }

        #endregion

        #region Install Operations

        /// <inheritdoc />
        public async Task<bool> InstallAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
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

            // Auto install - check for updates first
            try
            {
                var (hasNewer, hasLocalInstall) = await _modInstaller.CheckForNewerModsPackAsync(TargetPath);
                
                if (hasNewer && hasLocalInstall)
                {
                    var result = _view.ShowMessageBox(
                        "A newer ModsPack is available!\n\nDownload and install the update?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information
                    );
                    
                    if (result != DialogResult.Yes)
                    {
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

        /// <inheritdoc />
        public async Task<bool> ReinstallAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return false;

            var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            
            StartOperation();
            bool success = false;
            
            try
            {
                var result = await _view.RunWithProgressOverlayAsync("Reinstalling ModsPack...", async (ctx) =>
                {
                    var (opSuccess, _) = await _modInstaller.InstallModsAsync(
                        TargetPath!, appPath, ctx.Progress, ctx.Token, force: true, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    success = opSuccess;
                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Reinstallation failed");
                });

                if (result.Success)
                {
                    await RaiseStatusRefreshAsync();
                    _view.ShowStyledMessage(
                        "Reinstallation Complete",
                        "ModsPack reinstalled successfully!",
                        Forms.StyledMessageType.Success);
                }
                else
                {
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

            return success;
        }

        private async Task<bool> RunAutoInstallAsync()
        {
            StartOperation();
            var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

            bool success = false;
            bool isUpToDate = false;

            try
            {
                var result = await _view.RunWithProgressOverlayAsync("Initializing...", async (ctx) =>
                {
                    var (opSuccess, opIsUpToDate) = await _modInstaller.InstallModsAsync(
                        TargetPath!, appPath, ctx.Progress, ctx.Token, force: false, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    success = opSuccess;
                    isUpToDate = opIsUpToDate;

                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Installation failed");
                });

                if (!result.Success)
                {
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
                    await ReinstallAsync();
                }
            }
            else if (success)
            {
                await RaiseStatusRefreshAsync();
                _view.ShowStyledMessage(
                    "Installation Complete",
                    "ModsPack installed successfully!",
                    Forms.StyledMessageType.Success);
            }
            else
            {
                _view.ShowStyledMessage(
                    "Installation Failed",
                    "Installation failed. Check the console for details.",
                    Forms.StyledMessageType.Error);
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
                        TargetPath!, vpkPath, ctx.Progress, ctx.Token, speedProgress: ctx.Speed
                    ).ConfigureAwait(false);

                    if (opSuccess) success = true;

                    return opSuccess 
                        ? OperationResult.Ok() 
                        : OperationResult.Fail("Installation failed");
                });

                if (result.Success)
                {
                    await RaiseStatusRefreshAsync();
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

        #region Disable Operations

        /// <inheritdoc />
        public async Task DisableAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            StartOperation();

            try
            {
                var vpkPath = Path.Combine(TargetPath, RequiredModFilePath);
                if (File.Exists(vpkPath))
                {
                    File.Delete(vpkPath);
                    _logger.Log("Mods disabled - VPK file removed.");
                }

                await RaiseStatusRefreshAsync();
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

        /// <inheritdoc />
        public async Task DisableWithOptionsAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
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

            StartOperation();
            var token = _operationCts?.Token ?? CancellationToken.None;

            try
            {
                // Disable mods (remove VPK, revert signatures)
                bool success = await _modInstaller.DisableModsAsync(TargetPath, token);

                // If delete permanently, also remove _ArdysaMods folder and clear temp
                if (deletePermanently && success)
                {
                    string modsFolder = Path.Combine(TargetPath, "game", "_ArdysaMods");
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

                await RaiseStatusRefreshAsync();
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

        private void ClearTempFolder()
        {
            try
            {
                string tempPath = Path.GetTempPath();

                // Only delete app-specific folders (Ardysa*)
                foreach (var dir in Directory.GetDirectories(tempPath, "Ardysa*"))
                {
                    try 
                    { 
                        Directory.Delete(dir, true);
                        _logger.Log($"Cleaned up: {Path.GetFileName(dir)}");
                    } 
                    catch { /* ignore locked files */ }
                }

                // Also clean any app-specific temp files
                foreach (var file in Directory.GetFiles(tempPath, "Ardysa*"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to clean temp folder: {ex.Message}");
            }
        }

        #endregion

        #region Operation Control

        /// <inheritdoc />
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
            OperationStarted?.Invoke();
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

            OperationEnded?.Invoke();
        }

        private async Task RaiseStatusRefreshAsync()
        {
            if (StatusRefreshRequested != null)
            {
                await StatusRefreshRequested.Invoke();
            }
        }

        #endregion
    }
}
