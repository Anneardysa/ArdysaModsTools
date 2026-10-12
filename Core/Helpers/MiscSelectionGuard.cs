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
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Helpers
{
    public static class MiscSelectionGuard
    {
        private const string EtherealSuffix = "Ethereal";

        private static readonly Dictionary<string, string> CanonicalEffectNames =
            EtherealEffects.Effects.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);

        public static bool SupportsEthereal(MiscOption option) =>
            option.Id.Equals("Courier", StringComparison.OrdinalIgnoreCase) ||
            option.Id.Equals("Ward", StringComparison.OrdinalIgnoreCase);

        public static Dictionary<string, string> Sanitize(
            IReadOnlyList<MiscOption> options,
            IReadOnlyDictionary<string, string>? selections,
            out List<string> dropped)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dropped = new List<string>();
            if (selections == null) return result;

            var byId = new Dictionary<string, MiscOption>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in options) byId.TryAdd(o.Id, o);

            foreach (var (key, value) in selections)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (key.EndsWith(EtherealSuffix, StringComparison.OrdinalIgnoreCase)
                    && byId.TryGetValue(key[..^EtherealSuffix.Length], out var host)
                    && SupportsEthereal(host))
                {
                    var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var effects = parts
                        .Select(p => CanonicalEffectNames.GetValueOrDefault(p))
                        .OfType<string>()
                        .Distinct()
                        .Take(EtherealEffects.MaxParticleSlots)
                        .ToList();
                    if (effects.Count < parts.Length) dropped.Add(key);
                    if (effects.Count > 0) result[host.Id + EtherealSuffix] = string.Join(",", effects);
                    continue;
                }

                var choice = byId.TryGetValue(key, out var option) ? FindChoice(option, value) : null;
                if (choice == null)
                {
                    dropped.Add(key);
                    continue;
                }
                result[option!.Id] = choice;
            }

            return result;
        }

        public static bool HasCustomSelections(IReadOnlyDictionary<string, string> selections) =>
            selections.Any(kv => !string.IsNullOrWhiteSpace(kv.Value)
                && (kv.Key.EndsWith(EtherealSuffix, StringComparison.OrdinalIgnoreCase)
                    || !MiscOption.IsDefaultChoice(kv.Value)));

        private static string? FindChoice(MiscOption option, string value)
        {
            foreach (var choice in option.Choices)
            {
                if (choice.Equals(value, StringComparison.OrdinalIgnoreCase)) return choice;
                if (option.ChoiceStyles.TryGetValue(choice, out var styles))
                {
                    var style = styles.FirstOrDefault(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (style != null) return style;
                }
            }
            return null;
        }
    }
}
