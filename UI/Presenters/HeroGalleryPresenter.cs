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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
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
        private readonly Func<Task<FeatureCheckResult>> _featureCheck;
        private readonly ISkinSelectorCooldownService _cooldownService;
        private readonly ISkinManifestReader _manifestReader;
        private readonly Func<SkinTesterPlan, IAppLogger, IHeroGenerationService> _debugGeneration;

        private IHeroGalleryView? _view;
        private bool _isGenerating;

        public HeroGalleryPresenter(
            IHeroGenerationService generationService,
            IConfigService configService,
            IAppLogger? logger = null,
            Func<Task<FeatureCheckResult>>? featureCheck = null,
            ISkinSelectorCooldownService? cooldownService = null,
            ISkinManifestReader? manifestReader = null,
            Func<SkinTesterPlan, IAppLogger, IHeroGenerationService>? debugGeneration = null)
        {
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            _featureCheck = featureCheck
                ?? (() => FeatureAccessService.CheckFeatureAsync(FeatureAccessService.SkinSelectorFeature));
            _cooldownService = cooldownService ?? new ArdysaModsTools.Core.Services.Hero.SkinSelectorCooldownService(configService);
            _manifestReader = manifestReader ?? new ArdysaModsTools.Core.Services.Hero.SkinManifestReader(logger);
            _debugGeneration = debugGeneration ?? ((plan, trace) => plan.CreateGenerationService(trace));
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
            var particles = new Dictionary<string, IReadOnlyList<ParticlePick>>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (heroes == null || selections == null)
                return new GenerationPlan(sets, baseWithoutSet, particles);

            foreach (var kvp in selections)
            {
                var hero = heroes.FirstOrDefault(h =>
                    string.Equals(h.Id, kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (hero?.Sets == null)
                    continue;

                var state = kvp.Value;
                if (state == null)
                    continue;

                var orderedSets = hero.Sets.ToList();
                var setKeys = orderedSets.Select(kv => kv.Key).ToList();
                bool InRange(int i) => i >= 0 && i < setKeys.Count;

                if (state.ItemIndices == null || state.EffectIndices == null)
                    state = new HeroSelectionState
                    {
                        SetIndex = state.SetIndex, BaseIndex = state.BaseIndex, PrismaticIndex = state.PrismaticIndex,
                        ItemIndices = state.ItemIndices ?? new List<int>(),
                        EffectIndices = state.EffectIndices ?? new List<int>(),
                        Particles = state.Particles
                    };

                bool persona = state.SetIndex is int si && InRange(si)
                    && HeroModelMapper.ClassifySet(orderedSets[si].Value) == HeroModelMapper.SkinCategory.Persona;
                if (persona && (state.ItemIndices.Count > 0 || state.BaseIndex.HasValue || state.PrismaticIndex.HasValue
                    || state.EffectIndices.Count > 0 || state.Particles is { Count: > 0 }))
                {
                    _logger?.LogDebug($"Skin Selector: {hero.DisplayName} has a Persona — its items, base, prismatic and effects are dropped.");
                    state = new HeroSelectionState { SetIndex = state.SetIndex };
                }

                if (state.BaseIndex.HasValue && !state.SetIndex.HasValue)
                    baseWithoutSet.Add(hero.DisplayName);

                var conflicts = SetCompatibilityResolver.BuildConflictMap(orderedSets, hero.Incompatible);
                var carriers = SetCompatibilityResolver.BuildEffectCarriers(orderedSets, hero.SetNames);
                var kept = new List<int>();

                void AddLayer(int index, bool effectSlot = false)
                {
                    if (!InRange(index))
                        return;

                    var setName = setKeys[index];
                    if (string.IsNullOrEmpty(setName))
                        return;

                    bool isEffect = HeroModelMapper.ClassifySet(orderedSets[index].Value) == HeroModelMapper.SkinCategory.AbilityEffect;
                    if (isEffect != effectSlot)
                    {
                        _logger?.LogDebug($"Skin Selector: dropped '{setName}' for {hero.DisplayName} — wrong slot for its category.");
                        return;
                    }

                    if (kept.Any(k => conflicts[k].Contains(index) || conflicts[index].Contains(k)))
                    {
                        _logger?.LogDebug($"Skin Selector: dropped '{setName}' for {hero.DisplayName} — incompatible with an earlier pick.");
                        return;
                    }

                    if (seen.Add($"{hero.Id}\u0000{setName}"))
                    {
                        sets.Add((hero, setName));
                        kept.Add(index);
                    }
                }

                if (state.SetIndex.HasValue)
                    AddLayer(state.SetIndex.Value);

                foreach (var itemIdx in state.ItemIndices)
                    AddLayer(itemIdx);

                bool baseKept = false;
                if (state.BaseIndex is int baseIdx)
                {
                    AddLayer(baseIdx);
                    baseKept = kept.Contains(baseIdx);

                    var lockedSlots = baseKept ? BaseSlotLock.SlotsFor(hero, setKeys[baseIdx]) : Array.Empty<string>();
                    foreach (var itemIdx in state.ItemIndices.Where(i => InRange(i) && kept.Contains(i)).ToList())
                    {
                        if (!BaseSlotLock.IsLockedItemTag(HeroModelMapper.ExtractItemTag(orderedSets[itemIdx].Value), lockedSlots))
                            continue;
                        _logger?.LogDebug($"Skin Selector: dropped item '{setKeys[itemIdx]}' for {hero.DisplayName} — its slot is locked by the Base.");
                        sets.Remove((hero, setKeys[itemIdx]));
                        kept.Remove(itemIdx);
                    }
                }

                if (state.PrismaticIndex.HasValue && baseKept)
                    AddLayer(state.PrismaticIndex.Value);

                var effectTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var effectIdx in state.EffectIndices)
                {
                    if (!InRange(effectIdx)
                        || HeroModelMapper.ClassifySet(orderedSets[effectIdx].Value) != HeroModelMapper.SkinCategory.AbilityEffect
                        || HeroModelMapper.ExtractItemTag(orderedSets[effectIdx].Value) is not string effectTag)
                    {
                        _logger?.LogDebug($"Skin Selector: dropped effect index {effectIdx} for {hero.DisplayName} — not an Ability Effect.");
                        continue;
                    }
                    if (effectTags.Contains(effectTag))
                    {
                        _logger?.LogDebug($"Skin Selector: dropped effect '{setKeys[effectIdx]}' for {hero.DisplayName} — {effectTag} already has one.");
                        continue;
                    }
                    if (carriers[effectIdx].Any(kept.Contains))
                    {
                        _logger?.LogDebug($"Skin Selector: dropped effect '{setKeys[effectIdx]}' for {hero.DisplayName} — the item or set it was cut from is picked and already carries it.");
                        continue;
                    }
                    AddLayer(effectIdx, effectSlot: true);
                    if (kept.Contains(effectIdx))
                        effectTags.Add(effectTag);
                }

                if (state.Particles is { Count: > 0 })
                {
                    if (kept.Any(k => !setKeys[k].Equals("Default Set", StringComparison.OrdinalIgnoreCase)
                        && IsParticleCarrier(HeroModelMapper.ClassifySet(orderedSets[k].Value))))
                    {
                        var picks = HeroParticleInjector.Sanitize(hero, state.Particles, s => _logger?.LogDebug($"Skin Selector: {s}"));
                        if (picks.Count > 0) particles[hero.Id] = picks;
                    }
                    else
                    {
                        _logger?.LogDebug($"Skin Selector: {hero.DisplayName}'s particles dropped, no set, item or base is picked for them to ride on.");
                    }
                }
            }

            return new GenerationPlan(sets, baseWithoutSet, particles);
        }

        private static bool IsParticleCarrier(HeroModelMapper.SkinCategory category) =>
            category is HeroModelMapper.SkinCategory.LegacySet or HeroModelMapper.SkinCategory.CustomSet
                or HeroModelMapper.SkinCategory.Item or HeroModelMapper.SkinCategory.BaseHero;

        public async Task LoadParticleSlotsAsync(IReadOnlyList<HeroModel> heroes, string heroId, int setIndex, CancellationToken ct = default)
        {
            ParticleSlots? slots = null;
            var hero = heroes?.FirstOrDefault(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase));
            var sets = hero?.Sets?.ToList();
            if (hero != null && sets != null && setIndex >= 0 && setIndex < sets.Count
                && IsParticleCarrier(HeroModelMapper.ClassifySet(sets[setIndex].Value)))
            {
                try
                {
                    slots = await _generationService.GetParticleSlotsAsync(hero, sets[setIndex].Key, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogDebug($"Skin Selector: particle slots for {hero.DisplayName} '{sets[setIndex].Key}' failed: {ex.Message}");
                }
            }
            if (_view != null) await _view.ShowParticleSlotsAsync(heroId, setIndex, slots);
        }

        public async Task DetectInstalledAsync(
            IReadOnlyList<HeroModel> heroes,
            CancellationToken ct = default)
        {
            if (_view == null) return;

            try
            {
                var targetPath = _configService.GetLastTargetPath();
                var manifest = string.IsNullOrWhiteSpace(targetPath)
                    ? null
                    : await _manifestReader.ReadInstalledAsync(targetPath!, ct).ConfigureAwait(false);

                var selections = BuildSelectionsFromManifest(heroes, manifest);

                if (selections.Count == 0)
                {
                    await _view.ShowDetectionAsync(false, 0).ConfigureAwait(false);
                    return;
                }

                await _view.ApplyDetectedSelectionsAsync(selections).ConfigureAwait(false);
                await _view.ShowDetectionAsync(true, selections.Count).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Skin detection failed: {ex.Message}");
                try { await _view.ShowDetectionFailedAsync().ConfigureAwait(false); } catch { }
            }
        }

        internal static void AttachStems(IReadOnlyList<HeroModel>? heroes, IDictionary<string, HeroSelectionState>? selections)
        {
            if (heroes == null || selections == null) return;
            foreach (var (heroId, state) in selections)
            {
                var hero = heroes.FirstOrDefault(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase));
                if (hero?.Sets == null || state == null) continue;
                var ordered = hero.Sets.ToList();
                string? Stem(int? i) => i is int n && n >= 0 && n < ordered.Count
                    ? HeroModelMapper.ExtractArchiveStem(ordered[n].Value) : null;
                state.Stems = new SelectionStems
                {
                    Set = Stem(state.SetIndex),
                    Items = (state.ItemIndices ?? new List<int>()).Select(i => Stem(i)).OfType<string>().ToList(),
                    Base = Stem(state.BaseIndex),
                    Prismatic = Stem(state.PrismaticIndex),
                    Effects = (state.EffectIndices ?? new List<int>()).Select(i => Stem(i)).OfType<string>().ToList(),
                };
            }
        }

        internal static List<string> ResolveStems(IReadOnlyList<HeroModel>? heroes, IDictionary<string, HeroSelectionState>? selections)
        {
            var dropped = new List<string>();
            if (heroes == null || selections == null) return dropped;
            foreach (var (heroId, state) in selections)
            {
                var stems = state?.Stems;
                if (stems == null) continue;
                var hero = heroes.FirstOrDefault(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase));
                if (hero?.Sets == null) continue;

                var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var ordered = hero.Sets.ToList();
                for (var i = 0; i < ordered.Count; i++)
                    if (HeroModelMapper.ExtractArchiveStem(ordered[i].Value) is string s)
                        index.TryAdd(s, i);

                int? Find(string? stem)
                {
                    if (string.IsNullOrWhiteSpace(stem)) return null;
                    if (index.TryGetValue(stem.Trim(), out var i)) return i;
                    dropped.Add($"{hero.DisplayName}: {stem}");
                    return null;
                }

                state!.SetIndex = Find(stems.Set);
                state.ItemIndices = (stems.Items ?? new List<string>()).Select(Find).OfType<int>().ToList();
                state.BaseIndex = Find(stems.Base);
                state.PrismaticIndex = Find(stems.Prismatic);
                state.EffectIndices = (stems.Effects ?? new List<string>()).Select(Find).OfType<int>().ToList();
                state.Stems = null;
            }
            return dropped;
        }

        internal static Dictionary<string, HeroSelectionState> BuildSelectionsFromManifest(
            IReadOnlyList<HeroModel>? heroes,
            SkinManifest? manifest)
        {
            var selections = new Dictionary<string, HeroSelectionState>(StringComparer.OrdinalIgnoreCase);
            if (heroes == null || manifest?.Entries == null)
                return selections;

            foreach (var entry in manifest.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.HeroId) || string.IsNullOrWhiteSpace(entry.SetName))
                    continue;

                var hero = heroes.FirstOrDefault(h =>
                    string.Equals(h.Id, entry.HeroId, StringComparison.OrdinalIgnoreCase));
                if (hero?.Sets == null)
                    continue;

                string layer = entry.Layer?.ToLowerInvariant() ?? "set";
                if (layer == "particle")
                {
                    const string prefix = "particle:";
                    if (entry.SetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!selections.TryGetValue(hero.Id, out var withParticle))
                            selections[hero.Id] = withParticle = new HeroSelectionState();
                        withParticle.Particles.Add(new ParticlePick { Id = entry.SetName[prefix.Length..], Host = entry.Host ?? string.Empty });
                    }
                    continue;
                }
                if (layer is not ("set" or "" or "item" or "base" or "prismatic" or "effect"))
                    continue;

                int index = hero.Sets.Keys.ToList().FindIndex(k =>
                    string.Equals(k, entry.SetName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    continue;

                if (!selections.TryGetValue(hero.Id, out var state))
                    selections[hero.Id] = state = new HeroSelectionState();

                switch (layer)
                {
                    case "item":
                        if (!state.ItemIndices.Contains(index)) state.ItemIndices.Add(index);
                        break;
                    case "base":
                        state.BaseIndex = index;
                        break;
                    case "prismatic":
                        state.PrismaticIndex = index;
                        break;
                    case "effect":
                        if (!state.EffectIndices.Contains(index)) state.EffectIndices.Add(index);
                        break;
                    default:
                        state.SetIndex = index;
                        break;
                }
            }

            foreach (var (heroId, state) in selections)
            {
                if (state.PrismaticIndex.HasValue && !state.BaseIndex.HasValue)
                    state.PrismaticIndex = null;
                if (state.Particles.Count > 0)
                    state.Particles = HeroParticleInjector.Sanitize(
                        heroes.First(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase)), state.Particles).ToList();
            }

            foreach (var heroId in selections.Where(kvp => !kvp.Value.HasAnySelection).Select(kvp => kvp.Key).ToList())
                selections.Remove(heroId);

            return selections;
        }

        public async Task SyncCooldownStatusAsync()
        {
            if (_view == null) return;

            if (FeatureAccessService.CurrentConfig == null)
            {
                try { await FeatureAccessService.GetConfigAsync(); } catch { }
            }

            var status = _cooldownService.GetStatus();
            await _view.UpdateCooldownAsync(
                status.IsActive,
                (int)Math.Ceiling(status.Remaining.TotalSeconds),
                (int)status.TotalDuration.TotalSeconds,
                status.DailyGenerationsUsed,
                status.DailyGenerationsMax,
                status.IsDailyLimitReached);
        }

        public async Task ResetCooldownAsync()
        {
            _cooldownService.ResetCooldown();
            await SyncCooldownStatusAsync();
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
                if (FeatureAccessService.CurrentConfig == null)
                {
                    try { await FeatureAccessService.GetConfigAsync(); } catch { }
                }

                if (_cooldownService.IsOnCooldown(out var remaining, out var reason))
                {
                    if (reason == SkinSelectorLockReason.DailyLimitReached)
                    {
                        var hours = (int)remaining.TotalHours;
                        var minutes = remaining.Minutes;
                        _logger?.LogDebug($"Skin Selector generation blocked by daily limit: {hours}h {minutes}m remaining until reset");

                        await _view.ShowAlertAsync(
                            Loc.T("hero.cooldown.dailyLimitTitle"),
                            Loc.T("hero.cooldown.dailyLimitMessage", new { hours, minutes }));
                    }
                    else
                    {
                        var minutes = (int)remaining.TotalMinutes;
                        var seconds = remaining.Seconds;
                        _logger?.LogDebug($"Skin Selector generation blocked by cooldown: {minutes}m {seconds}s remaining");

                        await _view.ShowAlertAsync(
                            Loc.T("hero.cooldown.title"),
                            Loc.T("hero.cooldown.message", new { minutes, seconds }));
                    }

                    await SyncCooldownStatusAsync();
                    return;
                }

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
                    .Select(x => (x.hero, HeroModelMapper.SetDisplayName(x.hero, x.setName), ResolveThumbnail(x.hero, x.setName)))
                    .ToList();

                if (previewItems.Count == 0)
                {
                    await _view.ShowAlertAsync(
                        Loc.T("hero.noSelections.title"),
                        Loc.T("hero.noSelections.body"));
                    return;
                }

                var access = await _featureCheck().ConfigureAwait(true);
                if (!access.IsAllowed)
                {
                    _logger?.LogDebug($"Skin Selector generation blocked at write boundary: {access.BlockedMessage}");
                    await _view.ShowAlertAsync(
                        access.IsOutdated ? Loc.T("update.required.title") : Loc.T("common.error"),
                        access.BlockedMessage ?? Loc.T("feature.blocked.offline"));
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
                            plan.Particles,
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
                _cooldownService.RecordGeneration();
                await SyncCooldownStatusAsync();

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

                var playRequested = await _view.ShowGenerationAlertAsync(
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
                              (hasFailures ? $", {operationResult.FailedItems!.Count} failed" : string.Empty),
                    PlayRequested = playRequested
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

        #region Debug build (the Skin Selector's debug card - only Debug builds send these)

        public Task<SkinTesterPlan> PlanDebugBuildAsync(
            IReadOnlyList<string> zipPaths, string heroesJsonPath, CancellationToken ct = default)
            => SkinTesterPlan.FromFilesAsync(zipPaths, heroesJsonPath, ct);

        public async Task DebugBuildAsync(IReadOnlyList<string> zipPaths, string heroesJsonPath)
        {
            if (_view == null)
                throw new InvalidOperationException("SetView must be called before DebugBuildAsync.");
            if (_isGenerating)
                return;

            _isGenerating = true;
            try
            {
                var targetPath = _configService.GetLastTargetPath();
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    await _view.ShowAlertAsync("Debug build", "No Dota 2 folder - run Detect first.");
                    return;
                }

                var plan = await PlanDebugBuildAsync(zipPaths, heroesJsonPath);
                if (plan.Errors.Count > 0 || plan.Selections.Count == 0)
                {
                    await _view.ShowAlertAsync("Debug build",
                        plan.Errors.Count > 0 ? string.Join("\n", plan.Errors) : "Add at least one .zip.");
                    return;
                }

                var trace = new TraceLogger(_logger);
                var service = _debugGeneration(plan, trace);
                await RunDebugGenerationAsync("Debug build", $"Debug build: {plan.Selections.Count} zip(s)", trace,
                    (context, stageProgress) => service.GenerateBatchAsync(targetPath, plan.Selections,
                        s => context.Substatus.Report(s), null, stageProgress, context.Speed, ct: context.Token));
            }
            catch (Exception ex)
            {
                _logger?.LogError("Skin Selector debug build failed", ex);
                await _view.ShowAlertAsync("Debug build failed", ex.Message);
            }
            finally
            {
                _isGenerating = false;
                await _view.UpdateStatusAsync(Loc.T("hero.status.ready"));
            }
        }

        public async Task GenerateModsPackAsync(
            IReadOnlyList<HeroModel> heroes,
            IReadOnlyDictionary<string, HeroSelectionState> selections,
            string outputRoot)
        {
            if (_view == null)
                throw new InvalidOperationException("SetView must be called before GenerateModsPackAsync.");
            if (_isGenerating)
                return;

            _isGenerating = true;
            try
            {
                var targetPath = _configService.GetLastTargetPath();
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    await _view.ShowAlertAsync("Generate ModsPack", "No Dota 2 folder - run Detect first.");
                    return;
                }

                var sets = BuildPlan(heroes, selections).Sets;
                if (!sets.Any(x => !x.setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase)))
                {
                    await _view.ShowAlertAsync("Generate ModsPack", "Select at least one custom set.");
                    return;
                }

                var releaseDir = Path.Combine(outputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                await RunDebugGenerationAsync("ModsPack package", $"ModsPack package: {sets.Count} hero(es)", null,
                    (context, stageProgress) => _generationService.GenerateModsPackAsync(targetPath, sets, releaseDir,
                        s => context.Substatus.Report(s), stageProgress, context.Speed, context.Token));
            }
            catch (Exception ex)
            {
                _logger?.LogError("Skin Selector ModsPack generation failed", ex);
                await _view.ShowAlertAsync("Generate ModsPack failed", ex.Message);
            }
            finally
            {
                _isGenerating = false;
                await _view.UpdateStatusAsync(Loc.T("hero.status.ready"));
            }
        }

        private async Task RunDebugGenerationAsync(
            string title, string overlayTitle, TraceLogger? trace,
            Func<ProgressOperationRunnerContext, IProgress<(int percent, string stage)>, Task<OperationResult>> run)
        {
            var result = await _view!.RunGenerationWithProgressAsync(overlayTitle, async context =>
            {
                var stageProgress = new Progress<(int percent, string stage)>(p =>
                {
                    context.Status.Report(p.stage);
                    context.Progress.Report(p.percent);
                });
                return await run(context, stageProgress);
            });
            if (result.WasCanceled)
                return;

            var message = new StringBuilder(result.Message ?? (result.Success ? "Installed." : "Failed."));
            foreach (var (name, reason) in result.FailedItems ?? new List<(string, string)>())
                message.Append($"\n  • {name}: {reason}");
            foreach (var warning in result.Warnings ?? new List<string>())
                message.Append($"\n  • {warning}");
            var log = (result.LogLines ?? Array.Empty<string>()).AsEnumerable();
            if (trace != null)
                log = log.Concat(new[] { "", "--- body ownership / generation trace ---" }).Concat(trace.Lines);
            await _view.ShowGenerationAlertAsync(
                result.Success ? $"{title} installed" : $"{title} failed",
                message.ToString(),
                !result.Success || result.FailedItems is { Count: > 0 } || result.Warnings is { Count: > 0 },
                string.Join(Environment.NewLine, log));
        }

        private sealed class TraceLogger : IAppLogger
        {
            private readonly IAppLogger? _inner;
            private readonly ConcurrentQueue<string> _lines = new();
            public TraceLogger(IAppLogger? inner) => _inner = inner;
            public IEnumerable<string> Lines => _lines;
            public void Log(string message, LogLevel level = LogLevel.Info) { _lines.Enqueue($"[{level}] {message}"); _inner?.Log(message, level); }
            public void LogError(string message, Exception? ex = null) { _lines.Enqueue($"[Error] {message} {ex?.Message}".TrimEnd()); _inner?.LogError(message, ex); }
            public void LogWarning(string message) { _lines.Enqueue($"[Warning] {message}"); _inner?.LogWarning(message); }
            public void LogDebug(string message) { _lines.Enqueue($"[Debug] {message}"); _inner?.LogDebug(message); }
            public void FlushBufferedLogs() => _inner?.FlushBufferedLogs();
        }

        #endregion

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
            IReadOnlyList<string> baseWithoutSetHeroNames,
            IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? particles = null)
        {
            Sets = sets;
            BaseWithoutSetHeroNames = baseWithoutSetHeroNames;
            Particles = particles ?? new Dictionary<string, IReadOnlyList<ParticlePick>>();
        }

        public IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>> Particles { get; }

        public IReadOnlyList<(HeroModel hero, string setName)> Sets { get; }

        public IReadOnlyList<string> BaseWithoutSetHeroNames { get; }

        public bool HasSelections => Sets.Count > 0;
    }
}
