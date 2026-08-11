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
using System.Text;

namespace ArdysaModsTools.Core.Helpers
{
    public static class ItemsGameMerger
    {
        public readonly record struct MergeResult(string Text, int Applied, int Dropped);

        internal static readonly string[] ItemMarkers =
        {
            "\"prefab\"", "\"used_by_heroes\"", "\"item_slot\"", "\"model_player\"",
            "\"image_inventory\"", "\"portraits\"", "\"visuals\"", "\"item_name\"", "\"item_type_name\"", "\"name\""
        };

        internal static bool IsCosmeticItem(ReadOnlySpan<char> block)
        {
            if (block.IndexOf("\"level\"".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0) return false;
            int typeNameIdx = block.IndexOf("\"type_name\"".AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (typeNameIdx >= 0)
            {
                if (typeNameIdx < 5 || !block.Slice(typeNameIdx - 5, 5).Equals("item_".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            foreach (var marker in ItemMarkers)
                if (block.IndexOf(marker.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static MergeResult Merge(string? vanillaItemsGame, string? moddedItemsGame,
            IReadOnlyCollection<string>? patchedIds = null)
        {
            if (string.IsNullOrWhiteSpace(vanillaItemsGame))
                throw new ArgumentException("Vanilla item data is required as the merge base.", nameof(vanillaItemsGame));

            string basis = KeyValuesBlockHelper.NormalizeKvText(vanillaItemsGame);

            if (KeyValuesBlockHelper.IsOneLinerFormat(basis))
                basis = KeyValuesBlockHelper.PrettifyKvText(basis);

            string moddedNormalized = KeyValuesBlockHelper.NormalizeKvText(moddedItemsGame ?? "");
            var moddedBlocks = ItemsGameBlockIndex.IndexSpans(moddedNormalized);
            if (moddedBlocks.Count == 0)
                return new MergeResult(basis, 0, 0);

            var scope = patchedIds is { Count: > 0 }
                ? new HashSet<string>(patchedIds, StringComparer.OrdinalIgnoreCase)
                : null;

            var result = MergeInto(basis, moddedNormalized, moddedBlocks, scope, out int applied, out int matched);

            int moddedItems = 0;
            foreach (var kvp in moddedBlocks)
                if (IsCosmeticItem(moddedNormalized.AsSpan(kvp.Value.Start, kvp.Value.Length)) &&
                    (scope == null || scope.Contains(kvp.Key))) moddedItems++;

            return new MergeResult(result, applied, Math.Max(0, moddedItems - matched));
        }

        private static string MergeInto(string basis, string moddedNormalized,
            IReadOnlyDictionary<string, (int Start, int Length)> moddedBlocks,
            HashSet<string>? scope, out int applied, out int matched)
        {
            applied = 0;
            matched = 0;
            var sb = new StringBuilder(basis.Length + 4096);
            int copiedTo = 0;
            int pos = 0;

            bool hasItemsSection = KeyValuesBlockHelper.FindItemsSectionRange(basis, out int itemsStart, out int itemsEnd);

            while (pos < basis.Length)
            {
                int q1 = KeyValuesBlockHelper.IndexOfUncommentedQuote(basis, pos);
                if (q1 < 0) break;
                int q2 = KeyValuesBlockHelper.FindClosingQuote(basis, q1);
                if (q2 < 0) break;

                string token = basis.Substring(q1 + 1, q2 - q1 - 1);
                pos = q2 + 1;

                if (!IsNumeric(token)) continue;

                int braceStart = KeyValuesBlockHelper.SkipWhitespaceAndComments(basis, pos);
                if (braceStart >= basis.Length || basis[braceStart] != '{') continue;

                int braceEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(basis, braceStart);
                if (braceEnd < 0) continue;

                pos = braceEnd;

                if (hasItemsSection && (q1 < itemsStart || q1 >= itemsEnd)) continue;

                if (!moddedBlocks.TryGetValue(token, out var moddedAt)) continue;
                var moddedBlock = moddedNormalized.AsSpan(moddedAt.Start, moddedAt.Length);

                if (scope != null && !scope.Contains(token)) continue;

                int blockStart = KeyValuesBlockHelper.FindLineStart(basis, q1);

                var vanillaSpan = basis.AsSpan(blockStart, braceEnd - blockStart);

                if (!IsCosmeticItem(vanillaSpan)) continue;

                matched++;

                if (ItemsGameBlockIndex.CanonicalEquals(vanillaSpan, moddedBlock)) continue;

                string overlaid = KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(
                    basis.Substring(blockStart, braceEnd - blockStart),
                    moddedNormalized.Substring(moddedAt.Start, moddedAt.Length));

                sb.Append(basis, copiedTo, blockStart - copiedTo);
                sb.Append(overlaid.TrimEnd()).Append('\n');
                copiedTo = braceEnd;
                applied++;
            }

            sb.Append(basis, copiedTo, basis.Length - copiedTo);
            return sb.ToString();
        }

        public static bool IsEquivalent(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return ItemsGameBlockIndex.CanonicalEquals(a, b);
        }

        private static bool IsNumeric(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
