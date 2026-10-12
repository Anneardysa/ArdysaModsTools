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

namespace ArdysaModsTools.Core.Helpers
{
    public static class WornModelYield
    {
        public sealed record Yield(string BlockId, string Asset, string HiddenByLayer, string WornByLayer, string WornById);

        public sealed class Result
        {
            public Dictionary<string, HashSet<string>> StripRows { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Restore { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<Yield> Yields { get; } = new();
            public List<string> Warnings { get; } = new();
            public bool IsClean => StripRows.Count == 0 && Restore.Count == 0;
        }

        public static Result Compute(
            IReadOnlyDictionary<string, (string Block, string Layer)> finalBlocks,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            IReadOnlyDictionary<string, string> fileOwner,
            Func<string, string?> defaultModelOf)
        {
            var result = new Result();
            if (finalBlocks == null || finalBlocks.Count == 0) return result;

            var files = layerFiles.ToDictionary(
                kv => kv.Key,
                kv => new HashSet<string>(kv.Value.Select(KeyValuesBlockHelper.NormalizeAssetPath), StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
            var owner = fileOwner.ToDictionary(
                kv => KeyValuesBlockHelper.NormalizeAssetPath(kv.Key), kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            var wornAt = new Dictionary<string, (string Id, string Layer)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, (block, layer)) in finalBlocks.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                string? def = defaultModelOf(id);
                if (string.IsNullOrWhiteSpace(def)) continue;
                if (!KeyValuesBlockHelper.TryGetTopLevelValue(block, "model_player", out var mp) || mp == null) continue;

                string path = KeyValuesBlockHelper.NormalizeAssetPath(mp);
                if (!string.Equals(path, KeyValuesBlockHelper.NormalizeAssetPath(def), StringComparison.OrdinalIgnoreCase)) continue;
                if (!files.TryGetValue(layer, out var shipped) || !shipped.Contains(path + "_c")) continue;
                wornAt.TryAdd(path, (id, layer));
            }
            if (wornAt.Count == 0) return result;

            foreach (var (id, (block, layer)) in finalBlocks.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                foreach (var asset in KeyValuesBlockHelper.GetModelSwapAssets(block))
                {
                    if (!wornAt.TryGetValue(asset, out var worn) || worn.Id == id) continue;
                    if (string.Equals(worn.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"'{layer}' both hides {asset} (item {id}) and wears it (item {worn.Id}); left as built.");
                        continue;
                    }

                    if (!result.StripRows.TryGetValue(id, out var set))
                        result.StripRows[id] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    set.Add(asset);
                    result.Yields.Add(new Yield(id, asset, layer, worn.Layer, worn.Id));

                    string file = asset + "_c";
                    if (!owner.TryGetValue(file, out var last) ||
                        (!string.Equals(last, worn.Layer, StringComparison.OrdinalIgnoreCase) &&
                         !last.StartsWith($"{worn.Layer}#{BodyOwnership.VariantsDir}/", StringComparison.OrdinalIgnoreCase)))
                        result.Restore[file] = worn.Layer;
                }
            }
            return result;
        }
    }
}
