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
using System.Text;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Core.Services.Misc
{
    public record WardModelInfo
    {
        public string ModelPath { get; init; } = string.Empty;

        public int? StyleIndex { get; init; }
    }

    public record WardModelMapping
    {
        public string SourcePath { get; init; } = string.Empty;

        public string TargetFileName { get; init; } = string.Empty;
    }

    public static class WardPatcherService
    {
        public static readonly string[] AllBaseFiles =
        {
            "default_ward.vmdl_c"
        };

        public const string DefaultWardItemId = "596";

        private static readonly HashSet<string> ImmutableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "name", "prefab", "baseitem"
        };

        public static List<WardModelInfo> ParseWardVisuals(string wardBlock, int? styleIndex = null)
        {
            var models = new List<WardModelInfo>();
            if (string.IsNullOrEmpty(wardBlock)) return models;

            var visualsMatch = Regex.Match(wardBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return models;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(wardBlock, visualsStart);
            if (visualsEnd < 0) return models;

            string visualsContent = wardBlock.Substring(visualsStart, visualsEnd - visualsStart);

            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(blockStart, blockEnd - blockStart);

                var typeMatch = Regex.Match(modifierBlock,
                    @"""type""\s+""entity_model""", RegexOptions.IgnoreCase);
                if (!typeMatch.Success) continue;

                var modifierMatch = Regex.Match(modifierBlock,
                    @"""modifier""\s+""([^""]+\.vmdl)""", RegexOptions.IgnoreCase);
                if (!modifierMatch.Success) continue;

                string modelPath = modifierMatch.Groups[1].Value;

                int? entryStyle = null;
                var styleMatch = Regex.Match(modifierBlock,
                    @"""style""\s+""(\d+)""", RegexOptions.IgnoreCase);
                if (styleMatch.Success)
                {
                    entryStyle = int.Parse(styleMatch.Groups[1].Value);
                }

                if (styleIndex.HasValue)
                {
                    if (entryStyle.HasValue && entryStyle.Value != styleIndex.Value)
                        continue;
                }

                models.Add(new WardModelInfo
                {
                    ModelPath = modelPath,
                    StyleIndex = entryStyle
                });
            }

            return models;
        }

        public static List<string> ExtractParticleCreateEntries(string wardBlock, int? styleIndex = null)
        {
            var particles = new List<string>();
            if (string.IsNullOrEmpty(wardBlock)) return particles;

            var visualsMatch = Regex.Match(wardBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return particles;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(wardBlock, visualsStart);
            if (visualsEnd < 0) return particles;

            string visualsContent = wardBlock.Substring(visualsStart, visualsEnd - visualsStart);

            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(match.Index, blockEnd - match.Index);

                if (!Regex.IsMatch(modifierBlock, @"""type""\s+""particle_create""", RegexOptions.IgnoreCase))
                    continue;

                if (styleIndex.HasValue)
                {
                    var styleMatch = Regex.Match(modifierBlock,
                        @"""style""\s+""(\d+)""", RegexOptions.IgnoreCase);

                    if (styleMatch.Success)
                    {
                        int entryStyle = int.Parse(styleMatch.Groups[1].Value);
                        if (entryStyle != styleIndex.Value)
                            continue;
                    }
                }

                if (styleIndex.HasValue)
                {
                    modifierBlock = Regex.Replace(modifierBlock,
                        @"\r?\n[^\n]*""style""\s+""\d+""[^\n]*", "",
                        RegexOptions.IgnoreCase);
                }

                particles.Add(modifierBlock);
            }

            return particles;
        }

        public static string BuildMergedWardBlock(string defaultBlock, string selectedBlock, int? styleIndex = null)
        {
            if (string.IsNullOrEmpty(defaultBlock) || string.IsNullOrEmpty(selectedBlock))
                return defaultBlock ?? string.Empty;

            var defaultKvs = ParseTopLevelKeyValues(defaultBlock);
            var selectedKvs = ParseTopLevelKeyValues(selectedBlock);

            string? defaultVisualsBlock = ExtractNamedBlock(defaultBlock, "visuals");
            string? selectedVisualsBlock = ExtractNamedBlock(selectedBlock, "visuals");
            string? selectedPortraits = ExtractNamedBlock(selectedBlock, "portraits");
            string? selectedStaticAttributes = ExtractNamedBlock(selectedBlock, "static_attributes");

            var particleEntries = ExtractParticleCreateEntries(selectedBlock, styleIndex);

            var visualsKvPairs = ExtractVisualsKeyValues(selectedVisualsBlock);

            var sb = new StringBuilder();

            sb.AppendLine($"\t\"{DefaultWardItemId}\"");
            sb.AppendLine("\t{");

            foreach (var field in ImmutableFields)
            {
                if (defaultKvs.TryGetValue(field, out var val))
                {
                    sb.AppendLine($"\t\t\"{field}\"\t\t\"{val}\"");
                }
            }

            var skipFields = new HashSet<string>(ImmutableFields, StringComparer.OrdinalIgnoreCase)
            {
                "visuals", "portraits", "static_attributes", "creation_date"
            };

            foreach (var kvp in selectedKvs)
            {
                if (skipFields.Contains(kvp.Key)) continue;
                sb.AppendLine($"\t\t\"{kvp.Key}\"\t\t\"{kvp.Value}\"");
            }

            if (!string.IsNullOrEmpty(selectedStaticAttributes))
            {
                sb.AppendLine(IndentBlock(selectedStaticAttributes, 2));
            }

            if (!string.IsNullOrEmpty(selectedPortraits))
            {
                sb.AppendLine(IndentBlock(selectedPortraits, 2));
            }

            if (!string.IsNullOrEmpty(defaultVisualsBlock))
            {
                string mergedVisuals = defaultVisualsBlock;

                if (visualsKvPairs.Count > 0)
                {
                    mergedVisuals = InjectVisualsKVPairs(mergedVisuals, visualsKvPairs);
                }

                if (particleEntries.Count > 0)
                {
                    mergedVisuals = AppendToVisualsBlock(mergedVisuals, particleEntries);
                }

                sb.AppendLine(IndentBlock(mergedVisuals, 2));
            }

            sb.Append("\t}");

            return sb.ToString();
        }

        public static List<WardModelMapping> GetModelMapping(List<WardModelInfo> models)
        {
            var mappings = new List<WardModelMapping>();
            if (models == null || models.Count == 0) return mappings;

            var uniqueModels = models
                .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            string sourcePath = uniqueModels[0].ModelPath;
            foreach (var target in AllBaseFiles)
            {
                mappings.Add(new WardModelMapping
                {
                    SourcePath = sourcePath,
                    TargetFileName = target
                });
            }

            return mappings;
        }

        public static List<string> GetVpkExtractionPaths(List<WardModelInfo> models)
        {
            return models
                .Select(m => m.ModelPath + "_c")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string? ExtractVisualsBlock(string wardBlock)
        {
            return ExtractNamedBlock(wardBlock, "visuals");
        }

        #region Visuals Helpers

        private static Dictionary<string, string> ExtractVisualsKeyValues(string? visualsBlock)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(visualsBlock)) return result;

            var kvPattern = new Regex(
                @"^\s*""([^""]+)""\s+""([^""]*)""",
                RegexOptions.Multiline);

            var skipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "visuals", "type", "modifier", "asset", "style"
            };

            int outerBrace = visualsBlock.IndexOf('{');
            if (outerBrace < 0) return result;

            int depth = 0;
            var lineDepths = new Dictionary<int, int>();

            for (int i = outerBrace; i < visualsBlock.Length; i++)
            {
                if (visualsBlock[i] == '{') depth++;
                else if (visualsBlock[i] == '}') depth--;
                else if (visualsBlock[i] == '\n' && i + 1 < visualsBlock.Length)
                    lineDepths[i + 1] = depth;
            }

            foreach (Match match in kvPattern.Matches(visualsBlock))
            {
                int lineStart = match.Index;
                while (lineStart > 0 && visualsBlock[lineStart - 1] != '\n')
                    lineStart--;

                int lineDepth = 0;
                if (lineDepths.TryGetValue(lineStart, out int d))
                    lineDepth = d;
                else if (lineStart <= outerBrace + 1)
                    lineDepth = 1;

                if (lineDepth != 1) continue;

                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                if (int.TryParse(key, out _)) continue;
                if (skipKeys.Contains(key)) continue;
                if (key.StartsWith("asset_modifier", StringComparison.OrdinalIgnoreCase)) continue;

                result[key] = value;
            }

            return result;
        }

        private static string InjectVisualsKVPairs(string visualsBlock, Dictionary<string, string> kvPairs)
        {
            if (kvPairs.Count == 0) return visualsBlock;

            int bracePos = visualsBlock.IndexOf('{');
            if (bracePos < 0) return visualsBlock;

            var sb = new StringBuilder();
            foreach (var kvp in kvPairs)
            {
                sb.AppendLine($"\t\t\"{kvp.Key}\"\t\t\"{kvp.Value}\"");
            }

            int insertPos = bracePos + 1;
            if (insertPos < visualsBlock.Length && visualsBlock[insertPos] == '\r') insertPos++;
            if (insertPos < visualsBlock.Length && visualsBlock[insertPos] == '\n') insertPos++;

            return visualsBlock.Substring(0, insertPos) + sb.ToString() + visualsBlock.Substring(insertPos);
        }

        #endregion

        #region Private Helpers

        private static Dictionary<string, string> ParseTopLevelKeyValues(string block)
        {
            var kvs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(block)) return kvs;

            var kvPattern = new Regex(
                @"^\s*""([^""]+)""\s+""([^""]*)""",
                RegexOptions.Multiline);

            int outerBrace = block.IndexOf('{');
            if (outerBrace < 0) return kvs;

            int depth = 0;
            var lineDepths = new Dictionary<int, int>();

            for (int i = outerBrace; i < block.Length; i++)
            {
                if (block[i] == '{')
                {
                    depth++;
                }
                else if (block[i] == '}')
                {
                    depth--;
                }
                else if (block[i] == '\n')
                {
                    if (i + 1 < block.Length)
                        lineDepths[i + 1] = depth;
                }
            }

            foreach (Match match in kvPattern.Matches(block))
            {
                int matchPos = match.Index;
                int lineDepth = 0;

                int lineStart = matchPos;
                while (lineStart > 0 && block[lineStart - 1] != '\n')
                    lineStart--;

                if (lineDepths.TryGetValue(lineStart, out int d))
                    lineDepth = d;
                else if (lineStart <= outerBrace + 1)
                    lineDepth = 1;

                if (lineDepth != 1) continue;

                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                if (int.TryParse(key, out _)) continue;

                kvs[key] = value;
            }

            return kvs;
        }

        private static string? ExtractNamedBlock(string content, string blockName)
        {
            var pattern = new Regex(
                $@"""{Regex.Escape(blockName)}""\s*\{{",
                RegexOptions.IgnoreCase);

            var match = pattern.Match(content);
            if (!match.Success) return null;

            int braceStart = content.IndexOf('{', match.Index + blockName.Length + 2);
            if (braceStart < 0) return null;

            int braceEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(content, braceStart);
            if (braceEnd < 0) return null;

            int lineStart = FindLineStart(content, match.Index);
            return content.Substring(lineStart, braceEnd - lineStart);
        }

        private static string AppendToVisualsBlock(string visualsBlock, List<string> entries)
        {
            int lastBrace = visualsBlock.LastIndexOf('}');
            if (lastBrace < 0) return visualsBlock;

            var sb = new StringBuilder();
            sb.Append(visualsBlock.Substring(0, lastBrace));

            int maxIndex = -1;
            foreach (Match m in Regex.Matches(visualsBlock,
                @"""asset_modifier(\d+)""", RegexOptions.IgnoreCase))
            {
                if (int.TryParse(m.Groups[1].Value, out int idx) && idx > maxIndex)
                    maxIndex = idx;
            }
            int modifierIndex = maxIndex + 1;

            foreach (var entry in entries)
            {
                var renumbered = Regex.Replace(entry,
                    @"""asset_modifier\d*""",
                    $"\"asset_modifier{modifierIndex}\"",
                    RegexOptions.IgnoreCase);
                sb.AppendLine($"\t\t{renumbered.Trim()}");
                modifierIndex++;
            }

            sb.Append(visualsBlock.Substring(lastBrace));
            return sb.ToString();
        }

        private static string IndentBlock(string block, int tabDepth)
        {
            var prefix = new string('\t', tabDepth);
            var lines = block.Split('\n');
            var sb = new StringBuilder();

            int minTabs = int.MaxValue;
            foreach (var rawLine in lines)
            {
                var stripped = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(stripped)) continue;
                int tabs = 0;
                foreach (char c in stripped)
                {
                    if (c == '\t') tabs++;
                    else break;
                }
                minTabs = Math.Min(minTabs, tabs);
            }
            if (minTabs == int.MaxValue) minTabs = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var trimmed = line.TrimStart('\t', ' ');

                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                int existingTabs = 0;
                foreach (char c in line)
                {
                    if (c == '\t') existingTabs++;
                    else break;
                }
                int relativeTabs = Math.Max(0, existingTabs - minTabs);
                string newLine = prefix + new string('\t', relativeTabs) + trimmed;

                if (i < lines.Length - 1)
                    sb.AppendLine(newLine);
                else
                    sb.Append(newLine);
            }

            return sb.ToString();
        }

        private static int FindLineStart(string content, int index)
        {
            while (index > 0 && content[index - 1] != '\n')
                index--;
            return index;
        }

        #endregion
    }
}
