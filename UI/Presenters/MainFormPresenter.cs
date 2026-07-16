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
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Mods;
using ArdysaModsTools.Core.Services.Update;
using System.Net.Http;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Forms;

namespace ArdysaModsTools.UI.Presenters
{
    public class MainFormPresenter : IDisposable
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly UpdaterService _updater;
        private readonly ModInstallerService _modInstaller;
        private readonly DetectionService _detection;
        private readonly IStatusService _status;
        private readonly IConfigService _config;
        private readonly Dota2Monitor _dotaMonitor;
        private readonly DotaVersionService _versionService;
        
        private readonly INavigationPresenter _navigationPresenter;
        
        private bool _patchDialogDismissedByUser;
        private DotaPatchWatcherService? _patchWatcher;
        private readonly ModsPackUpdateService _modsPackUpdateService;

        private string? _targetPath;
        private CancellationTokenSource? _operationCts;
        private readonly CancellationTokenSource _lifetimeCts = new();
        private bool _commandInFlight;
        private TaskCompletionSource<bool>? _operationGate;
        private bool _disposed;
        private ModStatusInfo? _currentStatus;

        private const string RequiredModFilePath = DotaPaths.ModsVpk;

        #endregion

        #region Public Properties

        public bool IsOperationRunning => _operationCts != null;

        public UpdaterService GetUpdaterService() => _updater;

        #endregion



        #region Constructor & Initialization

        public MainFormPresenter(IMainFormView view, Logger logger, IConfigService configService, IStatusService statusService)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configService ?? throw new ArgumentNullException(nameof(configService));
            _status = statusService ?? throw new ArgumentNullException(nameof(statusService));

            _updater = new UpdaterService(_logger);
            _updater.OnVersionChanged += version =>
            {
                _view.InvokeOnUIThread(() => _view.SetVersion(Loc.T("app.versionLabel", new { version })));
            };

            _modInstaller = new ModInstallerService(_logger);
            _modsPackUpdateService = new ModsPackUpdateService(_modInstaller, _logger);
            _detection = new DetectionService(_logger);
            _versionService = new DotaVersionService(_logger);

            _dotaMonitor = new Dota2Monitor();
            _dotaMonitor.OnDota2StateChanged += OnDotaStateChanged;
            _dotaMonitor.Start();

            _navigationPresenter = new NavigationPresenter(_view, _logger, _status);
            
            WireUpPresenterEvents();

            LoadLastTargetPath();
        }
        
        private void WireUpPresenterEvents()
        {
            _navigationPresenter.StatusRefreshRequested += async () => await RefreshStatusAsync();
            _navigationPresenter.PatchRequested += async () => await ExecutePatchAsync();
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (await _updater.CheckForUpdatesAsync())
                    return;

                await UpdateVersionAsync();

                if (!string.IsNullOrEmpty(_targetPath))
                {
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    _view.EnableAllButtons();
                    _ = CheckModsPackUpdateOnStartupAsync();
                }
                else if (_config.AutoDetectOnStartup)
                {
                    await AutoDetectAsync();
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
            if (!string.IsNullOrEmpty(last))
            {
                if (_detection.ValidateDotaPath(last))
                {
                    _targetPath = last;
                    _view.TargetPath = last;
                    SyncTargetPath(last);
                }
                else
                {
                    _logger.Log($"Saved path is no longer valid (dota2.exe not found): {last}");
                    _config.SetLastTargetPath(null);
                }
            }
        }
        
        private void SyncTargetPath(string? path)
        {
            _navigationPresenter.TargetPath = path;
        }
        
        private void SyncCurrentStatus(ModStatusInfo? status)
        {
            _currentStatus = status;
            _navigationPresenter.CurrentStatus = status;
        }

        private async Task CheckModsPackUpdateOnStartupAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_targetPath))
                    return;

                bool hasUpdate = await _modsPackUpdateService
                    .CheckForUpdateAsync(_targetPath, _lifetimeCts.Token);

                if (!hasUpdate)
                    return;

                if (_commandInFlight)
                {
                    _logger.Log("[ModsPack] Update available, but an operation is already running — not prompting.");
                    return;
                }

                var owner = _view as System.Windows.Forms.IWin32Window;
                bool shouldUpdate = ModsPackUpdateDialog.ShowUpdateDialog(owner);

                if (shouldUpdate)
                {
                    _logger.Log("[ModsPack] User accepted update — starting install...");
                    await InstallAsync();
                }
                else
                {
                    _logger.Log("[ModsPack] User deferred update.");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Log($"[ModsPack] Startup update check failed: {ex.Message}");
            }
        }

        #endregion

        #region Detection Commands

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
                    SyncTargetPath(detectedPath);
                    _config.SetLastTargetPath(_targetPath);
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    await ShowInstallDialogIfNeededAsync();
                    _view.EnableAllButtons();
                    _view.ShowPathFoundBanner(detectedPath);
                    _ = CheckModsPackUpdateOnStartupAsync();
                }
                else
                {
                    _logger.LogLocalized("warning", LogSegment.T("log.autoDetect.notFound"));
                    _view.EnableDetectionButtonsOnly();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Auto-detect failed: {ex.Message}");
                _logger.LogLocalized("warning", LogSegment.T("log.autoDetect.notFound"));
                _view.EnableDetectionButtonsOnly();
            }
        }

        public async Task ManualDetectAsync()
        {
            _view.DisableAllButtons();

            try
            {
                var selectedPath = _detection.ManualDetect(_targetPath);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _targetPath = selectedPath;
                    _view.TargetPath = selectedPath;
                    SyncTargetPath(selectedPath);
                    _config.SetLastTargetPath(_targetPath);
                    _logger.Log($"Dota 2 path set: {_targetPath}");
                    await CheckModsStatusAsync();
                    await StartPatchWatcherAsync(_targetPath);
                    await ShowInstallDialogIfNeededAsync();
                    _view.EnableAllButtons();
                    _view.ShowPathFoundBanner(selectedPath);
                    _ = CheckModsPackUpdateOnStartupAsync();
                }
                else
                {
                    RestoreButtonsForCurrentPath();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Manual detect failed: {ex.Message}");
                RestoreButtonsForCurrentPath();
            }
        }

        public async Task<(string? Path, bool Changed)> ChangeTargetPathAsync()
        {
            var before = _targetPath;
            var selectedPath = _detection.ManualDetect(_targetPath);
            if (string.IsNullOrEmpty(selectedPath))
                return (null, false);

            _targetPath = selectedPath;
            _view.TargetPath = selectedPath;
            SyncTargetPath(selectedPath);
            _config.SetLastTargetPath(_targetPath);
            _logger.Log($"Dota 2 path changed: {_targetPath}");
            await CheckModsStatusAsync();
            await StartPatchWatcherAsync(_targetPath);
            _view.EnableAllButtons();

            bool changed = !string.Equals(before, selectedPath, StringComparison.OrdinalIgnoreCase);
            if (changed)
                _view.ShowPathFoundBanner(selectedPath);
            return (selectedPath, changed);
        }

        private void RestoreButtonsForCurrentPath()
        {
            if (!string.IsNullOrEmpty(_targetPath))
                _view.EnableAllButtons();
            else
                _view.EnableDetectionButtonsOnly();
        }

        #endregion

        #region Install Commands

        public async Task<bool> InstallAsync()
        {
            if (_commandInFlight)
            {
                _logger.Log("An operation is already in progress — ignoring duplicate request.");
                return false;
            }

            _commandInFlight = true;
            try
            {
                return await RunInstallCommandAsync();
            }
            finally
            {
                _commandInFlight = false;
            }
        }

        private async Task<bool> RunInstallCommandAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.ShowMessageBox(Loc.T("error.pathFirst"), Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var installAccess = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.InstallModsPackFeature);
            if (!installAccess.IsAllowed)
            {
                await UIHelpers.ShowFeatureUnavailableAsync(
                    _view, installAccess.FeatureDisplayName, installAccess.BlockedMessage!, _logger.Log);
                return false;
            }
            if (installAccess.IsDevModeBypass)
            {
                _logger.Log($"[DEV] Bypassed feature gate: {installAccess.FeatureDisplayName}");
            }

            var method = _view.ShowInstallMethodDialog();
            if (method == null)
                return false;

            if (method == false)
            {
                return await HandleManualInstallAsync();
            }

            
            try
            {
                if (await _modInstaller.CheckForNewerModsPackAsync(_targetPath))
                {
                    var result = _view.ShowMessageBox(
                        Loc.T("update.newerAvailable.body"),
                        Loc.T("update.available.title"),
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
            }

            return await RunAutoInstallAsync();
        }

        private async Task<bool> RunAutoInstallAsync()
        {
            StartOperation();
            var appPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

            bool success = false;
            bool isUpToDate = false;
            bool canceled = false;

            try
            {
                var result = await _view.RunWithProgressOverlayAsync(Loc.T("mods.progress.initializing"), async (ctx) =>
                {
                    var (opSuccess, opIsUpToDate) = await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, ctx.Progress, ctx.Token, force: false, speedProgress: ctx.Speed,
                        statusCallback: s => ctx.Status.Report(s)
                    ).ConfigureAwait(false);

                    success = opSuccess;
                    isUpToDate = opIsUpToDate;

                    if (!opSuccess && ctx.Token.IsCancellationRequested)
                        return OperationResult.Canceled();

                    return opSuccess
                        ? OperationResult.Ok()
                        : OperationResult.Fail("Installation failed");
                }, showPreview: true);

                canceled = result.WasCanceled;

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

            if (canceled)
            {
                ShowInstallCanceledToast();
                return false;
            }

            await HandleInstallResultAsync(success, isUpToDate, appPath);
            return success;
        }

        private async Task HandleInstallResultAsync(bool success, bool isUpToDate, string appPath)
        {
            if (isUpToDate)
            {
                var result = _view.ShowMessageBox(
                    Loc.T("mods.upToDate.body"),
                    Loc.T("mods.upToDate.title"),
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
                _view.ShowInstallCompleteCard(
                    Loc.T("mods.install.completeTitle"),
                    Loc.T("mods.install.successBody"));

                await ShowPatchRequiredIfNeededAsync();
            }
            else
            {
                _view.ShowInstallFailureCard(
                    Loc.T("mods.install.failedTitle"),
                    Loc.T("mods.install.failedBody", new { title = Loc.T("mods.install.failedTitle") }));
            }
        }

        private void ShowInstallCanceledToast()
        {
            _logger.Log("Installation canceled by user.");
            _view.ShowShellToast(
                Loc.T("mods.toast.canceled.title"),
                Loc.T("mods.toast.canceled.body"),
                "info");
        }

        private async Task ReinstallAsync(string appPath)
        {
            StartOperation();
            
            try
            {
                var result = await _view.RunWithProgressOverlayAsync(Loc.T("mods.progress.reinstalling"), async (ctx) =>
                {
                    var (opSuccess, _) = await _modInstaller.InstallModsAsync(
                        _targetPath!, appPath, ctx.Progress, ctx.Token, force: true, speedProgress: ctx.Speed,
                        statusCallback: s => ctx.Status.Report(s)
                    ).ConfigureAwait(false);

                    if (!opSuccess && ctx.Token.IsCancellationRequested)
                        return OperationResult.Canceled();

                    return opSuccess
                        ? OperationResult.Ok()
                        : OperationResult.Fail("Reinstallation failed");
                }, showPreview: true);

                if (result.WasCanceled)
                {
                    ShowInstallCanceledToast();
                }
                else if (result.Success)
                {
                    await CheckModsStatusAsync();
                    _view.ShowInstallCompleteCard(
                        Loc.T("mods.reinstall.completeTitle"),
                        Loc.T("mods.reinstall.successBody"));
                }
                else
                {
                    _view.ShowInstallFailureCard(
                        Loc.T("mods.reinstall.failedTitle"),
                        Loc.T("mods.install.failedBody", new { title = Loc.T("mods.reinstall.failedTitle") }));
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Reinstallation error: {ex.Message}");
                _view.ShowInstallFailureCard(
                    Loc.T("mods.reinstall.errorTitle"),
                    Loc.T("mods.error.generic", new { error = ex.Message }));
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task<bool> HandleManualInstallAsync()
        {
            var vpkPath = _view.PendingManualVpkPath
                ?? _view.ShowFileDialog(Loc.T("mods.fileDialog.vpkTitle"), "VPK File (*.vpk)|*.vpk");
            if (string.IsNullOrEmpty(vpkPath))
                return false;

            var fileName = Path.GetFileName(vpkPath);
            if (!fileName.Equals("pak01_dir.vpk", StringComparison.OrdinalIgnoreCase))
            {
                _view.ShowStyledMessage(
                    Loc.T("mods.invalidFile.title"),
                    Loc.T("mods.invalidFile.body"),
                    Forms.StyledMessageType.Warning);
                return false;
            }

            var (origin, needsRebuild) = await _modInstaller.ClassifyVpkAsync(vpkPath);
            if (origin == VpkOrigin.Unreadable)
            {
                _view.ShowStyledMessage(
                    Loc.T("mods.invalidVpk.title"),
                    Loc.T("mods.invalidVpk.body"),
                    Forms.StyledMessageType.Error);
                return false;
            }

            bool confirmResult;
            if (origin == VpkOrigin.Official)
            {
                confirmResult = await _view.ShowShellConfirmAsync(
                    Loc.T("mods.confirmInstall.title"),
                    Loc.T("mods.confirmInstall.heading"),
                    Loc.T("mods.confirmInstall.body", new { file = fileName }),
                    "",
                    Loc.T("mods.confirmInstall.confirm"),
                    Loc.T("common.cancel"));
            }
            else
            {
                confirmResult = await _view.ShowShellConfirmAsync(
                    Loc.T("mods.unofficialVpk.title"),
                    Loc.T("mods.unofficialVpk.heading"),
                    Loc.T("mods.unofficialVpk.body", new { file = fileName }),
                    Loc.T("mods.unofficialVpk.note"),
                    Loc.T("mods.unofficialVpk.confirm"),
                    Loc.T("common.cancel"),
                    accent: "warn");
            }

            if (!confirmResult)
                return false;

            StartOperation();
            bool success = false;

            try
            {
                var result = await _view.RunWithProgressOverlayAsync(Loc.T("mods.install.progress"), async (ctx) =>
                {
                    var opSuccess = await _modInstaller.ManualInstallModsAsync(
                        _targetPath!, vpkPath, ctx.Progress, ctx.Token, speedProgress: ctx.Speed,
                        statusCallback: s => ctx.Status.Report(s),
                        rebuild: needsRebuild
                    ).ConfigureAwait(false);

                    if (opSuccess) success = true;

                    if (!opSuccess && ctx.Token.IsCancellationRequested)
                        return OperationResult.Canceled();

                    return opSuccess
                        ? OperationResult.Ok()
                        : OperationResult.Fail("Installation failed");
                });

                if (result.WasCanceled)
                {
                    ShowInstallCanceledToast();
                    success = false;
                }
                else if (result.Success)
                {
                    await CheckModsStatusAsync();
                    _view.ShowInstallCompleteCard(
                        Loc.T("mods.install.completeTitle"),
                        Loc.T("mods.install.manualCompleteBody"));
                }
                else
                {
                    _logger.Log(result.Message ?? "Installation failed");
                    _view.ShowInstallFailureCard(
                        Loc.T("mods.install.failedTitle"),
                        Loc.T("mods.install.failedBodyConsole"));
                    success = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error installing VPK: {ex.Message}");
                _view.ShowInstallFailureCard(
                    Loc.T("mods.install.errorTitle"),
                    Loc.T("mods.error.generic", new { error = ex.Message }));
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

        public async Task DisableWithOptionsAsync()
        {
            if (_commandInFlight)
            {
                _logger.Log("An operation is already in progress — ignoring duplicate request.");
                return;
            }

            _commandInFlight = true;
            try
            {
                await RunDisableWithOptionsAsync();
            }
            finally
            {
                _commandInFlight = false;
            }
        }

        private async Task RunDisableWithOptionsAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                _logger.Log("No Dota 2 folder selected.");
                return;
            }

            var (shouldProceed, deletePermanently) = _view.ShowDisableOptionsDialog();
            if (!shouldProceed)
                return;

            if (deletePermanently)
            {
                var confirm = await _view.ShowShellConfirmAsync(
                    Loc.T("disable.confirm.eyebrow"),
                    Loc.T("disable.confirm.heading"),
                    Loc.T("disable.confirm.body"),
                    Loc.T("disable.confirm.note"),
                    Loc.T("disable.confirm.delete"),
                    Loc.T("common.cancel"));

                if (!confirm)
                    return;
            }

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                bool success = await _modInstaller.DisableModsAsync(_targetPath, token);

                if (!success)
                {
                    _view.ShowShellToast(Loc.T("disable.toast.failed.title"),
                        Loc.T("disable.toast.failed.body"), "error", 6000);
                }
                else if (deletePermanently)
                {
                    string modsFolder = Path.Combine(_targetPath, "game", "_ArdysaMods");
                    bool folderRemoved = true;
                    if (Directory.Exists(modsFolder))
                    {
                        try
                        {
                            Directory.Delete(modsFolder, true);
                            _logger.LogLocalized("default", LogSegment.T("log.disable.folderDeleted"));
                        }
                        catch (Exception ex)
                        {
                            folderRemoved = false;
                            _logger.Log($"Failed to delete mod folder: {ex.Message}");
                            _view.ShowShellToast(Loc.T("disable.toast.deleteFailed.title"),
                                Loc.T("disable.toast.deleteFailed.body"), "error", 6000);
                        }
                    }

                    ClearTempFolder();

                    if (folderRemoved)
                    {
                        var restart = await _view.ShowShellConfirmAsync(
                            Loc.T("disable.restart.eyebrow"),
                            Loc.T("disable.restart.heading"),
                            Loc.T("disable.restart.body"),
                            "",
                            Loc.T("disable.restart.confirm"),
                            Loc.T("disable.restart.later"));

                        if (restart)
                            _view.RestartApplication();
                    }
                }

                await CheckModsStatusAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Log($"Disable operation failed: {ex.Message}");
                _view.ShowShellToast(Loc.T("disable.toast.failed.title"),
                    Loc.T("disable.toast.failed.body"), "error", 6000);
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

                foreach (var dir in Directory.GetDirectories(tempPath, "Ardysa*"))
                {
                    try 
                    { 
                        Directory.Delete(dir, true);
                        _logger.Log($"Cleaned up: {Path.GetFileName(dir)}");
                    } 
                    catch {  }
                }

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

        #region Update Patcher Command

        public async Task UpdatePatcherAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
                return;

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                _logger.Log("Patching dota.signatures and gameinfo_branchspecific.gi...");

                var result = await _modInstaller.UpdatePatcherAsync(_targetPath, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        _logger.LogLocalized("success", LogSegment.T("log.patch.completed"));
                        _view.ShowShellToast(Loc.T("patch.toast.complete.title"),
                            Loc.T("patch.toast.complete.body"), "success");
                        break;

                    case PatchResult.AlreadyPatched:
                        _logger.LogLocalized("success", LogSegment.T("log.patch.alreadyPatched"));
                        _view.ShowShellToast(Loc.T("patch.toast.upToDate.title"),
                            Loc.T("patch.toast.alreadyPatched.body"), "info");
                        break;

                    case PatchResult.Failed:
                        _logger.LogLocalized("error", LogSegment.T("log.patch.failed"));
                        _view.ShowShellToast(Loc.T("patch.toast.failed.title"),
                            Loc.T("patch.toast.failed.body"), "error");
                        break;

                    case PatchResult.Cancelled:
                        _logger.LogLocalized("warning", LogSegment.T("log.patch.cancelled"));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Patch update error: {ex.Message}");
                _view.ShowShellToast(Loc.T("patch.toast.failed.title"),
                    Loc.T("patch.toast.failedEx", new { error = ex.Message }), "error");
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
                _view.SetModsStatus(false, Loc.T("mods.status.notDetected"));
                return;
            }

            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(_targetPath);

                bool isSameStatus = _currentStatus != null &&
                                   _currentStatus.Status == statusInfo.Status &&
                                   _currentStatus.Version == statusInfo.Version &&
                                   _currentStatus.ErrorMessage == statusInfo.ErrorMessage;

                _currentStatus = statusInfo;

                _view.InvokeOnUIThread(() =>
                {
                    _view.SetModsStatusDetailed(statusInfo);

                    if (!isSameStatus)
                    {
                        _logger.LogLocalized(
                            StatusLogCategory(statusInfo.Status),
                            LogSegment.Text("[STATUS] "),
                            LogSegment.T(statusInfo.StatusTextKey),
                            LogSegment.Text(": "),
                            LogSegment.T(statusInfo.DescriptionKey, statusInfo.DescriptionVars));
                    }
                    
                    if (statusInfo.Status == ModStatus.Error)
                    {
                        _view.EnableDetectionButtonsOnly();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Error checking status: {ex.Message}");
                _view.SetModsStatus(false, Loc.T("status.error.text"));
            }
        }

        private static string StatusLogCategory(ModStatus status) => status switch
        {
            ModStatus.Ready => "success",
            ModStatus.NeedUpdate => "warning",
            ModStatus.Disabled => "warning",
            ModStatus.NotInstalled => "error",
            ModStatus.Error => "error",
            ModStatus.Checking => "progress",
            _ => "default"
        };

        public async Task RefreshStatusAsync()
        {
            await CheckModsStatusAsync();
        }

        private async Task UpdateVersionAsync()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "Unknown";
                
                var versionText = FeatureAccessService.IsDevMode
                    ? Loc.T("app.versionLabelDev", new { version })
                    : Loc.T("app.versionLabel", new { version });

                _view.SetVersion(versionText);
            }
            catch
            {
                _view.SetVersion(Loc.T("app.versionUnknown"));
            }
        }

        private void OnDotaStateChanged(bool isRunning)
        {
            _view.InvokeOnUIThread(() =>
            {
                _view.SetDotaRunningState(isRunning);

                if (isRunning)
                {
                    _view.DisableAllButtons();
                    _logger.LogLocalized("warning", LogSegment.T("log.dota2.running"));
                    _view.ShowNotification(
                        Loc.T("notification.dota2Running.title"),
                        Loc.T("notification.dota2Running.body"),
                        System.Windows.Forms.ToolTipIcon.Info,
                        5000);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_targetPath))
                    {
                        _view.EnableAllButtons();
                        _ = CheckModsStatusAsync();
                    }
                    else
                    {
                        _view.EnableDetectionButtonsOnly();
                    }
                }
            });
        }

        #endregion

        #region Dota Patch Watcher

        public async Task StartPatchWatcherAsync(string dotaPath)
        {
            try
            {
                if (_patchWatcher != null && _patchWatcher.IsWatching)
                {
                    return;
                }

                _patchWatcher?.Dispose();

                _patchWatcher = new DotaPatchWatcherService(_logger);
                _patchWatcher.OnPatchDetected += OnPatchDetected;

                await _patchWatcher.StartWatchingAsync(dotaPath);
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Failed to start: {ex.Message}");
            }
        }

        private void OnPatchDetected(PatchDetectedEventArgs args)
        {
            try
            {
                _logger.Log($"[PatchWatcher] Dota 2 update detected: {args.ChangeSummary}");

                _view.InvokeOnUIThread(() =>
                {
                    ShowPatchDetectedNotification(args);
                    _view.SetPatchDetectedStatus();

                    _ = CheckModsStatusAsync();
                });
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
                
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                _logger.Log($"[PatchWatcher] Notification failed: {ex.Message}");
            }
        }

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

            _operationGate?.TrySetResult(true);

            _operationCts = new CancellationTokenSource();
            _operationGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

            _operationGate?.TrySetResult(true);
            _operationGate = null;

            if (string.IsNullOrEmpty(_targetPath))
                _view.EnableDetectionButtonsOnly();
            else
                _view.EnableAllButtons();
        }

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


        public string? TargetPath => _targetPath;

        public bool IsModFilePresent =>
            !string.IsNullOrEmpty(_targetPath) &&
            File.Exists(Path.Combine(_targetPath, RequiredModFilePath));

        #endregion





        #region Post-Install Dialogs

        public async Task ShowInstallDialogIfNeededAsync()
        {
            if (IsModFilePresent)
                return;

            if (_view.ShowInstallRequiredDialog())
            {
                await InstallAsync();
            }
        }

        public async Task ShowPatchRequiredIfNeededAsync(string? successMessage = null, bool fromDetection = false)
        {
            successMessage ??= Loc.T("mods.install.successBody");
            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(_targetPath);

                if (statusInfo.Status == ModStatus.Ready)
                {
                    _patchDialogDismissedByUser = false;
                    return;
                }

                if (fromDetection && _patchDialogDismissedByUser)
                {
                    return;
                }

                if (statusInfo.Status == ModStatus.NeedUpdate ||
                    statusInfo.Status == ModStatus.Disabled)
                {
                    if (_view.ShowPatchRequiredDialog(successMessage))
                    {
                        _patchDialogDismissedByUser = false;
                        await ExecutePatchAsync();
                    }
                    else
                    {
                        _patchDialogDismissedByUser = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Post-install check failed: {ex.Message}");
            }
        }




        public async Task ExecutePatchAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;
            if (!_modInstaller.IsRequiredModFilePresent(_targetPath))
            {
                _view.ShowShellToast(
                    Loc.T("patch.cannotPatch.title"),
                    Loc.T("patch.vpkNotFound"),
                    "error");
                return;
            }

            var cts = StartOperation();
            var token = cts.Token;

            try
            {
                var result = await _modInstaller.UpdatePatcherAsync(_targetPath, null, token);

                switch (result)
                {
                    case PatchResult.Success:
                        await CheckModsStatusAsync();

                        _view.ShowShellToast(
                            Loc.T("patch.complete.title"),
                            Loc.T("patch.complete.body"),
                            "success");
                        break;

                    case PatchResult.AlreadyPatched:
                        await CheckModsStatusAsync();
                        _view.ShowShellToast(
                            Loc.T("patch.alreadyUpToDate.title"),
                            Loc.T("patch.toast.alreadyPatched.body"),
                            "info");
                        break;

                    case PatchResult.Failed:
                        _view.ShowShellToast(
                            Loc.T("patch.failed.title"),
                            Loc.T("patch.failed.body"),
                            "error");
                        break;

                    case PatchResult.Cancelled:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[PATCH] Error: {ex.Message}");
                _view.ShowShellToast(
                    Loc.T("patch.failed.title"),
                    Loc.T("patch.errorEx", new { error = ex.Message }),
                    "error");
            }
            finally
            {
                EndOperation();
            }
        }

        public void ShowStatusDetails()
        {
            if (_currentStatus == null)
            {
                _view.ShowMessageBox(Loc.T("mods.statusLoading.body"), Loc.T("mods.statusLoading.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_targetPath))
            {
                _view.ShowMessageBox(Loc.T("mods.pathRequired.body"), Loc.T("mods.pathRequired.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _view.ShowStatusDetails(_currentStatus, ExecutePatchAsync);
        }

        #endregion


        public async Task VerifyModFilesAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                 _view.ShowMessageBox(Loc.T("mods.pathRequired.body"), Loc.T("mods.pathRequired.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            _logger.Log("[VERIFY] Opening verification dialog...");

            VerifyFilesDialogWebView.Show(
                _view as System.Windows.Forms.IWin32Window,
                _targetPath,
                _versionService,
                () => _ = ExecutePatchAsync());

            await CheckModsStatusAsync();
        }

        #region UI Commands
        

        public async Task HandlePatcherClickAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;

            _view.ShowCheckingState();
            await CheckModsStatusAsync();

            if (_currentStatus?.Status == ModStatus.NeedUpdate)
            {
                 var result = await _view.ShowShellConfirmAsync(
                    Loc.T("patch.statusPrompt.eyebrow"),
                    Loc.T("patch.statusPrompt.heading", new { status = _currentStatus.StatusText }),
                    Loc.T("patch.statusPrompt.body"),
                    note: "",
                    confirmText: Loc.T("patch.statusPrompt.confirm"),
                    cancelText: Loc.T("common.cancel"));

                if (result)
                {
                    await ExecutePatchAsync();
                }
            }
            else if (_currentStatus?.Status == ModStatus.NotInstalled)
            {
                 _view.ShowMessageBox(
                    Loc.T("patch.notInstalled.body"),
                    Loc.T("patch.notInstalled.title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _view.ShowPatchMenu();
            }
        }

        public async Task OpenMiscellaneousAsync()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;

            var miscAccess = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.MiscellaneousFeature);
            if (!miscAccess.IsAllowed)
            {
                await UIHelpers.ShowFeatureUnavailableAsync(
                    _view, miscAccess.FeatureDisplayName, miscAccess.BlockedMessage!, _logger.Log);
                return;
            }
            if (miscAccess.IsDevModeBypass)
            {
                _logger.Log($"[DEV] Bypassed feature gate: {miscAccess.FeatureDisplayName}");
            }

            var (result, generationResult) = _view.ShowMiscForm(_targetPath);

            if (generationResult != null)
                LogGenerationOutcome(generationResult);

            if (result == DialogResult.OK)
            {
                await CheckModsStatusAsync();
                
                if (generationResult != null && generationResult.Success)
                {
                    await Task.Delay(500);
                    await ShowPatchRequiredIfNeededAsync(Loc.T("nav.install.successMisc"));
                }
            }
        }

        public async Task OpenHeroSelectionAsync()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                return;
            }

            var skinAccess = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.SkinSelectorFeature);
            if (!skinAccess.IsAllowed)
            {
                await UIHelpers.ShowFeatureUnavailableAsync(
                    _view, skinAccess.FeatureDisplayName, skinAccess.BlockedMessage!, _logger.Log);
                return;
            }
            if (skinAccess.IsDevModeBypass)
            {
                _logger.Log($"[DEV] Bypassed feature gate: {skinAccess.FeatureDisplayName}");
            }

            var hasAccess = await CheckHeroesJsonAccessAsync();
            if (!hasAccess)
            {
                _view.ShowMessageBox(
                    Loc.T("nav.connectionError.body"),
                    Loc.T("nav.connectionError.title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _logger.LogLocalized("error", LogSegment.T("log.net.connectionFailed"));
                return;
            }

            var proceed = await _view.ShowShellConfirmAsync(
                Loc.T("nav.beta.eyebrow"),
                Loc.T("nav.beta.heading"),
                Loc.T("nav.beta.body"),
                Loc.T("nav.beta.note"),
                Loc.T("common.continue"),
                Loc.T("common.cancel"),
                countdownSeconds: 5);

            if (!proceed)
            {
                return;
            }

            DialogResult dialogResult;
            ModGenerationResult? generationResult = null;
            
            var (res, gen) = _view.ShowHeroGallery();
            dialogResult = res;
            generationResult = gen;
            
            if (dialogResult == DialogResult.Abort)
            {
                _logger.Log("WebView2 failed to initialize.");
                _view.ShowStyledMessage(
                    "Skin Selector Unavailable",
                    "The Skin Selector requires the Microsoft Edge WebView2 runtime, which could not be initialized.\n\n" +
                    "Please install or repair the WebView2 runtime, then try again.",
                    Forms.StyledMessageType.Warning);
                dialogResult = DialogResult.Cancel;
            }
            
            if (generationResult != null)
                LogGenerationOutcome(generationResult);

            await CheckModsStatusAsync();
            
            if (dialogResult == DialogResult.OK)
            {
                await Task.Delay(500);
                
                if (_currentStatus?.Status == ModStatus.Ready)
                {
                     _view.ShowNotification(Loc.T("common.success"),
                        Loc.T("nav.install.success"),
                        System.Windows.Forms.ToolTipIcon.Info, 3000);
                }
                else
                {
                    await ShowPatchRequiredIfNeededAsync(Loc.T("nav.install.success"));
                }
            }
        }

        private async Task<bool> CheckHeroesJsonAccessAsync()
        {
            var client = ArdysaModsTools.Helpers.HttpClientProvider.Client;
            
            var cdnBases = CdnConfig.GetCdnBaseUrls();
            
            foreach (var baseUrl in cdnBases)
            {
                var url = $"{baseUrl.TrimEnd('/')}/Assets/heroes.json";
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var request = new HttpRequestMessage(HttpMethod.Head, url);
                    using var response = await client.SendAsync(request, cts.Token);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    
                    _logger.Log($"[NET] Server returned {(int)response.StatusCode} for {url}");
                }
                catch (TaskCanceledException)
                {
                    _logger.Log($"[NET] Timeout connecting to: {url}");
                }
                catch (HttpRequestException ex)
                {
                    _logger.Log($"[NET] Connection failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[NET] Error: {ex.GetType().Name} - {ex.Message}");
                }
            }
            
            return false;
        }
        
        private void LogGenerationOutcome(ModGenerationResult result)
        {
            if (result.Success)
            {
                var details = result.Details ?? Loc.T("log.gen.completed");
                _logger.LogLocalized("success", LogSegment.Text("[GEN] "), LogSegment.T("log.gen.success", new { details }));
            }
            else
            {
                var error = result.ErrorMessage ?? Loc.T("common.unknownError");
                _logger.LogLocalized("error", LogSegment.Text("[GEN] "), LogSegment.T("log.gen.failed", new { error }));
            }
        }


        #endregion

        #region Shutdown & Disposal

        public async Task ShutdownAsync()
        {
            _operationCts?.Cancel();

            var gate = _operationGate;
            if (gate != null)
            {
                try
                {
                    await Task.WhenAny(gate.Task, Task.Delay(5000));
                }
                catch {  }
            }

            Dispose();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            StopPatchWatcher();
            _dotaMonitor?.Stop();
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            try { _lifetimeCts.Cancel(); _lifetimeCts.Dispose(); } catch {  }
            _disposed = true;
        }

        #endregion
    }
}

