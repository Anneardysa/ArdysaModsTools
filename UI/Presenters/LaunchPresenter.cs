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
        private readonly ISteamAppStateService _steam;

        private readonly Func<string, bool> _launcher;

        private CancellationTokenSource? _cts;

        private volatile bool _dotaRunning;

        public LaunchPresenter(IMainFormView view, Logger logger,
            IItemsGameMergeService merge, ISteamAppStateService steam,
            Func<string, bool>? launcher = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _merge = merge ?? throw new ArgumentNullException(nameof(merge));
            _steam = steam ?? throw new ArgumentNullException(nameof(steam));
            _launcher = launcher ?? (url => UIHelpers.OpenUrl(url, _logger.Log));
        }

        public bool IsRunning => _cts != null;

        public event Action? PackageRepaired;

        public void NotifyDotaRunning(bool running) => _dotaRunning = running;

        public void Cancel()
        {
            try { _cts?.Cancel(); } catch {  }
        }

        public async Task LaunchAsync(string? targetPath)
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
                await RunAsync(targetPath, cts.Token).ConfigureAwait(false);
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
                if (!await WaitForSteamToSettleAsync(targetPath, cts.Token).ConfigureAwait(false)) return;
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

        private async Task RunAsync(string? targetPath, CancellationToken ct)
        {
            Show("play.panel.checking", "play.panel.checkingDetail", percent: null);
            if (!await WaitForSteamToSettleAsync(targetPath, ct).ConfigureAwait(false))
                return;

            if (!await RepairAsync(targetPath, ct).ConfigureAwait(false))
                return;

            if (!Launch())
                return;

            await WaitForGameAsync(targetPath, ct).ConfigureAwait(false);
        }

        private async Task<bool> WaitForSteamToSettleAsync(string? targetPath, CancellationToken ct)
        {
            var state = _steam.Read(targetPath);
            if (!state.IsUpdatePending) return true;

            while (state.IsUpdatePending)
            {
                ct.ThrowIfCancellationRequested();

                Show("play.panel.steamUpdating", "play.panel.steamUpdatingDetail", state.DownloadPercent);
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
                state = _steam.Read(targetPath);
            }

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

        private bool Launch()
        {
            Show("play.panel.launching", "play.panel.launchingDetail", percent: null, canCancel: true);

            if (_launcher(SteamLaunchUrl)) return true;

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
                    if (!await WaitForSteamToSettleAsync(targetPath, ct).ConfigureAwait(false)) return;
                    if (!await RepairAsync(targetPath, ct).ConfigureAwait(false)) return;
                    if (!Launch()) return;

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
