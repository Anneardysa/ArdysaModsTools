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
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    public class MiscGenerationService
    {
        private readonly IVpkExtractor _extractor;
        private readonly IAssetModifier _modifier;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IAppLogger? _logger;
        private readonly HttpClient _httpClient;

        private static string[] GameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi")
        };

        public MiscGenerationService(
            IVpkExtractor? extractor = null,
            IAssetModifier? modifier = null,
            IVpkRecompiler? recompiler = null,
            IVpkReplacer? replacer = null,
            IAppLogger? logger = null)
        {
            logger ??= FileAppLogger.Instance;

            _extractor = extractor ?? new VpkExtractorService(logger);
            _modifier = modifier ?? new AssetModifierService(null, logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _logger = logger;
            _httpClient = HttpClientProvider.Client;
        }

        public async Task<OperationResult> PerformGenerationAsync(
            string targetPath,
            Dictionary<string, string> selections,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(targetPath))
                    return Fail("No target path set.", log);

                targetPath = PathUtility.NormalizeTargetPath(targetPath);
                string vpkPath = PathUtility.GetVpkPath(targetPath);
                string protectedVpkPath = ProtectedVpkStore.VpkPath(targetPath);

                if (!File.Exists(vpkPath))
                    return Fail($"VPK file not found at: {vpkPath}", log, ErrorCodes.VPK_FILE_NOT_FOUND);

                var packageBeforeRebuild = ProtectedVpkStore.GetActiveModVpkStamp(targetPath);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
                string vpkToolPath = Path.Combine(baseDir, "vpk.exe");
                if (!File.Exists(hlExtractPath) || !File.Exists(vpkToolPath))
                    return Fail("Missing required tools (HLExtract.exe / vpk.exe).", log, ErrorCodes.VPK_TOOL_NOT_FOUND);

                string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), $"ArdysaMods_{Guid.NewGuid():N}");
                string extractDir = Path.Combine(tempRoot, "extract");
                string buildDir = Path.Combine(tempRoot, "build");
                string protectedDir = Path.Combine(tempRoot, "protected");
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);
                Directory.CreateDirectory(protectedDir);

                try
                {
                    log("Extracting game files...");

                    if (!await _extractor.ExtractAsync(hlExtractPath, vpkPath, extractDir, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail(
                            "Could not read your existing mod package — it looks incomplete or corrupted.",
                            log,
                            ErrorCodes.VPK_EXTRACT_FAILED);

                    ct.ThrowIfCancellationRequested();

                    bool hadExistingProtected = File.Exists(protectedVpkPath);
                    if (hadExistingProtected)
                    {
                        try
                        {
                            await _extractor.ExtractAsync(hlExtractPath, protectedVpkPath, protectedDir, _ => { }, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug($"Could not extract existing protected package: {ex.Message}");
                        }
                    }

                    var previousLog = MiscExtractionLog.Load(targetPath);
                    _modifier.SetPreviousLog(previousLog);

                    if (hadExistingProtected && previousLog != null)
                    {
                        foreach (var kvp in previousLog.InstalledFiles)
                        {
                            foreach (var rel in kvp.Value)
                            {
                                try
                                {
                                    string p = Path.Combine(protectedDir, rel.Replace('/', Path.DirectorySeparatorChar));
                                    if (File.Exists(p)) File.Delete(p);
                                }
                                catch { }
                            }
                        }
                    }

                    if (!await _modifier.ApplyModificationsAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Modification failed.", log, ErrorCodes.MISC_APPLY_FAILED);

                    ct.ThrowIfCancellationRequested();

                    int protectedMoved = 0;
                    var protectedPaths = _modifier.GetProtectedPaths();
                    if (protectedPaths.Count > 0)
                    {
                        ProtectedVpkStore.Ensure(targetPath);
                        protectedMoved = ProtectedVpkStore.MoveProtected(
                            extractDir, protectedDir, protectedPaths, _logger, ct);
                    }

                    if (protectedMoved > 0)
                    {
                        _logger?.LogDebug($"Protected split: {protectedMoved} file(s) moved out of the main package into game/mod.");
                    }

                    log("Building...");
                    string? newVpk = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot,
                        vpkLog => _logger?.LogDebug($"[VPK] {vpkLog}"),
                        ct, speedProgress).ConfigureAwait(false);
                    if (newVpk == null)
                        return Fail("Could not rebuild the mod package.", log, ErrorCodes.VPK_RECOMPILE_FAILED);

                    ct.ThrowIfCancellationRequested();

                    string? newProtectedVpk = null;
                    bool hasProtectedFiles = Directory.Exists(protectedDir) && Directory.EnumerateFiles(protectedDir, "*", SearchOption.AllDirectories).Any();
                    if (hasProtectedFiles)
                    {
                        newProtectedVpk = await _recompiler.RecompileAsync(
                            vpkToolPath, protectedDir, buildDir, tempRoot,
                            vpkLog => _logger?.LogDebug($"[VPK] {vpkLog}"),
                            ct, speedProgress).ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(newProtectedVpk) ||
                            string.Equals(newProtectedVpk, newVpk, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogDebug("[VPK] Protected package build returned null or collided with main package.");
                            return Fail("Could not rebuild the protected mod package.", log, ErrorCodes.VPK_RECOMPILE_FAILED);
                        }
                    }

                    ct.ThrowIfCancellationRequested();

                    log("Installing...");
                    if (!await _replacer.ReplaceAsync(targetPath, newVpk, log, ct).ConfigureAwait(false))
                        return Fail("Could not install the rebuilt mod package.", log, ErrorCodes.VPK_REPLACE_FAILED);

                    await ItemsGameBaselineStore.RebindAndMergePatchedIdsAsync(targetPath, packageBeforeRebuild, _modifier.GetModifiedItemIds(), _modifier.GetUnpatchedItemIds(), ct).ConfigureAwait(false);

                    if (!await ProtectedVpkStore.DeployAsync(
                            targetPath, newProtectedVpk, log, CancellationToken.None, _logger).ConfigureAwait(false))
                        return Fail("Could not install the rebuilt protected mod package.", log, ErrorCodes.VPK_REPLACE_FAILED);

                    log("Finalizing...");
                    var extractionLog = new MiscExtractionLog
                    {
                        GeneratedAt = DateTime.UtcNow,
                        Mode = "AddToCurrent",
                        Selections = new Dictionary<string, string>(selections)
                    };
                    foreach (var kvp in _modifier.GetInstalledFiles())
                    {
                        extractionLog.AddFiles(kvp.Key, kvp.Value);
                    }
                    extractionLog.Save(targetPath);

                    var patchSuccess = await PatchSignaturesAndGameInfoAsync(targetPath, ct).ConfigureAwait(false);

                    await CleanupAsync(tempRoot, log).ConfigureAwait(false);

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
                    var message = warnings.Count > 0
                        ? $"Completed with {warnings.Count} warning(s). Some mods may not have been applied."
                        : "All mods successfully applied.";
                    return new OperationResult 
                    { 
                        Success = true, 
                        Message = message, 
                        Warnings = warnings.Count > 0 ? new List<string>(warnings) : null 
                    };
                }

                finally
                {
                    await CleanupAsync(tempRoot, log).ConfigureAwait(false);
                    
                    Core.Helpers.LargeWorkMemory.Release();
                }
            }
            catch (OperationCanceledException)
            {
                log("Operation canceled.");
                _logger?.Log("Operation canceled.");
                return OperationResult.Canceled();
            }
            catch (Exception ex)
            {
                log($"Error: {ex.Message}");
                _logger?.LogError($"[{ErrorCodes.MISC_GEN_FAILED}] PerformGenerationAsync exception", ex);
                return new OperationResult
                {
                    Success = false,
                    Message = ex.Message,
                    Exception = ex,
                    ErrorCode = ErrorCodes.MISC_GEN_FAILED
                };
            }
        }

        private OperationResult Fail(string message, Action<string> log, string? errorCode = null)
        {
            log($"Error: {message}");
            _logger?.LogError($"[{errorCode ?? ErrorCodes.MISC_GEN_FAILED}] Misc AddToCurrent failed: {message}");
            return new OperationResult { Success = false, Message = message, ErrorCode = errorCode };
        }

        private async Task CleanupAsync(string tempRoot, Action<string> log)
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    await Task.Run(() => Directory.Delete(tempRoot, true)).ConfigureAwait(false);
                }
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
                    _logger?.Log($"Failed to download patch files: {lastError?.Message}");
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

