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
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Helpers
{
    public sealed class ManifestMeta
    {
        public string Sha256 { get; set; } = "";

        public string? ETag { get; set; }

        public string? LastModified { get; set; }

        public DateTime FetchedAtUtc { get; set; }

        public int ItemCount { get; set; }

        public string Source { get; set; } = "";
    }

    public static class ManifestCache
    {
        private static readonly JsonSerializerOptions MetaJsonOptions = new() { WriteIndented = true };

        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools",
            "data");

        public static string GetManifestPath(string name) => Path.Combine(DataDirectory, name);

        private static string GetMetaPath(string name) => Path.Combine(DataDirectory, name + ".meta.json");

        public static string NormalizeJson(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            if (raw[0] == '\uFEFF') raw = raw.Substring(1);
            return raw.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public static string ComputeSha256(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static async Task WriteAsync(string name, string content, ManifestMeta meta, CancellationToken ct = default)
        {
            Directory.CreateDirectory(DataDirectory);

            string path = GetManifestPath(name);
            string tmp = path + ".tmp";

            await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false), ct).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);

            string metaJson = JsonSerializer.Serialize(meta, MetaJsonOptions);
            await File.WriteAllTextAsync(GetMetaPath(name), metaJson, new UTF8Encoding(false), ct).ConfigureAwait(false);
        }

        public static async Task<string?> ReadAsync(string name, CancellationToken ct = default)
        {
            string path = GetManifestPath(name);
            if (!File.Exists(path)) return null;
            try
            {
                return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public static ManifestMeta? ReadMeta(string name)
        {
            string path = GetMetaPath(name);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonSerializer.Deserialize<ManifestMeta>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
