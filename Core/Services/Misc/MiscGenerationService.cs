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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Orchestrator service for misc mod generation.
    /// Coordinates extraction, modification, recompilation, and replacement steps.
    /// Delegates actual work to focused services for single responsibility.
    /// </summary>
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
            _extractor = extractor ?? new VpkExtractorService(logger);
            _modifier = modifier ?? new AssetModifierService(null, logger);
            _recompiler = recompiler ?? new VpkRecompilerService(logger);
            _replacer = replacer ?? new VpkReplacerService(logger);
            _logger = logger;
        }

        /// <summary>
        /// Performs the full mod generation pipeline:
        /// 1. Extract VPK
        /// 2. Apply modifications
        /// 3. Recompile VPK
        /// 4. Replace original
        /// 5. Cleanup temp files
        /// </summary>
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
                    return Fail($"VPK file not found at: {vpkPath}", log);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
                string vpkToolPath = Path.Combine(baseDir, "vpk.exe");
                if (!File.Exists(hlExtractPath) || !File.Exists(vpkToolPath))
                    return Fail("Missing required tools (HLExtract.exe / vpk.exe).", log);

                // Create temp directories
                string tempRoot = Path.Combine(Path.GetTempPath(), $"ArdysaMods_{Guid.NewGuid():N}");
                string extractDir = Path.Combine(tempRoot, "extract");
                string buildDir = Path.Combine(tempRoot, "build");
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);

                try
                {
                    log("Extracting game files...");
                    if (!await _extractor.ExtractAsync(hlExtractPath, vpkPath, extractDir, _ => { }, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Extraction failed.", log);

                    ct.ThrowIfCancellationRequested();

                    // Load previous extraction log for cleanup
                    var previousLog = MiscExtractionLog.Load(targetPath);
                    _modifier.SetPreviousLog(previousLog);

                    if (!await _modifier.ApplyModificationsAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false))
                        return Fail("Modification failed.", log);

                    ct.ThrowIfCancellationRequested();

                    log("Building...");
                    string? newVpk = await _recompiler.RecompileAsync(vpkToolPath, extractDir, buildDir, tempRoot, _ => { }, ct, speedProgress).ConfigureAwait(false);
                    if (newVpk == null)
                        return Fail("Recompile failed: no output.", log);

                    await Task.Delay(2000, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    log("Installing...");
                    if (!await _replacer.ReplaceAsync(targetPath, newVpk, _ => { }, ct).ConfigureAwait(false))
                        return Fail("Replacement failed.", log);

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

                    log("Done!");
                    return new OperationResult { Success = true, Message = "All mods successfully applied." };
                }

                finally
                {
                    // Ensure cleanup on any exit
                    await CleanupAsync(tempRoot, log).ConfigureAwait(false);
                    
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
                _logger?.Log("Operation canceled.");
                return new OperationResult { Success = false, Message = "Canceled by user." };
            }
            catch (Exception ex)
            {
                log($"Error: {ex.Message}");
                _logger?.Log($"PerformGenerationAsync exception: {ex}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        private static OperationResult Fail(string message, Action<string> log)
        {
            log($"Error: {message}");
            return new OperationResult { Success = false, Message = message };
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

