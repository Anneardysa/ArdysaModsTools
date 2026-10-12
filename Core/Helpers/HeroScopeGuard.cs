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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Core.Helpers
{
    public static class HeroScopeGuard
    {
        private static readonly Regex HeroFolder = new(
            @"^(?:models/heroes/(?<h>[^/]+)/|particles/units/heroes/hero_(?<h>[^/]+)/|" +
            @"sounds/weapons/hero/(?<h>[^/]+)/|soundevents/game_sounds_heroes/game_sounds_(?<h>[^/]+)\.vsndevts_c$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const string NpcPrefix = "npc_dota_hero_";

        private static readonly Lazy<IReadOnlySet<string>> Claimed = new(() =>
            HeroDefaultItemRegistry.AllItems.Select(i => i.HeroId).Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(OwnTokens).ToHashSet(StringComparer.Ordinal));

        public static IReadOnlySet<string> OwnTokens(string heroId)
        {
            var own = new HashSet<string>(StringComparer.Ordinal);
            string npc = (heroId ?? string.Empty).Trim();
            if (npc.StartsWith(NpcPrefix, StringComparison.OrdinalIgnoreCase))
                own.Add(Fold(npc[NpcPrefix.Length..]));
            foreach (var item in HeroDefaultItemRegistry.AllItems)
            {
                if (!string.Equals(item.HeroId, npc, StringComparison.OrdinalIgnoreCase)) continue;
                var parts = KeyValuesBlockHelper.NormalizeAssetPath(item.ModelPlayer).Split('/');
                if (parts.Length > 3 && parts[0].Equals("models", StringComparison.OrdinalIgnoreCase)
                    && parts[1].Equals("heroes", StringComparison.OrdinalIgnoreCase))
                    own.Add(Fold(parts[2]));
            }
            own.Remove(string.Empty);
            return own;
        }

        public static bool IsForeign(string relativePath, IReadOnlySet<string> own, string? namedBy)
        {
            string rel = KeyValuesBlockHelper.NormalizeAssetPath(relativePath).TrimStart('/');
            var m = HeroFolder.Match(rel);
            if (!m.Success)
                return false;
            string folder = Fold(m.Groups["h"].Value);
            if (own.Any(o => folder.StartsWith(o, StringComparison.Ordinal))
                || !Claimed.Value.Any(o => folder.StartsWith(o, StringComparison.Ordinal)))
                return false;
            string resource = rel.EndsWith("_c", StringComparison.OrdinalIgnoreCase) ? rel[..^2] : rel;
            return namedBy == null
                || namedBy.Replace('\\', '/').IndexOf(resource, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string Fold(string name) => name.Replace("_", string.Empty).ToLowerInvariant();
    }
}
