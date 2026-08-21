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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    public sealed class LaunchPresenter : IDisposable
    {
        private const string SteamLaunchUrl = "steam://rungameid/570";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan LaunchTimeout = TimeSpan.FromMinutes(5);

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly IItemsGameMergeService _merge;
        private readonly IModInstallerService _modInstaller;
        private readonly ISteamAppStateService _steam;

        private readonly Func<string, bool> _launcher;

        private CancellationTokenSource? _cts;

        private volatile bool _dotaRunning;

        private volatile TaskCompletionSource<bool>? _confirmWaiter;

        public LaunchPresenter(IMainFormView view, Logger logger,
            IItemsGameMergeService merge, IModInstallerService modInstaller, ISteamAppStateService steam,
            Func<string, bool>? launcher = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _merge = merge ?? throw new ArgumentNullException(nameof(merge));
            _modInstaller = modInstaller ?? throw new ArgumentNullException(nameof(modInstaller));
            _steam = steam ?? throw new ArgumentNullException(nameof(steam));
            _launcher = launcher ?? (url => UIHelpers.OpenUrl(url, _logger.Log));
        }

        public bool IsRunning => _cts != null;

        public event Action? PackageRepaired;

        public void NotifyDotaRunning(bool running)
        {
            _dotaRunning = running;

            if (running) _confirmWaiter?.TrySetResult(false);
        }

        public void ConfirmLaunch() => _confirmWaiter?.TrySetResult(true);

        public void Cancel()
        {
            try { _cts?.Cancel(); } catch {  }
        }

        public async Task LaunchAsync(string? targetPath, bool needsPatch = false)
        {
            if (IsRunning) return;

            if (_dotaRunning)
            {
                _view.ShowShellToast(Loc.T("play.button"), Loc.T("play.alreadyRunning"), "info");
                return;
            }

            using var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                await RunAsync(targetPath, needsPatch, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _view.SetLaunchPanel(null);
            }
            catch (Exception ex)
            {
                _logger.Log($"[PLAY] {ex.Message}");
                ShowError("play.panel.failed", "play.merge.failed");
            }
            finally
            {
                _cts = null;
            }
        }

        public async Task RepairOnlyAsync(string? targetPath)
        {
            if (IsRunning) return;

            if (_dotaRunning)
            {
                _view.ShowShellToast(Loc.T("verify.chip.sync"), Loc.T("play.alreadyRunning"), "info");
                return;
            }

            using var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                Show("play.panel.checking", "play.panel.checkingDetail", percent: null);
                await WaitForSteamUpdateAsync(targetPath, cts.Token).ConfigureAwait(false);

                if (GameStartedWithoutUs(Loc.T("verify.chip.sync"))) return;
                if (!await RepairAsync(targetPath, cts.Token).ConfigureAwait(false)) return;

                _view.SetLaunchPanel(null);
                _view.ShowShellToast(Loc.T("verify.chip.sync"), Loc.T("play.sync.repaired"), "success");
            }
            catch (OperationCanceledException)
            {
                _view.SetLaunchPanel(null);
            }
            catch (Exception ex)
            {
                _logger.Log($"[PLAY] {ex.Message}");
                ShowError("play.panel.failed", "play.merge.failed");
            }
            finally
            {
                _cts = null;
            }
        }

        private async Task RunAsync(string? targetPath, bool needsPatch, CancellationToken ct)
        {
            Show("play.panel.checking", "play.panel.checkingDetail", percent: null);
            bool updated = await WaitForSteamUpdateAsync(targetPath, ct).ConfigureAwait(false);

            if (GameStartedWithoutUs(Loc.T("play.button")))
                return;

            if (needsPatch && !await PatchAsync(targetPath, ct).ConfigureAwait(false))
                return;

            if (!await RepairAsync(targetPath, ct).ConfigureAwait(false))
                return;

            if (updated && !await WaitForLaunchConfirmationAsync(ct).ConfigureAwait(false))
            {
                _view.SetLaunchPanel(null);
                return;
            }

            if (!Launch(targetPath))
                return;

            await WaitForGameAsync(targetPath, ct).ConfigureAwait(false);
        }

        private async Task<bool> WaitForSteamUpdateAsync(string? targetPath, CancellationToken ct)
        {
            var state = _steam.Read(targetPath);
            if (!state.IsUpdatePending) return false;

            while (state.IsUpdatePending && !_dotaRunning)
            {
                ct.ThrowIfCancellationRequested();

                Show("play.panel.steamUpdating", "play.panel.steamUpdatingDetail", state.DownloadPercent);
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
                state = _steam.Read(targetPath);
            }

            return true;
        }

        private bool GameStartedWithoutUs(string title)
        {
            if (!_dotaRunning) return false;

            _view.SetLaunchPanel(null);
            _view.ShowShellToast(title, Loc.T("play.alreadyRunning"), "info");
            return true;
        }

        private async Task<bool> WaitForLaunchConfirmationAsync(CancellationToken ct)
        {
            if (_dotaRunning) return false;

            var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _confirmWaiter = waiter;

            if (_dotaRunning) waiter.TrySetResult(false);

            try
            {
                _view.SetLaunchPanel(new LaunchPanelState
                {
                    HeadingKey = "play.panel.updated",
                    DetailKey = "play.panel.updatedDetail",
                    ConfirmKey = "play.panel.launchNow",
                    CanCancel = true
                });

                using (ct.Register(() => waiter.TrySetCanceled(ct)))
                    return await waiter.Task.ConfigureAwait(false);
            }
            finally
            {
                _confirmWaiter = null;
            }
        }

        private async Task<bool> PatchAsync(string? targetPath, CancellationToken ct)
        {
            Show("play.panel.patching", "play.panel.patchingDetail", percent: null, canCancel: true);

            var result = await _modInstaller.UpdatePatcherAsync(targetPath!, null, ct).ConfigureAwait(false);

            if (result == PatchResult.Cancelled)
                throw new OperationCanceledException(ct);

            if (result == PatchResult.Failed)
            {
                _logger.Log(Loc.T("patch.failed.body"));
                ShowError("play.panel.failed", "patch.failed.body");
                return false;
            }

            if (result == PatchResult.Success)
                _logger.Log(Loc.T("patch.complete.body"));

            return true;
        }

        private async Task<bool> RepairAsync(string? targetPath, CancellationToken ct)
        {
            var statusKey = "play.merge.reading";
            var status = new Progress<string>(key => statusKey = key);
            var percent = new Progress<int>(p => Show("play.panel.merging", statusKey, p, canCancel: true));

            Show("play.panel.merging", statusKey, 0);

            var result = await _merge.MergeAsync(targetPath, status, percent, ct).ConfigureAwait(false);

            if (result.Outcome == ItemsGameMergeOutcome.Failed)
            {
                _logger.Log(Loc.T(result.FailureKey ?? "play.merge.failed"));
                if (!string.IsNullOrWhiteSpace(result.Diagnostic))
                    _logger.LogDebug($"[PLAY] {result.Diagnostic}");

                ShowError("play.panel.failed", result.FailureKey ?? "play.merge.failed");
                return false;
            }

            if (result.Outcome == ItemsGameMergeOutcome.Merged)
            {
                _logger.Log(Loc.T("play.merge.done"));
                PackageRepaired?.Invoke();
            }

            return true;
        }

        private bool Launch(string? targetPath)
        {
            Show("play.panel.launching", "play.panel.launchingDetail", percent: null, canCancel: true);

            ActiveProcessSentry.KillRunningThreats(_logger);

            ProcessProtectionGuard.ProtectCurrentProcess(_logger);

            if (!string.IsNullOrEmpty(targetPath))
            {
                ProtectedVpkStore.MountSession(targetPath, _logger);
            }

            if (_launcher(SteamLaunchUrl)) return true;

            ProcessProtectionGuard.UnprotectCurrentProcess(_logger);

            if (!string.IsNullOrEmpty(targetPath))
            {
                ProtectedVpkStore.UnmountSession(targetPath, _logger);
            }

            ShowError("play.panel.failed", "play.panel.steamFailed");
            return false;
        }

        private async Task WaitForGameAsync(string? targetPath, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + LaunchTimeout;

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (_dotaRunning)
                {
                    Show("play.panel.running", "play.panel.runningDetail", percent: null, canCancel: false);
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                    _view.SetLaunchPanel(null);
                    return;
                }

                if (_steam.Read(targetPath).IsUpdatePending)
                {
                    await WaitForSteamUpdateAsync(targetPath, ct).ConfigureAwait(false);

                    if (_dotaRunning) continue;

                    if (!await PatchAsync(targetPath, ct).ConfigureAwait(false)) return;
                    if (!await RepairAsync(targetPath, ct).ConfigureAwait(false)) return;

                    if (!await WaitForLaunchConfirmationAsync(ct).ConfigureAwait(false))
                    {
                        _view.SetLaunchPanel(null);
                        return;
                    }

                    if (!Launch(targetPath)) return;

                    deadline = DateTime.UtcNow + LaunchTimeout;
                    continue;
                }

                Show("play.panel.waiting", "play.panel.waitingDetail", percent: null, canCancel: true);
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }

            ShowError("play.panel.timeout", "play.panel.timeoutDetail");
        }

        private void Show(string headingKey, string detailKey, int? percent, bool canCancel = true) =>
            _view.SetLaunchPanel(new LaunchPanelState
            {
                HeadingKey = headingKey,
                DetailKey = detailKey,
                Percent = percent,
                CanCancel = canCancel
            });

        private void ShowError(string headingKey, string detailKey) =>
            _view.SetLaunchPanel(new LaunchPanelState
            {
                HeadingKey = headingKey,
                DetailKey = detailKey,
                CanCancel = true,
                IsError = true
            });

        public void Dispose() => Cancel();
    }
}
