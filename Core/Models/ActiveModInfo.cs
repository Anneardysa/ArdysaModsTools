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
    public record ActiveModInfo
    {
        public ModStatus OverallStatus { get; init; }

        public IReadOnlyList<ActiveHeroMod> HeroMods { get; init; } = Array.Empty<ActiveHeroMod>();

        public IReadOnlyList<ActiveMiscMod> MiscMods { get; init; } = Array.Empty<ActiveMiscMod>();

        public int TotalModCount => HeroMods.Count + MiscMods.Count;

        public DateTime? LastGeneratedAt { get; init; }

        public bool HasActiveMods => TotalModCount > 0;

        public IReadOnlyList<string> GetActiveCategories()
        {
            var categories = new List<string>();
            if (HeroMods.Count > 0) categories.Add("Hero");
            categories.AddRange(MiscMods.Select(m => m.Category).Distinct());
            return categories;
        }
    }

    public record ActiveHeroMod
    {
        public string HeroId { get; init; } = string.Empty;

        public string SetName { get; init; } = string.Empty;

        public IReadOnlyList<string> InstalledFiles { get; init; } = Array.Empty<string>();
    }

    public record ActiveMiscMod
    {
        public string Category { get; init; } = string.Empty;

        public string SelectedChoice { get; init; } = string.Empty;

        public IReadOnlyList<string> InstalledFiles { get; init; } = Array.Empty<string>();
    }
}
