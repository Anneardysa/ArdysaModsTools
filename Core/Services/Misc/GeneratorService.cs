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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Prepares a downloaded "packed" set zip for generation:
    /// - downloads zip (cached)
    /// - extracts to working folder
    /// - finds the index .txt inside extracted files
    /// - parses KV blocks for item IDs
    /// - patches items_game.txt by replacing specified ID blocks
    /// - returns the prepared extracted folder (ready to be repacked into VPK)
    /// </summary>
    public sealed class GeneratorService
    {
        private static readonly HttpClient _http = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
        private readonly string _cacheRoot;

        public GeneratorService(string? baseFolder = null)
        {
            var bf = string.IsNullOrWhiteSpace(baseFolder) ? AppDomain.CurrentDomain.BaseDirectory : baseFolder!;
            _cacheRoot = Path.Combine(bf, "cache", "sets");
            Directory.CreateDirectory(_cacheRoot);
        }

        /// <summary>
        /// Prepare the set zip for generation.
        /// heroId: string identifier (e.g. npc_dota_hero_abaddon)
        /// heroItemIds: numeric item ids to replace in items_game.txt (e.g. 454,455,...)
        /// zipUrl: raw URL to packed zip that contains index .txt inside
        /// returns path to the extracted folder (prepared)
        /// </summary>
        public async Task<string> PrepareSetForGenerateAsync(
            string heroId,
            IReadOnlyList<int> heroItemIds,
            string zipUrl,
            IProgress<string>? log = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(heroId)) throw new ArgumentNullException(nameof(heroId));
            if (heroItemIds == null || heroItemIds.Count == 0) throw new ArgumentException(nameof(heroItemIds));
            if (string.IsNullOrWhiteSpace(zipUrl)) throw new ArgumentNullException(nameof(zipUrl));

            ct.ThrowIfCancellationRequested();
            log?.Report($"[Generator] Start preparing set for {heroId} ...");

            // resolve cache paths
            var safeHero = MakeSafe(heroId);
            var zipName = Path.GetFileName(new Uri(zipUrl).LocalPath);
            var cacheFolder = Path.Combine(_cacheRoot, safeHero, Path.GetFileNameWithoutExtension(zipName));
            Directory.CreateDirectory(cacheFolder);

            var localZip = Path.Combine(cacheFolder, zipName);

            // 1) Download zip (cached)
            if (!File.Exists(localZip))
            {
                log?.Report($"[Generator] Downloading zip to: {localZip}");
                await DownloadFileToPathAsync(zipUrl, localZip, log, ct).ConfigureAwait(false);
            }
            else
            {
                log?.Report($"[Generator] Using cached zip: {localZip}");
            }

            // 2) Extract to a unique working folder
            var workFolder = Path.Combine(cacheFolder, "work_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(workFolder);
            log?.Report($"[Generator] Extracting zip -> {workFolder}");
            ZipFile.ExtractToDirectory(localZip, workFolder);

            // 3) Find index file inside extraction
            log?.Report("[Generator] Looking for index (.txt/.json) inside extracted files...");
            var indexPath = FindIndexFileInFolder(workFolder);
            if (indexPath == null)
            {
                throw new FileNotFoundException("Index file (.txt/.json) not found inside the packed zip. Ensure the index is bundled in the zip.");
            }
            log?.Report($"[Generator] Index found: {indexPath}");

            // 4) Parse index blocks (id -> full block)
            var indexText = File.ReadAllText(indexPath, Encoding.UTF8);
            var indexBlocks = ParseKvBlocksFile(indexText);
            log?.Report($"[Generator] Parsed {indexBlocks.Count} index blocks.");

            // 5) Locate items_game.txt in extraction
            var itemsGamePath = FindItemsGameInFolder(workFolder);
            if (itemsGamePath == null)
            {
                log?.Report("[Generator] WARNING: items_game.txt not found inside extracted zip. No patching performed. Returning extracted folder.");
                return workFolder;
            }
            log?.Report($"[Generator] Found items_game.txt: {itemsGamePath}");

            // 6) Read original items_game.txt and perform replacements for all requested ids
            var original = File.ReadAllText(itemsGamePath, Encoding.UTF8);
            int replaced = 0;
            foreach (var id in heroItemIds)
            {
                ct.ThrowIfCancellationRequested();
                var idStr = id.ToString();
                if (!indexBlocks.TryGetValue(idStr, out var replacementBlock))
                {
                    log?.Report($"[Generator] Index does not contain ID {idStr} — skipping.");
                    continue;
                }

                original = ReplaceIdBlock(original, idStr, replacementBlock, out bool didReplace);
                if (didReplace)
                {
                    replaced++;
                    log?.Report($"[Generator] Replaced ID {idStr} in items_game.txt");
                }
                else
                {
                    log?.Report($"[Generator] ID {idStr} not found in items_game.txt — appending block.");
                    original += Environment.NewLine + replacementBlock + Environment.NewLine;
                    replaced++;
                }
            }

            // 7) Backup original and write patched
            var backup = itemsGamePath + ".bak_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            File.Copy(itemsGamePath, backup);
            File.WriteAllText(itemsGamePath, original, Encoding.UTF8);

            log?.Report($"[Generator] Patching completed. {replaced} block(s) applied. Backup saved to {backup}");
            log?.Report($"[Generator] Prepared folder: {workFolder}");

            return workFolder;
        }

        // -------------------- helpers --------------------

        private static async Task DownloadFileToPathAsync(string url, string destPath, IProgress<string>? log, CancellationToken ct)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var rs = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var fs = File.Create(destPath);
            await rs.CopyToAsync(fs, ct).ConfigureAwait(false);
            log?.Report($"[Generator] Download complete: {destPath}");
        }

        private static string? FindIndexFileInFolder(string root)
        {
            // prefer root-level index names
            foreach (var name in new[] { "index.txt", "index.json" })
            {
                var p = Path.Combine(root, name);
                if (File.Exists(p)) return p;
            }

            // search heuristically for name containing 'index' or unique patterns
            var txt = Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .FirstOrDefault(p => {
                    var n = Path.GetFileName(p).ToLowerInvariant();
                    return n.Contains("index") || n.Contains("items") || n.Contains("endless") || n.Contains("mod") || n.Contains("pack");
                });

            if (txt != null) return txt;
            // fallback: first txt
            return Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories).FirstOrDefault();
        }

        private static string? FindItemsGameInFolder(string root)
        {
            var candidates = new[] {
                Path.Combine(root, "scripts", "items", "items_game.txt"),
                Path.Combine(root, "scripts", "items_game.txt"),
                Path.Combine(root, "items_game.txt")
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return Directory.EnumerateFiles(root, "items_game.txt", SearchOption.AllDirectories).FirstOrDefault();
        }

        /// <summary>
        /// Parse id->block KV blocks from a KV style text (ex: "454" { ... }).
        /// Returns dictionary indexed by id (string).
        /// </summary>
        private static Dictionary<string, string> ParseKvBlocksFile(string raw)
        {
            var text = NormalizeForKv(raw);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int pos = 0;
            while (pos < text.Length)
            {
                int q1 = text.IndexOf('"', pos);
                if (q1 < 0) break;
                int q2 = text.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                var token = text.Substring(q1 + 1, q2 - q1 - 1);
                pos = q2 + 1;

                if (!Regex.IsMatch(token, @"^\d+$")) continue;

                // skip whitespace until '{'
                int i = pos;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i >= text.Length) break;
                if (text[i] != '{')
                {
                    int b = text.IndexOf('{', i);
                    if (b < 0) continue;
                    // avoid false positive if another quote occurs before brace
                    int nq = text.IndexOf('"', i);
                    if (nq >= 0 && nq < b) continue;
                    i = b;
                }

                int braceStart = i;
                int depth = 0;
                bool started = false;
                int j = braceStart;
                for (; j < text.Length; j++)
                {
                    char c = text[j];
                    if (c == '{') { depth++; started = true; }
                    else if (c == '}') depth--;
                    if (started && depth == 0) { j++; break; }
                }
                if (!started || depth != 0) continue;

                int headerStart = FindLineStart(text, q1);
                var block = text.Substring(headerStart, j - headerStart);
                result[token] = block;
                pos = j;
            }

            return result;
        }

        private static int FindLineStart(string text, int idx)
        {
            if (idx <= 0) return 0;
            int p = idx - 1;
            while (p >= 0)
            {
                if (text[p] == '\n') return p + 1;
                p--;
            }
            return 0;
        }

        private static string ReplaceIdBlock(string original, string id, string replacementBlock, out bool didReplace)
        {
            didReplace = false;
            if (string.IsNullOrEmpty(id)) return original;

            string quoted = $"\"{id}\"";
            int search = 0;
            while (true)
            {
                int pos = original.IndexOf(quoted, search, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) break;
                int idx = pos + quoted.Length;
                while (idx < original.Length && char.IsWhiteSpace(original[idx])) idx++;
                int bracePos = -1;
                if (idx < original.Length && original[idx] == '{') bracePos = idx;
                else
                {
                    var b = original.IndexOf('{', idx);
                    if (b >= 0)
                    {
                        var nextQuote = original.IndexOf('"', idx);
                        if (nextQuote >= 0 && nextQuote < b) { search = pos + quoted.Length; continue; }
                        bracePos = b;
                    }
                }
                if (bracePos < 0) { search = pos + quoted.Length; continue; }

                int depth = 0;
                bool started = false;
                int k = bracePos;
                for (; k < original.Length; k++)
                {
                    char c = original[k];
                    if (c == '{') { depth++; started = true; }
                    else if (c == '}') depth--;
                    if (started && depth == 0) { k++; break; }
                }
                if (!started || depth != 0) { search = pos + quoted.Length; continue; }

                int headerStart = FindLineStart(original, pos);
                int endPos = k;
                var before = original.Substring(0, headerStart);
                var after = original.Substring(endPos);
                var rep = replacementBlock.TrimEnd() + Environment.NewLine;
                original = before + rep + after;
                didReplace = true;
                return original;
            }

            return original;
        }

        private static string NormalizeForKv(string raw)
        {
            if (raw == null) return string.Empty;
            if (raw.Length > 0 && raw[0] == '\uFEFF') raw = raw.Substring(1);
            raw = raw.Replace("\r\n", "\n").Replace('\r', '\n');
            raw = raw.Replace('\u201C', '"').Replace('\u201D', '"').Replace('\u2018', '\'').Replace('\u2019', '\'');
            raw = raw.Replace('\u00A0', ' ').Replace('\u2007', ' ').Replace('\u202F', ' ');
            char[] toRemove = { '\u200B', '\u200C', '\u200D', '\uFEFF', '\u2060' };
            foreach (var ch in toRemove) raw = raw.Replace(ch.ToString(), string.Empty);
            return raw;
        }

        private static string MakeSafe(string s)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(invalid.Contains(c) ? '_' : c);
            return sb.ToString();
        }
    }
}

