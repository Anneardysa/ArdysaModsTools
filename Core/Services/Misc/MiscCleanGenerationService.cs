using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Clean generation service for Misc mods.
    /// Downloads Original.zip and creates a fresh VPK with only the selected Misc options.
    /// Similar to HeroGenerationService but for Misc mods.
    /// </summary>
    public sealed class MiscCleanGenerationService
    {
        private readonly IOriginalVpkProvider _originalProvider;
        private readonly AssetModifierService _modifier;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly ILogger? _logger;
        private readonly HttpClient _httpClient;

        // GameInfo URLs - loaded from environment configuration
        private static string[] GameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi")
        };

        public MiscCleanGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            AssetModifierService? modifier = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            ILogger? logger = null)
        {
            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _modifier = modifier ?? new AssetModifierService(null, logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _logger = logger;
            _httpClient = HttpClientProvider.Client;
        }

        /// <summary>
        /// Generate clean Misc mods VPK from Original.zip.
        /// Flow: Download Original.zip → Extract → Apply Misc → Recompile → Replace → Patch signatures
        /// </summary>
        public async Task<OperationResult> GenerateCleanAsync(
            string targetPath,
            Dictionary<string, string> selections,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(targetPath))
                    return Fail("No target path set.", log);

                if (selections == null || selections.Count == 0)
                    return Fail("No selections provided.", log);

                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string vpkToolPath = Path.Combine(baseDir, "vpk.exe");

                if (!File.Exists(vpkToolPath))
                    return Fail("vpk.exe not found.", log);

                // Create temp work folder
                string tempRoot = Path.Combine(Path.GetTempPath(), $"ArdysaMisc_{Guid.NewGuid():N}");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(buildDir);

                try
                {
                    log("Preparing base files...");
                    string extractDir;
                    try
                    {
                        // Use silent logger for OriginalVpkService to suppress verbose messages
                        extractDir = await _originalProvider.GetExtractedOriginalAsync(_ => { }, ct, speedProgress, null).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return Fail($"Failed to get base files: {ex.Message}", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    // Force GC after large extraction
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Note: We pass empty string for vpkPath since we're using extractDir directly
                    if (!await _modifier.ApplyModificationsAsync(string.Empty, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Modification failed.", log);

                    ct.ThrowIfCancellationRequested();

                    // Force GC after modifications
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    log("Building VPK...");
                    var newVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot, 
                        vpkLog => log($"[VPK] {vpkLog}"), // Capture VPK diagnostics
                        ct, speedProgress).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newVpkPath))
                    {
                        log("[VPK] Recompilation returned null - check logs above for details");
                        return Fail("VPK recompilation failed.", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    log("Installing...");
                    var replaceSuccess = await _replacer.ReplaceAsync(
                        targetPath, newVpkPath, _ => { }, ct).ConfigureAwait(false);

                    if (!replaceSuccess)
                        return Fail("VPK replacement failed.", log);

                    log("Finalizing...");
                    var extractionLog = new MiscExtractionLog
                    {
                        GeneratedAt = DateTime.UtcNow,
                        Mode = "GenerateOnly",
                        Selections = new Dictionary<string, string>(selections)
                    };
                    foreach (var kvp in _modifier.GetInstalledFiles())
                    {
                        extractionLog.AddFiles(kvp.Key, kvp.Value);
                    }
                    extractionLog.Save(targetPath);

                    var patchSuccess = await PatchSignaturesAndGameInfoAsync(targetPath, ct).ConfigureAwait(false);
                    if (!patchSuccess)
                    {
                        _logger?.Log("Warning: Failed to patch signatures/gameinfo, but VPK was installed.");
                    }

                    log("Done!");
                    return new OperationResult 
                    { 
                        Success = true, 
                        Message = "Miscellaneous mods generated successfully." 
                    };
                }
                finally
                {
                    await CleanupAsync(tempRoot).ConfigureAwait(false);

                    // Force garbage collection with LOH compaction
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
                _logger?.Log($"MiscCleanGenerationService error: {ex}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        private static OperationResult Fail(string message, Action<string> log)
        {
            log($"Error: {message}");
            return new OperationResult { Success = false, Message = message };
        }

        private async Task CleanupAsync(string tempRoot)
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    await Task.Run(() => Directory.Delete(tempRoot, true)).ConfigureAwait(false);

                // Cleanup ArdysaSelectHero temp folder (used by OriginalVpkService)
                var selectHeroTemp = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero");
                if (Directory.Exists(selectHeroTemp))
                    await Task.Run(() => Directory.Delete(selectHeroTemp, true)).ConfigureAwait(false);

                // Cleanup Original cache zip_contents folder (now in TEMP)
                var originalCache = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero", "cache", "original", "zip_contents");
                if (Directory.Exists(originalCache))
                    await Task.Run(() => Directory.Delete(originalCache, true)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Patches dota.signatures and downloads gameinfo_branchspecific.gi.
        /// </summary>
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

                // Patch signatures file
                string[] lines = await File.ReadAllLinesAsync(signaturesPath, ct).ConfigureAwait(false);
                int digestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:"));
                if (digestIndex < 0)
                {
                    _logger?.Log("Core file format invalid.");
                    return false;
                }

                var modified = new List<string>(lines[..(digestIndex + 1)])
                {
                    @"..\..\..\..\..\dota\gameinfo_branchspecific.gi~SHA1:1A9B91FB43FE89AD104B8001282D292EED94584D;CRC:043F604A"
                };

                string tmpSig = signaturesPath + ".tmp";
                await File.WriteAllLinesAsync(tmpSig, modified, ct).ConfigureAwait(false);
                File.Replace(tmpSig, signaturesPath, null);

                // Download and write gameinfo
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
