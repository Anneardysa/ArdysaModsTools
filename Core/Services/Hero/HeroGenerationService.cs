using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger? _logger;

        public HeroGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            IHeroSetDownloader? downloader = null,
            IHeroSetPatcher? patcher = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            ILogger? logger = null)
        {
            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _downloader = downloader ?? new HeroSetDownloaderService();
            _patcher = patcher ?? new HeroSetPatcherService(logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
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

                // Validate inputs
                if (string.IsNullOrWhiteSpace(targetPath))
                    return Fail("No target path set.", log);

                if (heroSets == null || heroSets.Count == 0)
                    return Fail("No hero sets provided.", log);

                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                // Validate tools
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

                // Create temp work folder
                string tempRoot = Path.Combine(Path.GetTempPath(), $"ArdysaHero_{Guid.NewGuid():N}");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(buildDir);

                var successfulHeroes = new List<string>();
                var failedHeroes = new List<(string heroName, string reason)>();
                var extractionLog = new HeroExtractionLog();

                try
                {
                    // Step 1: Download and extract Original.zip (0-20%)
                    stageProgress?.Report((0, "Preparing"));
                    log("Preparing...");
                    string extractDir;
                    // Create sub-progress for the 0-20% stage
                    var baseProgress = new Progress<int>(p => stageProgress?.Report((p / 5, "Preparing")));
                    
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

                    // Collect all index blocks from all heroes for merged patching
                    var mergedBlocks = new Dictionary<string, (string block, string heroId)>();

                    // Step 2: Process ONLY filtered heroes (20-60%)
                    stageProgress?.Report((20, "Processing"));
                    for (int i = 0; i < heroesToProcess.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        
                        var (hero, setName) = heroesToProcess[i];
                        int current = i + 1;
                        
                        // Calculate progress: 20% + (40% spread across heroes)
                        int heroProgress = 20 + (int)((current * 40.0) / totalHeroes);
                        stageProgress?.Report((heroProgress, $"Processing {hero.DisplayName}"));
                        
                        progress?.Report((current, totalHeroes, hero.DisplayName));
                        log($"Processing {hero.DisplayName}...");

                        try
                        {
                            // Validate hero (already pre-filtered, but defensive check)
                            if (hero == null)
                            {
                                failedHeroes.Add(("Unknown", "Hero is null"));
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(setName))
                            {
                                failedHeroes.Add((hero.DisplayName, "No set selected"));
                                continue;
                            }

                            // Note: Default Set already filtered out in FilterHeroesForProcessing

                            if (hero.Sets == null || !hero.Sets.TryGetValue(setName, out var setUrls) || setUrls == null || setUrls.Count == 0)
                            {
                                failedHeroes.Add((hero.DisplayName, $"Set '{setName}' not found"));
                                continue;
                            }

                            // Find zip URL in set
                            var zipUrl = setUrls.FirstOrDefault(u =>
                                u != null && u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                            if (string.IsNullOrWhiteSpace(zipUrl))
                            {
                                failedHeroes.Add((hero.DisplayName, $"No .zip file found for set '{setName}'"));
                                continue;
                            }

                            if (hero.ItemIds == null || hero.ItemIds.Count == 0)
                            {
                                failedHeroes.Add((hero.DisplayName, "No item IDs defined"));
                                continue;
                            }

                            // Download & Extract set zip
                            string setFolder;
                            try
                            {
                                setFolder = await _downloader.DownloadAndExtractAsync(
                                    hero.Id, setName, zipUrl, log, ct, speedProgress).ConfigureAwait(false);
#if DEBUG
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] setFolder = {setFolder}");
#endif
                            }
                            catch (Exception ex)
                            {
                                failedHeroes.Add((hero.DisplayName, $"Download failed: {ex.Message}"));
                                continue;
                            }

                            // Merge set assets into extracted folder and track files
                            var (contentRoot, copiedFiles) = await MergeSetAssetsAsync(setFolder, extractDir, ct).ConfigureAwait(false);
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Merged {copiedFiles.Count} files from contentRoot: {contentRoot}");
#endif

                            // Parse index.txt and collect blocks for merged patching
                            var heroBlocks = _patcher.ParseIndexFile(contentRoot, hero.Id, hero.ItemIds);
                            if (heroBlocks != null)
                            {
                                foreach (var kvp in heroBlocks)
                                {
                                    mergedBlocks[kvp.Key] = kvp.Value;
                                }
                            }

                            // Add to extraction log
                            extractionLog.InstalledSets.Add(new HeroSetEntry
                            {
                                HeroId = hero.Id,
                                SetName = setName,
                                Files = copiedFiles
                            });

                            successfulHeroes.Add(hero.DisplayName);
                            
                            // Cleanup downloaded set folder to free memory
                            try
                            {
                                if (Directory.Exists(setFolder))
                                    Directory.Delete(setFolder, true);
                            }
                            catch { }
                        }
                        catch (Exception ex)
                        {
                            failedHeroes.Add((hero.DisplayName, ex.Message));
                            _logger?.Log($"Error processing {hero?.DisplayName}: {ex}");
                        }
                        
                        // Force GC after each hero to prevent memory buildup
                        if (i % 3 == 2) // Every 3 heroes
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    // Check if any heroes were successful
                    if (successfulHeroes.Count == 0)
                    {
                        var errors = string.Join(", ", failedHeroes.Select(f => $"{f.heroName}: {f.reason}"));
                        return Fail($"All heroes failed: {errors}", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    // Step 3: Apply all merged index blocks to items_game.txt (ONCE)
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

                    // Step 3.5: Download and apply localization patches (60-65%)
                    stageProgress?.Report((60, "Downloading assets"));
                    log("Downloading assets...");
                    var locSuccess = await _localizationPatcher.PatchLocalizationAsync(
                        extractDir, s => { }, ct).ConfigureAwait(false);
                    if (!locSuccess)
                    {
                        _logger?.Log("Warning: Some localization files failed to download");
                    }

                    ct.ThrowIfCancellationRequested();

                    // Step 4: Build VPK (65-80%)
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

                    // Step 4.5: Install VPK (80-90%)
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
                        
                        // Cleanup ArdysaSelectHero folder
                        var selectHeroTemp = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero");
                        if (Directory.Exists(selectHeroTemp))
                            Directory.Delete(selectHeroTemp, true);
                        
                        // Cleanup hero set cache folder (individual hero downloads)
                        var setsCache = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero", "cache", "sets");
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
                return new OperationResult { Success = false, Message = "Canceled by user." };
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

            // Check if assets exist directly in folder
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


    }
}