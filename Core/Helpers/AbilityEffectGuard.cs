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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Core.Helpers
{
    public static class AbilityEffectGuard
    {
        public const string ArchivePrefix = "effect_";

        private static readonly Regex TagPattern = new(@"^ability[1-9]$", RegexOptions.CultureInvariant);
        private static readonly Regex SharedItemFx = new(@"^particles/items\d*_fx/", RegexOptions.CultureInvariant);
        private static readonly Regex SpellIcon = new(@"^panorama/images/spellicons/[a-z0-9_]+\.vtex_c$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static readonly IReadOnlySet<string> EffectModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "models/props_gameplay/frog.vmdl_c",
            "models/props_gameplay/chicken.vmdl_c",
            "models/heroes/weaver/weaver_bug.vmdl_c",
            "models/heroes/gyro/gyro_missile.vmdl_c",
            "models/heroes/dark_willow/dark_willow_wisp.vmdl_c",
            "models/heroes/lanaya/lanaya_trap_crystal_invis.vmdl_c",
            "models/heroes/visage/visage_familiar.vmdl_c",
            "models/heroes/techies/fx_techies_remote_cart.vmdl_c",
            "models/heroes/techies/fx_techies_remotebomb.vmdl_c",
            "models/heroes/techies/techies_sign.vmdl_c",
        };

        private static readonly Regex EffectSound = new(@"^sounds/[a-z0-9_/]+\.vsnd_c$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex SharedSound = new(@"^sounds/(null\.vsnd_c$|ui/|items/|music/)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static bool IsEffectSound(string? rel)
        {
            var path = (rel ?? "").Replace('\\', '/');
            return EffectSound.IsMatch(path) && !SharedSound.IsMatch(path);
        }

        public static bool IsValidTag(string? tag) => tag != null && TagPattern.IsMatch(tag);

        private static readonly Regex EffectStem = new(@"^effect_ability[1-9]_(?<rest>[a-z0-9_\-]+)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string? CarrierStem(string? effectStem)
        {
            var m = EffectStem.Match(effectStem?.Trim() ?? "");
            return m.Success ? "item_" + m.Groups["rest"].Value.ToLowerInvariant() : null;
        }

        public static IReadOnlyList<string> ValidateLayerFiles(IEnumerable<string>? relPaths)
        {
            var problems = new List<string>();
            var paths = relPaths?.ToList() ?? new List<string>();
            if (paths.Count == 0)
            {
                problems.Add("the effect ships no particle files");
                return problems;
            }

            foreach (var raw in paths)
            {
                var rel = (raw ?? "").Replace('\\', '/');
                if (rel.Length == 0 || Path.IsPathRooted(rel) || rel.StartsWith('/')
                    || rel.Split('/').Any(seg => seg == ".." || seg == "."))
                    problems.Add($"'{raw}' is not a plain relative path");
                else if (SpellIcon.IsMatch(rel) || EffectModels.Contains(rel) || IsEffectSound(rel))
                    continue;
                else if (!rel.StartsWith("particles/", StringComparison.OrdinalIgnoreCase)
                    || !rel.EndsWith(".vpcf_c", StringComparison.OrdinalIgnoreCase))
                    problems.Add($"'{rel}' is not a compiled particle (particles/**.vpcf_c) or an ability icon (panorama/images/spellicons/*.vtex_c) , a hex/summon model the effect may swap or an ability sound (sounds/**.vsnd_c)");
                else if (SharedItemFx.IsMatch(rel.ToLowerInvariant()))
                    problems.Add($"'{rel}' is a particle shared by every hero");
            }
            return problems;
        }

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> FindOverlaps(
            IReadOnlyDictionary<string, ISet<string>>? effectLayers)
        {
            var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (layer, files) in effectLayers ?? new Dictionary<string, ISet<string>>())
                foreach (var file in files ?? (ISet<string>)new HashSet<string>())
                {
                    var key = file.Replace('\\', '/');
                    if (!owners.TryGetValue(key, out var list)) owners[key] = list = new List<string>();
                    list.Add(layer);
                }

            return owners.Where(kv => kv.Value.Count > 1)
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
