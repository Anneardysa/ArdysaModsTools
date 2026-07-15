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
    public interface IHeroSetPatcher
    {
        Dictionary<string, (string block, string heroId)>? ParseIndexFile(
            string indexFolder,
            string heroId,
            IReadOnlyList<int> itemIds,
            string? fallbackFolder = null);

        Dictionary<string, (string block, string heroId)>? ParseIndexText(
            string indexText,
            string heroId,
            IReadOnlyList<int> itemIds);

        Task<bool> PatchWithMergedBlocksAsync(
            string itemsGameFolder,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            Action<string> log,
            CancellationToken ct = default,
            Action<int, int>? onBlockDone = null);

        Task<bool> PatchItemsGameAsync(
            string itemsGameFolder,
            IReadOnlyList<int> itemIds,
            string heroId,
            Action<string> log,
            CancellationToken ct = default,
            string? indexFolder = null);
    }

    public sealed class HeroSetPatcherService : IHeroSetPatcher
    {
        private readonly IAppLogger? _logger;

        public HeroSetPatcherService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

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
            return ParseIndexText(indexText, heroId, itemIds);
        }

        public Dictionary<string, (string block, string heroId)>? ParseIndexText(
            string indexText,
            string heroId,
            IReadOnlyList<int> itemIds)
        {
            if (string.IsNullOrWhiteSpace(indexText))
                return null;
            if (string.IsNullOrWhiteSpace(heroId))
                return null;
            if (itemIds == null || itemIds.Count == 0)
                return null;

            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexText);
            _logger?.LogDebug($"[Patcher] Parsed {blocks.Count} block(s) from index for {heroId}");

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
                    _logger?.LogDebug($"[Patcher] index does not contain block for ID {idStr} (hero: {heroId})");
                }
            }

            var requestedIds = new HashSet<string>(itemIds.Select(i => i.ToString()));
            foreach (var kvp in blocks)
            {
                if (!requestedIds.Contains(kvp.Key))
                {
                    _logger?.LogDebug($"[Patcher] index contains unrequested block ID {kvp.Key} (hero: {heroId}) — ignored");
                }
            }

            return result.Count > 0 ? result : null;
        }

        public async Task<bool> PatchWithMergedBlocksAsync(
            string itemsGameFolder,
            Dictionary<string, (string block, string heroId)> mergedBlocks,
            Action<string> log,
            CancellationToken ct = default,
            Action<int, int>? onBlockDone = null)
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

            var itemsGamePath = FindItemsGame(itemsGameFolder);
            if (itemsGamePath == null)
            {
                return true;
            }

            var original = await File.ReadAllTextAsync(itemsGamePath, Encoding.UTF8, ct).ConfigureAwait(false);

            if (KeyValuesBlockHelper.IsOneLinerFormat(original))
            {
                _logger?.LogDebug("[Patcher] items_game.txt is one-liner format — prettifying...");
                original = KeyValuesBlockHelper.PrettifyKvText(original);
            }

            int replacedCount = 0;
            int skippedCount = 0;
            var failedIds = new List<string>();

            int blocksProcessed = 0;
            foreach (var kvp in mergedBlocks)
            {
                ct.ThrowIfCancellationRequested();
                onBlockDone?.Invoke(blocksProcessed++, mergedBlocks.Count);
                var idStr = kvp.Key;
                var (replacementBlock, heroId) = kvp.Value;

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Processing ID {idStr} for {heroId}");
#endif

                var existingBlock = KeyValuesBlockHelper.ExtractBlockById(original, idStr, heroId);
                if (string.IsNullOrEmpty(existingBlock))
                {
                    log($"Warning: ID {idStr} not found in package for {heroId} — skipping.");
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

                var overlaidBlock = KeyValuesBlockHelper.OverlayBlockPreservingStructure(existingBlock, replacementBlock);

                var normalizedBlock = NormalizeBlockIndentation(overlaidBlock, original);
                original = KeyValuesBlockHelper.ReplaceIdBlock(original, idStr, normalizedBlock, out bool didReplace, heroId);

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Patch id={idStr} hero={heroId} replaced={didReplace}");

                if (didReplace)
                {
                    replacedCount++;
#if DEBUG
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

                await Task.Yield();
            }

            onBlockDone?.Invoke(mergedBlocks.Count, mergedBlocks.Count);

            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

            if (failedIds.Count > 0 || skippedCount > 0)
            {
                log($"Patching complete: {replacedCount}/{mergedBlocks.Count} applied, {skippedCount} skipped.");
                if (failedIds.Count > 0)
                    log($"Failed IDs: {string.Join(", ", failedIds)}");
            }
            else
            {
                log("Patching complete.");
            }
            _logger?.LogDebug($"[Patcher] merged-block patch: {replacedCount}/{mergedBlocks.Count} applied, {skippedCount} skipped");

            return true;
        }

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

            var itemsGamePath = FindItemsGame(itemsGameFolder);
            if (itemsGamePath == null)
            {
                log("Warning: package not found in extracted VPK.");
                return true;
            }

            log($"Found package: {Path.GetFileName(itemsGamePath)}");

            var original = await File.ReadAllTextAsync(itemsGamePath, Encoding.UTF8, ct).ConfigureAwait(false);

            if (KeyValuesBlockHelper.IsOneLinerFormat(original))
            {
                _logger?.LogDebug("[Patcher] items_game.txt is one-liner format — prettifying...");
                original = KeyValuesBlockHelper.PrettifyKvText(original);
            }
            
            int replacedCount = 0;

            foreach (var id in itemIds)
            {
                ct.ThrowIfCancellationRequested();
                var idStr = id.ToString();

                if (!indexBlocks.TryGetValue(idStr, out var replacementBlock))
                {
                    log($"ID {idStr} not in index.txt — skipping.");
                    continue;
                }

                var existingBlock = KeyValuesBlockHelper.ExtractBlockById(original, idStr, heroId);
                if (string.IsNullOrEmpty(existingBlock))
                {
                    log($"Warning: ID {idStr} not found in package — skipping.");
                    continue;
                }

                var validation = ValidateBlockMatch(existingBlock, replacementBlock, heroId, idStr);
                if (!validation.isValid)
                {
                    log($"Warning: Block {idStr} validation failed — {validation.error}");
                    continue;
                }

                var overlaidBlock = KeyValuesBlockHelper.OverlayBlockPreservingStructure(existingBlock, replacementBlock);

                var normalizedBlock = NormalizeBlockIndentation(overlaidBlock, original);
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

            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

            log($"Patching completed: {replacedCount} block(s) applied.");
            return true;
        }

        #region Validation

        private static (bool isValid, string error) ValidateBlockMatch(
            string existingBlock, string replacementBlock, string heroId, string expectedId)
        {
            if (!replacementBlock.Contains($"\"{expectedId}\""))
            {
                return (false, $"ID {expectedId} not found in replacement block");
            }

            bool existingHasHero = existingBlock.Contains("\"used_by_heroes\"") && 
                                    existingBlock.Contains($"\"{heroId}\"");
            
            if (!existingHasHero)
            {
                return (false, $"existing block not for hero {heroId}");
            }

            return (true, string.Empty);
        }

        #endregion

        #region Indentation Normalization

        private static string NormalizeBlockIndentation(string replacementBlock, string targetContent)
        {
            if (string.IsNullOrEmpty(replacementBlock) || string.IsNullOrEmpty(targetContent))
                return replacementBlock;

            bool targetUsesTabs = DetectIndentStyle(targetContent);
            bool sourceUsesTabs = DetectIndentStyle(replacementBlock);

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

                int indent = 0;
                int j = 0;
                while (j < line.Length)
                {
                    if (line[j] == '\t') { indent++; j++; }
                    else if (line[j] == ' ')
                    {
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

        private static bool DetectIndentStyle(string content)
        {
            int tabLines = 0, spaceLines = 0;
            int pos = 0;
            int sampled = 0;
            const int MAX_SAMPLE = 50;

            while (pos < content.Length && sampled < MAX_SAMPLE)
            {
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

                int eol = content.IndexOf('\n', pos);
                pos = eol < 0 ? content.Length : eol + 1;
            }

            return tabLines >= spaceLines;
        }

        #endregion

        #region File Discovery

        private static string? FindIndexFile(string folder)
        {
            var rootIndex = Path.Combine(folder, "index.txt");
            if (File.Exists(rootIndex)) return rootIndex;

            var candidates = Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p).Contains("index", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count > 0) return candidates[0];

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
