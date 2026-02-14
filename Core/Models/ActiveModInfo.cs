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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Unified snapshot of all currently active mods.
    /// Combines data from <see cref="HeroExtractionLog"/> and <see cref="MiscExtractionLog"/>
    /// into a single queryable result.
    /// </summary>
    public record ActiveModInfo
    {
        /// <summary>Overall mod installation status.</summary>
        public ModStatus OverallStatus { get; init; }

        /// <summary>Currently installed hero cosmetic sets.</summary>
        public IReadOnlyList<ActiveHeroMod> HeroMods { get; init; } = Array.Empty<ActiveHeroMod>();

        /// <summary>Currently installed misc mods (weather, HUD, terrain, etc.).</summary>
        public IReadOnlyList<ActiveMiscMod> MiscMods { get; init; } = Array.Empty<ActiveMiscMod>();

        /// <summary>Total number of active mods across all categories.</summary>
        public int TotalModCount => HeroMods.Count + MiscMods.Count;

        /// <summary>When the most recent generation occurred.</summary>
        public DateTime? LastGeneratedAt { get; init; }

        /// <summary>Whether any mods are currently active.</summary>
        public bool HasActiveMods => TotalModCount > 0;

        /// <summary>
        /// Get all active mod categories (e.g., "Hero", "Weather", "HUD").
        /// </summary>
        public IReadOnlyList<string> GetActiveCategories()
        {
            var categories = new List<string>();
            if (HeroMods.Count > 0) categories.Add("Hero");
            categories.AddRange(MiscMods.Select(m => m.Category).Distinct());
            return categories;
        }
    }

    /// <summary>
    /// Represents a single active hero cosmetic set.
    /// </summary>
    public record ActiveHeroMod
    {
        /// <summary>Internal hero NPC ID (e.g., "npc_dota_hero_antimage").</summary>
        public string HeroId { get; init; } = string.Empty;

        /// <summary>Name of the installed cosmetic set.</summary>
        public string SetName { get; init; } = string.Empty;

        /// <summary>Files installed by this hero set (relative paths).</summary>
        public IReadOnlyList<string> InstalledFiles { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a single active misc mod (weather, terrain, HUD, etc.).
    /// </summary>
    public record ActiveMiscMod
    {
        /// <summary>Mod category (e.g., "Weather", "Terrain", "HUD", "Shader").</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Selected choice within the category (e.g., "Rain", "Immortal Gardens").</summary>
        public string SelectedChoice { get; init; } = string.Empty;

        /// <summary>Files installed by this mod (relative paths).</summary>
        public IReadOnlyList<string> InstalledFiles { get; init; } = Array.Empty<string>();
    }
}
