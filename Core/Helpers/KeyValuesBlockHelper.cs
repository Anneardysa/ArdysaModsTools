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
using ValveKeyValue;

namespace ArdysaModsTools.Core.Helpers
{
    public static class KeyValuesBlockHelper
    {
        public static string? ExtractBlockById(string content, string id, string? heroId = null, bool requireItemMarkers = true)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(id)) return null;

            string token = $"\"{id}\"";
            int searchPos = 0;
            int attempts = 0;
            const int MAX_ATTEMPTS = 100000;

            while (true)
            {
                if (attempts++ > MAX_ATTEMPTS) break;
                int pos = content.IndexOf(token, searchPos, StringComparison.Ordinal);
                if (pos < 0) break;

                int closingQuote = pos + token.Length - 1;
                int after = SkipWhitespace(content, closingQuote + 1);
                if (after >= content.Length) { searchPos = pos + 1; continue; }
                if (content[after] != '{') { searchPos = pos + 1; continue; }

                int prev = PrevNonWhitespaceIndex(content, pos - 1);
                if (prev >= 0 && content[prev] == '"') { searchPos = pos + 1; continue; }

                int blockStart = FindLineStart(content, pos);
                if (blockStart == 0 && pos > 0 && content.IndexOf('\n', 0, pos) < 0)
                {
                    blockStart = pos;
                }

                int blockEndExclusive = ExtractBalancedBlockEnd(content, after);
                if (blockEndExclusive <= blockStart || blockEndExclusive < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                string block = content.Substring(blockStart, blockEndExclusive - blockStart);

                if (requireItemMarkers && !IsLikelyItemBlock(block))
                {
                    searchPos = pos + 1;
                    continue;
                }

                if (!string.IsNullOrEmpty(heroId) &&
                    block.IndexOf($"\"{heroId}\"", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                return block;
            }

            return null;
        }

        public static string ReplaceIdBlock(string content, string id, string replacementBlock, bool requireItemMarkers = true)
        {
            return ReplaceIdBlock(content, id, replacementBlock, out _, null, requireItemMarkers);
        }

        public static string ReplaceIdBlock(string content, string id, string replacementBlock, out bool didReplace, string? heroId = null, bool requireItemMarkers = true, string? requirePrefab = null)
        {
            didReplace = false;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(content)) return content;

            string token = $"\"{id}\"";
            int searchPos = 0;
            int attempts = 0;
            const int MAX_ATTEMPTS = 100000;

            while (true)
            {
                if (attempts++ > MAX_ATTEMPTS) break;
                int pos = content.IndexOf(token, searchPos, StringComparison.Ordinal);
                if (pos < 0) break;

                int closingQuote = pos + token.Length - 1;
                int after = SkipWhitespace(content, closingQuote + 1);
                if (after >= content.Length) { searchPos = pos + 1; continue; }
                if (content[after] != '{') { searchPos = pos + 1; continue; }

                int prev = PrevNonWhitespaceIndex(content, pos - 1);
                if (prev >= 0 && content[prev] == '"') { searchPos = pos + 1; continue; }

                int blockStart = FindLineStart(content, pos);
                if (blockStart == 0 && pos > 0 && content.IndexOf('\n', 0, pos) < 0)
                {
                    blockStart = pos;
                }

                int blockEndExclusive = ExtractBalancedBlockEnd(content, after);
                if (blockEndExclusive <= blockStart || blockEndExclusive < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                string existingBlock = content.Substring(blockStart, blockEndExclusive - blockStart);

                if (requireItemMarkers && !IsLikelyItemBlock(existingBlock))
                {
                    searchPos = pos + 1;
                    continue;
                }

                if (!string.IsNullOrEmpty(heroId) &&
                    existingBlock.IndexOf($"\"{heroId}\"", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                if (!string.IsNullOrEmpty(requirePrefab) &&
                    !(TryGetTopLevelValue(existingBlock, "prefab", out var existingPrefab) &&
                      string.Equals(existingPrefab, requirePrefab, StringComparison.OrdinalIgnoreCase)))
                {
                    searchPos = pos + 1;
                    continue;
                }

                var before = content.Substring(0, blockStart);
                var afterBlock = content.Substring(blockEndExclusive);
                var rep = replacementBlock.TrimEnd() + "\n";
                content = before + rep + afterBlock;
                didReplace = true;
                return content;
            }

            return content;
        }

        public static Dictionary<string, string> ParseKvBlocks(string raw)
        {
            raw = NormalizeKvText(raw);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int pos = 0;
            while (pos < raw.Length)
            {
                int q1 = raw.IndexOf('"', pos);
                if (q1 < 0) break;
                int q2 = raw.IndexOf('"', q1 + 1);
                if (q2 < 0) break;

                var token = raw.Substring(q1 + 1, q2 - q1 - 1);
                pos = q2 + 1;

                if (!Regex.IsMatch(token, @"^\d+$")) continue;

                int braceStart = SkipWhitespace(raw, pos);
                if (braceStart >= raw.Length || raw[braceStart] != '{') continue;

                int braceEnd = ExtractBalancedBlockEnd(raw, braceStart);
                if (braceEnd < 0) continue;

                int lineStart = FindLineStart(raw, q1);
                var block = raw.Substring(lineStart, braceEnd - lineStart);
                result[token] = block;
                pos = braceEnd;
            }

            return result;
        }

        public static bool TryGetTopLevelValue(string block, string key, out string? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(block)) return false;

            try
            {
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(block));
                var doc = kv.Deserialize(ms);
                if (doc.Value.ValueType != KVValueType.Collection) return false;

                var child = ((IEnumerable<KVObject>)doc.Value)
                    .FirstOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));
                if (child == null || child.Value.ValueType == KVValueType.Collection) return false;

                value = child.Value.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool AnyBlockHasItemSlot(string indexText, string slotValue)
        {
            if (string.IsNullOrWhiteSpace(indexText)) return false;

            foreach (var block in ParseKvBlocks(indexText).Values)
            {
                if (TryGetTopLevelValue(block, "item_slot", out var v) &&
                    string.Equals(v, slotValue, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static int ExtractBalancedBlockEnd(string text, int firstBraceIdx)
        {
            if (firstBraceIdx < 0 || firstBraceIdx >= text.Length || text[firstBraceIdx] != '{') return -1;
            int depth = 0;
            bool inQuote = false;
            bool escape = false;
            int n = text.Length;

            for (int i = firstBraceIdx; i < n; i++)
            {
                char c = text[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inQuote = !inQuote; continue; }
                if (inQuote) continue;
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }

            return -1;
        }

        public static bool IsLikelyItemBlock(string block)
        {
            if (string.IsNullOrEmpty(block)) return false;

            string[] markers = new[]
            {
                "\"used_by_heroes\"",
                "\"prefab\"",
                "\"model_player\"",
                "\"item_name\"",
                "\"image_inventory\"",
                "\"portraits\"",
                "\"visuals\"",
                "\"item_slot\"",
                "\"item_type_name\"",
                "\"name\""
            };

            foreach (var m in markers)
            {
                if (block.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return block.Length >= 80;
        }

        public static int SkipWhitespace(string s, int idx)
        {
            int n = s.Length;
            while (idx < n && char.IsWhiteSpace(s[idx])) idx++;
            return idx;
        }

        public static int PrevNonWhitespaceIndex(string s, int startIdx)
        {
            int p = startIdx;
            while (p >= 0 && char.IsWhiteSpace(s[p])) p--;
            return p;
        }

        public static int FindLineStart(string text, int idx)
        {
            if (idx <= 0) return 0;
            for (int i = idx - 1; i >= 0; i--)
            {
                if (text[i] == '\n') return i + 1;
            }
            return 0;
        }

        public static string NormalizeKvText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            if (raw[0] == '\uFEFF') raw = raw.Substring(1);

            raw = raw.Replace("\r\n", "\n").Replace('\r', '\n');

            raw = raw.Replace('\u201C', '"').Replace('\u201D', '"');
            raw = raw.Replace('\u2018', '\'').Replace('\u2019', '\'');

            raw = raw.Replace('\u00A0', ' ').Replace('\u2007', ' ').Replace('\u202F', ' ');

            char[] zeroWidth = { '\u200B', '\u200C', '\u200D', '\uFEFF', '\u2060' };
            foreach (var ch in zeroWidth)
                raw = raw.Replace(ch.ToString(), string.Empty);

            return raw;
        }

        public static string PrettifyKvText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            raw = NormalizeKvText(raw);

            int lineCount = raw.Count(c => c == '\n');
            if (lineCount > 100) return raw;

            var result = new System.Text.StringBuilder(raw.Length * 2);
            int indent = 0;
            bool inQuote = false;
            bool escape = false;

            int tokenOnLine = 0;

            bool afterBrace = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                if (escape)
                {
                    result.Append(c);
                    escape = false;
                    continue;
                }

                if (c == '\\' && inQuote)
                {
                    result.Append(c);
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    if (!inQuote)
                    {
                        if (afterBrace)
                        {
                            result.Append('\n');
                            result.Append('\t', indent);
                            afterBrace = false;
                            tokenOnLine = 0;
                        }
                        else if (tokenOnLine == 1)
                        {
                            result.Append('\t', 2);
                        }
                        else if (tokenOnLine >= 2)
                        {
                            result.Append('\n');
                            result.Append('\t', indent);
                            tokenOnLine = 0;
                        }

                        result.Append('"');
                        inQuote = true;
                    }
                    else
                    {
                        result.Append('"');
                        inQuote = false;
                        tokenOnLine++;
                    }
                    continue;
                }

                if (inQuote)
                {
                    result.Append(c);
                    continue;
                }

                if (c == '{')
                {
                    result.Append('\n');
                    result.Append('\t', indent);
                    result.Append('{');
                    indent++;
                    tokenOnLine = 0;
                    afterBrace = true;
                }
                else if (c == '}')
                {
                    indent = Math.Max(0, indent - 1);
                    result.Append('\n');
                    result.Append('\t', indent);
                    result.Append('}');
                    tokenOnLine = 0;
                    afterBrace = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                }
                else
                {
                    if (afterBrace)
                    {
                        result.Append('\n');
                        result.Append('\t', indent);
                        afterBrace = false;
                    }
                    else if (tokenOnLine >= 2)
                    {
                        result.Append('\n');
                        result.Append('\t', indent);
                        tokenOnLine = 0;
                    }
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        public static string MergeBlocks(string blockA, string blockB, bool preferRightSideStrongly = false)
        {
            if (string.IsNullOrEmpty(blockA)) return blockB;
            if (string.IsNullOrEmpty(blockB)) return blockA;

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            
            using var msA = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(blockA));
            var docA = kv.Deserialize(msA);
            
            using var msB = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(blockB));
            var docB = kv.Deserialize(msB);

            var mergedRoot = MergeObjects(docA, docB, preferRightSideStrongly);
            
            using var msOut = new MemoryStream();
            kv.Serialize(msOut, mergedRoot);
            var mergedText = System.Text.Encoding.UTF8.GetString(msOut.ToArray());
            return FormatBlockToDoubleTabs(mergedText);
        }

        private static KVObject MergeObjects(KVObject a, KVObject b, bool preferRightSideStrongly = false)
        {
            if (a == null) return b;
            if (b == null) return a;

            if (a.Value.ValueType == KVValueType.Collection && b.Value.ValueType != KVValueType.Collection)
                return a;
            if (b.Value.ValueType == KVValueType.Collection && a.Value.ValueType != KVValueType.Collection)
                return b;

            if (a.Value.ValueType == KVValueType.Collection && b.Value.ValueType == KVValueType.Collection)
            {
                var childrenA = ((IEnumerable<KVObject>)a.Value).ToList();
                var childrenB = ((IEnumerable<KVObject>)b.Value).ToList();

                var mergedList = new List<KVObject>();
                var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var multiKeys = new[] { "asset_modifier", "particle_combined" };

                foreach (var mk in multiKeys)
                {
                    var mka = childrenA.Where(c => string.Equals(c.Name, mk, StringComparison.OrdinalIgnoreCase)).ToList();
                    var mkb = childrenB.Where(c => string.Equals(c.Name, mk, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (mka.Count > 0 || mkb.Count > 0)
                    {
                        var mergedModifiers = new List<KVObject>();
                        foreach (var vanilla in mka)
                        {
                            var overrideByB = mkb.FirstOrDefault(modB => IsModifierOverride(vanilla, modB));
                            if (overrideByB != null)
                            {
                                mergedModifiers.Add(overrideByB);
                            }
                            else
                            {
                                mergedModifiers.Add(vanilla);
                            }
                        }
                        foreach (var modB in mkb)
                        {
                            var overridesA = mka.Any(vanilla => IsModifierOverride(vanilla, modB));
                            if (!overridesA)
                            {
                                if (!ContainsModifier(mergedModifiers, modB))
                                    mergedModifiers.Add(modB);
                            }
                        }
                        mergedList.AddRange(mergedModifiers);
                        processedKeys.Add(mk);
                    }
                }

                var allKeys = childrenA.Select(c => c.Name)
                    .Concat(childrenB.Select(c => c.Name))
                    .Where(k => !processedKeys.Contains(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var key in allKeys)
                {
                    var itemA = childrenA.FirstOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));
                    var itemB = childrenB.FirstOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));

                    if (itemA != null && itemB != null)
                    {
                        if (itemA.Value.ValueType == KVValueType.Collection && itemB.Value.ValueType == KVValueType.Collection)
                        {
                            mergedList.Add(MergeObjects(itemA, itemB, preferRightSideStrongly));
                        }
                        else
                        {
                            mergedList.Add(ChoosePreferredProperty(key, itemA, itemB, preferRightSideStrongly));
                        }
                    }
                    else if (itemA != null)
                    {
                        mergedList.Add(itemA);
                    }
                    else if (itemB != null)
                    {
                        mergedList.Add(itemB);
                    }
                }

                return new KVObject(a.Name, mergedList.ToArray());
            }
            else
            {
                return b;
            }
        }

        private static bool ContainsModifier(List<KVObject> list, KVObject item)
        {
            if (item.Value.ValueType != KVValueType.Collection) return false;
            var itemProps = ((IEnumerable<KVObject>)item.Value)
                .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            foreach (var existing in list)
            {
                if (existing.Value.ValueType != KVValueType.Collection) continue;
                var existingProps = ((IEnumerable<KVObject>)existing.Value)
                    .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                if (itemProps.Count != existingProps.Count) continue;

                bool match = true;
                foreach (var kvp in itemProps)
                {
                    if (!existingProps.TryGetValue(kvp.Key, out var existingVal) || !string.Equals(kvp.Value, existingVal, StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        private static KVObject ChoosePreferredProperty(string key, KVObject a, KVObject b, bool preferRightSideStrongly)
        {
            var valA = a.Value.ToString();
            var valB = b.Value.ToString();

            if (string.Equals(key, "skip_model_combine", StringComparison.OrdinalIgnoreCase))
            {
                if (valA == "1" || valB == "1")
                    return new KVObject(key, "1");
            }

            if (preferRightSideStrongly)
            {
                return b;
            }

            if (string.Equals(key, "image_inventory", StringComparison.OrdinalIgnoreCase))
            {
                if (IsVanillaValue("image_inventory", valB) && !string.IsNullOrEmpty(valA))
                    return a;
            }
            if (string.Equals(key, "item_name", StringComparison.OrdinalIgnoreCase))
            {
                if (IsVanillaValue("item_name", valB) && !string.IsNullOrEmpty(valA))
                    return a;
            }
            if (string.Equals(key, "item_type_name", StringComparison.OrdinalIgnoreCase))
            {
                if (IsVanillaValue("item_type_name", valB) && !string.IsNullOrEmpty(valA))
                    return a;
            }
            return b;
        }

        private static bool IsModifierOverride(KVObject a, KVObject b)
        {
            if (a.Value.ValueType != KVValueType.Collection || b.Value.ValueType != KVValueType.Collection) return false;

            var propsA = ((IEnumerable<KVObject>)a.Value).ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var propsB = ((IEnumerable<KVObject>)b.Value).ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            propsA.TryGetValue("type", out var typeA);
            propsB.TryGetValue("type", out var typeB);

            if (!string.Equals(typeA, typeB, StringComparison.OrdinalIgnoreCase)) return false;
            
            if (string.Equals(typeB, "particle_create", StringComparison.OrdinalIgnoreCase))
            {
                propsA.TryGetValue("required_arcana_level", out var reqA);
                propsB.TryGetValue("required_arcana_level", out var reqB);
                if (!string.IsNullOrEmpty(reqA) || !string.IsNullOrEmpty(reqB))
                {
                    return string.Equals(reqA, reqB, StringComparison.OrdinalIgnoreCase);
                }
                
                return true;
            }
            
            propsA.TryGetValue("asset", out var assetA);
            propsB.TryGetValue("asset", out var assetB);

            if (!string.IsNullOrEmpty(assetA) && !string.IsNullOrEmpty(assetB))
            {
                return string.Equals(assetA, assetB, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsVanillaValue(string key, string? val)
        {
            if (string.IsNullOrEmpty(val)) return false;

            if (string.Equals(key, "image_inventory", StringComparison.OrdinalIgnoreCase))
            {
                if (val.Contains("econ/heroes/", StringComparison.OrdinalIgnoreCase) && 
                    !val.Contains("econ/items/", StringComparison.OrdinalIgnoreCase) && 
                    !val.Contains("econ/sets/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (val.EndsWith("/base", StringComparison.OrdinalIgnoreCase) || val.EndsWith("/default", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            if (string.Equals(key, "item_name", StringComparison.OrdinalIgnoreCase))
            {
                if (val.StartsWith("#DOTA_Item_", StringComparison.OrdinalIgnoreCase))
                {
                    var suffixes = new[] { "_Bracers", "_Arms", "_Shoulders", "_Head", "_Belt", "_Back", "_Weapon", "_Offhand", "_Legs", "_Tail", "_Armor", "_Mount", "_Misc", "_Neck", "_Body", "_Hands", "_Base", "_base", "_default" };
                    foreach (var s in suffixes)
                    {
                        if (val.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                if (val.Contains("_Base", StringComparison.OrdinalIgnoreCase) || val.Contains("_base", StringComparison.OrdinalIgnoreCase) || val.Contains("_default", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            if (string.Equals(key, "item_type_name", StringComparison.OrdinalIgnoreCase))
            {
                if (val.StartsWith("#DOTA_WearableType_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        public static string FormatBlockToDoubleTabs(string block)
        {
            var lines = block.Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder(block.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (i < lines.Length - 1) result.Append('\n');
                    continue;
                }

                int firstQuote = line.IndexOf('"');
                if (firstQuote >= 0)
                {
                    int secondQuote = line.IndexOf('"', firstQuote + 1);
                    if (secondQuote >= 0)
                    {
                        int thirdQuote = line.IndexOf('"', secondQuote + 1);
                        if (thirdQuote >= 0)
                        {
                            var indentAndKey = line.Substring(0, secondQuote + 1);
                            var value = line.Substring(thirdQuote);
                            line = indentAndKey + "\t\t" + value;
                        }
                    }
                }
                result.Append(line);
                if (i < lines.Length - 1) result.Append('\n');
            }
            return result.ToString();
        }

        #region Structure-Preserving Overlay

        private static readonly HashSet<string> EssentialVanillaKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "used_by_heroes",
                "hero_presets",
                "item_slot",
                "prefab",
            };

        public static string OverlayBlockPreservingStructure(string vanillaBlock, string indexBlock)
        {
            if (string.IsNullOrEmpty(indexBlock)) return vanillaBlock;
            if (string.IsNullOrEmpty(vanillaBlock)) return indexBlock;

            indexBlock = indexBlock.Replace("\r\n", "\n").Replace('\r', '\n');
            string vanillaLf = vanillaBlock.Replace("\r\n", "\n").Replace('\r', '\n');

            var indexChildren = EnumerateTopLevelChildren(indexBlock);

            if (indexChildren.Count == 0) return indexBlock;

            var vanillaChildren = EnumerateTopLevelChildren(vanillaLf);
            var indexKeys = new HashSet<string>(indexChildren.Select(c => c.Key), StringComparer.OrdinalIgnoreCase);

            var vanillaOnly = vanillaChildren
                .Where(c => EssentialVanillaKeys.Contains(c.Key) && !indexKeys.Contains(c.Key))
                .ToList();

            if (vanillaOnly.Count == 0) return indexBlock;

            string childIndent = LeadingWhitespace(indexChildren[0].RawText);

            int braceStart = IndexOfTopLevelBrace(indexBlock);
            int braceEnd = ExtractBalancedBlockEnd(indexBlock, braceStart);
            if (braceStart < 0 || braceEnd < 0) return indexBlock;
            int closeLineStart = FindLineStart(indexBlock, braceEnd - 1);

            var appended = new System.Text.StringBuilder();
            foreach (var child in vanillaOnly)
            {
                appended.Append(ReindentChild(child.RawText, childIndent));
                appended.Append('\n');
            }

            string before = indexBlock.Substring(0, closeLineStart);
            string after = indexBlock.Substring(closeLineStart);
            if (!before.EndsWith("\n")) before += "\n";

            return before + appended.ToString() + after;
        }

        private readonly struct TopLevelChild
        {
            public readonly string Key;
            public readonly string RawText;
            public TopLevelChild(string key, string rawText) { Key = key; RawText = rawText; }
        }

        private static List<TopLevelChild> EnumerateTopLevelChildren(string block)
        {
            var result = new List<TopLevelChild>();
            if (string.IsNullOrEmpty(block)) return result;

            int braceStart = IndexOfTopLevelBrace(block);
            if (braceStart < 0) return result;

            int braceEnd = ExtractBalancedBlockEnd(block, braceStart);
            if (braceEnd < 0) return result;
            int bodyEnd = braceEnd - 1;

            int pos = braceStart + 1;
            while (pos < bodyEnd)
            {
                int keyQuote = block.IndexOf('"', pos);
                if (keyQuote < 0 || keyQuote >= bodyEnd) break;

                int keyQuoteEnd = FindClosingQuote(block, keyQuote);
                if (keyQuoteEnd < 0 || keyQuoteEnd >= bodyEnd) break;

                string key = block.Substring(keyQuote + 1, keyQuoteEnd - keyQuote - 1);
                int lineStart = FindLineStart(block, keyQuote);

                int afterKey = SkipWhitespace(block, keyQuoteEnd + 1);
                if (afterKey >= bodyEnd) break;

                int spanEnd;
                if (block[afterKey] == '{')
                {
                    int subEnd = ExtractBalancedBlockEnd(block, afterKey);
                    if (subEnd < 0) break;
                    spanEnd = subEnd;
                }
                else if (block[afterKey] == '"')
                {
                    int valEnd = FindClosingQuote(block, afterKey);
                    if (valEnd < 0) break;
                    spanEnd = valEnd + 1;
                }
                else
                {
                    break;
                }

                result.Add(new TopLevelChild(key, block.Substring(lineStart, spanEnd - lineStart)));
                pos = spanEnd;
            }

            return result;
        }

        private static int IndexOfTopLevelBrace(string s)
        {
            bool inQuote = false, escape = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inQuote = !inQuote; continue; }
                if (!inQuote && c == '{') return i;
            }
            return -1;
        }

        private static int FindClosingQuote(string s, int openQuoteIdx)
        {
            bool escape = false;
            for (int i = openQuoteIdx + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') return i;
            }
            return -1;
        }

        private static string LeadingWhitespace(string text)
        {
            int firstNl = text.IndexOf('\n');
            string first = firstNl < 0 ? text : text.Substring(0, firstNl);
            int i = 0;
            while (i < first.Length && (first[i] == '\t' || first[i] == ' ')) i++;
            return first.Substring(0, i);
        }

        private static string ReindentChild(string raw, string targetIndent)
        {
            var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string baseIndent = LeadingWhitespace(lines.Length > 0 ? lines[0] : raw);

            var sb = new System.Text.StringBuilder(raw.Length + 16);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                {
                    if (i < lines.Length - 1) sb.Append('\n');
                    continue;
                }

                if (baseIndent.Length > 0 && line.StartsWith(baseIndent, StringComparison.Ordinal))
                    line = line.Substring(baseIndent.Length);
                else
                    line = line.TrimStart('\t', ' ');

                sb.Append(targetIndent).Append(line);
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        #endregion

        public static bool IsOneLinerFormat(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            int lineCount = content.Count(c => c == '\n');
            return lineCount < 100 && content.Length > 10000;
        }
    }
}

