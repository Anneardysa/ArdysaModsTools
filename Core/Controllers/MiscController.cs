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
    /// <summary>
    /// Controller for Misc mod generation.
    /// Routes generation requests to appropriate service based on mode.
    /// Includes conflict detection and resolution support.
    /// </summary>
    public class MiscController
    {
        private readonly MiscGenerationService _generationService;
        private readonly MiscCleanGenerationService _cleanGenerationService;
        private readonly MiscUtilityService _utilityService;
        private readonly IConflictDetector _conflictDetector;
        private readonly IConflictResolver _conflictResolver;
        private readonly IModPriorityService _priorityService;

        /// <summary>
        /// Creates a new MiscController with default service instances.
        /// </summary>
        public MiscController()
        {
            _generationService = new MiscGenerationService();
            _cleanGenerationService = new MiscCleanGenerationService();
            _utilityService = new MiscUtilityService();
            _conflictDetector = new ConflictDetector();
            _conflictResolver = new ConflictResolver();
            _priorityService = new ModPriorityService();
        }

        /// <summary>
        /// Creates a new MiscController with injected services.
        /// </summary>
        public MiscController(
            IConflictDetector conflictDetector,
            IConflictResolver conflictResolver,
            IModPriorityService priorityService)
        {
            _generationService = new MiscGenerationService();
            _cleanGenerationService = new MiscCleanGenerationService();
            _utilityService = new MiscUtilityService();
            _conflictDetector = conflictDetector ?? new ConflictDetector();
            _conflictResolver = conflictResolver ?? new ConflictResolver();
            _priorityService = priorityService ?? new ModPriorityService();
        }

        /// <summary>
        /// Generate Misc mods using the specified mode.
        /// </summary>
        /// <param name="targetPath">Dota 2 installation path</param>
        /// <param name="selections">Selected Misc options</param>
        /// <param name="mode">Generation mode (AddToCurrent or GenerateOnly)</param>
        /// <param name="log">Logging callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
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
                // Basic validation
                if (string.IsNullOrWhiteSpace(targetPath))
                    return new OperationResult { Success = false, Message = "Target path not set." };

                if (selections == null || selections.Count == 0)
                    return new OperationResult { Success = false, Message = "No selections provided." };

                log("Validating environment...");
                await Task.Delay(200, cancellationToken);

                // For AddToCurrent mode, verify existing VPK has valid structure
                if (mode == MiscGenerationMode.AddToCurrent)
                {
                    var verifyResult = await VerifyExistingVpkAsync(targetPath, log, cancellationToken, speedProgress);
                    if (!verifyResult.Success)
                    {
                        return verifyResult;
                    }
                }

                // ═══════════════════════════════════════════════════════════════
                // CONFLICT DETECTION & RESOLUTION
                // ═══════════════════════════════════════════════════════════════
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

                    // Check for critical conflicts that require user intervention
                    if (_conflictDetector.HasCriticalConflicts(conflicts))
                    {
                        var criticalConflicts = _conflictDetector.GetConflictsBySeverity(
                            conflicts, ConflictSeverity.Critical).ToList();
                        
                        log("Critical conflicts detected that require manual resolution.");
                        
                        // Return conflicts for UI handling
                        return OperationResult.NeedsConflictResolution(
                            criticalConflicts,
                            $"Critical conflict: {criticalConflicts.First().Description}\n\n" +
                            "Please resolve these conflicts to continue.");
                    }

                    // Auto-resolve non-critical conflicts
                    var config = await _priorityService.LoadConfigAsync(targetPath, cancellationToken);
                    resolutions = (await _conflictResolver.ResolveAllAsync(
                        conflicts, config, cancellationToken)).ToList();

                    foreach (var resolution in resolutions.Where(r => r.Success))
                    {
                        log($"  ✓ Resolved: {resolution.WinningSource?.ModName} (via {resolution.UsedStrategy})");
                    }
                }
                else
                {
                    log("No conflicts detected.");
                }

                // Route to appropriate service based on mode
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
                }

                // Perform silent cleanup on success
                if (result.Success)
                {
                    await _utilityService.CleanupTempFoldersAsync(targetPath, log);
                }

                return result;
            }
            catch (Exception ex)
            {
                log($"Controller Error: {ex.Message}");
                return new OperationResult { Success = false, Message = ex.Message, Exception = ex };
            }
        }

        /// <summary>
        /// Applies user's conflict resolution choices and marks conflicts as resolved.
        /// Call this after showing the conflict dialog, then retry GenerateModsAsync.
        /// </summary>
        /// <param name="conflicts">Conflicts that were presented to the user.</param>
        /// <param name="userChoices">User's selected resolution options (by conflict ID).</param>
        /// <param name="targetPath">Target path for priority config.</param>
        /// <param name="log">Logging callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<OperationResult> ApplyConflictResolutionsAsync(
            IEnumerable<ModConflict> conflicts,
            Dictionary<string, ConflictResolutionOption> userChoices,
            string targetPath,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var config = await _priorityService.LoadConfigAsync(targetPath, cancellationToken);
                int resolved = 0;

                foreach (var conflict in conflicts)
                {
                    if (userChoices.TryGetValue(conflict.Id, out var choice))
                    {
                        var result = await _conflictResolver.ApplyUserChoiceAsync(conflict, choice, cancellationToken);
                        if (result.Success)
                        {
                            resolved++;
                            log($"  ✓ Resolved: {conflict.Description} (via {result.UsedStrategy})");
                            
                            // Update priority config based on user's choice
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

                // Save updated config
                await _priorityService.SaveConfigAsync(config, targetPath, cancellationToken);
                _priorityService.InvalidateCache();

                return OperationResult.Ok($"Resolved {resolved} conflict(s)");
            }
            catch (Exception ex)
            {
                log($"Error applying resolutions: {ex.Message}");
                return OperationResult.Fail(ex);
            }
        }

        /// <summary>
        /// Verifies that the existing pak01_dir.vpk has the required folder structure.
        /// This ensures we can safely add mods to an existing VPK.
        /// </summary>
        private async Task<OperationResult> VerifyExistingVpkAsync(
            string targetPath,
            Action<string> log,
            CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            try
            {
                log("Verifying existing VPK structure...");
                
                // Normalize path and get VPK location
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

                // Extract to temp folder to check structure
                string tempCheckDir = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), 
                    $"ArdysaCheck_{Guid.NewGuid():N}");
                
                try
                {
                    System.IO.Directory.CreateDirectory(tempCheckDir);
                    
                    // Use HLExtract to check VPK contents
                    string hlExtractPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
                    
                    if (!System.IO.File.Exists(hlExtractPath))
                    {
                        return new OperationResult 
                        { 
                            Success = false, 
                            Message = "HLExtract.exe not found." 
                        };
                    }

                    // Extract only the structure check (scripts folder)
                    var extractor = new VpkExtractorService();
                    bool extracted = await extractor.ExtractAsync(
                        hlExtractPath, vpkPath, tempCheckDir, 
                        s => { }, // Silent log
                        ct,
                        speedProgress);

                    if (!extracted)
                    {
                        return new OperationResult 
                        { 
                            Success = false, 
                            Message = "Failed to read pak01_dir.vpk. File may be corrupted.\n\nPlease use 'Generate Only' mode instead." 
                        };
                    }

                    // Check for ArdysaMods signature file
                    // The VPK should have version/_ArdysaMods if created by this tool
                    string versionFile = System.IO.Path.Combine(tempCheckDir, "version", "_ArdysaMods");

                    if (!System.IO.File.Exists(versionFile))
                    {
                        log("VPK verification failed - missing version/_ArdysaMods");
                        return new OperationResult 
                        { 
                            Success = false, 
                            Message = "The existing pak01_dir.vpk was not created by ArdysaModsTools (missing version/_ArdysaMods).\n\n" +
                                     "This usually means:\n" +
                                     "• The VPK was not created by this tool\n" +
                                     "• The VPK is from vanilla Dota 2 or another mod tool\n\n" +
                                     "Please use 'Generate Only' mode instead to create a fresh mod VPK."
                        };
                    }

                    log("VPK structure verified.");
                    return new OperationResult { Success = true };
                }
                finally
                {
                    // Cleanup check directory
                    try
                    {
                        if (System.IO.Directory.Exists(tempCheckDir))
                        {
                            System.IO.Directory.Delete(tempCheckDir, true);
                        }
                    }
                    catch { /* Ignore cleanup errors */ }
                }
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

        /// <summary>
        /// Generate Misc mods using AddToCurrent mode (legacy overload for compatibility).
        /// </summary>
        public Task<OperationResult> GenerateModsAsync(
            string targetPath,
            Dictionary<string, string> selections,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            return GenerateModsAsync(targetPath, selections, MiscGenerationMode.AddToCurrent, log, cancellationToken);
        }

        /// <summary>
        /// Converts user selections dictionary to ModSource objects.
        /// </summary>
        /// <param name="selections">Dictionary of category -> selected option.</param>
        /// <returns>List of ModSource objects representing the selections.</returns>
        private static List<ModSource> GetModSourcesFromSelections(Dictionary<string, string> selections)
        {
            var sources = new List<ModSource>();
            
            foreach (var (category, selection) in selections)
            {
                if (string.IsNullOrEmpty(selection) || 
                    selection.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip default/empty selections
                }

                sources.Add(ModSource.FromSelection(category, selection));
            }

            return sources;
        }
    }
}

