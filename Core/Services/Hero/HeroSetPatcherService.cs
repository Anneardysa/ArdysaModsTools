using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
        /// <param name="indexFolder">Folder containing index.txt</param>
        /// <param name="heroId">Hero ID for validation</param>
        /// <param name="itemIds">Expected item IDs</param>
        /// <returns>Dictionary of ID -> block content, or null if not found</returns>
        Dictionary<string, (string block, string heroId)>? ParseIndexFile(
            string indexFolder,
            string heroId,
            IReadOnlyList<int> itemIds);

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
    /// </summary>
    public sealed class HeroSetPatcherService : IHeroSetPatcher
    {
        private readonly ILogger? _logger;

        public HeroSetPatcherService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Dictionary<string, (string block, string heroId)>? ParseIndexFile(
            string indexFolder,
            string heroId,
            IReadOnlyList<int> itemIds)
        {
            if (string.IsNullOrWhiteSpace(indexFolder) || !Directory.Exists(indexFolder))
                return null;

            var indexPath = FindIndexFile(indexFolder);
            if (indexPath == null)
                return null;

            var indexText = File.ReadAllText(indexPath, Encoding.UTF8);
            var blocks = ParseKvBlocks(indexText);

            // Filter to only requested item IDs and add heroId
            var result = new Dictionary<string, (string block, string heroId)>();
            foreach (var id in itemIds)
            {
                var idStr = id.ToString();
                if (blocks.TryGetValue(idStr, out var block))
                {
                    result[idStr] = (block, heroId);
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

                // Extract existing block for validation
                var existingBlock = ExtractBlockById(original, idStr);
                if (string.IsNullOrEmpty(existingBlock))
                {
#if DEBUG
                    log($"[DEBUG] ID {idStr} not found in items_game.txt - skipping.");
#endif
                    failedIds.Add($"{idStr}(not found)");
                    skippedCount++;
                    continue;
                }

                // Validate match
                var validation = ValidateBlockMatch(existingBlock, replacementBlock, heroId, idStr);
                if (!validation.isValid)
                {
#if DEBUG
                    log($"[DEBUG] Block {idStr} validation failed - {validation.error}");
#endif
                    failedIds.Add($"{idStr}({validation.error})");
                    skippedCount++;
                    continue;
                }

                // Replace
                var previousLength = original.Length;
                original = ReplaceIdBlock(original, idStr, replacementBlock, out bool didReplace);
                
                if (didReplace)
                {
                    replacedCount++;
#if DEBUG
                    // Verify the replacement by checking if content changed
                    var verifyBlock = ExtractBlockById(original, idStr);
                    bool verified = verifyBlock != null && verifyBlock.Length > 0;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ✓ Patched ID {idStr} for {heroId} (verified={verified})");
#endif
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ✗ ReplaceIdBlock returned false for ID {idStr}");
#endif
                    failedIds.Add($"{idStr}(replace failed)");
                }

                // Small yield to allow cancellation check and prevent blocking
                await Task.Delay(1, ct).ConfigureAwait(false);
            }

            // Write patched file
            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

#if DEBUG
            log($"[DEBUG] Patching complete: {replacedCount}/{mergedBlocks.Count} applied, {skippedCount} skipped");
            if (failedIds.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Failed IDs: {string.Join(", ", failedIds)}");
            }
#endif

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
                indexBlocks = ParseKvBlocks(indexText);
                log($"Parsed {indexBlocks.Count} blocks from index.");
            }
            else
            {
                log("Warning: index.txt not found. Will use original blocks."); // ERR_HERO_001: Set has no index.txt file
                indexBlocks = new Dictionary<string, string>();
            }

            // Find items_game.txt in the VPK extracted folder
            var itemsGamePath = FindItemsGame(itemsGameFolder);
            if (itemsGamePath == null)
            {
                log("Warning: items_game.txt not found in extracted VPK."); // ERR_HERO_002: VPK extraction incomplete
                return true;
            }

            log($"Found items_game.txt: {Path.GetFileName(itemsGamePath)}");

            // Read original items_game.txt
            var original = await File.ReadAllTextAsync(itemsGamePath, Encoding.UTF8, ct).ConfigureAwait(false);
            
            int replacedCount = 0;

            // Patch each item ID - validate and replace with index.txt blocks
            foreach (var id in itemIds)
            {
                ct.ThrowIfCancellationRequested();
                var idStr = id.ToString();

                // Get replacement block from index.txt
                if (!indexBlocks.TryGetValue(idStr, out var replacementBlock))
                {
                    log($"ID {idStr} not in index.txt - skipping.");
                    continue;
                }

                // Find existing block in items_game.txt for validation
                var existingBlock = ExtractBlockById(original, idStr);
                if (string.IsNullOrEmpty(existingBlock))
                {
                    log($"Warning: ID {idStr} not found in items_game.txt - skipping."); // ERR_HERO_003: Item ID missing from VPK
                    continue;
                }

                // Validate blocks match on critical fields
                var validation = ValidateBlockMatch(existingBlock, replacementBlock, heroId, idStr);
                if (!validation.isValid)
                {
                    log($"Warning: Block {idStr} validation failed - {validation.error}"); // ERR_HERO_004: Block prefab/used_by mismatch
                    continue;
                }

                // Replace the block
                original = ReplaceIdBlock(original, idStr, replacementBlock, out bool didReplace);
                if (didReplace)
                {
                    replacedCount++;
                    log($"Replaced ID {idStr}");
                }
            }

            // Write patched file
            await File.WriteAllTextAsync(itemsGamePath, original, Encoding.UTF8, ct).ConfigureAwait(false);

            log($"Patching completed: {replacedCount} block(s) applied.");
            return true;
        }

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

        /// <summary>
        /// Extracts a block by ID from items_game.txt for validation.
        /// Uses robust quote-aware parsing from ArdysaIdExtractor.
        /// </summary>
        private static string? ExtractBlockById(string content, string id)
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

                // Check that this isn't a value (previous non-whitespace shouldn't be a quote)
                int prev = PrevNonWhitespaceIndex(content, pos - 1);
                if (prev >= 0 && content[prev] == '"') { searchPos = pos + 1; continue; }

                int blockStart = FindLineStart(content, pos);
                int blockEndExclusive = ExtractBalancedBlockEnd(content, after);
                if (blockEndExclusive <= blockStart || blockEndExclusive < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                string block = content.Substring(blockStart, blockEndExclusive - blockStart);
                
                // Verify this looks like an item block
                if (IsLikelyItemBlock(block)) return block;

                searchPos = pos + 1;
            }

            return null;
        }

        /// <summary>
        /// Balanced block extraction with proper quote/escape handling.
        /// </summary>
        private static int ExtractBalancedBlockEnd(string text, int firstBraceIdx)
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

        /// <summary>
        /// Heuristic: is this block likely an item block?
        /// </summary>
        private static bool IsLikelyItemBlock(string block)
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

        private static int SkipWhitespace(string s, int idx)
        {
            int n = s.Length;
            while (idx < n && char.IsWhiteSpace(s[idx])) idx++;
            return idx;
        }

        private static int PrevNonWhitespaceIndex(string s, int startIdx)
        {
            int p = startIdx;
            while (p >= 0 && char.IsWhiteSpace(s[p])) p--;
            return p;
        }

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

        /// <summary>
        /// Parses KV blocks from index.txt format: "id" { ... }
        /// Uses quote-aware parsing for proper brace matching.
        /// </summary>
        private static Dictionary<string, string> ParseKvBlocks(string raw)
        {
            raw = NormalizeKvText(raw);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int pos = 0;
            while (pos < raw.Length)
            {
                // Find quoted ID
                int q1 = raw.IndexOf('"', pos);
                if (q1 < 0) break;
                int q2 = raw.IndexOf('"', q1 + 1);
                if (q2 < 0) break;

                var token = raw.Substring(q1 + 1, q2 - q1 - 1);
                pos = q2 + 1;

                // Only process numeric IDs
                if (!Regex.IsMatch(token, @"^\d+$")) continue;

                // Find opening brace
                int braceStart = SkipWhitespace(raw, pos);
                if (braceStart >= raw.Length || raw[braceStart] != '{') continue;

                // Find matching closing brace using quote-aware extraction
                int braceEnd = ExtractBalancedBlockEnd(raw, braceStart);
                if (braceEnd < 0) continue;

                // Extract full block including the ID line
                int lineStart = FindLineStart(raw, q1);
                var block = raw.Substring(lineStart, braceEnd - lineStart);
                result[token] = block;
                pos = braceEnd;
            }

            return result;
        }

        /// <summary>
        /// Replaces a block by ID with quote-aware brace matching.
        /// </summary>
        private static string ReplaceIdBlock(string original, string id, string replacementBlock, out bool didReplace)
        {
            didReplace = false;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(original)) return original;

            string token = $"\"{id}\"";
            int searchPos = 0;
            int attempts = 0;
            const int MAX_ATTEMPTS = 100000;

            while (true)
            {
                if (attempts++ > MAX_ATTEMPTS) break;
                int pos = original.IndexOf(token, searchPos, StringComparison.Ordinal);
                if (pos < 0) break;

                int closingQuote = pos + token.Length - 1;
                int after = SkipWhitespace(original, closingQuote + 1);
                if (after >= original.Length) { searchPos = pos + 1; continue; }
                if (original[after] != '{') { searchPos = pos + 1; continue; }

                // Check that this isn't a value (previous non-whitespace shouldn't be a quote)
                int prev = PrevNonWhitespaceIndex(original, pos - 1);
                if (prev >= 0 && original[prev] == '"') { searchPos = pos + 1; continue; }

                int blockStart = FindLineStart(original, pos);
                int blockEndExclusive = ExtractBalancedBlockEnd(original, after);
                if (blockEndExclusive <= blockStart || blockEndExclusive < 0)
                {
                    searchPos = pos + 1;
                    continue;
                }

                string existingBlock = original.Substring(blockStart, blockEndExclusive - blockStart);
                
                // Verify this looks like an item block before replacing
                if (!IsLikelyItemBlock(existingBlock))
                {
                    searchPos = pos + 1;
                    continue;
                }

                // Replace the block
                var before = original.Substring(0, blockStart);
                var afterBlock = original.Substring(blockEndExclusive);
                var rep = replacementBlock.TrimEnd() + Environment.NewLine;
                original = before + rep + afterBlock;
                didReplace = true;
                return original;
            }

            return original;
        }

        private static int FindLineStart(string text, int idx)
        {
            if (idx <= 0) return 0;
            for (int i = idx - 1; i >= 0; i--)
            {
                if (text[i] == '\n') return i + 1;
            }
            return 0;
        }

        private static string NormalizeKvText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (raw[0] == '\uFEFF') raw = raw.Substring(1); // Remove BOM
            return raw.Replace("\r\n", "\n").Replace('\r', '\n');
        }

    }
}
