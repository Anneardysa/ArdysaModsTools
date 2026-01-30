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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Conflict
{
    /// <summary>
    /// Service for resolving conflicts between mods using various strategies.
    /// </summary>
    /// <remarks>
    /// Resolution strategies:
    /// - HigherPriority: Mod with lower priority number wins
    /// - MostRecent: Last applied mod wins
    /// - Merge: Attempt to combine changes (for scripts/configs)
    /// - KeepExisting: Preserve current mod state
    /// - UseNew: Apply new mod, discard existing
    /// - Interactive: Require user choice
    /// </remarks>
    public class ConflictResolver : IConflictResolver
    {
        private readonly IAppLogger? _logger;

        /// <summary>
        /// Creates a new ConflictResolver instance.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ConflictResolver(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ConflictResolutionResult> ResolveAsync(
            ModConflict conflict,
            ResolutionStrategy strategy,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            _logger?.Log($"ConflictResolver: Resolving '{conflict.Id}' using {strategy}");

            try
            {
                var winner = DetermineWinner(conflict, strategy);

                if (winner == null && strategy == ResolutionStrategy.Interactive)
                {
                    return ConflictResolutionResult.Failed(
                        conflict.Id,
                        strategy,
                        "Interactive resolution requires user choice.");
                }

                if (winner == null && strategy == ResolutionStrategy.Merge)
                {
                    return await TryMergeAsync(conflict, ct);
                }

                if (winner == null)
                {
                    return ConflictResolutionResult.Failed(
                        conflict.Id,
                        strategy,
                        "Could not determine winning mod source.");
                }

                // Mark the conflict as resolved
                var selectedOption = conflict.AvailableResolutions
                    .FirstOrDefault(r => r.PreferredSource?.ModId == winner.ModId);
                
                if (selectedOption != null)
                {
                    conflict.SelectedResolution = selectedOption;
                }

                return ConflictResolutionResult.Successful(
                    conflict.Id,
                    strategy,
                    winner,
                    conflict.AffectedFiles);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"ConflictResolver: Error resolving conflict: {ex.Message}");
                return ConflictResolutionResult.Failed(
                    conflict.Id,
                    strategy,
                    ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ConflictResolutionResult>> ResolveAllAsync(
            IEnumerable<ModConflict> conflicts,
            ModPriorityConfig config,
            CancellationToken ct = default)
        {
            var results = new List<ConflictResolutionResult>();
            var conflictList = conflicts.ToList();

            _logger?.Log($"ConflictResolver: Resolving {conflictList.Count} conflict(s)...");

            foreach (var conflict in conflictList)
            {
                ct.ThrowIfCancellationRequested();

                // Check if we can auto-resolve
                if (!CanAutoResolve(conflict, config))
                {
                    _logger?.Log($"ConflictResolver: Conflict '{conflict.Id}' requires user intervention.");
                    results.Add(ConflictResolutionResult.Failed(
                        conflict.Id,
                        ResolutionStrategy.Interactive,
                        "Conflict requires user intervention."));
                    continue;
                }

                // Determine the appropriate strategy
                var strategy = GetStrategyForConflict(conflict, config);

                var result = await ResolveAsync(conflict, strategy, ct);
                results.Add(result);

                if (result.Success)
                {
                    _logger?.Log($"ConflictResolver: Resolved '{conflict.Id}' → {result.WinningSource?.ModName}");
                }
                else
                {
                    _logger?.LogWarning($"ConflictResolver: Failed to resolve '{conflict.Id}': {result.ErrorMessage}");
                }
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<ConflictResolutionResult> TryMergeAsync(
            ModConflict conflict,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            _logger?.Log($"ConflictResolver: Attempting merge for '{conflict.Id}'");

            // Merge is only supported for Script and Configuration conflicts
            if (conflict.Type != ConflictType.Script && conflict.Type != ConflictType.Configuration)
            {
                _logger?.LogWarning($"ConflictResolver: Merge not supported for {conflict.Type} conflicts.");
                
                // Fall back to HigherPriority
                return await ResolveAsync(conflict, ResolutionStrategy.HigherPriority, ct);
            }

            // For now, merge falls back to priority-based resolution
            // Full merge implementation would require KV/script parsing
            // This can be extended later with actual merge logic
            
            _logger?.Log($"ConflictResolver: Merge not implemented, falling back to HigherPriority.");
            return await ResolveAsync(conflict, ResolutionStrategy.HigherPriority, ct);
        }

        /// <inheritdoc/>
        public async Task<ConflictResolutionResult> ApplyUserChoiceAsync(
            ModConflict conflict,
            ConflictResolutionOption chosenOption,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            if (chosenOption.PreferredSource == null && chosenOption.Strategy != ResolutionStrategy.Merge)
            {
                return ConflictResolutionResult.Failed(
                    conflict.Id,
                    chosenOption.Strategy,
                    "No preferred source in chosen option.");
            }

            _logger?.Log($"ConflictResolver: User chose '{chosenOption.Description}' for conflict '{conflict.Id}'");

            conflict.SelectedResolution = chosenOption;

            if (chosenOption.Strategy == ResolutionStrategy.Merge)
            {
                return await TryMergeAsync(conflict, ct);
            }

            return ConflictResolutionResult.Successful(
                conflict.Id,
                chosenOption.Strategy,
                chosenOption.PreferredSource!,
                conflict.AffectedFiles);
        }

        /// <inheritdoc/>
        public bool CanAutoResolve(ModConflict conflict, ModPriorityConfig config)
        {
            // Critical conflicts always require intervention
            if (conflict.Severity == ConflictSeverity.Critical)
            {
                return false;
            }

            // If auto-resolve is disabled, only None and Low can proceed
            if (!config.AutoResolveNonBreaking)
            {
                return conflict.Severity <= ConflictSeverity.Low;
            }

            // With auto-resolve enabled, Low and Medium can proceed
            return conflict.Severity <= ConflictSeverity.Medium;
        }

        /// <summary>
        /// Determines the winning mod source based on the strategy.
        /// </summary>
        private ModSource? DetermineWinner(ModConflict conflict, ResolutionStrategy strategy)
        {
            var sources = conflict.ConflictingSources;

            if (sources.Count < 2)
            {
                return sources.FirstOrDefault();
            }

            return strategy switch
            {
                ResolutionStrategy.HigherPriority => sources
                    .OrderBy(s => s.Priority)
                    .First(),

                ResolutionStrategy.LowerPriority => sources
                    .OrderByDescending(s => s.Priority)
                    .First(),

                ResolutionStrategy.MostRecent => sources
                    .OrderByDescending(s => s.AppliedAt)
                    .First(),

                ResolutionStrategy.KeepExisting => sources.First(),

                ResolutionStrategy.UseNew => sources.Last(),

                ResolutionStrategy.Merge => null, // Merge handled separately

                ResolutionStrategy.Interactive => null, // Requires user choice

                _ => sources.OrderBy(s => s.Priority).First()
            };
        }

        /// <summary>
        /// Gets the appropriate strategy for a conflict based on configuration.
        /// </summary>
        private ResolutionStrategy GetStrategyForConflict(
            ModConflict conflict, 
            ModPriorityConfig config)
        {
            // Check for category-specific strategy
            var categories = conflict.ConflictingSources
                .Select(s => s.Category)
                .Distinct()
                .ToList();

            foreach (var category in categories)
            {
                if (config.CategoryStrategies.TryGetValue(category, out var categoryStrategy))
                {
                    return categoryStrategy;
                }
            }

            // Use default strategy
            return config.DefaultStrategy;
        }
    }
}
