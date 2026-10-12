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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Hero
{
    public static class SkinManifestStore
    {
        public const string TreeRelativePath = "version/_ardysamods";

        private const string BeginFence = "# --- AMT SKIN MANIFEST v1 ---";
        private const string EndFencePrefix = "# --- END AMT SKIN MANIFEST sha256=";
        private const string EndFenceSuffix = " ---";

        private static readonly Regex BlockPattern = new(
            @"\r?\n?[ \t]*\#\ ---\ AMT\ SKIN\ MANIFEST\ v1\ ---.*?\#\ ---\ END\ AMT\ SKIN\ MANIFEST\ sha256=[0-9A-Fa-f]*\ ---[ \t]*",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static async Task WriteToTreeAsync(string extractDir, SkinManifest manifest, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(extractDir)) throw new ArgumentNullException(nameof(extractDir));
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));

            string path = Path.Combine(extractDir, TreeRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string existing = File.Exists(path)
                ? await File.ReadAllTextAsync(path, ct).ConfigureAwait(false)
                : string.Empty;

            string preserved = StripBlock(existing).TrimEnd();
            string block = Render(manifest);

            string content = preserved.Length > 0
                ? preserved + "\n\n" + block + "\n"
                : block + "\n";

            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), ct).ConfigureAwait(false);
        }

        public static SkinManifest? Parse(string? fileText)
        {
            if (string.IsNullOrWhiteSpace(fileText)) return null;

            int begin = fileText.LastIndexOf(BeginFence, StringComparison.Ordinal);
            if (begin < 0) return null;

            int jsonStart = begin + BeginFence.Length;
            int endFence = fileText.IndexOf(EndFencePrefix, jsonStart, StringComparison.Ordinal);
            if (endFence < 0) return null;

            int hashStart = endFence + EndFencePrefix.Length;
            int hashEnd = fileText.IndexOf(EndFenceSuffix, hashStart, StringComparison.Ordinal);
            if (hashEnd < 0) return null;

            string declaredHash = fileText.Substring(hashStart, hashEnd - hashStart).Trim();
            string json = fileText.Substring(jsonStart, endFence - jsonStart);

            if (!string.Equals(declaredHash, HashOf(json), StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var manifest = JsonSerializer.Deserialize<SkinManifest>(json);
                if (manifest is null || manifest.Version != SkinManifest.CurrentVersion) return null;
                manifest.Entries ??= new();
                return manifest;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Render(SkinManifest manifest)
        {
            string json = "\n" + JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
            return BeginFence + json + EndFencePrefix + HashOf(json) + EndFenceSuffix;
        }

        private static string HashOf(string json)
        {
            string normalized = json.Replace("\r", string.Empty).Trim();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        }

        private static string StripBlock(string text)
            => string.IsNullOrEmpty(text) ? string.Empty : BlockPattern.Replace(text, string.Empty);
    }
}
