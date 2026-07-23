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
using ArdysaModsTools.Core.Services.Localization;
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

        public HeroGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            IHeroSetDownloader? downloader = null,
            IHeroSetPatcher? patcher = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            IGameItemsGameExtractor? itemsGameExtractor = null,
            IHeroIndexProvider? indexProvider = null,
            IAppLogger? logger = null)
        {
            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _downloader = downloader ?? new HeroSetDownloaderService();
            _patcher = patcher ?? new HeroSetPatcherService(logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _itemsGameExtractor = itemsGameExtractor ?? new GameItemsGameExtractorService(logger);
            _indexProvider = indexProvider ?? new HeroIndexProvider(logger: logger);
            _localizationPatcher = new LocalizationPatcherService(logger);
            _logger = logger;
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
                ct).ConfigureAwait(false);
            return result;
        }

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

                var report = new GenerationReport(log);
                log = report.Log;

                var heroesToProcess = FilterHeroesForProcessing(heroSets, report);

                if (heroesToProcess.Count == 0)
                {
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
                    var baseProgress = new Progress<int>(p => 
                        stageProgress?.Report((p / 5, Loc.T("progress.downloadingBase", new { percent = p }))));
                    
                    try
                    {
                        string pristineBase = await _originalProvider.GetExtractedOriginalAsync(log, ct, speedProgress, baseProgress).ConfigureAwait(false);

                        extractDir = Path.Combine(tempRoot, "base");
                        CopyDirectory(pristineBase, extractDir, ct);
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

                    var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    stageProgress?.Report((20, "Processing"));

                    var heroGroups = heroesToProcess.GroupBy(x => x.hero.Id).ToList();
                    int processedSelectionsCount = 0;
                    int totalSelections = heroesToProcess.Count;

                    for (int g = 0; g < heroGroups.Count; g++)
                    {
                        var group = heroGroups[g];
                        var firstItem = group.First();
                        var hero = firstItem.hero;

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
                            catch (Exception ex)
                            {
                                failedHeroes.Add((item.hero.DisplayName, $"Download failed for {item.setName}: {ex.Message}"));
                                continue;
                            }

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

                        bool hasHeroBaseSlot = ResolveBaseWins(hero.Method, detectedHeroBase);

                        System.Diagnostics.Debug.WriteLine(
                            $"[DEBUG] Priority {hero.DisplayName}: method={(hero.Method?.ToString() ?? "null")}, " +
                            $"detectedHeroBase={detectedHeroBase}, baseWins={hasHeroBaseSlot} -> " +
                            (hasHeroBaseSlot ? "Base > Sets/Custom/Persona > Items" : "Sets/Custom/Persona > Items > Base"));

                        var orderedList = extractedList
                            .OrderByDescending(x => GetSortWeight(x.category, hasHeroBaseSlot))
                            .ToList();

                        if (orderedList.Count > 1)
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

                                string? indexText = null;
                                if (selection.category != HeroModelMapper.SkinCategory.Prismatic)
                                {
                                    indexText = await ResolveIndexTextAsync(selection.zipUrl, selection.folderPath, log, ct).ConfigureAwait(false);
                                    if (string.IsNullOrEmpty(indexText))
                                        throw new InvalidOperationException(
                                            $"No index found for set '{selection.setName}' — cloud index not synced to R2 and no bundled index.txt inside the set.");
                                }

                                var (_, copiedFiles) = await MergeSetAssetsAsync(selection.folderPath, extractDir, ct).ConfigureAwait(false);
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
                                else
                                {
                                    var heroBlocks = _patcher.ParseIndexText(indexText!, hero.Id, hero.ItemIds);
                                    if (heroBlocks == null || heroBlocks.Count == 0)
                                    {
                                        report.Warn($"{hero.DisplayName}: {selection.category} '{selection.setName}' contributed 0 patchable blocks — none of its index.txt ids are in this hero's id list (it cannot apply).");
                                    }
                                    if (heroBlocks != null)
                                    {
                                        foreach (var kvp in heroBlocks)
                                        {
                                            if (mergedBlocks.ContainsKey(kvp.Key))
                                                System.Diagnostics.Debug.WriteLine(
                                                    $"[DEBUG] Override {hero.DisplayName}: item {kvp.Key} -> {selection.category} (overrides earlier higher-layer block)");
                                            mergedBlocks[kvp.Key] = kvp.Value;
                                        }
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

                    ct.ThrowIfCancellationRequested();

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
                        extractDir, s => { }, ct).ConfigureAwait(false);
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

                    string protectedDir = Path.Combine(tempRoot, "protected");
                    int protectedMoved = 0;
                    if (protectedPaths.Count > 0 && ProtectedVpkStore.IsMounted(targetPath))
                        protectedMoved = ProtectedVpkStore.MoveProtected(
                            extractDir, protectedDir, protectedPaths, _logger, ct);
                    else if (protectedPaths.Count > 0)
                        _logger?.Log("Protected split skipped: the installed game config does not mount the second package yet.");

                    if (protectedMoved > 0)
                        _logger?.Log($"Protected split: {protectedMoved} file(s) moved out of the main package.");

                    stageProgress?.Report((65, "Building"));
                    log("Building VPK...");

                    var newVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot,
                        vpkLog => log($"[VPK] {vpkLog}"),
                        ct).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newVpkPath))
                    {
                        log("[VPK] Recompilation returned null - check logs above for details");
                        return Fail("VPK recompilation failed.", report, targetPath);
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
                            log("[VPK] Protected package build returned null - check logs above for details");
                            return Fail("VPK recompilation failed.", report, targetPath);
                        }
                    }

                    ct.ThrowIfCancellationRequested();

                    stageProgress?.Report((80, Loc.T("progress.installingShort")));
                    log("Installing...");
                    var replaceSuccess = await _replacer.ReplaceAsync(
                        targetPath, newVpkPath, log, ct).ConfigureAwait(false);

                    if (!replaceSuccess)
                        return Fail("VPK replacement failed.", report, targetPath);

                    if (!await ProtectedVpkStore.DeployAsync(
                            targetPath, newProtectedVpkPath, log, CancellationToken.None, _logger).ConfigureAwait(false))
                        return Fail("VPK replacement failed.", report, targetPath);

                    extractionLog.Save(targetPath);

                    var message = $"Successfully installed {successfulHeroes.Count} hero set(s)";
                    if (failedHeroes.Count > 0)
                    {
                        message += $". {failedHeroes.Count} failed.";
                    }
                    
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
                        
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Cleanup failed: {ex.Message}");
                    }

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

        private static void CopyDirectory(string sourceDir, string destDir, CancellationToken ct)
        {
            Directory.CreateDirectory(destDir);

            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(destDir, Path.GetRelativePath(sourceDir, dir)));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var destPath = Path.Combine(destDir, Path.GetRelativePath(sourceDir, file));
                var destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);
                File.Copy(file, destPath, overwrite: true);
            }
        }

        private async Task<(string contentRoot, List<string> files)> MergeSetAssetsAsync(string setFolder, string extractDir, CancellationToken ct)
        {
            var copiedFiles = new List<string>();

            var contentRoot = FindContentRoot(setFolder);
            if (string.IsNullOrEmpty(contentRoot))
            {
                _logger?.Log($"No content found in {setFolder}");
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
            _logger?.Log($"Merged {copiedFiles.Count} files");

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
                _logger?.Log($"Bundled index read failed for {setFolder}: {ex.Message}");
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

        private static OperationResult Fail(string message, GenerationReport report, string targetPath)
        {
            report.Log($"Error: {message}");
            report.Save(targetPath);
            return new OperationResult
            {
                Success = false,
                Message = message,
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
            => method == 1 ? true : method == 2 ? false : detectedHeroBase;

        internal static int GetSortWeight(HeroModelMapper.SkinCategory category, bool baseHasHeroBaseSlot)
        {
            switch (category)
            {
                case HeroModelMapper.SkinCategory.Prismatic:
                    return 0;
                case HeroModelMapper.SkinCategory.BaseHero:
                    return baseHasHeroBaseSlot ? 3 : 1;
                case HeroModelMapper.SkinCategory.LegacySet:
                case HeroModelMapper.SkinCategory.CustomSet:
                case HeroModelMapper.SkinCategory.Persona:
                    return baseHasHeroBaseSlot ? 2 : 3;
                case HeroModelMapper.SkinCategory.Item:
                    return baseHasHeroBaseSlot ? 1 : 2;
                default:
                    return -1;
            }
        }

    }
}
