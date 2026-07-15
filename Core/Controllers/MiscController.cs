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
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Conflict;
using ArdysaModsTools.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Controllers
{
    public class MiscController
    {
        private readonly MiscGenerationService _generationService;
        private readonly MiscCleanGenerationService _cleanGenerationService;
        private readonly MiscUtilityService _utilityService;
        private readonly IConflictDetector _conflictDetector;
        private readonly IConflictResolver _conflictResolver;
        private readonly IModPriorityService _priorityService;
        private readonly IModInstallerService _modInstaller;
        private readonly IAppLogger _logger;

        public MiscController(IAppLogger? logger = null)
        {
            _logger = logger ?? FileAppLogger.Instance;
            _generationService = new MiscGenerationService(logger: _logger);
            _cleanGenerationService = new MiscCleanGenerationService(logger: _logger);
            _utilityService = new MiscUtilityService();
            _conflictDetector = new ConflictDetector();
            _conflictResolver = new ConflictResolver();
            _priorityService = new ModPriorityService();
            _modInstaller = new ModInstallerService();
        }

        public MiscController(
            IConflictDetector conflictDetector,
            IConflictResolver conflictResolver,
            IModPriorityService priorityService,
            IModInstallerService? modInstaller = null,
            IAppLogger? logger = null)
        {
            _logger = logger ?? FileAppLogger.Instance;
            _generationService = new MiscGenerationService(logger: _logger);
            _cleanGenerationService = new MiscCleanGenerationService(logger: _logger);
            _utilityService = new MiscUtilityService();
            _conflictDetector = conflictDetector ?? new ConflictDetector();
            _conflictResolver = conflictResolver ?? new ConflictResolver();
            _priorityService = priorityService ?? new ModPriorityService();
            _modInstaller = modInstaller ?? new ModInstallerService();
        }

        public async Task<OperationResult> GenerateModsAsync(
            string targetPath,
            Dictionary<string, string> selections,
            MiscGenerationMode mode,
            Action<string> log,
            CancellationToken cancellationToken = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                    return new OperationResult { Success = false, Message = "Target path not set." };

                if (selections == null || selections.Count == 0)
                    return new OperationResult { Success = false, Message = "No selections provided." };

                log("Validating environment...");

                if (mode == MiscGenerationMode.AddToCurrent)
                {
                    var verifyResult = await VerifyExistingVpkAsync(targetPath, log, cancellationToken);
                    if (!verifyResult.Success)
                    {
                        return verifyResult;
                    }
                }

                var modSources = GetModSourcesFromSelections(selections);
                var prioritizedSources = await _priorityService.ApplyPrioritiesAsync(
                    modSources, targetPath, cancellationToken);

                log("Checking for mod conflicts...");
                var conflicts = await _conflictDetector.DetectConflictsAsync(
                    prioritizedSources, targetPath, cancellationToken);

                List<ConflictResolutionResult> resolutions = new();
                if (conflicts.Count > 0)
                {
                    log($"Detected {conflicts.Count} conflict(s). Resolving...");

                    if (_conflictDetector.HasCriticalConflicts(conflicts))
                    {
                        var criticalConflicts = _conflictDetector.GetConflictsBySeverity(
                            conflicts, ConflictSeverity.Critical).ToList();
                        
                        log("Critical conflicts detected that require manual resolution.");
                        
                        return OperationResult.NeedsConflictResolution(
                            criticalConflicts,
                            $"Critical conflict: {criticalConflicts.First().Description}\n\n" +
                            "Please resolve these conflicts to continue.");
                    }

                    var config = await _priorityService.LoadConfigAsync(targetPath, cancellationToken);
                    resolutions = (await _conflictResolver.ResolveAllAsync(
                        conflicts, config, cancellationToken)).ToList();

                    foreach (var resolution in resolutions.Where(r => r.Success))
                    {
                        log($"  ✓ Resolved: {resolution.WinningSource?.ModName} (via {resolution.UsedStrategy})");
                    }

                    selections = ApplyResolutionsToSelections(selections, conflicts, resolutions, log);
                }
                else
                {
                    log("No conflicts detected.");
                }

                OperationResult result;
                if (mode == MiscGenerationMode.GenerateOnly)
                {
                    log("Mode: Generate Only Miscellaneous Mods");
                    result = await _cleanGenerationService.GenerateCleanAsync(
                        targetPath,
                        selections,
                        log,
                        cancellationToken,
                        speedProgress);
                }
                else
                {
                    log("Mode: Add to Current Mods");
                    result = await _generationService.PerformGenerationAsync(
                        targetPath,
                        selections,
                        log,
                        cancellationToken,
                        speedProgress);

                    if (ShouldRebuildClean(result))
                        result = await RebuildCleanAsync(targetPath, selections, log, cancellationToken, speedProgress);
                }

                if (result.Success)
                {
                    await _utilityService.CleanupTempFoldersAsync(targetPath, log);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                log("Operation canceled.");
                return OperationResult.Canceled();
            }
            catch (Exception ex)
            {
                log($"Controller Error: {ex.Message}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        internal static bool ShouldRebuildClean(OperationResult result) =>
            !result.Success
            && !result.WasCanceled
            && !result.RequiresConflictResolution
            && result.ErrorCode == ErrorCodes.VPK_EXTRACT_FAILED;

        private async Task<OperationResult> RebuildCleanAsync(
            string targetPath,
            Dictionary<string, string> selections,
            Action<string> log,
            CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress)
        {
            _logger.LogWarning($"[{ErrorCodes.VPK_EXTRACT_FAILED}] Existing package unreadable — self-healing via clean rebuild.");
            log("Your existing mod package could not be read. Rebuilding it from a clean base...");

            var rebuilt = await _cleanGenerationService.GenerateCleanAsync(
                targetPath, selections, log, ct, speedProgress);

            if (!rebuilt.Success)
            {
                _logger.LogError($"[{ErrorCodes.MISC_GEN_FAILED}] Clean rebuild also failed: {rebuilt.Message}");
                return rebuilt;
            }

            var warnings = new List<string>(rebuilt.Warnings ?? Enumerable.Empty<string>())
            {
                "Your previous mod package was damaged and could not be read, so it was rebuilt from scratch. " +
                "Your Miscellaneous selections were applied — but any hero sets you had installed need to be applied again."
            };

            _logger.Log("Self-heal succeeded: package rebuilt from clean base.");

            return new OperationResult
            {
                Success = true,
                SuccessCount = rebuilt.SuccessCount,
                Message = rebuilt.Message,
                Warnings = warnings
            };
        }

        public async Task<(OperationResult Result, Dictionary<string, string> AdjustedSelections)> ApplyConflictResolutionsAsync(
            IEnumerable<ModConflict> conflicts,
            Dictionary<string, ConflictResolutionOption> userChoices,
            Dictionary<string, string> selections,
            string targetPath,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var conflictList = conflicts as IReadOnlyList<ModConflict> ?? conflicts.ToList();
                var config = await _priorityService.LoadConfigAsync(targetPath, cancellationToken);
                var results = new List<ConflictResolutionResult>();
                int resolved = 0;

                foreach (var conflict in conflictList)
                {
                    if (userChoices.TryGetValue(conflict.Id, out var choice))
                    {
                        var result = await _conflictResolver.ApplyUserChoiceAsync(conflict, choice, cancellationToken);
                        results.Add(result);
                        if (result.Success)
                        {
                            resolved++;
                            log($"  ✓ Resolved: {conflict.Description} (via {result.UsedStrategy})");

                            if (result.WinningSource != null)
                            {
                                config.SetPriority(
                                    result.WinningSource.ModId,
                                    result.WinningSource.ModName ?? result.WinningSource.ModId,
                                    result.WinningSource.Category ?? "Unknown",
                                    result.WinningSource.Priority);
                            }
                        }
                        else
                        {
                            log($"  ✗ Failed to resolve: {conflict.Description}");
                        }
                    }
                }

                await _priorityService.SaveConfigAsync(config, targetPath, cancellationToken);
                _priorityService.InvalidateCache();

                var adjusted = ApplyResolutionsToSelections(selections, conflictList, results, log);

                return (OperationResult.Ok($"Resolved {resolved} conflict(s)"), adjusted);
            }
            catch (Exception ex)
            {
                log($"Error applying resolutions: {ex.Message}");
                return (OperationResult.Fail(ex), selections);
            }
        }

        private static Dictionary<string, string> ApplyResolutionsToSelections(
            Dictionary<string, string> selections,
            IReadOnlyList<ModConflict> conflicts,
            IEnumerable<ConflictResolutionResult> resolutions,
            Action<string> log)
        {
            var conflictsById = conflicts.ToDictionary(c => c.Id);
            var winners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var losers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resolution in resolutions.Where(r => r.Success && r.WinningSource != null))
            {
                winners.Add(resolution.WinningSource!.ModId);

                if (!conflictsById.TryGetValue(resolution.ConflictId, out var conflict))
                    continue;

                foreach (var source in conflict.ConflictingSources)
                {
                    if (!string.Equals(source.ModId, resolution.WinningSource.ModId, StringComparison.OrdinalIgnoreCase))
                        losers.Add(source.ModId);
                }
            }

            losers.ExceptWith(winners);
            if (losers.Count == 0)
                return selections;

            var adjusted = new Dictionary<string, string>(selections, StringComparer.OrdinalIgnoreCase);
            foreach (var (category, selection) in selections)
            {
                if (string.IsNullOrEmpty(selection))
                    continue;

                var modId = ModSource.FromSelection(category, selection).ModId;
                if (losers.Contains(modId))
                {
                    adjusted.Remove(category);
                    log($"  ↪ Excluded '{selection}' ({category}) — lost conflict resolution.");
                }
            }

            return adjusted;
        }

        private async Task<OperationResult> VerifyExistingVpkAsync(
            string targetPath,
            Action<string> log,
            CancellationToken ct)
        {
            try
            {
                log("Verifying existing VPK structure...");

                targetPath = PathUtility.NormalizeTargetPath(targetPath);
                string vpkPath = PathUtility.GetVpkPath(targetPath);

                if (!System.IO.File.Exists(vpkPath))
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = "pak01_dir.vpk not found.\n\nPlease use 'Generate Only' mode or install mods first using the main installer."
                    };
                }

                string hlExtractPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
                if (!System.IO.File.Exists(hlExtractPath))
                {
                    return new OperationResult { Success = false, Message = "HLExtract.exe not found." };
                }

                var (isValid, _) = await _modInstaller.ValidateVpkAsync(vpkPath, ct).ConfigureAwait(false);

                if (!isValid)
                {
                    log("VPK verification failed - origin marker not found");
                    return new OperationResult
                    {
                        Success = false,
                        Message = "The existing pak01_dir.vpk was not created by ArdysaModsTools.\n\n" +
                                 "This usually means:\n" +
                                 "• The VPK was not created by this tool\n" +
                                 "• The VPK is from vanilla Dota 2 or another mod tool\n\n" +
                                 "Please use 'Generate Only' mode instead to create a fresh mod VPK."
                    };
                }

                log("VPK structure verified.");
                return new OperationResult { Success = true };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log($"VPK verification failed: {ex.Message}");
                return new OperationResult
                {
                    Success = false,
                    Message = $"Failed to verify VPK: {ex.Message}\n\nPlease use 'Generate Only' mode instead."
                };
            }
        }

        public Task<OperationResult> GenerateModsAsync(
            string targetPath,
            Dictionary<string, string> selections,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            return GenerateModsAsync(targetPath, selections, MiscGenerationMode.AddToCurrent, log, cancellationToken);
        }

        private static List<ModSource> GetModSourcesFromSelections(Dictionary<string, string> selections)
        {
            var sources = new List<ModSource>();
            
            foreach (var (category, selection) in selections)
            {
                if (string.IsNullOrEmpty(selection) || 
                    selection.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sources.Add(ModSource.FromSelection(category, selection));
            }

            return sources;
        }
    }
}

