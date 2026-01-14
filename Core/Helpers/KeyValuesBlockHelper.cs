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
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Helper class for KeyValues (Valve format) block manipulation.
    /// Provides balanced brace parsing for items_game.txt patching.
    /// </summary>
    public static class KeyValuesBlockHelper
    {
        /// <summary>
        /// Extracts a block by ID from items_game.txt content.
        /// Uses balanced brace matching with quote-awareness.
        /// </summary>
        /// <param name="content">Full items_game.txt content</param>
        /// <param name="id">Item ID (e.g. "555")</param>
        /// <param name="requireItemMarkers">If true, only return blocks that look like item definitions</param>
        /// <returns>The full block including ID line, or null if not found</returns>
        public static string? ExtractBlockById(string content, string id, bool requireItemMarkers = true)
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

                // For one-line files, use the ID position directly
                // For multi-line files, go back to line start
                int blockStart = FindLineStart(content, pos);
                // If no newline found (one-line file), blockStart will be 0
                // In that case, use the position of the ID
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

                // Verify this looks like an item block if required
                if (!requireItemMarkers || IsLikelyItemBlock(block)) return block;

                searchPos = pos + 1;
            }

            return null;
        }

        /// <summary>
        /// Replaces a block by ID with quote-aware brace matching.
        /// </summary>
        /// <param name="content">Full items_game.txt content</param>
        /// <param name="id">Item ID to replace</param>
        /// <param name="replacementBlock">New block content</param>
        /// <param name="requireItemMarkers">If true, only replace blocks that look like item definitions</param>
        /// <returns>Modified content with block replaced</returns>
        public static string ReplaceIdBlock(string content, string id, string replacementBlock, bool requireItemMarkers = true)
        {
            return ReplaceIdBlock(content, id, replacementBlock, requireItemMarkers, out _);
        }

        /// <summary>
        /// Replaces a block by ID with quote-aware brace matching.
        /// Works with both multi-line and one-line formatted files.
        /// </summary>
        public static string ReplaceIdBlock(string content, string id, string replacementBlock, bool requireItemMarkers, out bool didReplace)
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

                // Check that this isn't a value (previous non-whitespace shouldn't be a quote)
                int prev = PrevNonWhitespaceIndex(content, pos - 1);
                if (prev >= 0 && content[prev] == '"') { searchPos = pos + 1; continue; }

                // For one-line files, use the ID position directly
                // For multi-line files, go back to line start
                int blockStart = FindLineStart(content, pos);
                // If no newline found (one-line file), blockStart will be 0
                // In that case, use the position of the ID
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

                // Verify this looks like an item block before replacing
                if (requireItemMarkers && !IsLikelyItemBlock(existingBlock))
                {
                    searchPos = pos + 1;
                    continue;
                }

                // Replace the block
                var before = content.Substring(0, blockStart);
                var afterBlock = content.Substring(blockEndExclusive);
                var rep = replacementBlock.TrimEnd() + Environment.NewLine;
                content = before + rep + afterBlock;
                didReplace = true;
                return content;
            }

            return content;
        }

        /// <summary>
        /// Parses KV blocks from index.txt format: "id" { ... }
        /// </summary>
        public static Dictionary<string, string> ParseKvBlocks(string raw)
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

                // Find matching closing brace
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
        /// Balanced block extraction with proper quote/escape handling.
        /// </summary>
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

        /// <summary>
        /// Heuristic: is this block likely an item block?
        /// </summary>
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

        /// <summary>
        /// Skip whitespace characters starting from index.
        /// </summary>
        public static int SkipWhitespace(string s, int idx)
        {
            int n = s.Length;
            while (idx < n && char.IsWhiteSpace(s[idx])) idx++;
            return idx;
        }

        /// <summary>
        /// Find the previous non-whitespace character index.
        /// </summary>
        public static int PrevNonWhitespaceIndex(string s, int startIdx)
        {
            int p = startIdx;
            while (p >= 0 && char.IsWhiteSpace(s[p])) p--;
            return p;
        }

        /// <summary>
        /// Find the start of the line containing the given index.
        /// </summary>
        public static int FindLineStart(string text, int idx)
        {
            if (idx <= 0) return 0;
            for (int i = idx - 1; i >= 0; i--)
            {
                if (text[i] == '\n') return i + 1;
            }
            return 0;
        }

        /// <summary>
        /// Normalize KV text (remove BOM, normalize line endings).
        /// </summary>
        public static string NormalizeKvText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (raw[0] == '\uFEFF') raw = raw.Substring(1); // Remove BOM
            return raw.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>
        /// Prettify KeyValues text from one-liner format to proper multi-line Valve format.
        /// Handles quoted strings, braces, and proper indentation.
        /// </summary>
        public static string PrettifyKvText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            // Normalize first
            raw = NormalizeKvText(raw);

            int lineCount = raw.Count(c => c == '\n');
            if (lineCount > 100) return raw; // Already formatted

            var result = new System.Text.StringBuilder(raw.Length * 2);
            int indent = 0;
            bool inQuote = false;
            bool escape = false;
            bool needNewline = false;
            int lastNonWs = -1;

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
                    if (needNewline && !inQuote)
                    {
                        result.AppendLine();
                        result.Append('\t', indent);
                        needNewline = false;
                    }
                    result.Append(c);
                    inQuote = !inQuote;
                    lastNonWs = result.Length - 1;
                    continue;
                }

                if (inQuote)
                {
                    result.Append(c);
                    if (!char.IsWhiteSpace(c)) lastNonWs = result.Length - 1;
                    continue;
                }

                // Outside quotes
                if (c == '{')
                {
                    result.AppendLine();
                    result.Append('\t', indent);
                    result.Append('{');
                    indent++;
                    needNewline = true;
                }
                else if (c == '}')
                {
                    indent = Math.Max(0, indent - 1);
                    result.AppendLine();
                    result.Append('\t', indent);
                    result.Append('}');
                    needNewline = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    // Convert multiple whitespace to tabs between tokens on same line
                    if (lastNonWs >= 0 && result.Length > lastNonWs + 1)
                    {
                        // Skip adding more whitespace
                    }
                    else if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
                    {
                        result.Append('\t');
                    }
                }
                else
                {
                    if (needNewline)
                    {
                        result.AppendLine();
                        result.Append('\t', indent);
                        needNewline = false;
                    }
                    result.Append(c);
                    lastNonWs = result.Length - 1;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Check if content appears to be one-liner format (very few newlines for file size).
        /// </summary>
        public static bool IsOneLinerFormat(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            int lineCount = content.Count(c => c == '\n');
            // If file has less than 100 lines but is larger than 10KB, it's one-liner
            return lineCount < 100 && content.Length > 10000;
        }
    }
}

