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

namespace ArdysaModsTools.Core.Helpers
{
    public static class BodyOwnership
    {
        public const string VariantsDir = "variants";

        public static string VariantOwner(string layer, string bodyKey) => $"{layer}#{VariantsDir}/{bodyKey}";

        public static string StyledKey(string bodyKey, string? skin)
            => string.IsNullOrWhiteSpace(skin) || skin.Trim() == "0" ? bodyKey : $"{bodyKey}.skin{skin.Trim()}";

        public sealed class Result
        {
            public string? Owner { get; set; }
            public string? BodyKey { get; set; }
            public HashSet<string> BodyFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Restore { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (string Layer, string Key)> VariantApply { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> StripWearable { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Warnings { get; } = new();
            public bool IsClean => Restore.Count == 0 && VariantApply.Count == 0 && StripWearable.Count == 0;
        }

        public static Result Compute(
            IReadOnlyDictionary<string, (string Block, string Layer)> finalBlocks,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, ISet<string>>> layerVariants,
            IReadOnlyDictionary<string, string> fileOwner,
            string heroUnit,
            ISet<string> slotModelFiles,
            Func<string, string>? label = null,
            Func<string, string?>? defaultModelOf = null,
            Func<string, string?>? stemOf = null)
        {
            string L(string layer) => label?.Invoke(layer) ?? layer;
            var result = new Result();
            if (finalBlocks == null || finalBlocks.Count == 0 || string.IsNullOrWhiteSpace(heroUnit)) return result;

            var swaps = finalBlocks
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .SelectMany(kv => KeyValuesBlockHelper.GetEntityModelSwaps(kv.Value.Block)
                    .Where(s => string.Equals(s.Asset, heroUnit, StringComparison.OrdinalIgnoreCase)
                                && s.Modifier.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase))
                    .Select(s => (Layer: kv.Value.Layer, s.Modifier, kv.Value.Block)))
                .ToList();
            if (swaps.Count == 0)
            {
                if (defaultModelOf != null)
                    ModellessOwners(result, finalBlocks, layerFiles, layerVariants, fileOwner, slotModelFiles, L, defaultModelOf, stemOf);
                return result;
            }

            var owners = swaps.Select(s => s.Layer).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (owners.Count > 1)
                result.Warnings.Add($"{string.Join(" and ", owners.Select(o => $"'{L(o)}'"))} both swap the hero body; '{L(owners[0])}' keeps it.");

            string owner = owners[0];
            var ownerSwap = swaps.First(s => string.Equals(s.Layer, owner, StringComparison.OrdinalIgnoreCase));
            result.Owner = owner;
            result.BodyKey = Path.GetFileNameWithoutExtension(ownerSwap.Modifier);
            string styledKey = StyledKey(result.BodyKey, KeyValuesBlockHelper.GetModelSkin(ownerSwap.Block));

            var owned = Normalized(layerFiles.TryGetValue(owner, out var of) ? of : null);
            var slots = Normalized(slotModelFiles);
            var models = owned.Where(p => p.StartsWith("models/heroes/", StringComparison.OrdinalIgnoreCase)
                                          && p.EndsWith(".vmdl_c", StringComparison.OrdinalIgnoreCase)
                                          && !slots.Contains(p))
                              .ToList();
            var sidecarDirs = models.Select(m => m[..^".vmdl_c".Length] + "/").ToList();
            foreach (var p in owned)
                if (models.Contains(p, StringComparer.OrdinalIgnoreCase) ||
                    sidecarDirs.Any(d => p.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                    result.BodyFiles.Add(p);

            var lastWriter = fileOwner.ToDictionary(kv => KeyValuesBlockHelper.NormalizeAssetPath(kv.Key), kv => kv.Value,
                StringComparer.OrdinalIgnoreCase);

            var variantOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var keyOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var clashed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (layer, byKey) in layerVariants.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (string.Equals(layer, owner, StringComparison.OrdinalIgnoreCase)) continue;
                string key = byKey.ContainsKey(styledKey) ? styledKey : result.BodyKey;
                if (!byKey.TryGetValue(key, out var paths)) continue;
                keyOf[layer] = key;
                foreach (var p in Normalized(paths))
                {
                    if (variantOf.TryGetValue(p, out var other) && !string.Equals(other, layer, StringComparison.OrdinalIgnoreCase))
                    {
                        clashed.Add(layer);
                        clashed.Add(other);
                    }
                    else variantOf[p] = layer;
                }
            }
            if (clashed.Count > 0)
            {
                result.Warnings.Add($"{string.Join(" and ", clashed.OrderBy(c => c).Select(c => $"'{L(c)}'"))} each ship a " +
                                    $"'{result.BodyKey}' body; they can't be combined, so '{L(owner)}' keeps its own.");
                foreach (var p in variantOf.Where(kv => clashed.Contains(kv.Value)).Select(kv => kv.Key).ToList())
                    variantOf.Remove(p);
            }

            foreach (var (p, layer) in variantOf)
                if (!lastWriter.TryGetValue(p, out var last) ||
                    !string.Equals(last, VariantOwner(layer, keyOf[layer]), StringComparison.OrdinalIgnoreCase))
                    result.VariantApply[p] = (layer, keyOf[layer]);

            foreach (var p in result.BodyFiles)
            {
                if (variantOf.ContainsKey(p)) continue;
                if (!lastWriter.TryGetValue(p, out var last) || !string.Equals(last, owner, StringComparison.OrdinalIgnoreCase))
                    result.Restore[p] = owner;
            }

            var withVariant = new HashSet<string>(variantOf.Values, StringComparer.OrdinalIgnoreCase);
            var baked = new HashSet<string>(clashed, StringComparer.OrdinalIgnoreCase);
            baked.UnionWith(withVariant);
            foreach (var (layer, files) in layerFiles.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (string.Equals(layer, owner, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Normalized(files).Any(result.BodyFiles.Contains)) continue;
                baked.Add(layer);
                if (!withVariant.Contains(layer))
                    result.Warnings.Add($"'{L(layer)}' is built for the regular body — with '{L(owner)}' it shows " +
                                        "without its baked mesh.");
            }

            if (defaultModelOf != null)
                foreach (var (id, (block, layer)) in finalBlocks.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    if (baked.Contains(layer) && !withVariant.Contains(layer)
                        && defaultModelOf(id) is string def && def.Trim().Length == 0
                        && KeyValuesBlockHelper.TryGetTopLevelValue(block, "model_player", out var mp)
                        && !string.IsNullOrWhiteSpace(mp))
                        result.StripWearable.Add(id);
            return result;
        }

        internal static string PeerKey(IEnumerable<string> stems)
            => string.Join("+", stems.Select(s => s.Trim().ToLowerInvariant()).OrderBy(s => s, StringComparer.Ordinal));

        private static void ModellessOwners(
            Result result,
            IReadOnlyDictionary<string, (string Block, string Layer)> finalBlocks,
            IReadOnlyDictionary<string, ISet<string>> layerFiles,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, ISet<string>>> layerVariants,
            IReadOnlyDictionary<string, string> fileOwner,
            ISet<string> slotModelFiles,
            Func<string, string> L,
            Func<string, string?> defaultModelOf,
            Func<string, string?>? stemOf)
        {
            var modelless = new HashSet<string>(
                finalBlocks.Where(kv => defaultModelOf(kv.Key) is string d && d.Trim().Length == 0).Select(kv => kv.Value.Layer),
                StringComparer.OrdinalIgnoreCase);

            var slots = Normalized(slotModelFiles);
            var files = layerFiles.ToDictionary(kv => kv.Key, kv => Normalized(kv.Value), StringComparer.OrdinalIgnoreCase);
            var lastWriter = fileOwner.ToDictionary(kv => KeyValuesBlockHelper.NormalizeAssetPath(kv.Key), kv => kv.Value,
                StringComparer.OrdinalIgnoreCase);

            var writers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (layer, paths) in files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                foreach (var p in paths.Where(p => IsBody(p, slots)))
                    (writers.TryGetValue(p, out var w) ? w : writers[p] = new List<string>()).Add(layer);

            var carriers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (layer, byKey) in layerVariants.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                foreach (var p in byKey.Values.SelectMany(v => Normalized(v)).Distinct(StringComparer.OrdinalIgnoreCase))
                    if (IsBody(p, slots))
                        (carriers.TryGetValue(p, out var c) ? c : carriers[p] = new List<string>()).Add(layer);

            var warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var body in writers.Keys.Concat(carriers.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
                                             .OrderBy(b => b, StringComparer.Ordinal))
            {
                var layers = writers.TryGetValue(body, out var ws) ? ws : new List<string>();
                var company = layers.Concat(carriers.TryGetValue(body, out var cs) ? cs : new List<string>())
                                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (company.Count >= 2 && PeerVariant(body, company, layerVariants, stemOf) is var (carrier, key, variant))
                {
                    foreach (var p in variant)
                        if (!lastWriter.TryGetValue(p, out var last) ||
                            !string.Equals(last, VariantOwner(carrier, key), StringComparison.OrdinalIgnoreCase))
                            result.VariantApply[p] = (carrier, key);
                    continue;
                }
                if (layers.Count < 2) continue;

                var owners = layers.Where(modelless.Contains).ToList();
                if (owners.Count != 1)
                {
                    string winner = lastWriter.TryGetValue(body, out var w) ? w : layers[^1];
                    var names = layers.OrderBy(l => l, StringComparer.Ordinal).Select(l => $"'{L(l)}'").ToList();
                    if (warned.Add(string.Join("\n", names)))
                        result.Warnings.Add($"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]} all rebuild " +
                                            $"the same hero body and no combined body ships for them - '{L(winner)}' is the one " +
                                            "used, the others show without their baked mesh or animation.");
                    continue;
                }

                string owner = owners[0];
                var partners = layers.Where(l => !string.Equals(l, owner, StringComparison.OrdinalIgnoreCase)).ToList();
                string sidecar = body[..^".vmdl_c".Length] + "/";
                foreach (var p in files[owner].Where(p => string.Equals(p, body, StringComparison.OrdinalIgnoreCase)
                                                          || p.StartsWith(sidecar, StringComparison.OrdinalIgnoreCase)))
                {
                    result.BodyFiles.Add(p);
                    if (!lastWriter.TryGetValue(p, out var last) || !string.Equals(last, owner, StringComparison.OrdinalIgnoreCase))
                        result.Restore[p] = owner;
                }
                foreach (var partner in partners)
                    if (warned.Add(partner + "\n" + owner))
                        result.Warnings.Add($"'{L(partner)}' and '{L(owner)}' both ship the same hero body; " +
                                            $"'{L(owner)}' keeps it, so '{L(partner)}' may not sit right on it.");
            }
        }

        private static (string Layer, string Key, HashSet<string> Paths)? PeerVariant(
            string body,
            IReadOnlyList<string> layers,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, ISet<string>>> layerVariants,
            Func<string, string?>? stemOf)
        {
            if (stemOf == null) return null;
            var stems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in layers)
            {
                var s = stemOf(l);
                if (string.IsNullOrWhiteSpace(s)) return null;
                stems[l] = s;
            }
            foreach (var carrier in layers.OrderBy(l => stems[l].ToLowerInvariant(), StringComparer.Ordinal))
            {
                string key = PeerKey(layers.Where(o => !string.Equals(o, carrier, StringComparison.OrdinalIgnoreCase))
                                           .Select(o => stems[o]));
                if (layerVariants.TryGetValue(carrier, out var byKey) && byKey.TryGetValue(key, out var paths))
                {
                    var set = Normalized(paths);
                    if (set.Contains(body)) return (carrier, key, set);
                }
            }
            return null;
        }

        private static bool IsBody(string path, ISet<string> slots)
            => path.StartsWith("models/heroes/", StringComparison.OrdinalIgnoreCase)
               && path.EndsWith(".vmdl_c", StringComparison.OrdinalIgnoreCase)
               && !slots.Contains(path);

        private static HashSet<string> Normalized(IEnumerable<string>? paths)
            => new((paths ?? Enumerable.Empty<string>()).Select(KeyValuesBlockHelper.NormalizeAssetPath),
                   StringComparer.OrdinalIgnoreCase);
    }
}
