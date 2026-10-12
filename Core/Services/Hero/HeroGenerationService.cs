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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Hero;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Services
{
    public sealed class HeroGenerationService : IHeroGenerationService
    {
        
        private readonly LocalizationPatcherService _localizationPatcher;
        private readonly IOriginalVpkProvider _originalProvider;
        private readonly IHeroSetDownloader _downloader;
        private readonly IHeroSetPatcher _patcher;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IGameItemsGameExtractor _itemsGameExtractor;
        private readonly IHeroIndexProvider _indexProvider;
        private readonly IAppLogger? _logger;
        private readonly HttpClient _httpClient;
        private readonly IModInstallerService _installer;

        private static string[] GameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi")
        };

        public HeroGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            IHeroSetDownloader? downloader = null,
            IHeroSetPatcher? patcher = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            IGameItemsGameExtractor? itemsGameExtractor = null,
            IHeroIndexProvider? indexProvider = null,
            IAppLogger? logger = null,
            HttpClient? httpClient = null,
            IModInstallerService? installer = null)
        {
            _installer = installer ?? new ModInstallerService(logger);
            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _downloader = downloader ?? new HeroSetDownloaderService();
            _patcher = patcher ?? new HeroSetPatcherService(logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _itemsGameExtractor = itemsGameExtractor ?? new GameItemsGameExtractorService(logger);
            _indexProvider = indexProvider ?? new HeroIndexProvider(logger: logger);
            _localizationPatcher = new LocalizationPatcherService(logger);
            _logger = logger;
            _httpClient = httpClient ?? HttpClientProvider.Client;
        }

        public async Task<OperationResult> GenerateHeroSetAsync(
            string targetPath,
            HeroModel hero,
            string selectedSetName,
            Action<string> log,
            CancellationToken ct = default)
        {
            var result = await GenerateBatchAsync(
                targetPath,
                new[] { (hero, selectedSetName) },
                log,
                null,
                null,
                null,
                ct: ct).ConfigureAwait(false);
            return result;
        }

        public Task<OperationResult> GenerateBatchAsync(
            string targetPath,
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            Action<string> log,
            IProgress<(int current, int total, string heroName)>? progress = null,
            IProgress<(int percent, string stage)>? stageProgress = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? particles = null,
            CancellationToken ct = default)
            => GenerateCoreAsync(targetPath, heroSets, log, progress, stageProgress, speedProgress, releaseDir: null, ct, particles);

        public Task<OperationResult> GenerateModsPackAsync(
            string targetPath,
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            string releaseDir,
            Action<string> log,
            IProgress<(int percent, string stage)>? stageProgress = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            CancellationToken ct = default)
            => string.IsNullOrWhiteSpace(releaseDir)
                ? Task.FromResult(Fail("No release folder given.", log))
                : GenerateCoreAsync(targetPath, heroSets, log, null, stageProgress, speedProgress, releaseDir, ct);

        private async Task<OperationResult> GenerateCoreAsync(
            string targetPath,
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            Action<string> log,
            IProgress<(int current, int total, string heroName)>? progress,
            IProgress<(int percent, string stage)>? stageProgress,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress,
            string? releaseDir,
            CancellationToken ct,
            IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? particles = null)
        {
            bool modsPackRelease = releaseDir != null;
            if (modsPackRelease) particles = null;
            try
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(targetPath))
                    return Fail("No target path set.", log);

                if (heroSets == null || heroSets.Count == 0)
                    return Fail("No hero sets provided.", log);

                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string vpkToolPath = Path.Combine(baseDir, "vpk.exe");

                if (!File.Exists(vpkToolPath))
                    return Fail("vpk.exe not found.", log);

                var report = new GenerationReport(log);
                log = report.Log;

                var heroesToProcess = FilterHeroesForProcessing(heroSets, report);

                if (heroesToProcess.Count == 0)
                {
                    if (modsPackRelease)
                        return Fail("No custom sets selected — a ModsPack package needs at least one.", report, targetPath);
                    report.Save(targetPath);
                    return new OperationResult
                    {
                        Success = true,
                        Message = "No custom sets selected. All heroes using default.",
                        SuccessCount = 0,
                        Warnings = report.Warnings.Count > 0 ? report.Warnings.ToList() : null,
                        LogLines = report.Lines.ToList()
                    };
                }

                var requested = heroSets
                    .Where(x => x.hero != null && !string.IsNullOrWhiteSpace(x.setName)
                        && !x.setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (x.hero.Id, x.setName))
                    .ToList();
                if (modsPackRelease)
                {
                    var skipped = ModsPackReleasePackage.CompareSets(
                        requested, heroesToProcess.Select(x => (x.hero.Id, x.setName)), "the build");
                    if (skipped != null)
                        return Fail($"ModsPack package not built: {skipped}", report, targetPath);

                    if (Directory.Exists(releaseDir) || File.Exists(releaseDir) || Directory.Exists(releaseDir + ".partial"))
                        return Fail($"The release folder already exists: {releaseDir}", report, targetPath);
                    try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(releaseDir!))!); }
                    catch (Exception ex) { return Fail($"Cannot create the release folder: {ex.Message}", report, targetPath); }
                }

                int totalHeroes = heroesToProcess.Count;
                
                HeroExtractionLog.Delete(targetPath);

                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), $"ArdysaHero_{Guid.NewGuid():N}");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(buildDir);
                Core.Helpers.SafeTempPathHelper.HideDirectory(tempRoot);

                var successfulHeroes = new List<string>();
                var failedHeroes = new List<(string heroName, string reason)>();
                var extractionLog = new HeroExtractionLog();

                try
                {
                    stageProgress?.Report((0, Loc.T("progress.preparing")));
                    log("Preparing...");
                    string extractDir;
                    string pristineBase;
                    var baseProgress = new Progress<int>(p => 
                        stageProgress?.Report((p / 5, Loc.T("progress.downloadingBase", new { percent = p }))));
                    
                    try
                    {
                        pristineBase = await _originalProvider.GetExtractedOriginalAsync(log, ct, speedProgress, baseProgress).ConfigureAwait(false);

                        extractDir = Path.Combine(tempRoot, "base");
                        Core.Helpers.DirectoryCopy.Tree(pristineBase, extractDir, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return new OperationResult { Success = false, Message = "Cancelled" };
                    }
                    catch (Exception ex)
                    {
                        return Fail($"Failed to get base files: {ex.Message}", report, targetPath);
                    }

                    ct.ThrowIfCancellationRequested();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    if (!await _itemsGameExtractor.RefreshFromGameAsync(targetPath, extractDir, log, ct).ConfigureAwait(false))
                        return Fail("Could not read package from your Dota 2 install. Re-run Detect and try again.", report, targetPath);

                    ct.ThrowIfCancellationRequested();

                    var mergedBlocks = new Dictionary<string, (string block, string heroId)>();

                    var blockWeights = new Dictionary<string, int>();

                    var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var manifestEntries = new List<SkinManifestEntry>();

                    var builtHeroes = new List<HeroModel>();

                    stageProgress?.Report((20, "Processing"));

                    var heroGroups = heroesToProcess.GroupBy(x => x.hero.Id).ToList();
                    int processedSelectionsCount = 0;
                    int totalSelections = heroesToProcess.Count;

                    for (int g = 0; g < heroGroups.Count; g++)
                    {
                        var group = heroGroups[g];
                        var firstItem = group.First();
                        var hero = firstItem.hero;
                        var ownTokens = HeroScopeGuard.OwnTokens(hero.Id);

                        var extractedList = new List<(HeroModel hero, string setName, HeroModelMapper.SkinCategory category, string folderPath, string zipUrl, bool encrypted)>();

                        foreach (var item in group)
                        {
                            ct.ThrowIfCancellationRequested();
                            processedSelectionsCount++;

                            if (item.hero == null)
                            {
                                failedHeroes.Add(("Unknown", "Hero is null"));
                                continue;
                            }

                            int progressPercent = 20 + (int)((processedSelectionsCount * 40.0) / totalSelections);

                            log($"[{processedSelectionsCount}/{totalSelections}] Processing {item.hero.DisplayName} - {item.setName}...");
                            stageProgress?.Report((progressPercent, $"Processing {item.hero.DisplayName}"));
                            progress?.Report((processedSelectionsCount, totalSelections, item.hero.DisplayName));

                            if (string.IsNullOrWhiteSpace(item.setName))
                            {
                                failedHeroes.Add((item.hero.DisplayName, "No set selected"));
                                continue;
                            }

                            if (item.hero.Sets == null || !item.hero.Sets.TryGetValue(item.setName, out var setUrls) || setUrls == null || setUrls.Count == 0)
                            {
                                failedHeroes.Add((item.hero.DisplayName, $"Set '{item.setName}' not found"));
                                continue;
                            }

                            var zipUrl = setUrls.FirstOrDefault(u => u != null && u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                            if (string.IsNullOrWhiteSpace(zipUrl))
                            {
                                failedHeroes.Add((item.hero.DisplayName, $"No .zip file found for set '{item.setName}'"));
                                continue;
                            }

                            if (item.hero.ItemIds == null || item.hero.ItemIds.Count == 0)
                            {
                                failedHeroes.Add((item.hero.DisplayName, "No item IDs defined"));
                                continue;
                            }

                            string setFolder;
                            bool selEncrypted = false;
                            try
                            {
                                var fastZipUrl = Core.Services.Config.EnvironmentConfig.ConvertToFastUrl(zipUrl);
                                setFolder = await _downloader.DownloadAndExtractAsync(
                                    item.hero.Id, item.setName, fastZipUrl, log, ct, speedProgress,
                                    onEncryptedDetected: () => selEncrypted = true).ConfigureAwait(false);

                                speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics 
                                { 
                                    CurrentFile = processedSelectionsCount,
                                    TotalFiles = totalSelections
                                });
                            }
                            catch (DownloadException dex) when (dex.ErrorCode == ErrorCodes.DL_ASSET_INCOMPATIBLE)
                            {
                                _logger?.LogDebug($"[{dex.ErrorCode}] Aborting batch: {dex.Message}");
                                return Fail(dex.Message, report, targetPath, dex.ErrorCode);
                            }
                            catch (Exception ex)
                            {
                                failedHeroes.Add((item.hero.DisplayName, $"Download failed for {item.setName}: {ex.Message}"));
                                continue;
                            }

                            if (modsPackRelease && selEncrypted)
                                log($"Note: '{item.setName}' ({item.hero.DisplayName}) is an encrypted set — its models ship decrypted in the ModsPack package.");

                            var category = HeroModelMapper.ClassifySet(item.hero.Sets, item.setName);
                            extractedList.Add((item.hero, item.setName, category, setFolder, zipUrl, selEncrypted));
                        }

                        if (extractedList.Count == 0)
                            continue;

                        var baseSelection = extractedList.FirstOrDefault(x => x.category == HeroModelMapper.SkinCategory.BaseHero);
                        bool detectedHeroBase = false;
                        if (baseSelection != default)
                        {
                            var baseText = await ResolveIndexTextAsync(baseSelection.zipUrl, baseSelection.folderPath, log, ct).ConfigureAwait(false);
                            detectedHeroBase = baseText != null && KeyValuesBlockHelper.AnyBlockHasItemSlot(baseText, "hero_base");
                        }

                        var policy = hero.BasePriority ?? new BasePriorityPolicy();
                        bool hasHeroBaseSlot = policy.BaseWins(null, null, detectedHeroBase);

                        int LayerWeight(HeroModelMapper.SkinCategory category, string setName, int? itemId = null)
                            => HeroGenerationService.LayerWeight(policy, category, setName, detectedHeroBase, itemId);

                        System.Diagnostics.Debug.WriteLine(
                            $"[DEBUG] Priority {hero.DisplayName}: method={(policy.Default?.ToString() ?? "null")}, " +
                            $"setOverrides={policy.Sets.Count}, itemOverrides={policy.Items.Count}, " +
                            $"detectedHeroBase={detectedHeroBase}, baseWins={hasHeroBaseSlot} -> " +
                            (hasHeroBaseSlot ? "Base > Sets/Custom/Persona > Items" : "Sets/Custom/Persona > Items > Base"));

                        var orderedList = extractedList
                            .OrderByDescending(x => LayerWeight(x.category, x.setName))
                            .ToList();

                        if (orderedList.Count > 1)
                            foreach (var sel in orderedList)
                                System.Diagnostics.Debug.WriteLine(
                                    $"[DEBUG] Order {hero.DisplayName}: weight={LayerWeight(sel.category, sel.setName)} " +
                                    $"category={sel.category} set={sel.setName} scope={policy.ScopeOf(sel.setName, null)}");

                        bool heroSucceeded = false;
                        var heroBlockOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var heroFileOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var heroLayerFiles = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
                        var heroLayerRoots = new Dictionary<string, (string Root, bool Encrypted)>(StringComparer.OrdinalIgnoreCase);
                        var heroLayerVariants = new Dictionary<string, IReadOnlyDictionary<string, ISet<string>>>(StringComparer.OrdinalIgnoreCase);
                        var heroLayerBlocks = new Dictionary<string, Dictionary<string, (string block, string heroId)>>(StringComparer.OrdinalIgnoreCase);
                        var blocksBefore = new Dictionary<string, (string block, string heroId)>(mergedBlocks);
                        var weightsBefore = new Dictionary<string, int>(blockWeights);
                        var protectedBefore = new HashSet<string>(protectedPaths, StringComparer.OrdinalIgnoreCase);
                        int manifestBefore = manifestEntries.Count, installedBefore = extractionLog.InstalledSets.Count;
                        try
                        {
                            EnsureEffectLayersAreSafe(orderedList
                                .Where(x => x.category == HeroModelMapper.SkinCategory.AbilityEffect)
                                .Select(x => (x.setName, FindContentRoot(x.folderPath))));

                            foreach (var selection in orderedList)
                            {
                                ct.ThrowIfCancellationRequested();

                                string? indexText = null;
                                bool isEffect = selection.category == HeroModelMapper.SkinCategory.AbilityEffect;
                                if (!isEffect && selection.category != HeroModelMapper.SkinCategory.Prismatic)
                                {
                                    indexText = await ResolveIndexTextAsync(selection.zipUrl, selection.folderPath, log, ct).ConfigureAwait(false);
                                    if (string.IsNullOrEmpty(indexText))
                                        throw new InvalidOperationException(
                                            $"No index found for set '{selection.setName}' — cloud index not synced to R2 and no bundled index.txt inside the set.");
                                }

                                var parsedBlocks = indexText == null ? null : _patcher.ParseIndexText(indexText, hero.Id, hero.ItemIds);
                                var foreignFiles = new List<string>();
                                Func<string, bool>? foreign = null;
                                if (indexText != null)
                                {
                                    string named = string.Join("\n", (parsedBlocks?.Values ?? Enumerable.Empty<(string block, string heroId)>()).Select(b => b.block));
                                    foreign = rel => HeroScopeGuard.IsForeign(rel, ownTokens, named);
                                }
                                var (contentRoot, copiedFiles) = await MergeSetAssetsAsync(selection.folderPath, extractDir, ct, foreign, foreignFiles).ConfigureAwait(false);
                                if (foreignFiles.Count > 0)
                                    report.Log($"Note: '{selection.setName}' ships {foreignFiles.Count} file(s) of another hero, left out: " +
                                               string.Join(", ", foreignFiles.Take(3)) + (foreignFiles.Count > 3 ? ", ..." : ""));
                                heroLayerRoots[selection.setName] = (contentRoot, selection.encrypted);
                                heroLayerFiles[selection.setName] = new HashSet<string>(copiedFiles, StringComparer.OrdinalIgnoreCase);
                                foreach (var rel in copiedFiles)
                                    heroFileOwner[rel] = selection.setName;
                                heroLayerVariants[selection.setName] = ReadBodyVariants(contentRoot);
                                System.Diagnostics.Debug.WriteLine(
                                    $"[DEBUG] {hero.DisplayName}: '{selection.setName}' ({selection.category}) merged {copiedFiles.Count} asset file(s).");

                                foreach (var rel in copiedFiles)
                                {
                                    if (selection.encrypted && ProtectedVpkStore.IsProtectable(rel))
                                        protectedPaths.Add(rel);
                                    else
                                        protectedPaths.Remove(rel);
                                }

                                if (selection.category == HeroModelMapper.SkinCategory.Prismatic)
                                {
                                    log($"[Patcher] {hero.DisplayName}: Prismatic '{selection.setName}' merged {copiedFiles.Count} asset file(s) as an overlay (no index.txt).");
                                }
                                else if (isEffect)
                                {
                                    log($"[Patcher] {hero.DisplayName}: Ability Effect '{selection.setName}' applied {copiedFiles.Count} file(s).");
                                }
                                else
                                {
                                    var heroBlocks = parsedBlocks;
                                    if (heroBlocks == null || heroBlocks.Count == 0)
                                    {
                                        report.Warn($"{hero.DisplayName}: {selection.category} '{selection.setName}' contributed 0 patchable blocks — none of its index.txt ids are in this hero's id list (it cannot apply).");
                                    }
                                    if (heroBlocks != null)
                                    {
                                        heroLayerBlocks[selection.setName] = heroBlocks;
                                        foreach (var kvp in heroBlocks)
                                        {
                                            int weight = int.TryParse(kvp.Key, out var blockItemId)
                                                ? LayerWeight(selection.category, selection.setName, blockItemId)
                                                : LayerWeight(selection.category, selection.setName);

                                            if (blockWeights.TryGetValue(kvp.Key, out var ownerWeight) && ownerWeight < weight)
                                            {
                                                System.Diagnostics.Debug.WriteLine(
                                                    $"[DEBUG] Keep {hero.DisplayName}: item {kvp.Key} stays on the lower layer " +
                                                    $"(owner weight {ownerWeight} < {weight} from {selection.category} '{selection.setName}')");
                                                continue;
                                            }

                                            if (mergedBlocks.ContainsKey(kvp.Key))
                                                System.Diagnostics.Debug.WriteLine(
                                                    $"[DEBUG] Override {hero.DisplayName}: item {kvp.Key} -> {selection.category} " +
                                                    $"weight={weight} scope={policy.ScopeOf(selection.setName, blockItemId)} (overrides earlier higher-layer block)");

                                            mergedBlocks[kvp.Key] = kvp.Value;
                                            blockWeights[kvp.Key] = weight;
                                            heroBlockOwner[kvp.Key] = selection.setName;
                                        }
                                    }
                                }

                                extractionLog.InstalledSets.Add(new HeroSetEntry
                                {
                                    HeroId = hero.Id,
                                    SetName = selection.setName,
                                    Files = copiedFiles
                                });

                                manifestEntries.Add(await BuildManifestEntryAsync(
                                    hero.Id, selection.setName, selection.category, selection.zipUrl, ct)
                                    .ConfigureAwait(false));

                                heroSucceeded = true;
                            }

                            var overlays = LayersAppliedOverTheLock(extractedList.Select(x => (x.category, x.setName)));
                            if (baseSelection != default)
                                ApplyBaseSlotLock(hero, baseSelection.setName, mergedBlocks, heroBlockOwner,
                                    heroLayerBlocks, heroLayerFiles, heroFileOwner, heroLayerRoots, extractDir,
                                    protectedPaths, trace: s => _logger?.LogDebug(s), warn: report.Warn,
                                    yieldTo: overlays);
                            ApplyBodyOwnership(hero, mergedBlocks, heroBlockOwner, heroLayerFiles, heroLayerVariants,
                                heroFileOwner, heroLayerRoots, extractDir, protectedPaths,
                                trace: s => _logger?.LogDebug(s), warn: s => report.Log($"Note: {s}"), overlays: overlays);
                            ApplyWornModelYield(hero.DisplayName, mergedBlocks, heroBlockOwner, heroLayerFiles,
                                heroFileOwner, heroLayerRoots, extractDir, protectedPaths,
                                trace: s => _logger?.LogDebug(s), warn: report.Warn);

                            if (heroSucceeded)
                            {
                                successfulHeroes.Add(hero.DisplayName);
                                builtHeroes.Add(hero);
                            }
                        }
                        catch (Exception ex)
                        {
                            failedHeroes.Add((hero.DisplayName, ex.Message));
                            _logger?.LogDebug($"Error merging assets for {hero.DisplayName}: {ex}");

                            mergedBlocks.Clear();
                            foreach (var kv in blocksBefore) mergedBlocks[kv.Key] = kv.Value;
                            blockWeights.Clear();
                            foreach (var kv in weightsBefore) blockWeights[kv.Key] = kv.Value;
                            protectedPaths.Clear();
                            protectedPaths.UnionWith(protectedBefore);
                            manifestEntries.RemoveRange(manifestBefore, manifestEntries.Count - manifestBefore);
                            extractionLog.InstalledSets.RemoveRange(installedBefore, extractionLog.InstalledSets.Count - installedBefore);
                            RestoreFromPristine(heroFileOwner.Keys.Concat(heroLayerFiles.Values.SelectMany(f => f)), pristineBase, extractDir);
                        }
                        finally
                        {
                            foreach (var selection in extractedList)
                            {
                                try
                                {
                                    if (Directory.Exists(selection.folderPath))
                                        Directory.Delete(selection.folderPath, true);
                                }
                                catch { }
                            }
                        }

                        if (g % 3 == 2)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    if (successfulHeroes.Count == 0)
                    {
                        var errors = string.Join(", ", failedHeroes.Select(f => $"{f.heroName}: {f.reason}"));
                        return Fail($"All heroes failed: {errors}", report, targetPath);
                    }

                    if (modsPackRelease && failedHeroes.Count > 0)
                        return Fail("ModsPack package not built — every selected set must build: "
                            + string.Join("; ", failedHeroes.Select(f => $"{f.heroName}: {f.reason}")), report, targetPath);

                    ct.ThrowIfCancellationRequested();

                    if (particles is { Count: > 0 })
                        ApplyHeroParticles(builtHeroes, particles, mergedBlocks, manifestEntries, log, report.Warn);

                    string[] patchedIds = mergedBlocks.Keys.ToArray();

                    string? releaseIndex = null;
                    if (modsPackRelease)
                    {
                        releaseIndex = ModsPackReleasePackage.BuildIndex(mergedBlocks, out var indexError);
                        if (releaseIndex == null)
                            return Fail($"ModsPack package not built: {indexError}", report, targetPath);
                    }

                    if (mergedBlocks.Count > 0)
                    {
                        log("Patching...");
                        var patchSuccess = await _patcher.PatchWithMergedBlocksAsync(
                            extractDir, mergedBlocks, log, ct).ConfigureAwait(false);

                        mergedBlocks.Clear();

                        if (!patchSuccess)
                        {
                            return Fail("Patching package failed.", report, targetPath);
                        }
                        
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    ct.ThrowIfCancellationRequested();

                    stageProgress?.Report((60, Loc.T("progress.downloadingAssets")));
                    log("Downloading assets...");
                    var locSuccess = await _localizationPatcher.PatchLocalizationAsync(
                        extractDir, log, ct,
                        onFileDone: (done, total) =>
                            stageProgress?.Report((60 + done * 5 / Math.Max(total, 1),
                                Loc.T("progress.downloadingAssets"))))
                        .ConfigureAwait(false);
                    if (!locSuccess)
                    {
                        report.Warn("Some localization files failed to download — some text labels may be missing.");
                    }

                    ct.ThrowIfCancellationRequested();

                    speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics 
                    { 
                        DownloadSpeed = "-- MB/S",
                        DownloadedBytes = 0,
                        TotalBytes = 0 
                    });

                    await SkinManifestStore.WriteToTreeAsync(extractDir, new SkinManifest
                    {
                        GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
                        Build = typeof(HeroGenerationService).Assembly.GetName().Version?.ToString() ?? string.Empty,
                        Entries = manifestEntries
                    }, ct).ConfigureAwait(false);

                    if (modsPackRelease)
                    {
                        var treeError = ModsPackReleasePackage.PrepareTree(extractDir, requested);
                        if (treeError != null)
                            return Fail($"ModsPack package not built: {treeError}", report, targetPath);
                    }

                    string protectedDir = Path.Combine(tempRoot, "protected");
                    int protectedMoved = modsPackRelease ? 0 : ProtectedVpkStore.Split(
                        targetPath, extractDir, protectedDir, protectedPaths, _logger, ct);

                    if (protectedMoved > 0)
                    {
                        _logger?.LogDebug($"Protected split: {protectedMoved} file(s) moved out of the main package into game/mod.");
                    }

                    stageProgress?.Report((65, "Building"));
                    log("Building VPK...");

                    var newVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot,
                        vpkLog => log($"[VPK] {vpkLog}"),
                        ct).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newVpkPath))
                    {
                        ct.ThrowIfCancellationRequested();
                        log("[VPK] Recompilation returned null - check logs above for details");
                        return Fail("VPK recompilation failed.", report, targetPath);
                    }
                    if (modsPackRelease)
                    {
                        var sealError = await ModsPackReleasePackage.SealAsync(
                            newVpkPath, slim: false, _installer.ClassifyVpkAsync, ct).ConfigureAwait(false);
                        if (sealError != null)
                            return Fail($"ModsPack package failed verification: {sealError}", report, targetPath);

                        string installVpk = Path.Combine(tempRoot, "install", "pak01_dir.vpk");
                        Directory.CreateDirectory(Path.GetDirectoryName(installVpk)!);
                        File.Move(newVpkPath, installVpk);
                        newVpkPath = installVpk;

                        stageProgress?.Report((72, "Building release package"));
                        log("Building the slim release package...");
                        var stripError = ModsPackReleasePackage.StripPackageText(extractDir);
                        if (stripError != null)
                            return Fail($"ModsPack package not built: {stripError}", report, targetPath);

                        var slimVpk = await _recompiler.RecompileAsync(
                            vpkToolPath, extractDir, buildDir, tempRoot,
                            vpkLog => log($"[VPK] {vpkLog}"), ct).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(slimVpk) || string.Equals(slimVpk, installVpk, StringComparison.OrdinalIgnoreCase))
                        {
                            ct.ThrowIfCancellationRequested();
                            return Fail("Release package recompilation failed.", report, targetPath);
                        }
                        sealError = await ModsPackReleasePackage.SealAsync(
                            slimVpk, slim: true, _installer.ClassifyVpkAsync, ct).ConfigureAwait(false);
                        if (sealError != null)
                            return Fail($"ModsPack release package failed verification: {sealError}", report, targetPath);

                        var exportError = await ModsPackReleasePackage.ExportAsync(releaseDir!, slimVpk, releaseIndex!, ct).ConfigureAwait(false);
                        if (exportError != null)
                            return Fail($"ModsPack release files not written: {exportError}", report, targetPath);
                        log("ModsPack verified: installed package + slim release package, origin marker, signature section, manifest.");
                    }
                    else
                    {
                        VpkSignatureSection.TryApply(newVpkPath);
                    }

                    ct.ThrowIfCancellationRequested();

                    string? newProtectedVpkPath = null;
                    if (protectedMoved > 0)
                    {
                        newProtectedVpkPath = await _recompiler.RecompileAsync(
                            vpkToolPath, protectedDir, buildDir, tempRoot,
                            vpkLog => log($"[VPK] {vpkLog}"),
                            ct).ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(newProtectedVpkPath) ||
                            string.Equals(newProtectedVpkPath, newVpkPath, StringComparison.OrdinalIgnoreCase))
                        {
                            ct.ThrowIfCancellationRequested();
                            log("[VPK] Protected package build returned null - check logs above for details");
                            return Fail("VPK recompilation failed.", report, targetPath);
                        }
                        VpkSignatureSection.TryApply(newProtectedVpkPath);
                    }

                    ct.ThrowIfCancellationRequested();

                    stageProgress?.Report((80, Loc.T("progress.installingShort")));
                    log("Installing...");

                    if (!await ProtectedVpkStore.DeployPairAsync(
                            targetPath, newProtectedVpkPath,
                            () => _replacer.ReplaceAsync(targetPath, newVpkPath, log, ct),
                            log, _logger).ConfigureAwait(false))
                        return Fail(modsPackRelease
                            ? $"VPK replacement failed — the release files are complete in {releaseDir}, only the in-game test install did not happen."
                            : "VPK replacement failed.", report, targetPath);

                    await ItemsGameBaselineStore.CommitAsync(targetPath, patchedIds, ct).ConfigureAwait(false);

                    extractionLog.Save(targetPath);

                    var gameInfoPatchSuccess = await PatchSignaturesAndGameInfoAsync(targetPath, ct).ConfigureAwait(false);
                    if (!gameInfoPatchSuccess)
                    {
                        _logger?.Log("Warning: Failed to patch signatures/gameinfo, but VPK was installed.");
                        report.Warn("Could not update the game's signatures/gameinfo — mods may not load in-game. Try generating again.");
                    }

                    var message = $"Successfully installed {successfulHeroes.Count} hero set(s)";
                    if (failedHeroes.Count > 0)
                    {
                        message += $". {failedHeroes.Count} failed.";
                    }
                    if (modsPackRelease)
                        message += $". Release files: {releaseDir}";

                    stageProgress?.Report((100, "Done"));
                    log("Done!");
                    report.Save(targetPath);
                    return new OperationResult
                    {
                        Success = true,
                        Message = message,
                        SuccessCount = successfulHeroes.Count,
                        FailedItems = failedHeroes.Count > 0
                            ? failedHeroes.Select(f => (f.heroName, f.reason)).ToList()
                            : null,
                        Warnings = report.Warnings.Count > 0 ? report.Warnings.ToList() : null,
                        LogLines = report.Lines.ToList()
                    };
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(tempRoot))
                            Directory.Delete(tempRoot, true);
                        
                        var setsCache = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "sets");
                        if (Directory.Exists(setsCache))
                            Directory.Delete(setsCache, true);

                        var decryptedSets = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "HeroSets");
                        if (Directory.Exists(decryptedSets))
                            Directory.Delete(decryptedSets, true);


                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug($"Cleanup failed: {ex.Message}");
                    }

                    Core.Helpers.LargeWorkMemory.Release();
                }
            }
            catch (OperationCanceledException)
            {
                log("Operation canceled.");
                return OperationResult.Canceled();
            }
            catch (Exception ex)
            {
                log($"Error: {ex.Message}");
                _logger?.LogDebug($"HeroGenerationService batch error: {ex}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        internal async Task<(string contentRoot, List<string> files)> MergeSetAssetsAsync(string setFolder, string extractDir,
            CancellationToken ct, Func<string, bool>? skip = null, List<string>? skipped = null)
        {
            var copiedFiles = new List<string>();

            var contentRoot = FindContentRoot(setFolder);
            if (string.IsNullOrEmpty(contentRoot))
            {
                _logger?.LogDebug($"No content found in {setFolder}");
                return (setFolder, copiedFiles);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] contentRoot = {contentRoot}");
#endif

            foreach (var file in Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                
                var relativePath = Path.GetRelativePath(contentRoot, file);
                if (relativePath.Equals("index.txt", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsUnderVariants(relativePath))
                    continue;
                if (skip != null && skip(relativePath.Replace('\\', '/')))
                {
                    skipped?.Add(relativePath.Replace('\\', '/'));
                    continue;
                }

                var destPath = Path.Combine(extractDir, relativePath);

                var destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                File.Copy(file, destPath, overwrite: true);

                copiedFiles.Add(relativePath.Replace('\\', '/'));
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Copied {copiedFiles.Count} files");
#endif
            _logger?.LogDebug($"Merged {copiedFiles.Count} files");

            await Task.CompletedTask;
            return (contentRoot, copiedFiles);
        }

        private string FindContentRoot(string folder)
        {
            var assetTypes = new[] { "models", "particles", "materials", "sounds", "scripts", "panorama", "resource" };

            foreach (var assetType in assetTypes)
            {
                if (Directory.Exists(Path.Combine(folder, assetType)))
                    return folder;
            }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(folder))
                {
                    foreach (var assetType in assetTypes)
                    {
                        if (Directory.Exists(Path.Combine(subDir, assetType)))
                            return subDir;
                    }
                }
            }
            catch { }

            try
            {
                var subDirs = Directory.GetDirectories(folder);
                if (subDirs.Length == 1)
                    return subDirs[0];
            }
            catch { }

            return folder;
        }

        public async Task<ParticleSlots?> GetParticleSlotsAsync(HeroModel hero, string setName, CancellationToken ct = default)
        {
            if (hero?.Sets == null || string.IsNullOrWhiteSpace(setName)
                || !hero.Sets.TryGetValue(setName, out var urls) || urls == null)
                return null;
            var zipUrl = urls.FirstOrDefault(u => u != null && u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zipUrl == null) return null;

            var text = await _indexProvider.GetIndexTextAsync(zipUrl, _ => { }, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text)) return null;
            var blocks = _patcher.ParseIndexText(text, hero.Id, hero.ItemIds);
            return HeroParticleInjector.Slots(
                blocks ?? new Dictionary<string, (string block, string heroId)>(),
                HeroModelMapper.ClassifySet(urls), HeroModelMapper.ExtractItemTag(urls));
        }

        private async Task<string?> ResolveIndexTextAsync(string zipUrl, string setFolder, Action<string> log, CancellationToken ct)
        {
            var cloud = await _indexProvider.GetIndexTextAsync(zipUrl, log, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(cloud))
                return cloud;

            var bundled = FindBundledIndex(setFolder);
            if (bundled == null)
                return null;

            try
            {
                log("Cloud index unavailable — using the index.txt bundled in the set.");
                return await File.ReadAllTextAsync(bundled, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Bundled index read failed for {setFolder}: {ex.Message}");
                return null;
            }
        }

        private static string? FindBundledIndex(string setFolder)
        {
            if (string.IsNullOrWhiteSpace(setFolder) || !Directory.Exists(setFolder))
                return null;

            var root = Path.Combine(setFolder, "index.txt");
            if (File.Exists(root)) return root;

            try
            {
                return Directory.EnumerateFiles(setFolder, "index.txt", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static OperationResult Fail(string message, Action<string> log)
        {
            log($"Error: {message}");
            return new OperationResult { Success = false, Message = message };
        }

        private static OperationResult Fail(
            string message, GenerationReport report, string targetPath, string? errorCode = null)
        {
            report.Log($"Error: {message}");
            report.Save(targetPath);
            return new OperationResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Warnings = report.Warnings.Count > 0 ? report.Warnings.ToList() : null,
                LogLines = report.Lines.ToList()
            };
        }

        private List<(HeroModel hero, string setName)> FilterHeroesForProcessing(
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            GenerationReport report)
        {
            var result = new List<(HeroModel hero, string setName)>();

            foreach (var (hero, setName) in heroSets)
            {
                if (hero == null)
                    continue;
                if (string.IsNullOrWhiteSpace(setName))
                    continue;
                if (setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (hero.Sets == null || hero.Sets.Count == 0)
                {
                    report.Skip(hero.DisplayName, $"no sets defined for this hero (wanted '{setName}')");
                    continue;
                }

                if (!hero.Sets.TryGetValue(setName, out var setUrls) || setUrls == null || setUrls.Count == 0)
                {
                    report.Skip(hero.DisplayName, $"set '{setName}' not found");
                    continue;
                }

                bool hasZip = setUrls.Any(u => u != null && u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (!hasZip)
                {
                    report.Skip(hero.DisplayName, $"no .zip download for set '{setName}'");
                    continue;
                }

                if (hero.ItemIds == null || hero.ItemIds.Count == 0)
                {
                    report.Skip(hero.DisplayName, $"no item IDs defined (set '{setName}' cannot be patched)");
                    continue;
                }

                result.Add((hero, setName));
            }

            return result;
        }

        internal static bool ResolveBaseWins(int? method, bool detectedHeroBase)
            => BasePriorityPolicy.Resolve(method, detectedHeroBase);

        internal const int BaseAnchorWeight = 100;

        private static async Task<SkinManifestEntry> BuildManifestEntryAsync(
            string heroId, string setName, HeroModelMapper.SkinCategory category, string zipUrl, CancellationToken ct)
        {
            string? assetPath = CdnConfig.ExtractAssetPath(zipUrl);
            if (assetPath != null && zipUrl.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase))
                assetPath = assetPath.Replace(".001", string.Empty, StringComparison.OrdinalIgnoreCase);

            string sha = string.Empty;
            try
            {
                var expected = await AssetHashManifestService.Instance.GetExpectedAsync(assetPath, ct).ConfigureAwait(false);
                sha = expected?.Sha256 ?? string.Empty;
            }
            catch (OperationCanceledException) { throw; }
            catch {  }

            return new SkinManifestEntry
            {
                HeroId = heroId,
                SetName = setName,
                Layer = LayerOf(category),
                Sha256 = sha
            };
        }

        internal static void ApplyHeroParticles(
            IReadOnlyCollection<HeroModel> builtHeroes,
            IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>> particles,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            List<SkinManifestEntry> manifest,
            Action<string> log,
            Action<string> warn)
        {
            foreach (var (heroId, picks) in particles)
            {
                var hero = builtHeroes.FirstOrDefault(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase));
                if (hero == null)
                {
                    if (picks.Count > 0) log($"Note: particles for {heroId} skipped, its skins did not build.");
                    continue;
                }

                var labels = HeroParticleCatalog.HostsFor(hero).ToDictionary(h => h.Id, h => h.Label);
                foreach (var pick in HeroParticleInjector.Sanitize(hero, picks))
                {
                    HeroParticleCatalog.TryGet(pick.Id, out var particle);
                    string slot = labels[pick.Host];
                    try
                    {
                        if (!mergedBlocks.TryGetValue(pick.Host, out var owned))
                        {
                            warn($"{hero.DisplayName}: {particle.Name} skipped, no picked set, item or base has the {slot}.");
                            continue;
                        }
                        mergedBlocks[pick.Host] = (HeroParticleInjector.Inject(owned.block, particle.Path), hero.Id);
                        manifest.Add(new SkinManifestEntry { HeroId = hero.Id, SetName = "particle:" + particle.Id, Layer = "particle", Host = pick.Host });
                        log($"[Particles] {hero.DisplayName}: {particle.Name} on {slot}");
                        if (!HeroParticleInjector.HasWearableModel(owned.block))
                            log($"Note: {hero.DisplayName}: the {slot} has no wearable model, {particle.Name} may not render.");
                    }
                    catch (Exception ex)
                    {
                        warn($"{hero.DisplayName}: {particle.Name} skipped, {ex.Message}");
                    }
                }
            }
        }

        internal static void RestoreFromPristine(IEnumerable<string> relPaths, string pristineBase, string extractDir)
        {
            foreach (var rel in relPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var target = Path.Combine(extractDir, rel);
                var source = Path.Combine(pristineBase, rel);
                if (File.Exists(source)) File.Copy(source, target, overwrite: true);
                else if (File.Exists(target)) File.Delete(target);
            }
        }

        internal static ISet<string> LayersAppliedOverTheLock(
            IEnumerable<(HeroModelMapper.SkinCategory category, string setName)> layers) =>
            layers.Where(x => x.category is HeroModelMapper.SkinCategory.Prismatic or HeroModelMapper.SkinCategory.AbilityEffect)
                .Select(x => x.setName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        internal static void EnsureEffectLayersAreSafe(IEnumerable<(string setName, string contentRoot)> effects)
        {
            var layers = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (setName, contentRoot) in effects)
            {
                var files = Directory.Exists(contentRoot)
                    ? Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(contentRoot, f).Replace('\\', '/'))
                        .ToList()
                    : new List<string>();
                var problems = AbilityEffectGuard.ValidateLayerFiles(files);
                if (problems.Count > 0)
                    throw new InvalidOperationException(
                        $"Ability Effect '{setName}' was refused: {string.Join("; ", problems.Take(3))}" +
                        (problems.Count > 3 ? $" (+{problems.Count - 3} more)" : ""));
                layers[setName] = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            }

            var overlaps = AbilityEffectGuard.FindOverlaps(layers);
            if (overlaps.Count > 0)
            {
                var (file, owners) = overlaps.First();
                throw new InvalidOperationException(
                    $"Ability Effects {string.Join(" and ", owners)} both change '{file}' — pick one of them.");
            }
        }

        private static string LayerOf(HeroModelMapper.SkinCategory category) => category switch
        {
            HeroModelMapper.SkinCategory.Prismatic => "prismatic",
            HeroModelMapper.SkinCategory.BaseHero => "base",
            HeroModelMapper.SkinCategory.Item => "item",
            HeroModelMapper.SkinCategory.AbilityEffect => "effect",
            _ => "set"
        };

        private static bool CopyFromLayer(
            string rel, string sourceRel, string layer,
            IReadOnlyDictionary<string, (string Root, bool Encrypted)> layerRoots,
            string extractDir, Dictionary<string, string> fileOwner, HashSet<string> protectedPaths,
            string? ownerToken = null)
        {
            if (!layerRoots.TryGetValue(layer, out var lr)) return false;
            string src = Path.Combine(lr.Root, sourceRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src)) return false;

            string dest = Path.Combine(extractDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, overwrite: true);
            fileOwner[rel] = ownerToken ?? layer;
            if (lr.Encrypted && ProtectedVpkStore.IsProtectable(rel))
                protectedPaths.Add(rel);
            else
                protectedPaths.Remove(rel);
            return true;
        }

        internal static bool IsUnderVariants(string relativePath)
            => relativePath.Replace('\\', '/').StartsWith(BodyOwnership.VariantsDir + "/", StringComparison.OrdinalIgnoreCase);

        internal static IReadOnlyDictionary<string, ISet<string>> ReadBodyVariants(string contentRoot)
        {
            var result = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
            string dir = Path.Combine(contentRoot, BodyOwnership.VariantsDir);
            if (!Directory.Exists(dir)) return result;

            foreach (var keyDir in Directory.EnumerateDirectories(dir))
            {
                var files = Directory.EnumerateFiles(keyDir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(keyDir, f).Replace('\\', '/'));
                result[Path.GetFileName(keyDir)] = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }

        internal static BaseSlotLock.Result ApplyBaseSlotLock(
            HeroModel hero,
            string baseLayer,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            Dictionary<string, string> blockOwner,
            IReadOnlyDictionary<string, Dictionary<string, (string block, string heroId)>> layerBlocks,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            Dictionary<string, string> fileOwner,
            IReadOnlyDictionary<string, (string Root, bool Encrypted)> layerRoots,
            string extractDir,
            HashSet<string> protectedPaths,
            Action<string> trace,
            Action<string> warn,
            ISet<string>? yieldTo = null,
            Func<int, string?>? slotOf = null)
        {
            var slots = BaseSlotLock.SlotsFor(hero, baseLayer);
            if (slots.Count == 0)
                return new BaseSlotLock.Result();

            string Label(string layer) => hero.Sets != null && hero.Sets.TryGetValue(layer, out var urls)
                && HeroModelMapper.ExtractArchiveStem(urls) is string stem && hero.SetNames != null
                && hero.SetNames.TryGetValue(stem, out var name) && !string.IsNullOrWhiteSpace(name) ? name : layer;

            var lockedIds = BaseSlotLock.LockedIds(hero.ItemIds, slots, slotOf);
            if (lockedIds.Count == 0)
            {
                warn($"{hero.DisplayName}: '{Label(baseLayer)}' locks {string.Join(", ", slots)}, but the hero has no such slot.");
                return new BaseSlotLock.Result();
            }

            var own = layerBlocks.TryGetValue(baseLayer, out var parsed) ? parsed : new Dictionary<string, (string block, string heroId)>();
            var baseBlocks = own.ToDictionary(kv => kv.Key, kv => kv.Value.block, StringComparer.OrdinalIgnoreCase);
            var slotModels = (hero.ItemIds ?? new List<int>()).Distinct().ToDictionary(
                id => id.ToString(),
                id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer ?? "" : "");

            Dictionary<string, (string Block, string Layer)> Final() => blockOwner
                .Where(kv => mergedBlocks.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => (mergedBlocks[kv.Key].block, kv.Value), StringComparer.OrdinalIgnoreCase);

            BaseSlotLock.Result Compute() => BaseSlotLock.Compute(Final(), baseLayer, baseBlocks, lockedIds,
                layerFiles, fileOwner, slotModels, yieldTo, Label);

            var result = Compute();
            foreach (var w in result.Warnings)
                warn($"{hero.DisplayName}: {w}");
            if (result.IsClean)
                return result;

            foreach (var id in result.Reclaim.Keys)
            {
                trace($"{hero.DisplayName}: item {id} locked to '{baseLayer}' (was '{(blockOwner.TryGetValue(id, out var o) ? o : "none")}').");
                mergedBlocks[id] = own[id];
                blockOwner[id] = baseLayer;
            }
            foreach (var (rel, layer) in result.Restore)
                if (!CopyFromLayer(rel, rel, layer, layerRoots, extractDir, fileOwner, protectedPaths))
                    throw new InvalidOperationException($"internal: {hero.DisplayName}: locked file {rel} of '{layer}' vanished.");

            trace($"{hero.DisplayName}: '{baseLayer}' locks {string.Join(", ", slots)}: " +
                  $"{result.Reclaim.Count} block(s) reclaimed, {result.Restore.Count} file(s) restored.");

            if (!Compute().IsClean)
                throw new InvalidOperationException(
                    $"internal: {hero.DisplayName}: locked slot still owned by another layer after merge ('{baseLayer}').");
            return result;
        }

        internal static BodyOwnership.Result ApplyBodyOwnership(
            HeroModel hero,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            IReadOnlyDictionary<string, string> blockOwner,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, ISet<string>>> layerVariants,
            Dictionary<string, string> fileOwner,
            IReadOnlyDictionary<string, (string Root, bool Encrypted)> layerRoots,
            string extractDir,
            HashSet<string> protectedPaths,
            Action<string> trace,
            Action<string> warn,
            ISet<string>? overlays = null)
        {
            var slotModels = new HashSet<string>(
                (hero.ItemIds ?? new List<int>())
                    .Select(id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer : "")
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => KeyValuesBlockHelper.NormalizeAssetPath(m) + "_c"),
                StringComparer.OrdinalIgnoreCase);

            string? Stem(string layer) => hero.Sets != null && hero.Sets.TryGetValue(layer, out var urls)
                ? HeroModelMapper.ExtractArchiveStem(urls) : null;

            string Label(string layer)
            {
                var stem = Stem(layer);
                return stem != null && hero.SetNames != null && hero.SetNames.TryGetValue(stem, out var name)
                    && !string.IsNullOrWhiteSpace(name) ? name : layer;
            }

            Dictionary<string, (string Block, string Layer)> Final() => blockOwner
                .Where(kv => mergedBlocks.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => (mergedBlocks[kv.Key].block, kv.Value), StringComparer.OrdinalIgnoreCase);

            BodyOwnership.Result Compute() => BodyOwnership.Compute(
                Final(), layerFiles, layerVariants, fileOwner, hero.Id, slotModels, Label,
                id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer ?? "" : null, Stem, overlays);

            var result = Compute();
            foreach (var w in result.Warnings)
                warn($"{hero.DisplayName}: {w}");
            if (result.IsClean)
                return result;

            foreach (var (rel, (layer, key)) in result.VariantApply)
                if (!CopyFromLayer(rel, $"{BodyOwnership.VariantsDir}/{key}/{rel}", layer,
                        layerRoots, extractDir, fileOwner, protectedPaths,
                        BodyOwnership.VariantOwner(layer, key)))
                    throw new InvalidOperationException($"internal: {hero.DisplayName}: variant {rel} of '{layer}' vanished.");
            foreach (var (rel, layer) in result.Restore)
                if (!CopyFromLayer(rel, rel, layer, layerRoots, extractDir, fileOwner, protectedPaths))
                    throw new InvalidOperationException($"internal: {hero.DisplayName}: body file {rel} of '{layer}' vanished.");

            var wearableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "model_player", "match_cycle_to_parent" };
            foreach (var id in result.StripWearable)
                mergedBlocks[id] = (KeyValuesBlockHelper.RemoveTopLevelKeys(mergedBlocks[id].block, wearableKeys, out _),
                                    mergedBlocks[id].heroId);

            string owners = result.Owner ?? string.Join(", ", result.Restore.Values
                .Concat(result.VariantApply.Values.Select(v => $"{v.Layer}#{v.Key}")).Distinct(StringComparer.OrdinalIgnoreCase));
            trace($"{hero.DisplayName}: body owner '{owners}' ({result.BodyKey}): " +
                  $"{result.Restore.Count} body file(s) restored, {result.VariantApply.Count} variant file(s) applied, " +
                  $"{result.StripWearable.Count} demo-only wearable(s) dropped.");

            if (!Compute().IsClean)
                throw new InvalidOperationException(
                    $"internal: {hero.DisplayName}: hero body still mixed after merge (owner '{result.Owner}').");
            return result;
        }

        internal static WornModelYield.Result ApplyWornModelYield(
            string heroName,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            IReadOnlyDictionary<string, string> blockOwner,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            Dictionary<string, string> fileOwner,
            IReadOnlyDictionary<string, (string Root, bool Encrypted)> layerRoots,
            string extractDir,
            HashSet<string> protectedPaths,
            Action<string> trace,
            Action<string> warn,
            Func<string, string?>? defaultModelOf = null)
        {
            defaultModelOf ??= id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer : null;

            Dictionary<string, (string Block, string Layer)> Final() => blockOwner
                .Where(kv => mergedBlocks.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => (mergedBlocks[kv.Key].block, kv.Value), StringComparer.OrdinalIgnoreCase);

            var result = WornModelYield.Compute(Final(), layerFiles, fileOwner, defaultModelOf);
            foreach (var w in result.Warnings)
                warn($"{heroName}: {w}");
            if (result.IsClean)
                return result;

            foreach (var (id, assets) in result.StripRows)
                mergedBlocks[id] = (KeyValuesBlockHelper.RemoveModelSwapRows(mergedBlocks[id].block, assets, out _),
                                    mergedBlocks[id].heroId);

            var unrestored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (rel, layer) in result.Restore)
            {
                if (!CopyFromLayer(rel, rel, layer, layerRoots, extractDir, fileOwner, protectedPaths))
                {
                    unrestored.Add(rel);
                    warn($"{heroName}: could not restore {rel} from '{layer}' — that piece may show the wrong model.");
                }
            }

            foreach (var y in result.Yields)
                trace($"{heroName}: item {y.BlockId} ('{y.HiddenByLayer}') no longer hides {y.Asset} — " +
                      $"item {y.WornById} ('{y.WornByLayer}') wears its own mesh there.");

            var verify = WornModelYield.Compute(Final(), layerFiles, fileOwner, defaultModelOf);
            var stillHidden = verify.StripRows.SelectMany(kv => kv.Value).ToList();
            var stillWrong = verify.Restore.Keys.Where(k => !unrestored.Contains(k)).ToList();
            if (stillHidden.Count > 0 || stillWrong.Count > 0)
                throw new InvalidOperationException(
                    $"internal: {heroName}: worn models still hidden after merge ({string.Join(", ", stillHidden.Concat(stillWrong))}).");

            return result;
        }

        internal static int LayerWeight(BasePriorityPolicy policy, HeroModelMapper.SkinCategory category,
                                        string setName, bool detectedHeroBase, int? itemId = null)
            => GetSortWeight(category, policy.BaseWins(setName, itemId, detectedHeroBase));


        internal static int GetSortWeight(HeroModelMapper.SkinCategory category, bool baseWins)
        {
            switch (category)
            {
                case HeroModelMapper.SkinCategory.AbilityEffect:
                    return -10;
                case HeroModelMapper.SkinCategory.Prismatic:
                    return 0;
                case HeroModelMapper.SkinCategory.BaseHero:
                    return BaseAnchorWeight;
                case HeroModelMapper.SkinCategory.LegacySet:
                case HeroModelMapper.SkinCategory.CustomSet:
                case HeroModelMapper.SkinCategory.Persona:
                    return baseWins ? BaseAnchorWeight - 10
                                    : BaseAnchorWeight + 20;
                case HeroModelMapper.SkinCategory.Item:
                    return baseWins ? BaseAnchorWeight - 20
                                    : BaseAnchorWeight + 10;
                default:
                    return -1;
            }
        }

        private async Task<bool> PatchSignaturesAndGameInfoAsync(string targetPath, CancellationToken ct)
        {
            try
            {
                string signaturesPath = Path.Combine(targetPath, "game", "bin", "win64", "dota.signatures");
                string gameInfoPath = Path.Combine(targetPath, "game", "dota", "gameinfo_branchspecific.gi");

                if (!File.Exists(signaturesPath))
                {
                    _logger?.Log("Cannot patch: Core game file not found.");
                    return false;
                }

                string[] lines = await File.ReadAllLinesAsync(signaturesPath, ct).ConfigureAwait(false);
                int digestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:"));
                if (digestIndex < 0)
                {
                    _logger?.Log("Core file format invalid.");
                    return false;
                }

                var modified = new List<string>(lines[..(digestIndex + 1)])
                {
                    ModConstants.ModPatchLine
                };

                string tmpSig = signaturesPath + ".tmp";
                await File.WriteAllLinesAsync(tmpSig, modified, ct).ConfigureAwait(false);
                File.Replace(tmpSig, signaturesPath, null);

                Directory.CreateDirectory(Path.GetDirectoryName(gameInfoPath)!);
                byte[]? fileBytes = null;
                Exception? lastError = null;

                foreach (var url in GameInfoUrls)
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(15));
                        fileBytes = await _httpClient.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
                        if (fileBytes != null && fileBytes.Length > 0)
                            break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger?.Log($"Failed to download patch files.");
                    return false;
                }

                string tmpGi = gameInfoPath + ".tmp";
                await File.WriteAllBytesAsync(tmpGi, fileBytes, ct).ConfigureAwait(false);
                if (File.Exists(gameInfoPath))
                    File.Replace(tmpGi, gameInfoPath, null);
                else
                    File.Move(tmpGi, gameInfoPath, true);

                ProtectedVpkStore.Ensure(targetPath);

                _logger?.Log("Game files patched successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Log($"PatchSignaturesAndGameInfoAsync failed: {ex.Message}");
                return false;
            }
        }
    }
}
