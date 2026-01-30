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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Service for resolving conflicts between mods.
    /// Applies resolution strategies based on priority and configuration.
    /// </summary>
    public interface IConflictResolver
    {
        /// <summary>
        /// Resolves a single conflict using the specified strategy.
        /// </summary>
        /// <param name="conflict">The conflict to resolve.</param>
        /// <param name="strategy">Resolution strategy to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Result of the resolution attempt.</returns>
        Task<ConflictResolutionResult> ResolveAsync(
            ModConflict conflict,
            ResolutionStrategy strategy,
            CancellationToken ct = default);

        /// <summary>
        /// Resolves all conflicts using the provided configuration.
        /// Automatically resolves non-critical conflicts if config allows.
        /// </summary>
        /// <param name="conflicts">Collection of conflicts to resolve.</param>
        /// <param name="config">Priority configuration with resolution strategies.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of resolution results for each conflict.</returns>
        Task<IReadOnlyList<ConflictResolutionResult>> ResolveAllAsync(
            IEnumerable<ModConflict> conflicts,
            ModPriorityConfig config,
            CancellationToken ct = default);

        /// <summary>
        /// Attempts to merge conflicting changes where possible.
        /// Only applicable for Script and Configuration conflicts.
        /// </summary>
        /// <param name="conflict">The conflict to merge.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Result of the merge attempt.</returns>
        Task<ConflictResolutionResult> TryMergeAsync(
            ModConflict conflict,
            CancellationToken ct = default);

        /// <summary>
        /// Applies a user's explicit choice for a conflict requiring intervention.
        /// </summary>
        /// <param name="conflict">The conflict being resolved.</param>
        /// <param name="chosenOption">The option chosen by the user.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Result of applying the user's choice.</returns>
        Task<ConflictResolutionResult> ApplyUserChoiceAsync(
            ModConflict conflict,
            ConflictResolutionOption chosenOption,
            CancellationToken ct = default);

        /// <summary>
        /// Determines if a conflict can be auto-resolved without user input.
        /// </summary>
        /// <param name="conflict">The conflict to check.</param>
        /// <param name="config">Current priority configuration.</param>
        /// <returns>True if auto-resolution is possible.</returns>
        bool CanAutoResolve(ModConflict conflict, ModPriorityConfig config);
    }
}
