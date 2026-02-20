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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for hero set item patching.
    /// </summary>
    public interface IHeroSetPatcher
    {
        /// <summary>
        /// Parses index.txt and returns blocks with validation info.
        /// </summary>
        /// <param name="indexFolder">Primary folder containing index.txt</param>
        /// <param name="heroId">Hero ID for validation</param>
        /// <param name="itemIds">Expected item IDs</param>
        /// <param name="fallbackFolder">Optional fallback folder to search for index.txt (e.g. zip root)</param>
        /// <returns>Dictionary of ID -> block content, or null if not found</returns>
        Dictionary<string, (string block, string heroId)>? ParseIndexFile(
            string indexFolder,
            string heroId,
            IReadOnlyList<int> itemIds,
            string? fallbackFolder = null);

        /// <summary>
        /// Patches items_game.txt using pre-merged blocks from multiple heroes.
        /// </summary>
        /// <param name="itemsGameFolder">Folder containing items_game.txt</param>
        /// <param name="mergedBlocks">Dictionary of ID -> (block, heroId) from all heroes</param>
        /// <param name="log">Progress logger</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if patching succeeded</returns>
        Task<bool> PatchWithMergedBlocksAsync(
            string itemsGameFolder,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            Action<string> log,
            CancellationToken ct = default);

        /// <summary>
        /// Patches items_game.txt using index.txt blocks (legacy single-hero method).
        /// </summary>
        Task<bool> PatchItemsGameAsync(
            string itemsGameFolder,
            IReadOnlyList<int> itemIds,
            string heroId,
            Action<string> log,
            CancellationToken ct = default,
            string? indexFolder = null);
    }

    /// <summary>
    /// Focused service for patching items_game.txt based on index.txt blocks.
    /// Single responsibility: Parse KV blocks and patch items_game.txt.
    /// Delegates all KV text manipulation to <see cref="KeyValuesBlockHelper"/>.
    /// </summary>
    public sealed class HeroSetPatcherService : IHeroSetPatcher
    {
        private readonly IAppLogger? _logger;

        public HeroSetPatcherService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Dictionary<string, (string block, string heroId)>? ParseIndexFile(
            string indexFolder,
            string heroId,
            IReadOnlyList<int> itemIds,
            string? fallbackFolder = null)
        {
            if (string.IsNullOrWhiteSpace(indexFolder) || !Directory.Exists(indexFolder))
                return null;

            if (string.IsNullOrWhiteSpace(heroId))
                return null;

            if (itemIds == null || itemIds.Count == 0)
                return null;

            var indexPath = FindIndexFile(indexFolder);
            
            // Fallback: search in parent/zip-root folder if not found in content root
            if (indexPath == null && !string.IsNullOrWhiteSpace(fallbackFolder) && Directory.Exists(fallbackFolder))
            {
                _logger?.LogDebug($"[Patcher] index.txt not found in {indexFolder}, trying fallback: {fallbackFolder}");
                indexPath = FindIndexFile(fallbackFolder);
            }
            
            if (indexPath == null)
            {
                _logger?.LogDebug($"[Patcher] index.txt not found for hero {heroId}");
                return null;
            }

            _logger?.LogDebug($"[Patcher] Found index.txt at: {indexPath}");
            var indexText = File.ReadAllText(indexPath, Encoding.UTF8);
            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexText);
            _logger?.LogDebug($"[Patcher] Parsed {blocks.Count} block(s) from index.txt for {heroId}");

            // Filter to only requested item IDs and add heroId
            var result = new Dictionary<string, (string block, string heroId)>();
            foreach (var id in itemIds)
            {
                var idStr = id.ToString();
                if (blocks.TryGetValue(idStr, out var block))
                {
                    result[idStr] = (block, heroId);
                }
                else
                {
                    _logger?.LogDebug($"[Patcher] index.txt does not contain block for ID {idStr} (hero: {heroId})");
                }
            }

            // Warn about blocks in index.txt that weren't requested
            var requestedIds = new HashSet<string>(itemIds.Select(i => i.ToString()));
            foreach (var kvp in blocks)
            {
                if (!requestedIds.Contains(kvp.Key))
                {
                    _logger?.LogDebug($"[Patcher] index.txt contains unrequested block ID {kvp.Key} (hero: {heroId}) — ignored");
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <inheritdoc />
        public async Task<bool> PatchWithMergedBlocksAsync(
            string itemsGameFolder,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            Action<string> log,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(itemsGameFolder) || !Directory.Exists(itemsGameFolder))
            {
                log("Items game folder not found.");
                return false;
            }

            if (mergedBlocks == null || mergedBlocks.Count == 0)
            {
                log("No blocks to patch.");
                return true;
            }

            // Find items_game.txt
            var itemsGamePath = FindItemsGame(itemsGameFolder);
            if (itemsGamePath == null)
            {
                return true;
            }

            // Read items_game.txt
            var original = await File.ReadAllTextAsync(itemsGamePath, Encoding.UTF8, ct).ConfigureAwait(false);

            // Prettify one-liner format to multi-line for reliable block extraction
            if (KeyValuesBlockHelper.IsOneLinerFormat(original))
            {
                _logger?.LogDebug("[Patcher] items_game.txt is one-liner format — prettifying...");
                original = KeyValuesBlockHelper.PrettifyKvText(original);
            }

            int replacedCount = 0;
            int skippedCount = 0;
            var failedIds = new List<string>();

#if DEBUG
            log($"[DEBUG] Patching {mergedBlocks.Count} blocks...");
#endif

            // Apply all merged blocks
            foreach (var kvp in mergedBlocks)
            {
                ct.ThrowIfCancellationRequested();
                var idStr = kvp.Key;
                var (replacementBlock, heroId) = kvp.Value;

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Processing ID {idStr} for {heroId}");
#endif

                // Extract existing block for validation (pass heroId to avoid false matches)
                var existingBlock = KeyValuesBlockHelper.ExtractBlockById(original, idStr, heroId);
                if (string.IsNullOrEmpty(existingBlock))
                {
                    log($"Warning: ID {idStr} not found in items_game.txt for {heroId} — skipping.");
                    _logger?.LogDebug($"[Patcher] ExtractBlockById returned null for ID {idStr} (heroId={heroId})");
                    failedIds.Add($"{idStr}(not found)");
                    skippedCount++;
                    continue;
                }

                var validation = ValidateBlockMatch(existingBlock, replacementBlock, heroId, idStr);
                if (!validation.isValid)
                {
                    log($"Warning: Block {idStr} validation failed — {validation.error}");
                    _logger?.LogDebug($"[Patcher] Validation failed for {idStr}: {validation.error}\nExisting block length: {existingBlock.Length}, Replacement block length: {replacementBlock.Length}");
                    failedIds.Add($"{idStr}({validation.error})");
                    skippedCount++;
                    continue;
                }

                // Normalize indentation to match items_game.txt format before replacing
                var normalizedBlock = NormalizeBlockIndentation(replacementBlock, original);
                original = KeyValuesBlockHelper.ReplaceIdBlock(original, idStr, normalizedBlock, out bool didReplace, heroId);
                
                if (didReplace)
                {
                    replacedCount++;
#if DEBUG
                    // Verify the replacement by checking if content changed
                    var verifyBlock = KeyValuesBlockHelper.ExtractBlockById(original, idStr);
                    bool verified = verifyBlock != null && verifyBlock.Length > 0;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ✓ Patched ID {idStr} for {heroId} (verified={verified})");
#endif
                }
                else
                {
                    log($"Warning: ReplaceIdBlock failed for ID {idStr} ({heroId}).");
                    failedIds.Add($"{idStr}(replace failed)");
                }

                // Small yield to allow cancellation check and prevent blocking
                await Task.Delay(1, ct).ConfigureAwait(false);
            }

            // Write patched file
            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

            log($"Patching complete: {replacedCount}/{mergedBlocks.Count} applied, {skippedCount} skipped.");
            if (failedIds.Count > 0)
            {
                log($"Failed IDs: {string.Join(", ", failedIds)}");
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> PatchItemsGameAsync(
            string itemsGameFolder,
            IReadOnlyList<int> itemIds,
            string heroId,
            Action<string> log,
            CancellationToken ct = default,
            string? indexFolder = null)
        {
            if (string.IsNullOrWhiteSpace(itemsGameFolder) || !Directory.Exists(itemsGameFolder))
            {
                log("Items game folder not found.");
                return false;
            }

            if (itemIds == null || itemIds.Count == 0)
            {
                log("No item IDs provided for patching.");
                return false;
            }

            ct.ThrowIfCancellationRequested();
            log("Looking for index.txt...");

            // Find index file - look in indexFolder first, then itemsGameFolder
            var searchFolder = !string.IsNullOrWhiteSpace(indexFolder) && Directory.Exists(indexFolder) 
                ? indexFolder 
                : itemsGameFolder;
            var indexPath = FindIndexFile(searchFolder);
            
            Dictionary<string, string> indexBlocks;
            if (indexPath != null)
            {
                log($"Found index: {Path.GetFileName(indexPath)}");
                var indexText = await File.ReadAllTextAsync(indexPath, Encoding.UTF8, ct).ConfigureAwait(false);
                indexBlocks = KeyValuesBlockHelper.ParseKvBlocks(indexText);
                log($"Parsed {indexBlocks.Count} blocks from index.");
            }
            else
            {
                log("Warning: index.txt not found. Will use original blocks.");
                indexBlocks = new Dictionary<string, string>();
            }

            // Find items_game.txt in the VPK extracted folder
            var itemsGamePath = FindItemsGame(itemsGameFolder);
            if (itemsGamePath == null)
            {
                log("Warning: items_game.txt not found in extracted VPK.");
                return true;
            }

            log($"Found items_game.txt: {Path.GetFileName(itemsGamePath)}");

            // Read original items_game.txt
            var original = await File.ReadAllTextAsync(itemsGamePath, Encoding.UTF8, ct).ConfigureAwait(false);

            // Prettify one-liner format to multi-line for reliable block extraction
            if (KeyValuesBlockHelper.IsOneLinerFormat(original))
            {
                _logger?.LogDebug("[Patcher] items_game.txt is one-liner format — prettifying...");
                original = KeyValuesBlockHelper.PrettifyKvText(original);
            }
            
            int replacedCount = 0;

            // Patch each item ID - validate and replace with index.txt blocks
            foreach (var id in itemIds)
            {
                ct.ThrowIfCancellationRequested();
                var idStr = id.ToString();

                // Get replacement block from index.txt
                if (!indexBlocks.TryGetValue(idStr, out var replacementBlock))
                {
                    log($"ID {idStr} not in index.txt — skipping.");
                    continue;
                }

                // Find existing block in items_game.txt for validation (pass heroId to avoid false matches)
                var existingBlock = KeyValuesBlockHelper.ExtractBlockById(original, idStr, heroId);
                if (string.IsNullOrEmpty(existingBlock))
                {
                    log($"Warning: ID {idStr} not found in items_game.txt — skipping.");
                    continue;
                }

                var validation = ValidateBlockMatch(existingBlock, replacementBlock, heroId, idStr);
                if (!validation.isValid)
                {
                    log($"Warning: Block {idStr} validation failed — {validation.error}");
                    continue;
                }

                // Normalize indentation and replace the block
                var normalizedBlock = NormalizeBlockIndentation(replacementBlock, original);
                original = KeyValuesBlockHelper.ReplaceIdBlock(original, idStr, normalizedBlock, out bool didReplace, heroId);
                if (didReplace)
                {
                    replacedCount++;
                    log($"Replaced ID {idStr}");
                }
                else
                {
                    log($"Warning: ReplaceIdBlock failed for ID {idStr}.");
                }
            }

            // Write patched file
            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

            log($"Patching completed: {replacedCount} block(s) applied.");
            return true;
        }

        #region Validation

        /// <summary>
        /// Validates that the replacement block matches critical fields.
        /// 1. ID must match
        /// 2. Both must have "prefab" "default_item"
        /// 3. Both must have same "used_by_heroes" hero
        /// </summary>
        private static (bool isValid, string error) ValidateBlockMatch(
            string existingBlock, string replacementBlock, string heroId, string expectedId)
        {
            // Check replacement has the ID
            if (!replacementBlock.Contains($"\"{expectedId}\""))
            {
                return (false, $"ID {expectedId} not found in replacement block");
            }

            // Check both have "prefab" "default_item"
            bool existingHasPrefab = existingBlock.Contains("\"prefab\"") && 
                                      existingBlock.Contains("\"default_item\"");
            bool replacementHasPrefab = replacementBlock.Contains("\"prefab\"") && 
                                         replacementBlock.Contains("\"default_item\"");
            
            if (!existingHasPrefab)
            {
                return (false, "existing block missing prefab/default_item");
            }
            if (!replacementHasPrefab)
            {
                return (false, "replacement block missing prefab/default_item");
            }

            // Check both have "used_by_heroes" with the same hero
            bool existingHasHero = existingBlock.Contains("\"used_by_heroes\"") && 
                                    existingBlock.Contains($"\"{heroId}\"");
            bool replacementHasHero = replacementBlock.Contains("\"used_by_heroes\"") && 
                                       replacementBlock.Contains($"\"{heroId}\"");
            
            if (!existingHasHero)
            {
                return (false, $"existing block not for hero {heroId}");
            }
            if (!replacementHasHero)
            {
                return (false, $"replacement block not for hero {heroId}");
            }

            return (true, string.Empty);
        }

        #endregion

        #region Indentation Normalization

        /// <summary>
        /// Normalizes the indentation of a replacement block to match the target file's style.
        /// Detects whether the target uses tabs or spaces and converts accordingly.
        /// </summary>
        private static string NormalizeBlockIndentation(string replacementBlock, string targetContent)
        {
            if (string.IsNullOrEmpty(replacementBlock) || string.IsNullOrEmpty(targetContent))
                return replacementBlock;

            // Detect target indent style by finding the first indented line
            bool targetUsesTabs = DetectIndentStyle(targetContent);
            bool sourceUsesTabs = DetectIndentStyle(replacementBlock);

            // If both use the same style, no conversion needed
            if (targetUsesTabs == sourceUsesTabs) return replacementBlock;

            var lines = replacementBlock.Split('\n');
            var result = new StringBuilder(replacementBlock.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (i < lines.Length - 1) result.Append('\n');
                    continue;
                }

                // Count leading indent
                int indent = 0;
                int j = 0;
                while (j < line.Length)
                {
                    if (line[j] == '\t') { indent++; j++; }
                    else if (line[j] == ' ')
                    {
                        // Count spaces and convert to indent levels (assume 4 or 8 spaces per level)
                        int spaces = 0;
                        while (j < line.Length && line[j] == ' ') { spaces++; j++; }
                        indent += Math.Max(1, spaces / 4);
                    }
                    else break;
                }

                string content = line.Substring(j);
                string indentStr = targetUsesTabs
                    ? new string('\t', indent)
                    : new string(' ', indent * 4);

                result.Append(indentStr).Append(content);
                if (i < lines.Length - 1) result.Append('\n');
            }

            return result.ToString();
        }

        /// <summary>
        /// Detects whether content primarily uses tabs or spaces for indentation.
        /// </summary>
        private static bool DetectIndentStyle(string content)
        {
            int tabLines = 0, spaceLines = 0;
            int pos = 0;
            int sampled = 0;
            const int MAX_SAMPLE = 50;

            while (pos < content.Length && sampled < MAX_SAMPLE)
            {
                // Find start of next line
                if (pos > 0)
                {
                    int nl = content.IndexOf('\n', pos);
                    if (nl < 0) break;
                    pos = nl + 1;
                }
                if (pos >= content.Length) break;

                char c = content[pos];
                if (c == '\t') { tabLines++; sampled++; }
                else if (c == ' ') { spaceLines++; sampled++; }
                else { pos++; continue; }

                // Skip to end of line
                int eol = content.IndexOf('\n', pos);
                pos = eol < 0 ? content.Length : eol + 1;
            }

            return tabLines >= spaceLines; // true = tabs
        }

        #endregion

        #region File Discovery

        private static string? FindIndexFile(string folder)
        {
            // Priority: index.txt at root
            var rootIndex = Path.Combine(folder, "index.txt");
            if (File.Exists(rootIndex)) return rootIndex;

            // Search for *.txt containing "index" in name
            var candidates = Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p).Contains("index", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count > 0) return candidates[0];

            // Fallback: first .txt file
            return Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories).FirstOrDefault();
        }

        private static string? FindItemsGame(string folder)
        {
            var candidates = new[]
            {
                Path.Combine(folder, "scripts", "items", "items_game.txt"),
                Path.Combine(folder, "scripts", "items_game.txt"),
                Path.Combine(folder, "items_game.txt")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return Directory.EnumerateFiles(folder, "items_game.txt", SearchOption.AllDirectories).FirstOrDefault();
        }

        #endregion
    }
}
