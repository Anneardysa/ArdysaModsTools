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
    public class ConflictResolver : IConflictResolver
    {
        private readonly IAppLogger? _logger;

        public ConflictResolver(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<ConflictResolutionResult> ResolveAsync(
            ModConflict conflict,
            ResolutionStrategy strategy,
            CancellationToken ct = default)
        {
            await Task.Yield();
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

                if (!CanAutoResolve(conflict, config))
                {
                    _logger?.Log($"ConflictResolver: Conflict '{conflict.Id}' requires user intervention.");
                    results.Add(ConflictResolutionResult.Failed(
                        conflict.Id,
                        ResolutionStrategy.Interactive,
                        "Conflict requires user intervention."));
                    continue;
                }

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

        public async Task<ConflictResolutionResult> TryMergeAsync(
            ModConflict conflict,
            CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            _logger?.Log($"ConflictResolver: Attempting merge for '{conflict.Id}'");

            if (conflict.Type != ConflictType.Script && conflict.Type != ConflictType.Configuration)
            {
                _logger?.LogWarning($"ConflictResolver: Merge not supported for {conflict.Type} conflicts.");
                
                return await ResolveAsync(conflict, ResolutionStrategy.HigherPriority, ct);
            }

            
            _logger?.Log($"ConflictResolver: Merge not implemented, falling back to HigherPriority.");
            return await ResolveAsync(conflict, ResolutionStrategy.HigherPriority, ct);
        }

        public async Task<ConflictResolutionResult> ApplyUserChoiceAsync(
            ModConflict conflict,
            ConflictResolutionOption chosenOption,
            CancellationToken ct = default)
        {
            await Task.Yield();
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

        public bool CanAutoResolve(ModConflict conflict, ModPriorityConfig config)
        {
            if (conflict.Severity == ConflictSeverity.Critical)
            {
                return false;
            }

            if (!config.AutoResolveNonBreaking)
            {
                return conflict.Severity <= ConflictSeverity.Low;
            }

            return conflict.Severity <= ConflictSeverity.Medium;
        }

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

                ResolutionStrategy.Merge => null,

                ResolutionStrategy.Interactive => null,

                _ => sources.OrderBy(s => s.Priority).First()
            };
        }

        private ResolutionStrategy GetStrategyForConflict(
            ModConflict conflict, 
            ModPriorityConfig config)
        {
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

            return config.DefaultStrategy;
        }
    }
}
