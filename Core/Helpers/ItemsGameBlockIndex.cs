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
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ArdysaModsTools.Core.Helpers
{
    public static class ItemsGameBlockIndex
    {
        public static Dictionary<string, string> Build(string? rawItemsGame)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawItemsGame)) return result;

            foreach (var kvp in IndexSpans(rawItemsGame))
                result[kvp.Key] = HashBlock(rawItemsGame.AsSpan(kvp.Value.Start, kvp.Value.Length));

            return result;
        }

        public static HashSet<string> FindDifferingItemIds(string? vanillaText, string? moddedText)
        {
            var differing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(vanillaText) || string.IsNullOrWhiteSpace(moddedText))
                return differing;

            var vanillaSpans = IndexSpans(vanillaText);
            var moddedSpans = IndexSpans(moddedText);

            foreach (var kvp in moddedSpans)
            {
                var moddedSpan = moddedText.AsSpan(kvp.Value.Start, kvp.Value.Length);
                if (!ItemsGameMerger.IsCosmeticItem(moddedSpan)) continue;

                if (vanillaSpans.TryGetValue(kvp.Key, out var vanillaPos))
                {
                    var vanillaSpan = vanillaText.AsSpan(vanillaPos.Start, vanillaPos.Length);
                    if (!CanonicalEquals(vanillaSpan, moddedSpan))
                    {
                        differing.Add(kvp.Key);
                    }
                }
                else
                {
                    differing.Add(kvp.Key);
                }
            }

            return differing;
        }

        public static Dictionary<string, (int Start, int Length)> IndexSpans(string? text)
        {
            var result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return result;

            bool hasItemsSection = KeyValuesBlockHelper.FindItemsSectionRange(text, out int itemsStart, out int itemsEnd);

            int pos = 0;
            while (pos < text.Length)
            {
                int q1 = KeyValuesBlockHelper.IndexOfUncommentedQuote(text, pos);
                if (q1 < 0) break;
                int q2 = KeyValuesBlockHelper.FindClosingQuote(text, q1);
                if (q2 < 0) break;

                var token = text.AsSpan(q1 + 1, q2 - q1 - 1);
                pos = q2 + 1;

                if (!IsNumeric(token)) continue;

                int braceStart = KeyValuesBlockHelper.SkipWhitespaceAndComments(text, pos);
                if (braceStart >= text.Length || text[braceStart] != '{') continue;

                int braceEnd = KeyValuesBlockHelper.ExtractBalancedBlockEnd(text, braceStart);
                if (braceEnd < 0) continue;

                pos = braceEnd;

                if (hasItemsSection && (q1 < itemsStart || q1 >= itemsEnd)) continue;

                int start = KeyValuesBlockHelper.FindLineStart(text, q1);
                var blockSpan = text.AsSpan(start, braceEnd - start);

                if (ItemsGameMerger.IsCosmeticItem(blockSpan))
                {
                    result[token.ToString()] = (start, braceEnd - start);
                }
            }

            return result;
        }

        private static bool IsNumeric(ReadOnlySpan<char> s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return true;
        }

        public readonly record struct IdDiff(
            int Added,
            int Removed,
            int Changed,
            IReadOnlyList<string> AddedIds)
        {
            public bool HasIdDelta => Added > 0 || Removed > 0;
        }

        private const int SampleSize = 8;

        public static IdDiff Compare(
            IReadOnlyDictionary<string, string> vanilla,
            IReadOnlyDictionary<string, string> modded)
        {
            if (vanilla == null) throw new ArgumentNullException(nameof(vanilla));
            if (modded == null) throw new ArgumentNullException(nameof(modded));

            int added = 0, changed = 0;
            var sample = new List<string>(SampleSize);

            foreach (var kvp in vanilla)
            {
                if (!modded.TryGetValue(kvp.Key, out var moddedHash))
                {
                    added++;
                    if (sample.Count < SampleSize) sample.Add(kvp.Key);
                }
                else if (!string.Equals(kvp.Value, moddedHash, StringComparison.Ordinal))
                {
                    changed++;
                }
            }

            int removed = 0;
            foreach (var key in modded.Keys)
                if (!vanilla.ContainsKey(key)) removed++;

            return new IdDiff(added, removed, changed, sample);
        }

        private static string HashBlock(ReadOnlySpan<char> block)
        {
            char[] rented = ArrayPool<char>.Shared.Rent(block.Length);
            try
            {
                int n = 0;
                var cursor = new CanonicalCursor(block);
                while (cursor.TryNext(out char c)) rented[n++] = c;

                Span<byte> digest = stackalloc byte[32];
                SHA256.HashData(MemoryMarshal.AsBytes(rented.AsSpan(0, n)), digest);
                return Convert.ToHexString(digest);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        public static bool CanonicalEquals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var ca = new CanonicalCursor(a);
            var cb = new CanonicalCursor(b);

            while (true)
            {
                bool hasA = ca.TryNext(out char x);
                bool hasB = cb.TryNext(out char y);

                if (!hasA || !hasB) return hasA == hasB;
                if (x != y) return false;
            }
        }

        private ref struct CanonicalCursor
        {
            private readonly ReadOnlySpan<char> _s;
            private int _i;

            private bool _inToken;

            private char _pending;

            public CanonicalCursor(ReadOnlySpan<char> s)
            {
                _s = s;
                _i = 0;
                _inToken = false;
                _pending = '\0';
            }

            public bool TryNext(out char c)
            {
                if (_pending != '\0')
                {
                    c = _pending;
                    _pending = '\0';
                    return true;
                }

                while (_i < _s.Length)
                {
                    char ch = _s[_i];

                    if (_inToken)
                    {
                        _i++;
                        if (ch == '\\' && _i < _s.Length)
                        {
                            _pending = _s[_i];
                            _i++;
                            c = ch;
                            return true;
                        }
                        if (ch == '"') _inToken = false;
                        c = ch;
                        return true;
                    }

                    if (ch == '"')
                    {
                        _i++;
                        _inToken = true;
                        c = ch;
                        return true;
                    }

                    if (ch == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        while (_i < _s.Length && _s[_i] != '\n') _i++;
                        continue;
                    }

                    if (ch == '{' || ch == '}')
                    {
                        _i++;
                        c = ch;
                        return true;
                    }

                    _i++;
                }

                c = '\0';
                return false;
            }
        }

        public static string Canonicalize(string? block)
        {
            if (string.IsNullOrEmpty(block)) return string.Empty;

            var sb = new StringBuilder(block.Length);
            var cursor = new CanonicalCursor(block);
            while (cursor.TryNext(out char c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
