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
    /// <summary>
    /// Represents a single ward model entry from visuals block.
    /// </summary>
    public record WardModelInfo
    {
        /// <summary>Model path (e.g., "models/items/wards/dreamleague/dreamleague.vmdl").</summary>
        public string ModelPath { get; init; } = string.Empty;

        /// <summary>Style index (0 = default). Null if no style specified.</summary>
        public int? StyleIndex { get; init; }
    }

    /// <summary>
    /// Represents a mapping from source model to target base ward model.
    /// </summary>
    public record WardModelMapping
    {
        /// <summary>Source model path from the selected ward.</summary>
        public string SourcePath { get; init; } = string.Empty;

        /// <summary>Target filename in models/props_gameplay/ (e.g., "default_ward.vmdl_c").</summary>
        public string TargetFileName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Service for ward block parsing, merging, and model file mapping.
    /// Handles all ward-specific logic for replacing the default ward
    /// with a selected cosmetic ward.
    /// Mirrors CourierPatcherService architecture but simplified:
    ///   - Single model target (default_ward.vmdl_c)
    ///   - No radiant/dire side differentiation
    ///   - No flying variants
    ///   - Supports styles, skin field, and particle_create injection
    /// </summary>
    public static class WardPatcherService
    {
        /// <summary>
        /// The base ward target filename.
        /// </summary>
        public static readonly string[] AllBaseFiles =
        {
            "default_ward.vmdl_c"
        };

        /// <summary>
        /// The Default Ward item ID in items_game.txt.
        /// </summary>
        public const string DefaultWardItemId = "596";

        /// <summary>
        /// Fields in the Default Ward block that must NEVER be overwritten.
        /// </summary>
        private static readonly HashSet<string> ImmutableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "name", "prefab", "baseitem"
        };

        /// <summary>
        /// Parses ward model entries from a ward's visuals block in items_game.txt.
        /// Extracts entries where type is "entity_model".
        /// When styleIndex is provided, only returns entries matching that style.
        /// </summary>
        /// <param name="wardBlock">The full ward block text (including ID and outer braces).</param>
        /// <param name="styleIndex">Optional style index to filter by. Null = all entries (for unstyled wards) or style 0.</param>
        /// <returns>List of ward model info entries.</returns>
        public static List<WardModelInfo> ParseWardVisuals(string wardBlock, int? styleIndex = null)
        {
            var models = new List<WardModelInfo>();
            if (string.IsNullOrEmpty(wardBlock)) return models;

            // Find the "visuals" block
            var visualsMatch = Regex.Match(wardBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return models;

            // Extract balanced visuals block
            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1; // at the '{'
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(wardBlock, visualsStart);
            if (visualsEnd < 0) return models;

            string visualsContent = wardBlock.Substring(visualsStart, visualsEnd - visualsStart);

            // Find all asset_modifier blocks within visuals
            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(blockStart, blockEnd - blockStart);

                // Extract type — wards use "entity_model"
                var typeMatch = Regex.Match(modifierBlock,
                    @"""type""\s+""entity_model""", RegexOptions.IgnoreCase);
                if (!typeMatch.Success) continue;

                // Extract modifier (model path)
                var modifierMatch = Regex.Match(modifierBlock,
                    @"""modifier""\s+""([^""]+\.vmdl)""", RegexOptions.IgnoreCase);
                if (!modifierMatch.Success) continue;

                string modelPath = modifierMatch.Groups[1].Value;

                // Extract style index
                int? entryStyle = null;
                var styleMatch = Regex.Match(modifierBlock,
                    @"""style""\s+""(\d+)""", RegexOptions.IgnoreCase);
                if (styleMatch.Success)
                {
                    entryStyle = int.Parse(styleMatch.Groups[1].Value);
                }

                // Filter by style if specified
                if (styleIndex.HasValue)
                {
                    // If entry has a style field, it must match
                    // If entry has no style field, include it (shared across all styles)
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

        /// <summary>
        /// Extracts particle_create entries from a ward's visuals block.
        /// When styleIndex is provided, only returns entries matching that style.
        /// Entries without a style field are always included (shared across all styles).
        /// </summary>
        /// <param name="wardBlock">The full ward block text.</param>
        /// <param name="styleIndex">Optional style index to filter by. Null = all entries.</param>
        /// <returns>List of particle_create block texts (each including "asset_modifierN" { ... }).</returns>
        public static List<string> ExtractParticleCreateEntries(string wardBlock, int? styleIndex = null)
        {
            var particles = new List<string>();
            if (string.IsNullOrEmpty(wardBlock)) return particles;

            // Find the "visuals" block
            var visualsMatch = Regex.Match(wardBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return particles;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(wardBlock, visualsStart);
            if (visualsEnd < 0) return particles;

            string visualsContent = wardBlock.Substring(visualsStart, visualsEnd - visualsStart);

            // Find all asset_modifier blocks containing "particle_create"
            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(match.Index, blockEnd - match.Index);

                // Check if this is a particle_create type
                if (!Regex.IsMatch(modifierBlock, @"""type""\s+""particle_create""", RegexOptions.IgnoreCase))
                    continue;

                // Filter by style (same logic as ParseWardVisuals)
                if (styleIndex.HasValue)
                {
                    var styleMatch = Regex.Match(modifierBlock,
                        @"""style""\s+""(\d+)""", RegexOptions.IgnoreCase);

                    if (styleMatch.Success)
                    {
                        // Entry has a style field — must match the requested style
                        int entryStyle = int.Parse(styleMatch.Groups[1].Value);
                        if (entryStyle != styleIndex.Value)
                            continue;
                    }
                    // Entry has no style field — include it (shared across all styles)
                }

                // Strip the "style" field from the entry since filtering already applied.
                // The merged block should not carry style metadata.
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

        /// <summary>
        /// Builds a merged ward block: Default Ward structure with selected ward's cosmetic properties.
        /// 
        /// IMMUTABLE (kept from default ward block "596"):
        ///   - "name", "prefab", "baseitem" fields
        ///   - The visuals model references (default_ward.vmdl path)
        ///     Model swapping happens at file level, not in items_game.txt.
        /// 
        /// REPLACED (taken from selected ward):
        ///   - All other KV fields (image_inventory, item_name, item_quality, etc.)
        ///   - "portraits" block
        ///   - "static_attributes" block
        /// 
        /// INJECTED into default visuals from selected ward:
        ///   - "skin" field (for wards with skin variants)
        ///   - particle_create entries (ambient effects)
        /// </summary>
        /// <param name="defaultBlock">The Default Ward block (ID "596") from items_game.txt.</param>
        /// <param name="selectedBlock">The selected ward block from items_game.txt.</param>
        /// <param name="styleIndex">Optional style index. Null for unstyled wards or default style.</param>
        /// <returns>The merged block ready to replace the default ward block.</returns>
        public static string BuildMergedWardBlock(string defaultBlock, string selectedBlock, int? styleIndex = null)
        {
            if (string.IsNullOrEmpty(defaultBlock) || string.IsNullOrEmpty(selectedBlock))
                return defaultBlock ?? string.Empty;

            // Parse key-value pairs from both blocks (top-level only, not nested)
            var defaultKvs = ParseTopLevelKeyValues(defaultBlock);
            var selectedKvs = ParseTopLevelKeyValues(selectedBlock);

            // Extract nested blocks
            string? defaultVisualsBlock = ExtractNamedBlock(defaultBlock, "visuals");
            string? selectedVisualsBlock = ExtractNamedBlock(selectedBlock, "visuals");
            string? selectedPortraits = ExtractNamedBlock(selectedBlock, "portraits");
            string? selectedStaticAttributes = ExtractNamedBlock(selectedBlock, "static_attributes");

            // Extract particle_create entries from selected ward (filtered by style)
            var particleEntries = ExtractParticleCreateEntries(selectedBlock, styleIndex);

            // Extract visuals-level KV pairs from selected ward (e.g., "skin" "1")
            var visualsKvPairs = ExtractVisualsKeyValues(selectedVisualsBlock);

            // Build the merged block
            var sb = new StringBuilder();

            // --- Item ID ---
            sb.AppendLine($"\t\"{DefaultWardItemId}\"");
            sb.AppendLine("\t{");

            // --- 1. Immutable fields from default (name, prefab, baseitem) ---
            foreach (var field in ImmutableFields)
            {
                if (defaultKvs.TryGetValue(field, out var val))
                {
                    sb.AppendLine($"\t\t\"{field}\"\t\t\"{val}\"");
                }
            }

            // --- 2. Mutable KV pairs from selected ward ---
            var skipFields = new HashSet<string>(ImmutableFields, StringComparer.OrdinalIgnoreCase)
            {
                "visuals", "portraits", "static_attributes", "creation_date"
            };

            foreach (var kvp in selectedKvs)
            {
                if (skipFields.Contains(kvp.Key)) continue;
                sb.AppendLine($"\t\t\"{kvp.Key}\"\t\t\"{kvp.Value}\"");
            }

            // --- 3. Static attributes from selected ward ---
            if (!string.IsNullOrEmpty(selectedStaticAttributes))
            {
                sb.AppendLine(IndentBlock(selectedStaticAttributes, 2));
            }

            // --- 4. Portraits from selected ward ---
            if (!string.IsNullOrEmpty(selectedPortraits))
            {
                sb.AppendLine(IndentBlock(selectedPortraits, 2));
            }

            // --- 5. Visuals: keep default ward model, inject skin + particles from selected ---
            if (!string.IsNullOrEmpty(defaultVisualsBlock))
            {
                string mergedVisuals = defaultVisualsBlock;

                // Inject visuals-level KV pairs from selected ward (e.g., "skin" "1")
                if (visualsKvPairs.Count > 0)
                {
                    mergedVisuals = InjectVisualsKVPairs(mergedVisuals, visualsKvPairs);
                }

                // Append particle_create entries from selected ward
                if (particleEntries.Count > 0)
                {
                    mergedVisuals = AppendToVisualsBlock(mergedVisuals, particleEntries);
                }

                sb.AppendLine(IndentBlock(mergedVisuals, 2));
            }

            sb.Append("\t}");

            return sb.ToString();
        }

        /// <summary>
        /// Maps selected ward model to the base ward target filename.
        /// Wards are simpler than couriers — always a single model mapping.
        /// </summary>
        /// <param name="models">Parsed ward model info list.</param>
        /// <returns>List of mappings (source path → target filename).</returns>
        public static List<WardModelMapping> GetModelMapping(List<WardModelInfo> models)
        {
            var mappings = new List<WardModelMapping>();
            if (models == null || models.Count == 0) return mappings;

            // Deduplicate by model path to find unique models
            var uniqueModels = models
                .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Use the first unique model for the single ward target
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

        /// <summary>
        /// Gets the unique source model paths from a ward block that need to be
        /// extracted from the game VPK. Appends "_c" suffix since VPK stores compiled models.
        /// </summary>
        /// <param name="models">Parsed ward model info list.</param>
        /// <returns>Unique model paths with _c suffix for VPK extraction.</returns>
        public static List<string> GetVpkExtractionPaths(List<WardModelInfo> models)
        {
            return models
                .Select(m => m.ModelPath + "_c") // .vmdl -> .vmdl_c (compiled format in VPK)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Extracts the "visuals" block from a ward block.
        /// </summary>
        public static string? ExtractVisualsBlock(string wardBlock)
        {
            return ExtractNamedBlock(wardBlock, "visuals");
        }

        #region Visuals Helpers

        /// <summary>
        /// Extracts top-level key-value pairs from a visuals block (e.g., "skin" "1").
        /// Only returns simple KV pairs at depth 1 inside the visuals block,
        /// ignoring any KV pairs inside nested sub-blocks like "styles",
        /// "alternate_icons", or "asset_modifier" entries.
        /// Uses brace-depth tracking to determine nesting level.
        /// </summary>
        private static Dictionary<string, string> ExtractVisualsKeyValues(string? visualsBlock)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(visualsBlock)) return result;

            var kvPattern = new Regex(
                @"^\s*""([^""]+)""\s+""([^""]*)""",
                RegexOptions.Multiline);

            // Skip keys that are block names or asset_modifier internal fields
            var skipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "visuals", "type", "modifier", "asset", "style"
            };

            // Find the opening brace of the visuals block
            int outerBrace = visualsBlock.IndexOf('{');
            if (outerBrace < 0) return result;

            // Build depth map: track brace depth at each line start
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
                // Find line start and its depth
                int lineStart = match.Index;
                while (lineStart > 0 && visualsBlock[lineStart - 1] != '\n')
                    lineStart--;

                int lineDepth = 0;
                if (lineDepths.TryGetValue(lineStart, out int d))
                    lineDepth = d;
                else if (lineStart <= outerBrace + 1)
                    lineDepth = 1;

                // Only capture depth 1 (direct children of the visuals block)
                if (lineDepth != 1) continue;

                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // Skip numeric keys (block IDs), block names, and asset_modifier field names
                if (int.TryParse(key, out _)) continue;
                if (skipKeys.Contains(key)) continue;
                if (key.StartsWith("asset_modifier", StringComparison.OrdinalIgnoreCase)) continue;

                result[key] = value;
            }

            return result;
        }

        /// <summary>
        /// Injects KV pairs into a visuals block right after the opening brace.
        /// Used for fields like "skin" that need to be in the visuals block.
        /// </summary>
        private static string InjectVisualsKVPairs(string visualsBlock, Dictionary<string, string> kvPairs)
        {
            if (kvPairs.Count == 0) return visualsBlock;

            // Find the opening brace of the visuals block
            int bracePos = visualsBlock.IndexOf('{');
            if (bracePos < 0) return visualsBlock;

            // Build the KV lines to inject
            var sb = new StringBuilder();
            foreach (var kvp in kvPairs)
            {
                sb.AppendLine($"\t\t\"{kvp.Key}\"\t\t\"{kvp.Value}\"");
            }

            // Insert right after the opening brace + newline
            int insertPos = bracePos + 1;
            if (insertPos < visualsBlock.Length && visualsBlock[insertPos] == '\r') insertPos++;
            if (insertPos < visualsBlock.Length && visualsBlock[insertPos] == '\n') insertPos++;

            return visualsBlock.Substring(0, insertPos) + sb.ToString() + visualsBlock.Substring(insertPos);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Parses top-level key-value pairs from a KV block (not nested blocks).
        /// Only captures simple "key" "value" pairs at depth 1 (inside the item's
        /// outer braces), ignoring any KV pairs inside nested blocks like
        /// "visuals", "portraits", or "static_attributes".
        /// Uses brace-depth tracking to determine nesting level.
        /// </summary>
        private static Dictionary<string, string> ParseTopLevelKeyValues(string block)
        {
            var kvs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(block)) return kvs;

            var kvPattern = new Regex(
                @"^\s*""([^""]+)""\s+""([^""]*)""",
                RegexOptions.Multiline);

            // Find the outer opening brace of the item block
            int outerBrace = block.IndexOf('{');
            if (outerBrace < 0) return kvs;

            // Track brace depth to determine which KV pairs are at the top level
            // Depth 0 = outside outer braces
            // Depth 1 = top-level KV pairs (what we want)
            // Depth 2+ = inside nested blocks (skip)
            int depth = 0;
            var lineDepths = new Dictionary<int, int>(); // lineStartIndex -> depth at that line

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
                    // Record depth at start of next line
                    if (i + 1 < block.Length)
                        lineDepths[i + 1] = depth;
                }
            }

            foreach (Match match in kvPattern.Matches(block))
            {
                // Find the nearest recorded line depth
                int matchPos = match.Index;
                int lineDepth = 0;

                // Walk backwards to find the line start and its depth
                int lineStart = matchPos;
                while (lineStart > 0 && block[lineStart - 1] != '\n')
                    lineStart--;

                if (lineDepths.TryGetValue(lineStart, out int d))
                    lineDepth = d;
                else if (lineStart <= outerBrace + 1)
                    lineDepth = 1; // First line after outer brace

                // Only capture depth 1 (inside outer brace, not inside nested blocks)
                if (lineDepth != 1) continue;

                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // Skip the ID line (numeric-only key at top level)
                if (int.TryParse(key, out _)) continue;

                kvs[key] = value;
            }

            return kvs;
        }

        /// <summary>
        /// Extracts a named block (e.g., "visuals" { ... }) from a KV block.
        /// Returns the full block including the name and braces.
        /// </summary>
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

            // Include the name line
            int lineStart = FindLineStart(content, match.Index);
            return content.Substring(lineStart, braceEnd - lineStart);
        }

        /// <summary>
        /// Appends new entries inside a visuals block (before the closing brace).
        /// </summary>
        private static string AppendToVisualsBlock(string visualsBlock, List<string> entries)
        {
            // Find the last closing brace
            int lastBrace = visualsBlock.LastIndexOf('}');
            if (lastBrace < 0) return visualsBlock;

            var sb = new StringBuilder();
            sb.Append(visualsBlock.Substring(0, lastBrace));

            // Scan for the highest existing asset_modifier index to avoid key collisions
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
                // Renumber the asset_modifier to avoid key conflicts
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

        /// <summary>
        /// Indents a block of text to the specified tab depth, preserving relative indentation.
        /// Computes the minimum indentation as a baseline and re-indents relative to it.
        /// </summary>
        private static string IndentBlock(string block, int tabDepth)
        {
            var prefix = new string('\t', tabDepth);
            var lines = block.Split('\n');
            var sb = new StringBuilder();

            // Find minimum indentation level to use as baseline
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

                // Count existing tabs and compute relative depth from baseline
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

        /// <summary>
        /// Finds the start of the line containing the given index.
        /// </summary>
        private static int FindLineStart(string content, int index)
        {
            while (index > 0 && content[index - 1] != '\n')
                index--;
            return index;
        }

        #endregion
    }
}
