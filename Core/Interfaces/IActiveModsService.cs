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
    /// Service for querying currently installed and active mods.
    /// Provides a unified view over hero sets and misc mods by reading
    /// extraction logs created during generation.
    /// </summary>
    public interface IActiveModsService
    {
        /// <summary>
        /// Get a unified snapshot of all active mods (hero sets + misc mods).
        /// </summary>
        /// <param name="dotaPath">Path to the Dota 2 installation root.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Snapshot containing all active mods with metadata.</returns>
        Task<ActiveModInfo> GetActiveModsAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Get only the active hero cosmetic sets.
        /// </summary>
        /// <param name="dotaPath">Path to the Dota 2 installation root.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of active hero mods.</returns>
        Task<IReadOnlyList<ActiveHeroMod>> GetActiveHeroModsAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Get only the active misc mods (weather, terrain, HUD, etc.).
        /// </summary>
        /// <param name="dotaPath">Path to the Dota 2 installation root.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of active misc mods.</returns>
        Task<IReadOnlyList<ActiveMiscMod>> GetActiveMiscModsAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Check if a specific hero has an active cosmetic set.
        /// </summary>
        /// <param name="dotaPath">Path to the Dota 2 installation root.</param>
        /// <param name="heroId">Hero NPC ID (e.g., "npc_dota_hero_antimage").</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The active hero mod, or null if no set is installed.</returns>
        Task<ActiveHeroMod?> GetActiveHeroModAsync(string dotaPath, string heroId, CancellationToken ct = default);

        /// <summary>
        /// Check if a specific misc mod category has an active selection.
        /// </summary>
        /// <param name="dotaPath">Path to the Dota 2 installation root.</param>
        /// <param name="category">Mod category (e.g., "Weather", "HUD").</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The active misc mod, or null if nothing is selected.</returns>
        Task<ActiveMiscMod?> GetActiveMiscModAsync(string dotaPath, string category, CancellationToken ct = default);
    }
}
