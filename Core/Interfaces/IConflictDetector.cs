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
    /// Service for detecting conflicts between mods.
    /// Analyzes mod sources and their affected files to identify overlaps.
    /// </summary>
    public interface IConflictDetector
    {
        /// <summary>
        /// Detects all conflicts among the given mods within the target path.
        /// </summary>
        /// <param name="modsToApply">Collection of mods to check for conflicts.</param>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of detected conflicts.</returns>
        Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
            IEnumerable<ModSource> modsToApply,
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Checks for a conflict between two specific mods.
        /// Useful for quick pair-wise conflict checking.
        /// </summary>
        /// <param name="newMod">The new mod being applied.</param>
        /// <param name="existingMod">An existing mod already applied.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A ModConflict if conflict exists, null otherwise.</returns>
        Task<ModConflict?> CheckSingleConflictAsync(
            ModSource newMod,
            ModSource existingMod,
            CancellationToken ct = default);

        /// <summary>
        /// Determines if any conflict in the collection is Critical severity.
        /// Critical conflicts must be resolved before proceeding.
        /// </summary>
        /// <param name="conflicts">Collection of conflicts to check.</param>
        /// <returns>True if any Critical conflicts exist.</returns>
        bool HasCriticalConflicts(IEnumerable<ModConflict> conflicts);

        /// <summary>
        /// Determines if any conflict requires user intervention.
        /// </summary>
        /// <param name="conflicts">Collection of conflicts to check.</param>
        /// <returns>True if any Interactive resolution is required.</returns>
        bool RequiresUserIntervention(IEnumerable<ModConflict> conflicts);

        /// <summary>
        /// Gets all conflicts of a specific severity level.
        /// </summary>
        /// <param name="conflicts">Collection of conflicts to filter.</param>
        /// <param name="severity">Severity to filter by.</param>
        /// <returns>Filtered list of conflicts.</returns>
        IReadOnlyList<ModConflict> GetConflictsBySeverity(
            IEnumerable<ModConflict> conflicts,
            ConflictSeverity severity);
    }
}
