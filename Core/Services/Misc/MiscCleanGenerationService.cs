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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public sealed class MiscCleanGenerationService
    {
        private readonly IOriginalVpkProvider _originalProvider;
        private readonly AssetModifierService _modifier;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IGameItemsGameExtractor _itemsGameExtractor;
        private readonly IAppLogger? _logger;
        private readonly HttpClient _httpClient;

        private static string[] GameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi")
        };

        public MiscCleanGenerationService(
            IOriginalVpkProvider? originalProvider = null,
            AssetModifierService? modifier = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            IGameItemsGameExtractor? itemsGameExtractor = null,
            IAppLogger? logger = null)
        {
            logger ??= FileAppLogger.Instance;

            _originalProvider = originalProvider ?? new OriginalVpkService(logger: logger);
            _modifier = modifier ?? new AssetModifierService(null, logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _itemsGameExtractor = itemsGameExtractor ?? new GameItemsGameExtractorService(logger);
            _logger = logger;
            _httpClient = HttpClientProvider.Client;
        }

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
                    return Fail("VPK tool not found. Please ensure vpk.exe is in the application directory.", log);

                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), $"ArdysaMisc_{Guid.NewGuid():N}");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(buildDir);

                try
                {
                    log("Preparing base files...");
                    log("Downloading base files — first run can take a few minutes...");
                    string extractDir;
                    try
                    {
                        extractDir = await _originalProvider.GetExtractedOriginalAsync(log, ct, speedProgress, null).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return Fail($"Failed to get base files: {ex.Message}", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    if (!await _itemsGameExtractor.RefreshFromGameAsync(targetPath, extractDir, log, ct).ConfigureAwait(false))
                        return Fail("Could not read items_game.txt from your Dota 2 install. Re-run Detect and try again.", log);

                    ct.ThrowIfCancellationRequested();

                    string dummyVpkPath = Path.Combine(modsDir, "pak01_dir.vpk");
                    if (!await _modifier.ApplyModificationsAsync(dummyVpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Modification failed.", log);

                    ct.ThrowIfCancellationRequested();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    log("Building VPK...");
                    var newVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot, 
                        vpkLog => _logger?.Log($"[VPK] {vpkLog}"),
                        ct, speedProgress).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newVpkPath))
                    {
                        log("[VPK] Recompilation returned null - check logs above for details");
                        return Fail("VPK recompilation failed.", log);
                    }

                    ct.ThrowIfCancellationRequested();

                    log("Installing...");
                    var replaceSuccess = await _replacer.ReplaceAsync(
                        targetPath, newVpkPath, log, ct).ConfigureAwait(false);

                    if (!replaceSuccess)
                        return Fail("VPK replacement failed.", log);

                    ProtectedVpkStore.Clear(targetPath);

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

                    var warnings = new List<string>(_modifier.GetWarnings());
                    if (!patchSuccess)
                    {
                        _logger?.Log("Warning: Failed to patch signatures/gameinfo, but VPK was installed.");
                        warnings.Add("Could not update the game's signatures/gameinfo — mods may not load in-game. Try generating again.");
                    }
                    if (warnings.Count > 0)
                    {
                        log($"Completed with {warnings.Count} warning(s):");
                        foreach (var w in warnings)
                            log($"  ⚠ {w}");
                    }

                    log("Done!");
                    return new OperationResult
                    {
                        Success = true,
                        Message = warnings.Count > 0
                            ? $"Completed with {warnings.Count} warning(s). Some mods may not have been applied."
                            : "Miscellaneous mods generated successfully.",
                        Warnings = warnings.Count > 0 ? warnings : null
                    };
                }
                finally
                {
                    await CleanupAsync(tempRoot).ConfigureAwait(false);

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

                var selectHeroTemp = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero");
                if (Directory.Exists(selectHeroTemp))
                    await Task.Run(() => Directory.Delete(selectHeroTemp, true)).ConfigureAwait(false);

                var originalCache = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "original", "zip_contents");
                if (Directory.Exists(originalCache))
                    await Task.Run(() => Directory.Delete(originalCache, true)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Cleanup failed: {ex.Message}");
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

