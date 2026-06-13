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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Orchestrator service for hero set generation.
    /// Coordinates: Download Original.zip → Modify → Recompile → Replace.
    /// </summary>
    public sealed class HeroGenerationService : IHeroGenerationService
    {
        
        private readonly LocalizationPatcherService _localizationPatcher;
        private readonly IOriginalVpkProvider _originalProvider;
        private readonly IHeroSetDownloader _downloader;
        private readonly IHeroSetPatcher _patcher;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IGameItemsGameExtractor _itemsGameExtractor;
        private readonly IAppLogger? _logger;

        public HeroGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            IHeroSetDownloader? downloader = null,
            IHeroSetPatcher? patcher = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            IGameItemsGameExtractor? itemsGameExtractor = null,
            IAppLogger? logger = null)
        {
            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _downloader = downloader ?? new HeroSetDownloaderService();
            _patcher = patcher ?? new HeroSetPatcherService(logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _itemsGameExtractor = itemsGameExtractor ?? new GameItemsGameExtractorService(logger);
            _localizationPatcher = new LocalizationPatcherService(logger);
            _logger = logger;
        }

        /// <summary>
        /// Generate and install a hero set.
        /// </summary>
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
                null,  // progress
                null,  // stageProgress
                null,  // speedProgress
                ct).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Generate and install multiple hero sets in a single VPK operation.
        /// Flow: Download Original.zip → (Merge + Patch each hero) → Recompile → Replace
        /// </summary>
        /// <param name="stageProgress">Reports overall progress: (percent 0-100, stageName)</param>
        public async Task<OperationResult> GenerateBatchAsync(
            string targetPath,
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            Action<string> log,
            IProgress<(int current, int total, string heroName)>? progress = null,
            IProgress<(int percent, string stage)>? stageProgress = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            CancellationToken ct = default)
        {
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

                // PRE-FILTER: Only process heroes with non-default custom sets
                var heroesToProcess = FilterHeroesForProcessing(heroSets);
                
                if (heroesToProcess.Count == 0)
                {
                    return new OperationResult 
                    { 
                        Success = true, 
                        Message = "No custom sets selected. All heroes using default.",
                        SuccessCount = 0
                    };
                }

                int totalHeroes = heroesToProcess.Count;
                
                // Delete existing extraction log (will recreate after patching)
                HeroExtractionLog.Delete(targetPath);

                // Ensure _ArdysaMods folder exists early in the process
                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                // Create temp work folder - use safe path for non-ASCII username compatibility
                string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), $"ArdysaHero_{Guid.NewGuid():N}");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(buildDir);

                var successfulHeroes = new List<string>();
                var failedHeroes = new List<(string heroName, string reason)>();
                var extractionLog = new HeroExtractionLog();

                try
                {
                    stageProgress?.Report((0, "Preparing"));
                    log("Preparing...");
                    string extractDir;
                    // Create sub-progress for the 0-20% stage — show download % in status text
                    var baseProgress = new Progress<int>(p => 
                        stageProgress?.Report((p / 5, $"Downloading base files ({p}%)")));
                    
                    try
                    {
                        extractDir = await _originalProvider.GetExtractedOriginalAsync(log, ct, speedProgress, baseProgress).ConfigureAwait(false);
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] extractDir = {extractDir}");
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        // User cancelled - return silent failure
                        return new OperationResult { Success = false, Message = "Cancelled" };
                    }
                    catch (Exception ex)
                    {
                        return Fail($"Failed to get base files: {ex.Message}", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    // Force GC after large extraction to release memory
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Inject the LATEST items_game.txt from the detected Dota 2 install so generation
                    // always patches the current game version (and a clean base, never a stale/patched
                    // cached copy). Fatal if unavailable — without it there is nothing to patch.
                    if (!await _itemsGameExtractor.RefreshFromGameAsync(targetPath, extractDir, log, ct).ConfigureAwait(false))
                        return Fail("Could not read items_game.txt from your Dota 2 install. Re-run Detect and try again.", log);

                    ct.ThrowIfCancellationRequested();

                    // Collect all index blocks from all heroes for merged patching
                    var mergedBlocks = new Dictionary<string, (string block, string heroId)>();

                    stageProgress?.Report((20, "Processing"));

                    // Group selections by hero to process all layers of each hero in the correct priority order
                    var heroGroups = heroesToProcess.GroupBy(x => x.hero.Id).ToList();
                    int processedSelectionsCount = 0;
                    int totalSelections = heroesToProcess.Count;

                    for (int g = 0; g < heroGroups.Count; g++)
                    {
                        var group = heroGroups[g];
                        var firstItem = group.First();
                        var hero = firstItem.hero;

                        // Download all selections for this hero
                        var extractedList = new List<(HeroModel hero, string setName, HeroModelMapper.SkinCategory category, string folderPath)>();

                        foreach (var item in group)
                        {
                            ct.ThrowIfCancellationRequested();
                            processedSelectionsCount++;

                            if (item.hero == null)
                            {
                                failedHeroes.Add(("Unknown", "Hero is null"));
                                continue;
                            }

                            // Calculate progress: 20% + (40% spread across selections)
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
                            try
                            {
                                var fastZipUrl = Core.Services.Config.EnvironmentConfig.ConvertToFastUrl(zipUrl);
                                setFolder = await _downloader.DownloadAndExtractAsync(
                                    item.hero.Id, item.setName, fastZipUrl, log, ct, speedProgress).ConfigureAwait(false);

                                speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics 
                                { 
                                    CurrentFile = processedSelectionsCount,
                                    TotalFiles = totalSelections
                                });
                            }
                            catch (Exception ex)
                            {
                                failedHeroes.Add((item.hero.DisplayName, $"Download failed for {item.setName}: {ex.Message}"));
                                continue;
                            }

                            var category = HeroModelMapper.ClassifySet(item.hero.Sets, item.setName);
                            extractedList.Add((item.hero, item.setName, category, setFolder));
                        }

                        if (extractedList.Count == 0)
                            continue;

                        // Check base hero item slot logic
                        var baseSelection = extractedList.FirstOrDefault(x => x.category == HeroModelMapper.SkinCategory.BaseHero);
                        bool detectedHeroBase = false;
                        if (baseSelection != default)
                        {
                            var baseContentRoot = FindContentRoot(baseSelection.folderPath);
                            detectedHeroBase = IndexFileHasHeroBaseSlot(baseContentRoot) || IndexFileHasHeroBaseSlot(baseSelection.folderPath);
                        }

                        // Explicit per-hero method (heroes.json) overrides auto-detection when present.
                        bool hasHeroBaseSlot = ResolveBaseWins(hero.Method, detectedHeroBase);

                        // Always-on: states whether the heroes.json method actually reached generation.
                        // If this says "auto-detection" for a hero you set a method on, the method never
                        // arrived (stale app build or stale/cached CDN heroes.json) — fix that first.
                        log($"[Patcher] {hero.DisplayName}: priority source = " +
                            (hero.Method is 1 or 2
                                ? $"heroes.json method {hero.Method}"
                                : "auto-detection (no method in heroes.json)") +
                            $"; baseWins={hasHeroBaseSlot}.");

                        // DEBUG (Visual Studio Output window): surface the inputs and resolved priority decision.
                        // method=null here means the heroes.json method never reached generation.
                        System.Diagnostics.Debug.WriteLine(
                            $"[DEBUG] Priority {hero.DisplayName}: method={(hero.Method?.ToString() ?? "null")}, " +
                            $"detectedHeroBase={detectedHeroBase}, baseWins={hasHeroBaseSlot} -> " +
                            (hasHeroBaseSlot ? "Base > Sets/Custom/Persona > Items" : "Sets/Custom/Persona > Items > Base"));

                        if (hero.Method == 1 || hero.Method == 2)
                        {
                            log($"[Patcher] {hero.DisplayName}: explicit method {hero.Method} from heroes.json overrides detection — " +
                                (hasHeroBaseSlot ? "Base takes top priority (Base > Sets > Items)." : "Base is lowest priority (Sets > Items > Base)."));
                        }
                        else if (baseSelection != default)
                        {
                            log(hasHeroBaseSlot
                                ? $"[Patcher] Base Hero mod for {hero.DisplayName} has item_slot hero_base — Base takes top priority (Base > Sets > Items)."
                                : $"[Patcher] Base Hero mod for {hero.DisplayName} does not have item_slot hero_base — Base is lowest priority (Sets > Items > Base).");
                        }

                        // Apply as layers, foundation→top (weight 3 → 2 → 1). Combined with the last-writer-wins
                        // merge below, every layer is applied and a later (lower-weight) layer — e.g. a specific
                        // Item — overrides earlier ones on the slots it provides. Nothing is skipped.
                        var orderedList = extractedList
                            .OrderByDescending(x => GetSortWeight(x.category, hasHeroBaseSlot))
                            .ToList();

                        // DEBUG: selections foundation→top; the LAST line that defines a given item id wins it.
                        foreach (var sel in orderedList)
                            System.Diagnostics.Debug.WriteLine(
                                $"[DEBUG] Order {hero.DisplayName}: weight={GetSortWeight(sel.category, hasHeroBaseSlot)} " +
                                $"category={sel.category} set={sel.setName}");

                        bool heroSucceeded = false;
                        try
                        {
                            foreach (var selection in orderedList)
                            {
                                ct.ThrowIfCancellationRequested();

                                var (contentRoot, copiedFiles) = await MergeSetAssetsAsync(selection.folderPath, extractDir, ct).ConfigureAwait(false);

                                var heroBlocks = _patcher.ParseIndexFile(contentRoot, hero.Id, hero.ItemIds, selection.folderPath);
                                if (heroBlocks == null || heroBlocks.Count == 0)
                                {
                                    // A selection that contributes no patchable blocks can never win a conflict — its
                                    // index.txt item-ids aren't in this hero's id list (heroes.json). Surface it.
                                    log($"[Patcher] WARNING {hero.DisplayName}: {selection.category} '{selection.setName}' " +
                                        $"contributed 0 patchable blocks — none of its index.txt ids are in this hero's id list (it cannot win).");
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[DEBUG] WARNING {hero.DisplayName}: {selection.category} '{selection.setName}' contributed 0 patchable blocks (ids not in hero id list)");
                                }
                                if (heroBlocks != null)
                                {
                                    foreach (var kvp in heroBlocks)
                                    {
                                        // Layered apply (last-writer-wins): selections run foundation→top
                                        // (GetSortWeight descending — Base first, Items last). EVERY selection's
                                        // block is applied; a later, lower-layer selection (e.g. a specific Item)
                                        // overrides an earlier one for the same item id, so nothing is skipped and
                                        // the most specific pick shows on its slot. Verbatim block, no deep-merge.
                                        if (mergedBlocks.ContainsKey(kvp.Key))
                                            System.Diagnostics.Debug.WriteLine(
                                                $"[DEBUG] Override {hero.DisplayName}: item {kvp.Key} -> {selection.category} (overrides earlier higher-layer block)");
                                        mergedBlocks[kvp.Key] = kvp.Value;
                                    }
                                }

                                extractionLog.InstalledSets.Add(new HeroSetEntry
                                {
                                    HeroId = hero.Id,
                                    SetName = selection.setName,
                                    Files = copiedFiles
                                });

                                heroSucceeded = true;
                            }

                            if (heroSucceeded)
                            {
                                successfulHeroes.Add(hero.DisplayName);
                            }
                        }
                        catch (Exception ex)
                        {
                            failedHeroes.Add((hero.DisplayName, ex.Message));
                            _logger?.Log($"Error merging assets for {hero.DisplayName}: {ex}");
                        }
                        finally
                        {
                            // Clean up folders
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

                        // Force GC after each hero to prevent memory buildup
                        if (g % 3 == 2)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    if (successfulHeroes.Count == 0)
                    {
                        var errors = string.Join(", ", failedHeroes.Select(f => $"{f.heroName}: {f.reason}"));
                        return Fail($"All heroes failed: {errors}", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    if (mergedBlocks.Count > 0)
                    {
                        log("Patching...");
                        var patchSuccess = await _patcher.PatchWithMergedBlocksAsync(
                            extractDir, mergedBlocks, log, ct).ConfigureAwait(false);

                        // Clear mergedBlocks to release memory before VPK build
                        mergedBlocks.Clear();
                        
                        if (!patchSuccess)
                        {
                            return Fail("Patching items_game.txt failed.", log);
                        }
                        
                        // Force GC after patching large items_game.txt
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    ct.ThrowIfCancellationRequested();

                    stageProgress?.Report((60, "Downloading assets"));
                    log("Downloading assets...");
                    var locSuccess = await _localizationPatcher.PatchLocalizationAsync(
                        extractDir, s => { }, ct).ConfigureAwait(false);
                    if (!locSuccess)
                    {
                        _logger?.Log("Warning: Some localization files failed to download");
                    }

                    ct.ThrowIfCancellationRequested();

                    // Hide download progress before building - signal UI to clear download display
                    speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics 
                    { 
                        DownloadSpeed = "-- MB/S",
                        DownloadedBytes = 0,
                        TotalBytes = 0 
                    });

                    stageProgress?.Report((65, "Building"));
                    log("Building VPK...");
                    
                    // Pass log function to capture VPK diagnostics for troubleshooting
                    var newVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot, 
                        vpkLog => log($"[VPK] {vpkLog}"), // Capture VPK tool output
                        ct).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newVpkPath))
                    {
                        log("[VPK] Recompilation returned null - check logs above for details");
                        return Fail("VPK recompilation failed.", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    stageProgress?.Report((80, "Installing"));
                    log("Installing...");
                    var replaceSuccess = await _replacer.ReplaceAsync(
                        targetPath, newVpkPath, s => { }, ct).ConfigureAwait(false);

                    if (!replaceSuccess)
                        return Fail("VPK replacement failed.", log);

                    // Save extraction log
                    extractionLog.Save(targetPath);

                    // Build result message
                    var message = $"Successfully installed {successfulHeroes.Count} hero set(s)";
                    if (failedHeroes.Count > 0)
                    {
                        message += $". {failedHeroes.Count} failed.";
                    }
                    
                    // Complete (100%)
                    stageProgress?.Report((100, "Done"));
                    log("Done!");
                    return new OperationResult 
                    { 
                        Success = true, 
                        Message = message,
                        SuccessCount = successfulHeroes.Count,
                        FailedItems = failedHeroes.Count > 0 
                            ? failedHeroes.Select(f => (f.heroName, f.reason)).ToList() 
                            : null
                    };
                }
                finally
                {
                    // Cleanup temp folders
                    try
                    {
                        if (Directory.Exists(tempRoot))
                            Directory.Delete(tempRoot, true);
                        
                        // Cleanup hero set cache folder (individual hero downloads)
                        var setsCache = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "sets");
                        if (Directory.Exists(setsCache))
                            Directory.Delete(setsCache, true);
                        
                        // NOTE: We intentionally keep cache/original/ in TEMP to speed up subsequent runs
                        // The Original.zip and extracted VPK are cached permanently
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Cleanup failed: {ex.Message}");
                    }

                    // Force garbage collection with LOH compaction to release large strings
                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
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
                _logger?.Log($"HeroGenerationService batch error: {ex}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        /// <summary>
        /// Merge ALL content from set folder into the extracted folder.
        /// Copies all files preserving folder structure.
        /// Returns (contentRoot, copiedFiles) for extraction log and patching.
        /// </summary>
        private async Task<(string contentRoot, List<string> files)> MergeSetAssetsAsync(string setFolder, string extractDir, CancellationToken ct)
        {
            var copiedFiles = new List<string>();

            // Find the actual content root (might be nested in a subfolder)
            var contentRoot = FindContentRoot(setFolder);
            if (string.IsNullOrEmpty(contentRoot))
            {
                _logger?.Log($"No content found in {setFolder}");
                return (setFolder, copiedFiles);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] contentRoot = {contentRoot}");
#endif

            // Copy ALL files from content root to extract dir
            foreach (var file in Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                
                // Skip index.txt and other config files at root
                var relativePath = Path.GetRelativePath(contentRoot, file);
                if (relativePath.Equals("index.txt", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destPath = Path.Combine(extractDir, relativePath);

                var destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                // Last-writer-wins: selections are merged foundation→top (Base first, Items last), so a
                // later, lower-layer selection's files overwrite earlier ones — consistent with the
                // last-writer-wins items_game.txt blocks above.
                File.Copy(file, destPath, overwrite: true);

                // Track file path for extraction log (use forward slashes for consistency)
                copiedFiles.Add(relativePath.Replace('\\', '/'));
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Copied {copiedFiles.Count} files");
#endif
            _logger?.Log($"Merged {copiedFiles.Count} files");

            await Task.CompletedTask;
            return (contentRoot, copiedFiles);
        }

        /// <summary>
        /// Finds the content root folder that contains actual mod files.
        /// Handles cases where zip has nested structure.
        /// </summary>
        private string FindContentRoot(string folder)
        {
            // Known asset folders to look for
            var assetTypes = new[] { "models", "particles", "materials", "sounds", "scripts", "panorama", "resource" };

            foreach (var assetType in assetTypes)
            {
                if (Directory.Exists(Path.Combine(folder, assetType)))
                    return folder;
            }

            // Check one level deep for nested structure
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

            // Fallback: if there's only one subfolder, use it
            try
            {
                var subDirs = Directory.GetDirectories(folder);
                if (subDirs.Length == 1)
                    return subDirs[0];
            }
            catch { }

            return folder; // Fallback to original folder
        }

        private static OperationResult Fail(string message, Action<string> log)
        {
            log($"Error: {message}");
            return new OperationResult { Success = false, Message = message };
        }

        /// <summary>
        /// Pre-filters hero sets to only include heroes that need processing.
        /// Excludes: Default Set, null heroes, empty set names, sets without valid URLs.
        /// This dramatically improves performance when only a few heroes have custom sets.
        /// </summary>
        private List<(HeroModel hero, string setName)> FilterHeroesForProcessing(
            IReadOnlyList<(HeroModel hero, string setName)> heroSets)
        {
            var result = new List<(HeroModel hero, string setName)>();

            foreach (var (hero, setName) in heroSets)
            {
                // Skip null heroes
                if (hero == null)
                    continue;

                // Skip empty or whitespace set names
                if (string.IsNullOrWhiteSpace(setName))
                    continue;

                // Skip "Default Set" - these don't need processing
                if (setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip if hero has no sets defined
                if (hero.Sets == null || hero.Sets.Count == 0)
                    continue;

                // Skip if the selected set doesn't exist
                if (!hero.Sets.TryGetValue(setName, out var setUrls) || setUrls == null || setUrls.Count == 0)
                    continue;

                // Skip if no .zip URL in set
                bool hasZip = setUrls.Any(u => u != null && u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (!hasZip)
                    continue;

                // Skip if no item IDs defined (required for patching)
                if (hero.ItemIds == null || hero.ItemIds.Count == 0)
                    continue;

                // This hero passes all checks - add to processing list
                result.Add((hero, setName));
            }

            return result;
        }

        // method 1 → Base wins (true); method 2 → Base last (false); else fall back to detection.
        internal static bool ResolveBaseWins(int? method, bool detectedHeroBase)
            => method == 1 ? true : method == 2 ? false : detectedHeroBase;

        internal static int GetSortWeight(HeroModelMapper.SkinCategory category, bool baseHasHeroBaseSlot)
        {
            switch (category)
            {
                case HeroModelMapper.SkinCategory.BaseHero:
                    return baseHasHeroBaseSlot ? 3 : 1;            // hero_base → Base wins; else Base lowest
                case HeroModelMapper.SkinCategory.LegacySet:
                case HeroModelMapper.SkinCategory.CustomSet:
                case HeroModelMapper.SkinCategory.Persona:
                    return baseHasHeroBaseSlot ? 2 : 3;            // Sets win when no hero_base
                case HeroModelMapper.SkinCategory.Item:
                    return baseHasHeroBaseSlot ? 1 : 2;
                default:
                    return 0;                                       // unknown → applied first, never wins
            }
        }

        internal static bool IndexFileHasHeroBaseSlot(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return false;

                string? indexPath = null;
                var rootIndex = Path.Combine(folder, "index.txt");
                if (File.Exists(rootIndex))
                {
                    indexPath = rootIndex;
                }
                else
                {
                    var candidates = Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
                        .Where(p => Path.GetFileName(p).Contains("index", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (candidates.Count > 0)
                        indexPath = candidates[0];
                    else
                        indexPath = Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories).FirstOrDefault();
                }

                if (indexPath == null) return false;

                var text = File.ReadAllText(indexPath, System.Text.Encoding.UTF8);
                // VKV-aware: only true when an item block has a top-level item_slot == hero_base,
                // not when "hero_base" merely appears somewhere in the file (e.g. a nested visual block).
                return KeyValuesBlockHelper.AnyBlockHasItemSlot(text, "hero_base");
            }
            catch
            {
                return false;
            }
        }

    }
}
