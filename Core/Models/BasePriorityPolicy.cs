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

namespace ArdysaModsTools.Models
{
    public sealed class BasePriorityPolicy
    {
        public int? Default { get; set; }

        public Dictionary<string, int> Sets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, int> Items { get; } = new();

        public bool HasOverrides => Sets.Count > 0 || Items.Count > 0;

        public bool BaseWins(string? setName, int? itemId, bool detectedHeroBase)
        {
            if (itemId is int id && Items.TryGetValue(id, out var im) && IsExplicit(im))
                return im == 1;

            if (TryGetSetMethod(setName, out var sm))
                return sm == 1;

            return Resolve(Default, detectedHeroBase);
        }

        public string ScopeOf(string? setName, int? itemId)
        {
            if (itemId is int id && Items.TryGetValue(id, out var im) && IsExplicit(im)) return "item";
            if (TryGetSetMethod(setName, out _)) return "set";
            return IsExplicit(Default ?? 0) ? "hero" : "auto";
        }

        public BasePriorityPolicy Clone()
        {
            var copy = new BasePriorityPolicy { Default = Default };
            foreach (var kvp in Sets) copy.Sets[kvp.Key] = kvp.Value;
            foreach (var kvp in Items) copy.Items[kvp.Key] = kvp.Value;
            return copy;
        }

        public static bool Resolve(int? method, bool detectedHeroBase)
            => method == 1 ? true : method == 2 ? false : detectedHeroBase;

        private bool TryGetSetMethod(string? setName, out int method)
        {
            method = 0;
            if (string.IsNullOrWhiteSpace(setName)) return false;

            if (Sets.TryGetValue(setName!, out var exact) && IsExplicit(exact))
            {
                method = exact;
                return true;
            }

            var paren = setName!.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0 && Sets.TryGetValue(setName.Substring(0, paren), out var group) && IsExplicit(group))
            {
                method = group;
                return true;
            }

            return false;
        }

        private static bool IsExplicit(int method) => method == 1 || method == 2;
    }
}
