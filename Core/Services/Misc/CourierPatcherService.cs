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
    public record CourierModelInfo
    {
        public string Type { get; init; } = string.Empty;

        public string ModelPath { get; init; } = string.Empty;

        public string Side { get; init; } = string.Empty;

        public int? StyleIndex { get; init; }
    }

    public record CourierModelMapping
    {
        public string SourcePath { get; init; } = string.Empty;

        public string TargetFileName { get; init; } = string.Empty;
    }

    public static class CourierPatcherService
    {

        public static readonly string[] AllBaseFiles =
        {
            "donkey.vmdl_c",
            "donkey_dire.vmdl_c",
            "donkey_wings.vmdl_c",
            "donkey_dire_wings.vmdl_c"
        };

        public const string DefaultCourierItemId = "595";

        private static readonly HashSet<string> ImmutableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "name", "prefab", "baseitem"
        };

        public static List<CourierModelInfo> ParseCourierVisuals(string courierBlock, int? styleIndex = null)
        {
            var models = new List<CourierModelInfo>();
            if (string.IsNullOrEmpty(courierBlock)) return models;

            var visualsMatch = Regex.Match(courierBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return models;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(courierBlock, visualsStart);
            if (visualsEnd < 0) return models;

            string visualsContent = courierBlock.Substring(visualsStart, visualsEnd - visualsStart);

            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(blockStart, blockEnd - blockStart);

                var typeMatch = Regex.Match(modifierBlock,
                    @"""type""\s+""(courier|courier_flying)""", RegexOptions.IgnoreCase);
                if (!typeMatch.Success) continue;

                string type = typeMatch.Groups[1].Value.ToLowerInvariant();

                var modifierMatch = Regex.Match(modifierBlock,
                    @"""modifier""\s+""([^""]+\.vmdl)""", RegexOptions.IgnoreCase);
                if (!modifierMatch.Success) continue;

                string modelPath = modifierMatch.Groups[1].Value;

                var assetMatch = Regex.Match(modifierBlock,
                    @"""asset""\s+""(radiant|dire)""", RegexOptions.IgnoreCase);
                string side = assetMatch.Success ? assetMatch.Groups[1].Value.ToLowerInvariant() : "radiant";

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

                models.Add(new CourierModelInfo
                {
                    Type = type,
                    ModelPath = modelPath,
                    Side = side,
                    StyleIndex = entryStyle
                });
            }

            return models;
        }

        public static List<string> ExtractParticleCreateEntries(string courierBlock, int? styleIndex = null)
        {
            var particles = new List<string>();
            if (string.IsNullOrEmpty(courierBlock)) return particles;

            var visualsMatch = Regex.Match(courierBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return particles;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(courierBlock, visualsStart);
            if (visualsEnd < 0) return particles;

            string visualsContent = courierBlock.Substring(visualsStart, visualsEnd - visualsStart);

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

        private static Dictionary<string, string> ExtractStyleOverrides(string? selectedVisualsBlock, int styleIndex)
        {
            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(selectedVisualsBlock)) return overrides;

            string? stylesBlock = ExtractNamedBlock(selectedVisualsBlock, "styles");
            if (!string.IsNullOrEmpty(stylesBlock))
            {
                var styleEntryPattern = new Regex(
                    $@"""{styleIndex}""\s*\{{", RegexOptions.IgnoreCase);
                var styleEntryMatch = styleEntryPattern.Match(stylesBlock);
                if (styleEntryMatch.Success)
                {
                    int braceStart = stylesBlock.IndexOf('{', styleEntryMatch.Index + styleEntryMatch.Length - 1);
                    if (braceStart >= 0)
                    {
                        int braceEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(stylesBlock, braceStart);
                        if (braceEnd > 0)
                        {
                            string styleContent = stylesBlock.Substring(braceStart, braceEnd - braceStart);

                            var nameMatch = Regex.Match(styleContent,
                                @"""name""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                            if (nameMatch.Success)
                                overrides["item_name"] = nameMatch.Groups[1].Value;

                            var skinMatch = Regex.Match(styleContent,
                                @"""skin""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                            if (skinMatch.Success)
                                overrides["skin"] = skinMatch.Groups[1].Value;
                        }
                    }
                }
            }

            string? altIconsBlock = ExtractNamedBlock(selectedVisualsBlock, "alternate_icons");
            if (!string.IsNullOrEmpty(altIconsBlock))
            {
                var iconEntryPattern = new Regex(
                    $@"""{styleIndex}""\s*\{{", RegexOptions.IgnoreCase);
                var iconEntryMatch = iconEntryPattern.Match(altIconsBlock);
                if (iconEntryMatch.Success)
                {
                    int braceStart = altIconsBlock.IndexOf('{', iconEntryMatch.Index + iconEntryMatch.Length - 1);
                    if (braceStart >= 0)
                    {
                        int braceEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(altIconsBlock, braceStart);
                        if (braceEnd > 0)
                        {
                            string iconContent = altIconsBlock.Substring(braceStart, braceEnd - braceStart);

                            var iconMatch = Regex.Match(iconContent,
                                @"""icon_path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                            if (iconMatch.Success)
                                overrides["image_inventory"] = iconMatch.Groups[1].Value;
                        }
                    }
                }
            }

            return overrides;
        }

        public static string BuildMergedCourierBlock(string defaultBlock, string selectedBlock, int? styleIndex = null)
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

            if (styleIndex.HasValue && !string.IsNullOrEmpty(selectedVisualsBlock))
            {
                var styleOverrides = ExtractStyleOverrides(selectedVisualsBlock, styleIndex.Value);

                if (styleOverrides.TryGetValue("item_name", out var styleName))
                    selectedKvs["item_name"] = styleName;
                if (styleOverrides.TryGetValue("image_inventory", out var styleIcon))
                    selectedKvs["image_inventory"] = styleIcon;

                if (styleOverrides.TryGetValue("skin", out var styleSkin))
                    visualsKvPairs["skin"] = styleSkin;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"\t\"{DefaultCourierItemId}\"");
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


        public static List<CourierModelMapping> GetModelMapping(List<CourierModelInfo> models)
        {
            var mappings = new List<CourierModelMapping>();
            if (models == null || models.Count == 0) return mappings;

            var uniqueModels = models
                .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var groundModels = models.Where(m => m.Type == "courier").ToList();
            var flyingModels = models.Where(m => m.Type == "courier_flying").ToList();

            if (uniqueModels.Count == 1)
            {
                string singlePath = uniqueModels[0].ModelPath;
                foreach (var target in AllBaseFiles)
                {
                    mappings.Add(new CourierModelMapping
                    {
                        SourcePath = singlePath,
                        TargetFileName = target
                    });
                }
            }
            else if (groundModels.Count > 0 && flyingModels.Count > 0)
            {
                string groundPath = groundModels[0].ModelPath;
                mappings.Add(new CourierModelMapping { SourcePath = groundPath, TargetFileName = "donkey.vmdl_c" });
                mappings.Add(new CourierModelMapping { SourcePath = groundPath, TargetFileName = "donkey_dire.vmdl_c" });

                string flyingPath = flyingModels[0].ModelPath;
                mappings.Add(new CourierModelMapping { SourcePath = flyingPath, TargetFileName = "donkey_wings.vmdl_c" });
                mappings.Add(new CourierModelMapping { SourcePath = flyingPath, TargetFileName = "donkey_dire_wings.vmdl_c" });

                var groundRadiant = groundModels.FirstOrDefault(m => m.Side == "radiant");
                var groundDire = groundModels.FirstOrDefault(m => m.Side == "dire");
                var flyingRadiant = flyingModels.FirstOrDefault(m => m.Side == "radiant");
                var flyingDire = flyingModels.FirstOrDefault(m => m.Side == "dire");

                if (groundRadiant != null && groundDire != null &&
                    !string.Equals(groundRadiant.ModelPath, groundDire.ModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    mappings[0] = new CourierModelMapping { SourcePath = groundRadiant.ModelPath, TargetFileName = "donkey.vmdl_c" };
                    mappings[1] = new CourierModelMapping { SourcePath = groundDire.ModelPath, TargetFileName = "donkey_dire.vmdl_c" };
                }

                if (flyingRadiant != null && flyingDire != null &&
                    !string.Equals(flyingRadiant.ModelPath, flyingDire.ModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    mappings[2] = new CourierModelMapping { SourcePath = flyingRadiant.ModelPath, TargetFileName = "donkey_wings.vmdl_c" };
                    mappings[3] = new CourierModelMapping { SourcePath = flyingDire.ModelPath, TargetFileName = "donkey_dire_wings.vmdl_c" };
                }
            }
            else
            {
                string firstPath = uniqueModels[0].ModelPath;
                foreach (var target in AllBaseFiles)
                {
                    mappings.Add(new CourierModelMapping
                    {
                        SourcePath = firstPath,
                        TargetFileName = target
                    });
                }
            }

            return mappings;
        }

        public static List<string> GetVpkExtractionPaths(List<CourierModelInfo> models)
        {
            return models
                .Select(m => m.ModelPath + "_c")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string? ExtractVisualsBlock(string courierBlock)
        {
            return ExtractNamedBlock(courierBlock, "visuals");
        }

        public static int CountExistingParticles(string courierBlock)
        {
            return ExtractParticleCreateEntries(courierBlock).Count;
        }

        public static string RemoveParticleCreateEntries(string visualsBlock)
        {
            if (string.IsNullOrEmpty(visualsBlock)) return visualsBlock ?? string.Empty;

            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            var matches = assetModifierPattern.Matches(visualsBlock).Cast<Match>().ToList();
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsBlock, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsBlock.Substring(blockStart, blockEnd - blockStart);

                if (!Regex.IsMatch(modifierBlock, @"""type""\s+""particle_create""", RegexOptions.IgnoreCase))
                    continue;

                int removeStart = match.Index;
                while (removeStart > 0 && visualsBlock[removeStart - 1] != '\n')
                    removeStart--;

                int removeEnd = blockEnd;
                if (removeEnd < visualsBlock.Length && visualsBlock[removeEnd] == '\r') removeEnd++;
                if (removeEnd < visualsBlock.Length && visualsBlock[removeEnd] == '\n') removeEnd++;

                visualsBlock = visualsBlock.Substring(0, removeStart) + visualsBlock.Substring(removeEnd);
            }

            return visualsBlock;
        }

        public static string AppendEtherealEffects(string visualsBlock, List<string> effectPaths,
            int existingParticleCount, bool replaceExisting = false)
        {
            if (string.IsNullOrEmpty(visualsBlock) || effectPaths.Count == 0)
                return visualsBlock ?? string.Empty;

            int availableSlots;
            if (replaceExisting)
            {
                visualsBlock = RemoveParticleCreateEntries(visualsBlock);
                availableSlots = Data.EtherealEffects.MaxParticleSlots;
            }
            else
            {
                availableSlots = Data.EtherealEffects.GetAvailableSlots(existingParticleCount);
                if (availableSlots <= 0) return visualsBlock;
            }

            var effectsToAdd = effectPaths.Take(availableSlots).ToList();

            var entries = new List<string>();
            foreach (var path in effectsToAdd)
            {
                var entry = $@"""asset_modifier0""
{{
	""type""		""particle_create""
	""modifier""	""{path}""
}}";
                entries.Add(entry);
            }

            return AppendToVisualsBlock(visualsBlock, entries);
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
