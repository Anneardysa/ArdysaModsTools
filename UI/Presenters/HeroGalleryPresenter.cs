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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    public sealed class HeroGalleryPresenter
    {
        private readonly IHeroGenerationService _generationService;
        private readonly IConfigService _configService;
        private readonly IAppLogger? _logger;

        private IHeroGalleryView? _view;
        private bool _isGenerating;

        public HeroGalleryPresenter(
            IHeroGenerationService generationService,
            IConfigService configService,
            IAppLogger? logger = null)
        {
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
        }

        public void SetView(IHeroGalleryView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public GenerationPlan BuildPlan(
            IReadOnlyList<HeroModel> heroes,
            IReadOnlyDictionary<string, HeroSelectionState> selections)
        {
            var sets = new List<(HeroModel hero, string setName)>();
            var baseWithoutSet = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (heroes == null || selections == null)
                return new GenerationPlan(sets, baseWithoutSet);

            foreach (var kvp in selections)
            {
                var hero = heroes.FirstOrDefault(h =>
                    string.Equals(h.Id, kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (hero?.Sets == null)
                    continue;

                var state = kvp.Value;
                if (state == null)
                    continue;

                var setKeys = hero.Sets.Keys.ToList();

                if (state.BaseIndex.HasValue && !state.SetIndex.HasValue)
                    baseWithoutSet.Add(hero.DisplayName);

                void AddLayer(int index)
                {
                    if (index < 0 || index >= setKeys.Count)
                        return;

                    var setName = setKeys[index];
                    if (string.IsNullOrEmpty(setName))
                        return;

                    if (seen.Add($"{hero.Id}\u0000{setName}"))
                        sets.Add((hero, setName));
                }

                if (state.SetIndex.HasValue)
                    AddLayer(state.SetIndex.Value);

                foreach (var itemIdx in state.ItemIndices)
                    AddLayer(itemIdx);

                if (state.BaseIndex.HasValue)
                    AddLayer(state.BaseIndex.Value);

                if (state.PrismaticIndex.HasValue && state.BaseIndex.HasValue)
                    AddLayer(state.PrismaticIndex.Value);
            }

            return new GenerationPlan(sets, baseWithoutSet);
        }

        public async Task GenerateAsync(
            IReadOnlyList<HeroModel> heroes,
            IReadOnlyDictionary<string, HeroSelectionState> selections)
        {
            if (_view == null)
                throw new InvalidOperationException("SetView must be called before GenerateAsync.");

            if (_isGenerating)
                return;

            _isGenerating = true;
            try
            {
                var plan = BuildPlan(heroes, selections);

                if (plan.BaseWithoutSetHeroNames.Count > 0)
                {
                    var proceed = await _view.ConfirmBaseNoSetAsync(
                        Loc.T("hero.compatAlert.title"),
                        BuildBaseNoSetMessage(plan.BaseWithoutSetHeroNames));

                    if (!proceed)
                    {
                        await _view.UpdateStatusAsync(Loc.T("hero.status.cancelled"));
                        return;
                    }
                }

                if (!plan.HasSelections)
                {
                    await _view.UpdateStatusAsync(Loc.T("hero.status.selectOne"));
                    return;
                }

                var targetPath = _configService.GetLastTargetPath();
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    _view.ShowWarning(
                        Loc.T("hero.noPath.body"),
                        Loc.T("common.error"));
                    return;
                }

                var previewItems = plan.Sets
                    .Where(x => !x.setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (x.hero, x.setName, ResolveThumbnail(x.hero, x.setName)))
                    .ToList();

                if (previewItems.Count == 0)
                {
                    await _view.ShowAlertAsync(
                        Loc.T("hero.noSelections.title"),
                        Loc.T("hero.noSelections.body"));
                    return;
                }

                if (!_view.ShowGenerationPreview(previewItems))
                    return;

                await _view.SaveSelectionsAsync();

                var startTime = DateTime.Now;
                var heroCount = plan.Sets.Count;

                var operationResult = await _view.RunGenerationWithProgressAsync(
                    Loc.TPlural("hero.preparing", heroCount),
                    async context =>
                    {
                        var stageProgress = new Progress<(int percent, string stage)>(p =>
                        {
                            context.Status.Report(p.stage);
                            context.Progress.Report(p.percent);
                        });

                        return await _generationService.GenerateBatchAsync(
                            targetPath,
                            plan.Sets,
                            s => context.Substatus.Report(s),
                            null,
                            stageProgress,
                            context.Speed,
                            context.Token);
                    });

                await HandleResultAsync(operationResult, heroCount, startTime);
            }
            catch (Exception ex)
            {
                _logger?.LogError("HeroGallery generation failed", ex);
                _view.ShowErrorDialog(
                    Loc.T("hero.unexpected.title"),
                    Loc.T("hero.unexpected.body"),
                    $"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
            finally
            {
                _isGenerating = false;
                await _view.UpdateStatusAsync(Loc.T("hero.status.ready"));
            }
        }

        private async Task HandleResultAsync(OperationResult operationResult, int heroCount, DateTime startTime)
        {
            if (_view == null)
                return;

            var hasFailures = operationResult.FailedItems is { Count: > 0 };
            var hasWarnings = operationResult.Warnings is { Count: > 0 };

            if (operationResult.Success)
            {
                var message = new StringBuilder();
                message.AppendLine(operationResult.Message ?? Loc.T("hero.installComplete"));

                if (hasFailures)
                {
                    message.AppendLine();
                    message.AppendLine(Loc.T("hero.failedSets"));
                    foreach (var (name, reason) in operationResult.FailedItems!)
                        message.AppendLine($"  • {name}: {reason}");
                }

                if (hasWarnings)
                {
                    message.AppendLine();
                    message.AppendLine(Loc.T("hero.skippedSets"));
                    foreach (var warning in operationResult.Warnings!)
                        message.AppendLine($"  • {warning}");
                }

                string? logText = operationResult.LogLines is { Count: > 0 }
                    ? string.Join(Environment.NewLine, operationResult.LogLines)
                    : null;

                await _view.ShowGenerationAlertAsync(
                    Loc.T("hero.complete.title"),
                    message.ToString().TrimEnd(),
                    hasFailures || hasWarnings,
                    logText);

                _view.StoreResult(new ModGenerationResult
                {
                    Success = true,
                    Type = GenerationType.SkinSelector,
                    OptionsCount = heroCount,
                    Duration = DateTime.Now - startTime,
                    Details = $"{heroCount} hero set(s)" +
                              (hasFailures ? $", {operationResult.FailedItems!.Count} failed" : string.Empty)
                });

                _view.CloseWithSuccess();
                return;
            }

            if (operationResult.WasCanceled)
                return;

            _view.StoreResult(new ModGenerationResult
            {
                Success = false,
                Type = GenerationType.SkinSelector,
                OptionsCount = heroCount,
                Duration = DateTime.Now - startTime,
                ErrorMessage = operationResult.Message
            });

            var failDetails = new StringBuilder();
            failDetails.AppendLine(operationResult.Message ?? Loc.T("common.unknownError"));
            if (hasWarnings)
            {
                failDetails.AppendLine();
                failDetails.AppendLine(Loc.T("hero.skippedSets"));
                foreach (var warning in operationResult.Warnings!)
                    failDetails.AppendLine($"  • {warning}");
            }

            if (operationResult.LogLines is { Count: > 0 })
            {
                failDetails.AppendLine();
                failDetails.AppendLine("--- Generation log ---");
                foreach (var line in operationResult.LogLines)
                    failDetails.AppendLine(line);
            }

            _view.ShowErrorDialog(
                Loc.T("hero.failed.title"),
                Loc.T("hero.failed.body"),
                failDetails.ToString().TrimEnd());
        }

        private static string BuildBaseNoSetMessage(IReadOnlyList<string> heroNames)
        {
            var names = string.Join(", ", heroNames);
            return Loc.T("hero.baseNoSet.message", new { names });
        }

        private static string? ResolveThumbnail(HeroModel hero, string setName)
        {
            if (hero.Sets != null && hero.Sets.TryGetValue(setName, out var urls) && urls != null)
            {
                return urls.FirstOrDefault(u =>
                    u != null &&
                    (u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)));
            }

            return null;
        }
    }

    public sealed class GenerationPlan
    {
        public GenerationPlan(
            IReadOnlyList<(HeroModel hero, string setName)> sets,
            IReadOnlyList<string> baseWithoutSetHeroNames)
        {
            Sets = sets;
            BaseWithoutSetHeroNames = baseWithoutSetHeroNames;
        }

        public IReadOnlyList<(HeroModel hero, string setName)> Sets { get; }

        public IReadOnlyList<string> BaseWithoutSetHeroNames { get; }

        public bool HasSelections => Sets.Count > 0;
    }
}
