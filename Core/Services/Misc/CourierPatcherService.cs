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
    /// Represents a single courier model entry from visuals block.
    /// </summary>
    public record CourierModelInfo
    {
        /// <summary>Type: "courier" (ground) or "courier_flying".</summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>Model path (e.g., "models/courier/drodo/drodo.vmdl").</summary>
        public string ModelPath { get; init; } = string.Empty;

        /// <summary>Side: "radiant" or "dire".</summary>
        public string Side { get; init; } = string.Empty;

        /// <summary>Style index (0 = default). Null if no style specified.</summary>
        public int? StyleIndex { get; init; }
    }

    /// <summary>
    /// Represents a mapping from source model to target base courier model.
    /// </summary>
    public record CourierModelMapping
    {
        /// <summary>Source model path from the selected courier.</summary>
        public string SourcePath { get; init; } = string.Empty;

        /// <summary>Target filename in models/props_gameplay/ (e.g., "donkey.vmdl_c").</summary>
        public string TargetFileName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Service for courier block parsing, merging, and model file mapping.
    /// Handles all courier-specific logic for replacing the default courier
    /// with a selected cosmetic courier.
    /// </summary>
    public static class CourierPatcherService
    {
        // The 4 base courier model filenames that must exist
        private static readonly string[] BaseGroundRadiant = { "donkey.vmdl_c" };
        private static readonly string[] BaseGroundDire = { "donkey_dire.vmdl_c" };
        private static readonly string[] BaseFlyingRadiant = { "donkey_wings.vmdl_c" };
        private static readonly string[] BaseFlyingDire = { "donkey_dire_wings.vmdl_c" };

        /// <summary>
        /// All base courier target filenames.
        /// </summary>
        public static readonly string[] AllBaseFiles =
        {
            "donkey.vmdl_c",
            "donkey_dire.vmdl_c",
            "donkey_wings.vmdl_c",
            "donkey_dire_wings.vmdl_c"
        };

        /// <summary>
        /// The Default Courier item ID in items_game.txt.
        /// </summary>
        public const string DefaultCourierItemId = "595";

        /// <summary>
        /// Fields in the Default Courier block that must NEVER be overwritten.
        /// </summary>
        private static readonly HashSet<string> ImmutableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "name", "prefab", "baseitem"
        };

        /// <summary>
        /// Parses courier model entries from a courier's visuals block in items_game.txt.
        /// Extracts entries where type is "courier" or "courier_flying".
        /// When styleIndex is provided, only returns entries matching that style.
        /// </summary>
        /// <param name="courierBlock">The full courier block text (including ID and outer braces).</param>
        /// <param name="styleIndex">Optional style index to filter by. Null = all entries (for unstyled couriers) or style 0.</param>
        /// <returns>List of courier model info entries.</returns>
        public static List<CourierModelInfo> ParseCourierVisuals(string courierBlock, int? styleIndex = null)
        {
            var models = new List<CourierModelInfo>();
            if (string.IsNullOrEmpty(courierBlock)) return models;

            // Find the "visuals" block
            var visualsMatch = Regex.Match(courierBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return models;

            // Extract balanced visuals block
            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1; // at the '{'
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(courierBlock, visualsStart);
            if (visualsEnd < 0) return models;

            string visualsContent = courierBlock.Substring(visualsStart, visualsEnd - visualsStart);

            // Find all asset_modifier blocks within visuals
            var assetModifierPattern = new Regex(
                @"""asset_modifier\d*""\s*\{", RegexOptions.IgnoreCase);

            foreach (Match match in assetModifierPattern.Matches(visualsContent))
            {
                int blockStart = match.Index + match.Length - 1;
                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsContent, blockStart);
                if (blockEnd < 0) continue;

                string modifierBlock = visualsContent.Substring(blockStart, blockEnd - blockStart);

                // Extract type
                var typeMatch = Regex.Match(modifierBlock,
                    @"""type""\s+""(courier|courier_flying)""", RegexOptions.IgnoreCase);
                if (!typeMatch.Success) continue;

                string type = typeMatch.Groups[1].Value.ToLowerInvariant();

                // Extract modifier (model path)
                var modifierMatch = Regex.Match(modifierBlock,
                    @"""modifier""\s+""([^""]+\.vmdl)""", RegexOptions.IgnoreCase);
                if (!modifierMatch.Success) continue;

                string modelPath = modifierMatch.Groups[1].Value;

                // Extract asset (side)
                var assetMatch = Regex.Match(modifierBlock,
                    @"""asset""\s+""(radiant|dire)""", RegexOptions.IgnoreCase);
                string side = assetMatch.Success ? assetMatch.Groups[1].Value.ToLowerInvariant() : "radiant";

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

        /// <summary>
        /// Extracts all particle_create entries from a courier's visuals block.
        /// These need to be appended to the base courier's visuals.
        /// </summary>
        /// <param name="courierBlock">The full courier block text.</param>
        /// <returns>List of particle_create block texts (each including "asset_modifierN" { ... }).</returns>
        public static List<string> ExtractParticleCreateEntries(string courierBlock)
        {
            var particles = new List<string>();
            if (string.IsNullOrEmpty(courierBlock)) return particles;

            // Find the "visuals" block
            var visualsMatch = Regex.Match(courierBlock,
                @"""visuals""\s*\{", RegexOptions.IgnoreCase);
            if (!visualsMatch.Success) return particles;

            int visualsStart = visualsMatch.Index + visualsMatch.Length - 1;
            int visualsEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(courierBlock, visualsStart);
            if (visualsEnd < 0) return particles;

            string visualsContent = courierBlock.Substring(visualsStart, visualsEnd - visualsStart);

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
                if (Regex.IsMatch(modifierBlock, @"""type""\s+""particle_create""", RegexOptions.IgnoreCase))
                {
                    particles.Add(modifierBlock);
                }
            }

            return particles;
        }

        /// <summary>
        /// Builds a merged courier block: Default Courier structure with selected courier properties.
        /// Preserves immutable fields (name, prefab, baseitem) and base model paths in visuals.
        /// Merges mutable fields and appends particle_create entries from the selected courier.
        /// When a style is selected, uses that style's specific models and particles.
        /// </summary>
        /// <param name="defaultBlock">The Default Courier block (ID "595") from items_game.txt.</param>
        /// <param name="selectedBlock">The selected courier block from items_game.txt.</param>
        /// <param name="styleIndex">Optional style index. Null for unstyled couriers or default style.</param>
        /// <returns>The merged block ready to replace the default courier block.</returns>
        public static string BuildMergedCourierBlock(string defaultBlock, string selectedBlock, int? styleIndex = null)
        {
            if (string.IsNullOrEmpty(defaultBlock) || string.IsNullOrEmpty(selectedBlock))
                return defaultBlock ?? string.Empty;

            // Parse key-value pairs from both blocks (top-level only, not nested)
            var defaultKvs = ParseTopLevelKeyValues(defaultBlock);
            var selectedKvs = ParseTopLevelKeyValues(selectedBlock);

            // Extract visuals and portraits blocks separately
            string? defaultVisualsBlock = ExtractNamedBlock(defaultBlock, "visuals");
            string? selectedVisualsBlock = ExtractNamedBlock(selectedBlock, "visuals");
            string? selectedPortraits = ExtractNamedBlock(selectedBlock, "portraits");
            string? selectedStaticAttributes = ExtractNamedBlock(selectedBlock, "static_attributes");

            // Extract particle_create entries from selected courier (optionally filtered by style)
            var particleEntries = ExtractParticleCreateEntries(selectedBlock);

            // Build the merged block
            var sb = new StringBuilder();

            // Start with the ID line
            sb.AppendLine($"\t\"{DefaultCourierItemId}\"");
            sb.AppendLine("\t{");

            // 1. Always keep immutable fields from default
            foreach (var field in ImmutableFields)
            {
                if (defaultKvs.TryGetValue(field, out var val))
                {
                    sb.AppendLine($"\t\t\"{field}\"\t\t\"{val}\"");
                }
            }

            // 2. Merge mutable KV pairs from selected (skip immutable, visuals, portraits, static_attributes)
            var skipFields = new HashSet<string>(ImmutableFields, StringComparer.OrdinalIgnoreCase)
            {
                "visuals", "portraits", "static_attributes", "creation_date"
            };

            foreach (var kvp in selectedKvs)
            {
                if (skipFields.Contains(kvp.Key)) continue;
                sb.AppendLine($"\t\t\"{kvp.Key}\"\t\t\"{kvp.Value}\"");
            }

            // 3. Add static_attributes if present
            if (!string.IsNullOrEmpty(selectedStaticAttributes))
            {
                sb.AppendLine(IndentBlock(selectedStaticAttributes, 2));
            }

            // 4. Add portraits from selected courier
            if (!string.IsNullOrEmpty(selectedPortraits))
            {
                sb.AppendLine(IndentBlock(selectedPortraits, 2));
            }

            // 5. Build merged visuals block
            if (!string.IsNullOrEmpty(selectedVisualsBlock) && styleIndex.HasValue)
            {
                // Style-aware merge: use selected courier's visuals but FILTER asset_modifiers to the selected style
                string mergedVisuals = BuildStyleFilteredVisuals(selectedVisualsBlock, styleIndex.Value);

                // Append particle entries for this style
                if (particleEntries.Count > 0)
                {
                    mergedVisuals = AppendToVisualsBlock(mergedVisuals, particleEntries);
                }

                sb.AppendLine(IndentBlock(mergedVisuals, 2));
            }
            else if (!string.IsNullOrEmpty(defaultVisualsBlock))
            {
                // No style: keep default visuals, append selected courier's particles
                string mergedVisuals = defaultVisualsBlock;

                if (!string.IsNullOrEmpty(selectedVisualsBlock))
                {
                    // Replace asset_modifier entries from default with those from selected
                    mergedVisuals = ReplaceAssetModifiers(defaultVisualsBlock, selectedVisualsBlock);
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

        /// <summary>
        /// Builds a visuals block containing only asset_modifiers that match the given style index.
        /// Rewrites style-specific entries to have no style tag (so the game uses them as default).
        /// </summary>
        private static string BuildStyleFilteredVisuals(string visualsBlock, int styleIndex)
        {
            // Build a new visuals block with only the asset_modifiers matching the selected style
            var sb = new StringBuilder();
            sb.AppendLine("\"visuals\"");
            sb.AppendLine("{");

            // Find all asset_modifier blocks, keep only those matching the style
            var modPattern = new Regex(
                @"""(asset_modifier\d*)""(\s*)\{", RegexOptions.IgnoreCase);

            int modifierIndex = 0;
            foreach (Match match in modPattern.Matches(visualsBlock))
            {
                int blockStart = visualsBlock.IndexOf('{', match.Index + match.Groups[1].Length);
                if (blockStart < 0) continue;

                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsBlock, blockStart);
                if (blockEnd < 0) continue;

                string modBlock = visualsBlock.Substring(blockStart, blockEnd - blockStart);

                // Check if this modifier has a style field
                var styleMatch = Regex.Match(modBlock, @"""style""\s+""(\d+)""", RegexOptions.IgnoreCase);
                if (styleMatch.Success)
                {
                    int entryStyle = int.Parse(styleMatch.Groups[1].Value);
                    if (entryStyle != styleIndex) continue; // Skip non-matching styles
                }
                // If no style field, it's shared - include it

                // Rewrite with sequential naming, remove style field (game will use as default)
                string cleanedBlock = Regex.Replace(modBlock,
                    @"\s*""style""\s+""[^""]*""", "", RegexOptions.IgnoreCase);

                modifierIndex++;
                sb.AppendLine($"\t\"asset_modifier{modifierIndex}\"");
                sb.AppendLine($"\t{cleanedBlock.TrimStart()}");
            }

            // Copy styles block from selected courier (needed for the game to recognize styles)
            var stylesMatch = Regex.Match(visualsBlock,
                @"""styles""\s*\{", RegexOptions.IgnoreCase);
            if (stylesMatch.Success)
            {
                int stylesStart = visualsBlock.IndexOf('{', stylesMatch.Index);
                int stylesEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(visualsBlock, stylesStart);
                if (stylesEnd > 0)
                {
                    string stylesBlock = visualsBlock.Substring(stylesMatch.Index, stylesEnd - stylesMatch.Index);
                    sb.AppendLine($"\t{stylesBlock}");
                }
            }

            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// Replaces the asset_modifier entries in the default visuals with those from the selected courier.
        /// Used for unstyled couriers.
        /// </summary>
        private static string ReplaceAssetModifiers(string defaultVisuals, string selectedVisuals)
        {
            // Extract courier/courier_flying modifiers from selected
            var modPattern = new Regex(
                @"""(asset_modifier\d*)""(\s*)\{", RegexOptions.IgnoreCase);

            // Collect selected courier model modifiers
            var selectedModifiers = new List<string>();
            foreach (Match match in modPattern.Matches(selectedVisuals))
            {
                int blockStart = selectedVisuals.IndexOf('{', match.Index + match.Groups[1].Length);
                if (blockStart < 0) continue;

                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(selectedVisuals, blockStart);
                if (blockEnd < 0) continue;

                string modBlock = selectedVisuals.Substring(blockStart, blockEnd - blockStart);
                if (Regex.IsMatch(modBlock, @"""type""\s+""(courier|courier_flying)""", RegexOptions.IgnoreCase))
                {
                    selectedModifiers.Add(modBlock);
                }
            }

            if (selectedModifiers.Count == 0) return defaultVisuals;

            // Remove existing courier model modifiers from default
            var cleanupSb = new StringBuilder();
            int currentPos = 0;
            var findModPattern = new Regex(@"""(asset_modifier\d*)""(\s*)\{", RegexOptions.IgnoreCase);

            while (true)
            {
                var match = findModPattern.Match(defaultVisuals, currentPos);
                if (!match.Success)
                {
                    cleanupSb.Append(defaultVisuals.Substring(currentPos));
                    break;
                }

                int blockStart = defaultVisuals.IndexOf('{', match.Index + match.Groups[1].Length);
                if (blockStart < 0)
                {
                    cleanupSb.Append(defaultVisuals.Substring(currentPos, match.Index + match.Length - currentPos));
                    currentPos = match.Index + match.Length;
                    continue;
                }

                int blockEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(defaultVisuals, blockStart);
                if (blockEnd < 0)
                {
                    cleanupSb.Append(defaultVisuals.Substring(currentPos));
                    break;
                }

                string modBlock = defaultVisuals.Substring(blockStart, blockEnd - blockStart);
                if (Regex.IsMatch(modBlock, @"""type""\s+""(courier|courier_flying)""", RegexOptions.IgnoreCase))
                {
                    // Skip this block because it's a courier model modifier
                    cleanupSb.Append(defaultVisuals.Substring(currentPos, match.Index - currentPos));
                    // Also skip any trailing whitespace up to newline to clean it up nicely
                    while (blockEnd < defaultVisuals.Length && (defaultVisuals[blockEnd] == ' ' || defaultVisuals[blockEnd] == '\t' || defaultVisuals[blockEnd] == '\r'))
                        blockEnd++;
                    if (blockEnd < defaultVisuals.Length && defaultVisuals[blockEnd] == '\n')
                        blockEnd++;
                        
                    currentPos = blockEnd;
                }
                else
                {
                    // Keep this block
                    cleanupSb.Append(defaultVisuals.Substring(currentPos, blockEnd - currentPos));
                    currentPos = blockEnd;
                }
            }
            string result = cleanupSb.ToString();
            
            // Find insertion point (before closing brace of visuals)
            int lastBrace = result.LastIndexOf('}');
            if (lastBrace < 0) return defaultVisuals;

            // Insert selected courier modifiers
            var insertSb = new StringBuilder();
            for (int i = 0; i < selectedModifiers.Count; i++)
            {
                insertSb.AppendLine($"\t\"asset_modifier{i + 1}\"");
                insertSb.AppendLine($"\t{selectedModifiers[i].TrimStart()}");
            }

            result = result.Insert(lastBrace, insertSb.ToString());

            return result;
        }

        /// <summary>
        /// Maps selected courier models to the 4 base courier target filenames.
        /// Handles 1-model, 2-model (most common), and 4-model couriers.
        /// </summary>
        /// <param name="models">Parsed courier model info list.</param>
        /// <returns>List of mappings (source path → target filename).</returns>
        public static List<CourierModelMapping> GetModelMapping(List<CourierModelInfo> models)
        {
            var mappings = new List<CourierModelMapping>();
            if (models == null || models.Count == 0) return mappings;

            // Deduplicate by model path to find unique models
            var uniqueModels = models
                .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Separate by type
            var groundModels = models.Where(m => m.Type == "courier").ToList();
            var flyingModels = models.Where(m => m.Type == "courier_flying").ToList();

            if (uniqueModels.Count == 1)
            {
                // Single model → duplicate to all 4 base files
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
                // Most common: 2+ models (ground + flying)
                // Use first ground model for both radiant/dire ground
                string groundPath = groundModels[0].ModelPath;
                mappings.Add(new CourierModelMapping { SourcePath = groundPath, TargetFileName = "donkey.vmdl_c" });
                mappings.Add(new CourierModelMapping { SourcePath = groundPath, TargetFileName = "donkey_dire.vmdl_c" });

                // Use first flying model for both radiant/dire flying
                string flyingPath = flyingModels[0].ModelPath;
                mappings.Add(new CourierModelMapping { SourcePath = flyingPath, TargetFileName = "donkey_wings.vmdl_c" });
                mappings.Add(new CourierModelMapping { SourcePath = flyingPath, TargetFileName = "donkey_dire_wings.vmdl_c" });

                // If we have separate radiant/dire models, use them
                var groundRadiant = groundModels.FirstOrDefault(m => m.Side == "radiant");
                var groundDire = groundModels.FirstOrDefault(m => m.Side == "dire");
                var flyingRadiant = flyingModels.FirstOrDefault(m => m.Side == "radiant");
                var flyingDire = flyingModels.FirstOrDefault(m => m.Side == "dire");

                // Override with side-specific models if they differ
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
                // Fallback: map whatever we have, duplicate first model to remaining
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

        /// <summary>
        /// Gets the unique source model paths from a courier block that need to be
        /// extracted from the game VPK. Appends "_c" suffix since VPK stores compiled models.
        /// </summary>
        /// <param name="models">Parsed courier model info list.</param>
        /// <returns>Unique model paths with _c suffix for VPK extraction.</returns>
        public static List<string> GetVpkExtractionPaths(List<CourierModelInfo> models)
        {
            return models
                .Select(m => m.ModelPath + "_c") // .vmdl -> .vmdl_c (compiled format in VPK)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #region Private Helpers

        /// <summary>
        /// Parses top-level key-value pairs from a KV block (not nested blocks).
        /// Only captures simple "key" "value" pairs, not block keys like "visuals" { }.
        /// </summary>
        private static Dictionary<string, string> ParseTopLevelKeyValues(string block)
        {
            var kvs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Match "key" "value" or "key"\t\t"value" patterns (not followed by {)
            var pattern = new Regex(
                @"^\s*""([^""]+)""\s+""([^""]*)""\s*$",
                RegexOptions.Multiline);

            foreach (Match match in pattern.Matches(block))
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // Skip the ID line (numeric-only key at top level)
                if (int.TryParse(key, out _)) continue;

                // Skip keys that are block openers (next non-whitespace char is '{')
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

            // Append each particle entry with a unique modifier index
            int modifierIndex = 10; // Start high to avoid conflicts with existing modifiers
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
        /// Indents a block of text to the specified tab depth.
        /// </summary>
        private static string IndentBlock(string block, int tabDepth)
        {
            var prefix = new string('\t', tabDepth);
            var lines = block.Split('\n');
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var trimmed = line.TrimStart('\t', ' ');

                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Count existing indentation level
                int existingTabs = 0;
                foreach (char c in line)
                {
                    if (c == '\t') existingTabs++;
                    else break;
                }

                // Re-indent relative to target depth
                string newLine = prefix + trimmed;

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
