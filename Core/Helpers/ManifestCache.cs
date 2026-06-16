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
    /// <summary>
    /// Integrity/identity metadata stored next to a persisted remote manifest (e.g. heroes.json).
    /// Lets the app tell whether the local copy is the latest without trusting the bundled snapshot.
    /// </summary>
    public sealed class ManifestMeta
    {
        /// <summary>Lowercase hex SHA-256 of the normalized manifest content.</summary>
        public string Sha256 { get; set; } = "";

        /// <summary>HTTP ETag of the copy that was downloaded, if the CDN provided one.</summary>
        public string? ETag { get; set; }

        /// <summary>HTTP Last-Modified of the downloaded copy, if provided.</summary>
        public string? LastModified { get; set; }

        /// <summary>When this copy was fetched and persisted (UTC).</summary>
        public DateTime FetchedAtUtc { get; set; }

        /// <summary>Domain item count for display (e.g. number of cosmetic sets).</summary>
        public int ItemCount { get; set; }

        /// <summary>Where this copy came from: "cdn", "manual", "bundled".</summary>
        public string Source { get; set; } = "";
    }

    /// <summary>
    /// Persists the last successfully-downloaded copy of small remote JSON manifests (heroes.json,
    /// set_update.json) under <c>%LocalAppData%\ArdysaModsTools\data</c>, alongside a SHA-256 meta
    /// sidecar. On an impaired launch the app prefers this fresh copy over the stale snapshot bundled
    /// inside the installed version — keeping hero data consistent with the live "Latest Updates" feed.
    /// </summary>
    /// <remarks>
    /// [AMT:OPUS] Hashing + last-known-good persistence. The SHA-256 here gates the manual
    /// "is my database up to date" check (Settings → Hero Database). A wrong hash or a non-atomic
    /// write would silently serve stale or torn hero data — keep the hash and atomic-swap paths intact.
    /// </remarks>
    public static class ManifestCache
    {
        private static readonly JsonSerializerOptions MetaJsonOptions = new() { WriteIndented = true };

        /// <summary>Persistent data directory for last-known-good manifests.</summary>
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools",
            "data");

        /// <summary>Full path of a persisted manifest by name (e.g. "heroes.json").</summary>
        public static string GetManifestPath(string name) => Path.Combine(DataDirectory, name);

        private static string GetMetaPath(string name) => Path.Combine(DataDirectory, name + ".meta.json");

        /// <summary>BOM-strip + CRLF→LF, matching HeroService.NormalizeJson so hashes are comparable.</summary>
        public static string NormalizeJson(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            if (raw[0] == '\uFEFF') raw = raw.Substring(1);
            return raw.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>Lowercase hex SHA-256 of UTF-8 content.</summary>
        public static string ComputeSha256(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Atomically persist a manifest and its meta sidecar. Failures are non-fatal (logged by caller).
        /// </summary>
        public static async Task WriteAsync(string name, string content, ManifestMeta meta, CancellationToken ct = default)
        {
            Directory.CreateDirectory(DataDirectory);

            string path = GetManifestPath(name);
            string tmp = path + ".tmp";

            // Write to a temp file then move into place so a crash mid-write can't leave a torn manifest.
            await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false), ct).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);

            string metaJson = JsonSerializer.Serialize(meta, MetaJsonOptions);
            await File.WriteAllTextAsync(GetMetaPath(name), metaJson, new UTF8Encoding(false), ct).ConfigureAwait(false);
        }

        /// <summary>Read the persisted manifest content, or null if absent/unreadable.</summary>
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

        /// <summary>Read the persisted meta sidecar, or null if absent/unreadable.</summary>
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
