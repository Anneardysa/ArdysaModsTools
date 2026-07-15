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

namespace ArdysaModsTools.Core.Services
{
    public class MiscGenerationService
    {
        private readonly IVpkExtractor _extractor;
        private readonly AssetModifierService _modifier;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IAppLogger? _logger;

        public MiscGenerationService(
            IVpkExtractor? extractor = null,
            AssetModifierService? modifier = null,
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
                if (!File.Exists(vpkPath))
                    return Fail($"VPK file not found at: {vpkPath}", log, ErrorCodes.VPK_FILE_NOT_FOUND);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
                string vpkToolPath = Path.Combine(baseDir, "vpk.exe");
                if (!File.Exists(hlExtractPath) || !File.Exists(vpkToolPath))
                    return Fail("Missing required tools (HLExtract.exe / vpk.exe).", log, ErrorCodes.VPK_TOOL_NOT_FOUND);

                string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), $"ArdysaMods_{Guid.NewGuid():N}");
                string extractDir = Path.Combine(tempRoot, "extract");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);

                try
                {
                    log("Extracting game files...");
                    if (!await _extractor.ExtractAsync(hlExtractPath, vpkPath, extractDir, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail(
                            "Could not read your existing mod package — it looks incomplete or corrupted.",
                            log,
                            ErrorCodes.VPK_EXTRACT_FAILED);

                    ct.ThrowIfCancellationRequested();

                    var previousLog = MiscExtractionLog.Load(targetPath);
                    _modifier.SetPreviousLog(previousLog);

                    if (!await _modifier.ApplyModificationsAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Modification failed.", log, ErrorCodes.MISC_APPLY_FAILED);

                    ct.ThrowIfCancellationRequested();

                    log("Building...");
                    string? newVpk = await _recompiler.RecompileAsync(
                        vpkToolPath, extractDir, buildDir, tempRoot,
                        vpkLog => _logger?.LogDebug($"[VPK] {vpkLog}"),
                        ct, speedProgress).ConfigureAwait(false);
                    if (newVpk == null)
                        return Fail("Could not rebuild the mod package.", log, ErrorCodes.VPK_RECOMPILE_FAILED);

                    ct.ThrowIfCancellationRequested();

                    log("Installing...");
                    bool wasHidden = false;
                    try
                    {
                        var attrs = File.GetAttributes(vpkPath);
                        wasHidden = attrs.HasFlag(FileAttributes.Hidden) && attrs.HasFlag(FileAttributes.System);
                    }
                    catch {  }

                    if (!await _replacer.ReplaceAsync(targetPath, newVpk, log, ct, hideOutput: wasHidden).ConfigureAwait(false))
                        return Fail("Could not install the rebuilt mod package.", log, ErrorCodes.VPK_REPLACE_FAILED);

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

                    await CleanupAsync(tempRoot, log).ConfigureAwait(false);

                    var warnings = _modifier.GetWarnings();
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
                    
                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
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
    }
}

