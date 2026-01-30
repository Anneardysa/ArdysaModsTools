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
    /// Service for managing mod priorities.
    /// Handles priority configuration persistence and lookup.
    /// </summary>
    public interface IModPriorityService
    {
        /// <summary>
        /// Loads the priority configuration from the target path.
        /// Returns default configuration if none exists.
        /// </summary>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Priority configuration.</returns>
        Task<ModPriorityConfig> LoadConfigAsync(
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Saves the priority configuration to disk.
        /// </summary>
        /// <param name="config">Configuration to save.</param>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        Task SaveConfigAsync(
            ModPriorityConfig config,
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Gets the priority for a specific mod.
        /// Returns default priority (100) if not configured.
        /// </summary>
        /// <param name="modId">The mod identifier.</param>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Priority value (lower = higher priority).</returns>
        Task<int> GetModPriorityAsync(
            string modId,
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Sets the priority for a specific mod.
        /// </summary>
        /// <param name="modId">The mod identifier.</param>
        /// <param name="modName">Human-readable mod name.</param>
        /// <param name="category">Mod category.</param>
        /// <param name="priority">Priority value (1-999).</param>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        Task SetModPriorityAsync(
            string modId,
            string modName,
            string category,
            int priority,
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Gets all mod priorities ordered by priority value.
        /// Optionally filtered by category.
        /// </summary>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="category">Optional category filter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Ordered list of mod priorities.</returns>
        Task<IReadOnlyList<ModPriority>> GetOrderedPrioritiesAsync(
            string targetPath,
            string? category = null,
            CancellationToken ct = default);

        /// <summary>
        /// Applies priority values to a collection of mod sources.
        /// </summary>
        /// <param name="modSources">Mod sources to update.</param>
        /// <param name="targetPath">The Dota 2 installation path.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Updated mod sources with priorities set.</returns>
        Task<IReadOnlyList<ModSource>> ApplyPrioritiesAsync(
            IEnumerable<ModSource> modSources,
            string targetPath,
            CancellationToken ct = default);

        /// <summary>
        /// Invalidates the cached priority configuration.
        /// Call after modifying priorities to ensure fresh reads.
        /// </summary>
        void InvalidateCache();
    }
}
