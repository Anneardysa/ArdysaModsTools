/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Helpers
{
    public static class HeroParticleInjector
    {
        private static readonly Regex VpcfPath = new(@"^particles/[A-Za-z0-9_\-/]+\.vpcf$", RegexOptions.CultureInvariant);
        private static readonly Regex ModifierHeader = new(@"""asset_modifier(\d*)""\s*\{", RegexOptions.IgnoreCase);

        public static IReadOnlyList<ParticlePick> Sanitize(HeroModel? hero, IEnumerable<ParticlePick?>? picks, Action<string>? trace = null)
        {
            var kept = new List<ParticlePick>();
            if (hero == null || picks == null) return kept;

            var hosts = HeroParticleCatalog.HostsFor(hero).Select(h => h.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var pick in picks)
            {
                if (pick == null) continue;
                if (!HeroParticleCatalog.TryGet(pick.Id, out var particle))
                {
                    trace?.Invoke($"{hero.DisplayName}: dropped particle '{pick.Id}', not in the catalog.");
                    continue;
                }
                string host = pick.Host?.Trim() ?? string.Empty;
                if (!hosts.Contains(host))
                {
                    trace?.Invoke($"{hero.DisplayName}: dropped {particle.Name}, item '{host}' cannot carry a particle.");
                    continue;
                }
                if (kept.Any(k => k.Id == particle.Id && k.Host == host))
                    continue;
                if (kept.Count >= HeroParticleCatalog.MaxPerHero)
                {
                    trace?.Invoke($"{hero.DisplayName}: dropped {particle.Name}, a hero carries at most {HeroParticleCatalog.MaxPerHero} particles.");
                    continue;
                }
                kept.Add(new ParticlePick { Id = particle.Id, Host = host });
            }
            return kept;
        }

        public static ParticleSlots Slots(
            IReadOnlyDictionary<string, (string block, string heroId)> blocks,
            HeroModelMapper.SkinCategory category,
            string? itemTag,
            Func<string, (string Slot, string Label)?>? slotOf = null)
        {
            slotOf ??= id => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? (info.Slot, info.SlotDisplayName) : null;
            var slots = new List<ParticleSlot>();
            string? heroBase = null, ownSlot = null;
            foreach (var (id, (block, _)) in blocks.OrderBy(kv => int.TryParse(kv.Key, out var n) ? n : int.MaxValue))
            {
                bool model = HasWearableModel(block);
                bool isBase = KeyValuesBlockHelper.TryGetTopLevelValue(block, "item_slot", out var s)
                    && string.Equals(s?.Trim(), "hero_base", StringComparison.OrdinalIgnoreCase);
                if (!model && !isBase) continue;

                var known = slotOf(id);
                slots.Add(new ParticleSlot(id, known?.Label ?? (isBase ? "Base" : $"Item {id}"), model));
                if (isBase) heroBase ??= id;
                if (known is { } k && BaseSlotLock.IsLockedItemTag(itemTag, new[] { string.IsNullOrWhiteSpace(k.Slot) ? "weapon" : k.Slot }))
                    ownSlot ??= id;
            }

            string? preferred = category switch
            {
                HeroModelMapper.SkinCategory.Item => ownSlot ?? (slots.Count == 1 ? slots[0].Id : null),
                HeroModelMapper.SkinCategory.BaseHero => heroBase ?? (slots.Count == 1 ? slots[0].Id : null),
                _ => null,
            };
            return new ParticleSlots(slots, preferred);
        }

        public static bool HasWearableModel(string? block) =>
            block != null
            && KeyValuesBlockHelper.TryGetTopLevelValue(block, "model_player", out var model)
            && !string.IsNullOrWhiteSpace(model);

        public static string Inject(string block, string path)
        {
            if (string.IsNullOrEmpty(block)) throw new ArgumentException("No item block to add a particle to.", nameof(block));
            if (path == null || !VpcfPath.IsMatch(path))
                throw new ArgumentException($"Refusing to write '{path}' into the package: not a particles/*.vpcf path.", nameof(path));

            string eol = block.Contains("\r\n") ? "\r\n" : "\n";
            block = WithUnusualQuality(block, eol);

            int visualsBrace = FindTopLevelBlock(block, "visuals");
            if (visualsBrace < 0)
            {
                int close = block.LastIndexOf('}');
                if (close < 0) throw new ArgumentException("The item block has no closing brace.", nameof(block));
                string inner = IndentOfLine(block, close) + "\t";
                string visuals = $"{inner}\"visuals\"{eol}{inner}{{{eol}{Modifier(0, path, inner + "\t", eol)}{inner}}}{eol}";
                return InsertBeforeLineOf(block, close, visuals, eol);
            }

            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(block, visualsBrace);
            if (visualsEnd < 0) throw new ArgumentException("The item block's visuals are not balanced.", nameof(block));
            string body = block.Substring(visualsBrace, visualsEnd - visualsBrace);
            if (SpawnsPath(body, path)) return block;

            int next = 0;
            foreach (Match m in ModifierHeader.Matches(body))
                next = Math.Max(next, (int.TryParse(m.Groups[1].Value, out var n) ? n : -1) + 1);

            int closeBrace = visualsEnd - 1;
            string entryIndent = IndentOfLine(block, visualsBrace) + "\t";
            return InsertBeforeLineOf(block, closeBrace, Modifier(next, path, entryIndent, eol), eol);
        }

        private static string Modifier(int index, string path, string indent, string eol) =>
            $"{indent}\"asset_modifier{index}\"{eol}{indent}{{{eol}" +
            $"{indent}\t\"type\"\t\t\"particle_create\"{eol}" +
            $"{indent}\t\"modifier\"\t\t\"{path}\"{eol}" +
            $"{indent}}}{eol}";

        private static bool SpawnsPath(string visualsBody, string path)
        {
            foreach (Match m in ModifierHeader.Matches(visualsBody))
            {
                int open = m.Index + m.Length - 1;
                int end = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsBody, open);
                if (end < 0) continue;
                string mod = visualsBody.Substring(open, end - open);
                if (Regex.IsMatch(mod, @"""type""\s+""particle_create""", RegexOptions.IgnoreCase)
                    && Regex.IsMatch(mod, $@"""modifier""\s+""{Regex.Escape(path)}""", RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }

        private static string WithUnusualQuality(string block, string eol)
        {
            foreach (Match m in Regex.Matches(block, @"^[ \t]*""forced_item_quality""[ \t]+""[^""]*""[ \t]*\r?\n", RegexOptions.Multiline | RegexOptions.IgnoreCase).Reverse())
                if (DepthAt(block, m.Index) == 1)
                    block = block.Remove(m.Index, m.Length);

            foreach (Match m in Regex.Matches(block, @"(""item_quality""\s+"")([^""]*)("")", RegexOptions.IgnoreCase))
                if (DepthAt(block, m.Index) == 1)
                    return block.Substring(0, m.Groups[2].Index) + "unusual" + block.Substring(m.Groups[2].Index + m.Groups[2].Length);

            foreach (Match m in Regex.Matches(block, @"""item_rarity""", RegexOptions.IgnoreCase))
                if (DepthAt(block, m.Index) == 1)
                    return InsertBeforeLineOf(block, m.Index, $"{IndentOfLine(block, m.Index)}\"item_quality\"\t\t\"unusual\"{eol}", eol);

            int open = block.IndexOf('{');
            if (open < 0) return block;
            int lineEnd = block.IndexOf('\n', open);
            string line = $"{IndentOfLine(block, open)}\t\"item_quality\"\t\t\"unusual\"{eol}";
            return lineEnd < 0 ? block.Insert(open + 1, eol + line) : block.Insert(lineEnd + 1, line);
        }

        private static int FindTopLevelBlock(string block, string key)
        {
            foreach (Match m in Regex.Matches(block, $@"""{Regex.Escape(key)}""\s*\{{", RegexOptions.IgnoreCase))
                if (DepthAt(block, m.Index) == 1)
                    return m.Index + m.Length - 1;
            return -1;
        }

        private static int DepthAt(string text, int index)
        {
            int depth = 0;
            bool inQuote = false, escape = false;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                char c = text[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inQuote = !inQuote; continue; }
                if (inQuote) continue;
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            return depth;
        }

        private static string IndentOfLine(string text, int index)
        {
            int start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            int end = start;
            while (end < text.Length && (text[end] == '\t' || text[end] == ' ')) end++;
            return text.Substring(start, end - start);
        }

        private static string InsertBeforeLineOf(string text, int index, string lines, string eol)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            bool onlyIndent = text.AsSpan(lineStart, index - lineStart).Trim().IsEmpty;
            return onlyIndent ? text.Insert(lineStart, lines) : text.Insert(index, eol + lines);
        }
    }
}
