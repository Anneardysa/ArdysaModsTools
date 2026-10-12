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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    public sealed class UtilitiesPresenter
    {
        private static readonly JsonSerializerOptions BridgeJson = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };

        private readonly IUtilitiesView _view;
        private readonly IRankChangerService _rankChanger;
        private readonly IHeroBadgeService _heroBadge;
        private readonly IAppLogger _logger;
        private readonly string? _gamePath;

        private int _packageBusy;

        public UtilitiesPresenter(
            IUtilitiesView view,
            IRankChangerService rankChanger,
            IHeroBadgeService heroBadge,
            IAppLogger logger,
            string? gamePath = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _rankChanger = rankChanger ?? throw new ArgumentNullException(nameof(rankChanger));
            _heroBadge = heroBadge ?? throw new ArgumentNullException(nameof(heroBadge));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gamePath = gamePath;

            _view.OnViewShown += View_OnViewShown;
            _view.OnRankApplyRequested += View_OnRankApplyRequested;
            _view.OnRankResetRequested += View_OnRankResetRequested;
            _view.OnBadgeApplyRequested += View_OnBadgeApplyRequested;
            _view.OnBadgeResetRequested += View_OnBadgeResetRequested;
        }

        private async void View_OnViewShown(object? sender, EventArgs e) => await PushStateAsync();

        private async void View_OnRankApplyRequested(object? sender, string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson)) return;

            RankChangerRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<RankChangerRequest>(requestJson, BridgeJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"[UtilitiesPresenter] Malformed rank request: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("rank.error.invalidSelection"), "error");
                return;
            }

            if (request is null)
            {
                await _view.ShowToastAsync(Loc.T("rank.error.invalidSelection"), "error");
                return;
            }

            await RunAsync(
                progress => _rankChanger.ApplyAsync(_gamePath!, request, progress),
                Loc.T("rank.toast.applied"),
                "rank apply");
        }

        private async void View_OnRankResetRequested(object? sender, EventArgs e)
            => await RunAsync(progress => _rankChanger.ResetAsync(_gamePath!, progress), Loc.T("rank.toast.reset"), "rank reset");

        private async void View_OnBadgeApplyRequested(object? sender, string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson)) return;

            HeroBadgeRequest? request = null;
            try
            {
                request = JsonSerializer.Deserialize<HeroBadgeRequest>(requestJson, BridgeJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"[UtilitiesPresenter] Malformed hero badge request: {ex.Message}", ex);
            }

            if (request is null)
            {
                await _view.ShowToastAsync(Loc.T("badge.error.invalidSelection"), "error");
                return;
            }

            await RunAsync(
                progress => _heroBadge.ApplyAsync(_gamePath!, request, progress),
                Loc.T("badge.toast.applied"),
                "badge apply");
        }

        private async void View_OnBadgeResetRequested(object? sender, EventArgs e)
            => await RunAsync(progress => _heroBadge.ResetAsync(_gamePath!, progress), Loc.T("badge.toast.reset"), "badge reset");

        private async Task RunAsync(Func<IProgress<RankChangerProgress>, Task<OperationResult>> action, string successMessage, string what)
        {
            if (string.IsNullOrEmpty(_gamePath))
            {
                await _view.ShowToastAsync(Loc.T("rank.error.noPath"), "error");
                return;
            }

            if (!PathUtility.IsModInstalled(_gamePath))
            {
                await _view.ShowToastAsync(Loc.T("rank.error.notInstalled"), "error");
                return;
            }

            if (Interlocked.CompareExchange(ref _packageBusy, 1, 0) != 0)
                return;

            await _view.SetBusyAsync(true);
            try
            {
                var progress = new Progress<RankChangerProgress>(p =>
                    _ = _view.ShowProgressAsync(p.Stage.ToString().ToLowerInvariant(), p.Percent));
                var result = await action(progress).ConfigureAwait(true);
                await _view.ShowToastAsync(
                    result.Success ? successMessage : result.Message ?? Loc.T("utilities.error.runFailed", new { error = what }),
                    result.Success ? "success" : "error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UtilitiesPresenter] {what} failed: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("utilities.error.runFailed", new { error = ex.Message }), "error");
            }
            finally
            {
                Interlocked.Exchange(ref _packageBusy, 0);
                await _view.SetBusyAsync(false);
                await PushStateAsync();
            }
        }

        private async Task PushStateAsync()
        {
            try
            {
                bool isModInstalled = PathUtility.IsModInstalled(_gamePath);
                await _view.LoadStateAsync(JsonSerializer.Serialize(
                    new
                    {
                        rank = _rankChanger.LoadState(_gamePath),
                        badge = _heroBadge.LoadState(_gamePath),
                        isModInstalled
                    }, BridgeJson));
            }
            catch (Exception ex)
            {
                _logger.LogError($"[UtilitiesPresenter] Failed to push state: {ex.Message}", ex);
            }
        }
    }
}
