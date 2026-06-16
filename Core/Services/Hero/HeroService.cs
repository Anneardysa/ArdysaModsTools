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
// Services/HeroService.cs
using ArdysaModsTools.Models;
using ArdysaModsTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    public class HeroSummary
    {
        public string Name { get; init; } = "";
        public string UsedByHeroes { get; init; } = "";
        public string Prefab { get; init; } = "";
        public string PrimaryAttr { get; init; } = "universal";
        public string[] Ids { get; init; } = Array.Empty<string>();
        public Dictionary<string, string[]> Sets { get; init; } = new();

        // Optional explicit base-priority method (1 = Base wins, 2 = Base last).
        // Null → fall back to item_slot hero_base auto-detection during generation.
        public int? Method { get; init; }
    }

    public class HeroService
    {
        private readonly string _baseFolder;
        private readonly string _heroesJsonPath;
        private readonly string _setUpdatesJsonPath;

        /// <summary>Name of the persisted last-known-good hero manifest (see <see cref="ManifestCache"/>).</summary>
        public const string HeroesManifestName = "heroes.json";
        
        // URLs loaded from environment configuration (with cache-busting for real-time updates)
        private static string GitHubHeroesUrl => EnvironmentConfig.BuildFreshUrl("Assets/heroes.json");
        private static string GitHubSetUpdatesUrl => EnvironmentConfig.BuildFreshUrl("Assets/set_update.json");

        public HeroService(string baseFolder)
        {
            _baseFolder = baseFolder ?? throw new ArgumentNullException(nameof(baseFolder));
            _heroesJsonPath = Path.Combine(_baseFolder, "heroes.json");
            _setUpdatesJsonPath = Path.Combine(_baseFolder, "Assets", "set_update.json");
        }

        /// <summary>
        /// Load set updates from GitHub (or local fallback).
        /// Returns empty data if file doesn't exist or fails to load.
        /// </summary>
        public async Task<Models.SetUpdatesData> LoadSetUpdatesAsync()
        {
            try
            {
                string raw;
                
                // Try loading from CDN with fallback first
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var json = await ArdysaModsTools.Core.Services.Cdn.CdnFallbackService.Instance.DownloadStringWithFallbackAsync(GitHubSetUpdatesUrl, cts.Token).ConfigureAwait(false);
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        raw = json;
                    }
                    else
                    {
                        throw new Exception("CDN download returned empty");
                    }
                }
                catch
                {
                    // Fallback to local file
                    if (!File.Exists(_setUpdatesJsonPath))
                        return new Models.SetUpdatesData();
                    
                    raw = await File.ReadAllTextAsync(_setUpdatesJsonPath, Encoding.UTF8).ConfigureAwait(false);
                }
                
                raw = NormalizeJson(raw);
                return ParseSetUpdatesJson(raw);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load set updates: {ex.Message}");
                return new Models.SetUpdatesData();
            }
        }

        private static Models.SetUpdatesData ParseSetUpdatesJson(string raw)
        {
            var options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            using var doc = JsonDocument.Parse(raw, options);
            var root = doc.RootElement;
            
            var data = new Models.SetUpdatesData();
            
            if (root.TryGetProperty("version", out var versionEl))
                data.Version = versionEl.GetString() ?? "1.0.0";
                
            if (root.TryGetProperty("lastUpdated", out var lastUpdatedEl) && 
                DateTime.TryParse(lastUpdatedEl.GetString(), out var lastUpdated))
                data.LastUpdated = lastUpdated;
            
            if (root.TryGetProperty("updates", out var updatesEl) && updatesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var batchEl in updatesEl.EnumerateArray())
                {
                    var batch = new Models.SetUpdateBatch();
                    
                    if (batchEl.TryGetProperty("date", out var dateEl) && 
                        DateTime.TryParse(dateEl.GetString(), out var batchDate))
                    {
                        batch = batch with { Date = batchDate };
                    }
                    
                    if (batchEl.TryGetProperty("sets", out var setsEl) && setsEl.ValueKind == JsonValueKind.Array)
                    {
                        var sets = new List<Models.SetUpdateEntry>();
                        foreach (var setEl in setsEl.EnumerateArray())
                        {
                            var heroId = "";
                            var setName = "";
                            
                            if (setEl.TryGetProperty("heroId", out var heroIdEl))
                                heroId = heroIdEl.GetString() ?? "";
                            if (setEl.TryGetProperty("setName", out var setNameEl))
                                setName = setNameEl.GetString() ?? "";
                            
                            if (!string.IsNullOrEmpty(heroId) && !string.IsNullOrEmpty(setName))
                            {
                                sets.Add(new Models.SetUpdateEntry { HeroId = heroId, SetName = setName });
                            }
                        }
                        batch = batch with { Sets = sets };
                    }
                    
                    if (batch.Sets.Count > 0)
                        data.Updates.Add(batch);
                }
            }
            
            return data;
        }

        public async Task<List<HeroSummary>> LoadHeroesAsync()
        {
            // 1. Live CDN copy (preferred). On success it is persisted so later impaired launches reuse
            //    it instead of the stale bundled snapshot — keeping hero data in sync with set_update.json.
            try
            {
                var (json, etag, lastModified) = await LoadFromGitHubAsync().ConfigureAwait(false);
                var normalized = NormalizeJson(json);
                var heroes = ParseHeroesJson(normalized);
                await PersistHeroesAsync(normalized, etag, lastModified, heroes).ConfigureAwait(false);
                return heroes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[HeroService] Remote heroes.json unavailable ({ex.Message}); falling back to last-known-good, then bundled.");
            }

            // 2. Last successfully-downloaded copy (fresher than the bundled snapshot). See plan #2.
            var persisted = await ManifestCache.ReadAsync(HeroesManifestName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(persisted))
            {
                try
                {
                    return ParseHeroesJson(NormalizeJson(persisted));
                }
                catch (Exception pex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HeroService] Persisted heroes.json unreadable: {pex.Message}");
                }
            }

            // 3. Bundled snapshot shipped with the installed version (last resort — may be stale).
            if (!File.Exists(_heroesJsonPath))
                throw new FileNotFoundException("heroes.json not found locally and remote fetch failed", _heroesJsonPath);

            var local = await File.ReadAllTextAsync(_heroesJsonPath, Encoding.UTF8).ConfigureAwait(false);
            return ParseHeroesJson(NormalizeJson(local));
        }

        /// <summary>
        /// Load heroes.json from the CDN fallback chain, returning the raw JSON plus freshness headers.
        /// </summary>
        private async Task<(string Json, string? ETag, string? LastModified)> LoadFromGitHubAsync()
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await ArdysaModsTools.Core.Services.Cdn.CdnFallbackService.Instance
                .DownloadWithFallbackAsync(GitHubHeroesUrl, cts.Token).ConfigureAwait(false);

            if (!result.Success || result.Data == null)
                throw new Exception("Failed to download heroes.json from all CDNs.");

            var bytes = result.Data;
            string json = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
                : Encoding.UTF8.GetString(bytes);

            return (json, result.ETag, result.LastModified);
        }

        /// <summary>Total non-default cosmetic sets across all heroes (shown in Settings → Hero Database).</summary>
        public static int CountSets(IEnumerable<HeroSummary> heroes) =>
            heroes.Sum(h => h.Sets?.Keys.Count(k =>
                !string.Equals(k, "Default Set", StringComparison.OrdinalIgnoreCase)) ?? 0);

        /// <summary>
        /// Persist a freshly-downloaded heroes.json + SHA-256 meta as the last-known-good copy.
        /// Best-effort: a failure here never breaks loading (the in-memory data is already parsed).
        /// </summary>
        private static async Task PersistHeroesAsync(string normalizedJson, string? etag, string? lastModified, List<HeroSummary> heroes)
        {
            try
            {
                var meta = new ManifestMeta
                {
                    Sha256 = ManifestCache.ComputeSha256(normalizedJson),
                    ETag = etag,
                    LastModified = lastModified,
                    FetchedAtUtc = DateTime.UtcNow,
                    ItemCount = CountSets(heroes),
                    Source = "cdn"
                };
                await ManifestCache.WriteAsync(HeroesManifestName, normalizedJson, meta).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeroService] Persisting heroes.json failed: {ex.Message}");
            }
        }

        private List<HeroSummary> ParseHeroesJson(string raw)
        {
            // JsonDocumentOptions for parsing with trailing commas and comments
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            // Try parse as array
            var list = new List<HeroSummary>();
            try
            {
                using var doc = JsonDocument.Parse(raw, docOptions);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var hero = ParseHeroElement(el);
                        if (hero != null) list.Add(hero);
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var hero = ParseHeroElement(doc.RootElement);
                    if (hero != null) list.Add(hero);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to parse heroes.json: " + ex.Message, ex);
            }

            return list.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static HeroSummary? ParseHeroElement(JsonElement el)
        {
            try
            {
                string? name = null;
                string? used = null;
                string? prefab = null;
                string primaryAttr = "universal";
                string[] ids = Array.Empty<string>();
                var sets = new Dictionary<string, string[]>();
                int? method = null;

                if (el.TryGetProperty("name", out var ne) && ne.ValueKind == JsonValueKind.String) name = ne.GetString();
                if (el.TryGetProperty("used_by_heroes", out var ue) && ue.ValueKind == JsonValueKind.String) used = ue.GetString();
                if (el.TryGetProperty("prefab", out var pe) && pe.ValueKind == JsonValueKind.String) prefab = pe.GetString();
                if (el.TryGetProperty("primary_attr", out var pae) && pae.ValueKind == JsonValueKind.String)
                    primaryAttr = pae.GetString() ?? "universal";
                if (el.TryGetProperty("method", out var me) && me.ValueKind == JsonValueKind.Number && me.TryGetInt32(out var m))
                    method = m;

                if (el.TryGetProperty("id", out var idEl))
                {
                    if (idEl.ValueKind == JsonValueKind.Array)
                    {
                        var tmp = new List<string>();
                        foreach (var it in idEl.EnumerateArray())
                        {
                            if (it.ValueKind == JsonValueKind.Number) tmp.Add(it.GetRawText());
                            else if (it.ValueKind == JsonValueKind.String) tmp.Add(it.GetString() ?? "");
                        }
                        ids = tmp.ToArray();
                    }
                    else if (idEl.ValueKind == JsonValueKind.Number) ids = new[] { idEl.GetRawText() };
                    else if (idEl.ValueKind == JsonValueKind.String) ids = new[] { idEl.GetString() ?? "" };
                }

                // parse sets (allow two shapes)
                if (el.TryGetProperty("sets", out var setsEl))
                {
                    if (setsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in setsEl.EnumerateObject())
                        {
                            var setName = prop.Name;
                            var valueEl = prop.Value;
                            if (valueEl.ValueKind == JsonValueKind.Array)
                            {
                                var arr = valueEl.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                                        .Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                                sets[setName] = arr;
                            }
                            else if (valueEl.ValueKind == JsonValueKind.String)
                            {
                                var s = valueEl.GetString() ?? "";
                                sets[setName] = new[] { s };
                            }
                            else
                            {
                                // ignore other shapes
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(name)) return null;

                return new HeroSummary
                {
                    Name = name!,
                    UsedByHeroes = used ?? "",
                    Prefab = prefab ?? "",
                    PrimaryAttr = string.IsNullOrWhiteSpace(primaryAttr) ? "universal" : primaryAttr.ToLowerInvariant(),
                    Ids = ids,
                    Sets = sets,
                    Method = method
                };
            }
            catch
            {
                return null;
            }
        }

        // small normalizer: remove BOM and convert CRLF to \n
        private static string NormalizeJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            if (raw[0] == '\uFEFF') raw = raw.Substring(1);
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            return raw;
        }
    }
}

