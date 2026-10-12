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
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Helpers
{
    public static class BaseSlotLock
    {
        public sealed class Result
        {
            public Dictionary<string, string> Reclaim { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Restore { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Warnings { get; } = new();
            public bool IsClean => Reclaim.Count == 0 && Restore.Count == 0;
        }

        public static IReadOnlyList<string> SlotsFor(HeroModel hero, string? setName)
        {
            if (hero?.BaseLocks == null || hero.BaseLocks.Count == 0 || hero.Sets == null
                || string.IsNullOrEmpty(setName) || !hero.Sets.TryGetValue(setName, out var urls))
                return Array.Empty<string>();
            if (HeroModelMapper.ClassifySet(urls) != HeroModelMapper.SkinCategory.BaseHero)
                return Array.Empty<string>();

            var stem = HeroModelMapper.ExtractArchiveStem(urls);
            return stem != null && hero.BaseLocks.TryGetValue(stem, out var slots)
                ? slots
                : Array.Empty<string>();
        }

        private static readonly Dictionary<string, string> SlotAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["arm"] = "arms", ["offweapon"] = "offhand", ["offweap"] = "offhand", ["offhand_weapon"] = "offhand",
            ["hand"] = "hands", ["gloves"] = "hands", ["shoes"] = "legs",
        };

        public static string NormalizeSlot(string? tag)
        {
            var raw = (tag ?? "").Trim().ToLowerInvariant();
            return SlotAliases.TryGetValue(raw, out var slot) ? slot : raw;
        }

        public static bool IsLockedItemTag(string? itemTag, IReadOnlyList<string> slots)
        {
            if (string.IsNullOrWhiteSpace(itemTag) || slots == null || slots.Count == 0) return false;
            var tag = NormalizeSlot(itemTag);
            foreach (var s in slots)
            {
                var slot = NormalizeSlot(s);
                if (slot == tag || slot.Split('_').Select(NormalizeSlot).Contains(tag)) return true;
            }
            return false;
        }

        public static ISet<string> LockedIds(IEnumerable<int>? heroItemIds, IEnumerable<string> slots,
            Func<int, string?>? slotOf = null)
        {
            slotOf ??= id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.Slot : null;
            var wanted = new HashSet<string>((slots ?? Array.Empty<string>()).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (wanted.Count == 0 || heroItemIds == null) return ids;

            foreach (var id in heroItemIds)
            {
                var slot = slotOf(id);
                if (!string.IsNullOrWhiteSpace(slot) && wanted.Contains(slot!.Trim()))
                    ids.Add(id.ToString());
            }
            return ids;
        }

        public static Result Compute(
            IReadOnlyDictionary<string, (string Block, string Layer)> finalBlocks,
            string baseLayer,
            IReadOnlyDictionary<string, string> baseBlocks,
            ISet<string> lockedIds,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            IReadOnlyDictionary<string, string> fileOwner,
            IReadOnlyDictionary<string, string>? slotModels = null,
            ISet<string>? yieldTo = null,
            Func<string, string>? label = null)
        {
            string L(string layer) => label?.Invoke(layer) ?? layer;
            var result = new Result();
            if (lockedIds == null || lockedIds.Count == 0 || string.IsNullOrEmpty(baseLayer)) return result;

            var shipped = layerFiles != null && layerFiles.TryGetValue(baseLayer, out var files)
                ? new HashSet<string>(files.Select(KeyValuesBlockHelper.NormalizeAssetPath), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var owner = (fileOwner ?? new Dictionary<string, string>()).ToDictionary(
                kv => KeyValuesBlockHelper.NormalizeAssetPath(kv.Key), kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            string? Compiled(string? model) => string.IsNullOrWhiteSpace(model) ? null
                : KeyValuesBlockHelper.NormalizeAssetPath(model!) is var p && p.EndsWith("_c", StringComparison.OrdinalIgnoreCase) ? p : p + "_c";
            var models = slotModels ?? new Dictionary<string, string>();
            var lockedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var otherModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, model) in models)
                if (Compiled(model) is string c)
                    (lockedIds.Contains(id) ? lockedModels : otherModels).Add(c);
            otherModels.ExceptWith(lockedModels);

            static string SidecarDir(string compiledModel) => compiledModel[..^"_c".Length] is var m
                && m.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase) ? m[..^".vmdl".Length] + "/" : compiledModel + "/";
            bool Under(string path, IEnumerable<string> compiledModels) => compiledModels.Any(m =>
                path.Equals(m, StringComparison.OrdinalIgnoreCase) || path.StartsWith(SidecarDir(m), StringComparison.OrdinalIgnoreCase));

            foreach (var id in lockedIds.OrderBy(i => i, StringComparer.Ordinal))
            {
                if (baseBlocks == null || !baseBlocks.TryGetValue(id, out var baseBlock) || string.IsNullOrWhiteSpace(baseBlock))
                {
                    result.Warnings.Add($"'{L(baseLayer)}' locks item {id} but does not define it; left to the other layers.");
                    continue;
                }

                if (finalBlocks == null || !finalBlocks.TryGetValue(id, out var final)
                    || !string.Equals(final.Layer, baseLayer, StringComparison.OrdinalIgnoreCase))
                    result.Reclaim[id] = baseBlock;

                var bodies = KeyValuesBlockHelper.GetEntityModelSwaps(baseBlock)
                    .Select(s => Compiled(s.Modifier)).OfType<string>().ToList();
                var slotFiles = new HashSet<string>(ReferencedFiles(baseBlock), StringComparer.OrdinalIgnoreCase);
                if (models.TryGetValue(id, out var own) && Compiled(own) is string ownModel)
                    slotFiles.UnionWith(shipped.Where(p => Under(p, new[] { ownModel })));

                foreach (var rel in slotFiles)
                {
                    if (!shipped.Contains(rel)) continue;
                    if (Under(rel, otherModels) || Under(rel, bodies)) continue;
                    if (!owner.TryGetValue(rel, out var last))
                    {
                        result.Restore[rel] = baseLayer;
                        continue;
                    }
                    if (string.Equals(last, baseLayer, StringComparison.OrdinalIgnoreCase)) continue;
                    if (yieldTo != null && yieldTo.Contains(last)) continue;
                    result.Restore[rel] = baseLayer;
                }
            }
            return result;
        }

        private static readonly Regex QuotedValue = new("\"([^\"\\r\\n]+)\"", RegexOptions.Compiled);

        internal static IEnumerable<string> ReferencedFiles(string block)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in QuotedValue.Matches(block ?? string.Empty))
            {
                var path = KeyValuesBlockHelper.NormalizeAssetPath(m.Groups[1].Value);
                if (!path.Contains('/') || !path.Contains('.')) continue;
                if (seen.Add(path)) yield return path;
                if (!path.EndsWith("_c", StringComparison.OrdinalIgnoreCase) && seen.Add(path + "_c"))
                    yield return path + "_c";
            }
        }
    }
}
