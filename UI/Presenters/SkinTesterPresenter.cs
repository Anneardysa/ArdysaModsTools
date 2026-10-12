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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.UI.Presenters
{
    public interface ISkinTesterView
    {
        IReadOnlyList<string> ZipPaths { get; }
        string HeroesJsonPath { get; }
        bool LiveSandbox { get; }
        void ShowTarget(string? targetPath);
        void AppendLog(string line);
        void SetBusy(bool busy);
    }

    public sealed class SkinTesterPresenter
    {
        private readonly ISkinTesterView _view;
        private readonly IConfigService _config;
        private readonly Func<SkinTesterPlan, IAppLogger, IHeroGenerationService> _generation;
        private CancellationTokenSource? _cts;

        public SkinTesterPresenter(ISkinTesterView view, IConfigService config,
            Func<SkinTesterPlan, IAppLogger, IHeroGenerationService>? generation = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _generation = generation ?? ((plan, logger) => plan.CreateGenerationService(logger));
            _view.ShowTarget(_config.GetLastTargetPath());
        }

        public void Cancel() => _cts?.Cancel();

        public async void OnBuildRequested()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _view.SetBusy(true);
            try
            {
                await BuildAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                _view.AppendLog("Cancelled.");
            }
            catch (Exception ex)
            {
                _view.AppendLog($"FAILED: {ex.Message}");
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _view.SetBusy(false);
            }
        }

        internal async Task BuildAsync(CancellationToken ct)
        {
            string? target = _config.GetLastTargetPath();
            if (string.IsNullOrWhiteSpace(target))
            {
                _view.AppendLog("No Dota 2 folder - run Detect in the app first.");
                return;
            }
            if (_view.ZipPaths.Count == 0)
            {
                _view.AppendLog("Add at least one .zip.");
                return;
            }
            var plan = await SkinTesterPlan.FromFilesAsync(_view.ZipPaths, _view.HeroesJsonPath, ct);
            foreach (var error in plan.Errors)
                _view.AppendLog($"ERROR {error}");
            if (plan.Errors.Count > 0 || plan.Selections.Count == 0)
            {
                _view.AppendLog("Nothing built - fix the errors above.");
                return;
            }
            foreach (var group in plan.Selections.GroupBy(s => s.Hero.Id))
                _view.AppendLog($"{group.Key}: {string.Join(", ", group.Select(s => s.SetName))}");

            string installTo = target;
            IReadOnlyDictionary<string, (long Length, DateTime WriteUtc)>? untouched = null;
            if (_view.LiveSandbox)
            {
                installTo = SkinTesterSandbox.Root(target);
                string? refused = SkinTesterSandbox.Prepare(target, installTo);
                if (refused != null)
                {
                    _view.AppendLog($"FAILED - live sandbox: {refused}");
                    return;
                }
                untouched = SkinTesterSandbox.Snapshot(target);
                _view.AppendLog($"Live sandbox: {installTo} - the Dota install is not touched.");
            }

            var service = _generation(plan, new ViewLogger(_view));
            var result = await service.GenerateBatchAsync(installTo, plan.Selections, _view.AppendLog, ct: ct);
            if (untouched != null && SkinTesterSandbox.VerifyUntouched(untouched) is { } changed)
                _view.AppendLog($"WARNING - files of the real Dota install changed during a sandbox build: {changed}");
            _view.AppendLog(!result.Success
                ? $"FAILED - {result.Message}"
                : untouched != null
                    ? $"OK - {result.Message}. Sandbox ready - push it from live.html (Push Skin Tester build)."
                    : $"OK - {result.Message}. Installed to {Path.Combine(target, "game", "_ArdysaMods", "pak01_dir.vpk")}");
        }

        private sealed class ViewLogger : IAppLogger
        {
            private readonly ISkinTesterView _view;
            public ViewLogger(ISkinTesterView view) => _view = view;
            public void Log(string message, LogLevel level = LogLevel.Info) => _view.AppendLog($"[{level}] {message}");
            public void LogError(string message, Exception? ex = null) => _view.AppendLog($"[Error] {message} {ex?.Message}");
            public void LogWarning(string message) => _view.AppendLog($"[Warning] {message}");
            public void LogDebug(string message) => _view.AppendLog($"[Debug] {message}");
            public void FlushBufferedLogs() { }
        }
    }
}
