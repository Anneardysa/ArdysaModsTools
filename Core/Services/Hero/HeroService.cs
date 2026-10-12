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

        public Dictionary<string, SetStyleInfo> SetStyles { get; init; } = new();

        public Dictionary<string, string> SetNames { get; init; } = new();

        public List<SetIncompatibilityRule> Incompatible { get; init; } = new();

        public Dictionary<string, List<SetNotice>> Notices { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<string>> BaseLocks { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> AbilityNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> AbilityIcons { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> EffectIcons { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public BasePriorityPolicy BasePriority { get; init; } = new BasePriorityPolicy();

        public int? Method
        {
            get => BasePriority.Default;
            init => BasePriority.Default = value;
        }
    }

    public class HeroService
    {
        private readonly string _baseFolder;
        private readonly string _heroesJsonPath;
        private readonly string _setUpdatesJsonPath;

        public const string HeroesManifestName = "heroes.json";
        
        private static string GitHubHeroesUrl => EnvironmentConfig.BuildFreshUrl("Assets/heroes.json");
        private static string GitHubSetUpdatesUrl => EnvironmentConfig.BuildFreshUrl("Assets/set_update.json");

        public HeroService(string baseFolder)
        {
            _baseFolder = baseFolder ?? throw new ArgumentNullException(nameof(baseFolder));
            _heroesJsonPath = Path.Combine(_baseFolder, "heroes.json");
            _setUpdatesJsonPath = Path.Combine(_baseFolder, "Assets", "set_update.json");
        }

        public async Task<Models.SetUpdatesData> LoadSetUpdatesAsync()
        {
            try
            {
                string raw;
                
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

            if (!File.Exists(_heroesJsonPath))
                throw new FileNotFoundException("heroes.json not found locally and remote fetch failed", _heroesJsonPath);

            var local = await File.ReadAllTextAsync(_heroesJsonPath, Encoding.UTF8).ConfigureAwait(false);
            return ParseHeroesJson(NormalizeJson(local));
        }

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

        public static int CountSets(IEnumerable<HeroSummary> heroes) =>
            heroes.Sum(h => h.Sets?.Keys.Count(k =>
                !string.Equals(k, "Default Set", StringComparison.OrdinalIgnoreCase)) ?? 0);

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

        internal static List<HeroSummary> ParseHeroesJson(string raw)
        {
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

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
                var setStyles = new Dictionary<string, SetStyleInfo>();
                var setNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var incompatible = new List<SetIncompatibilityRule>();

                if (el.TryGetProperty("name", out var ne) && ne.ValueKind == JsonValueKind.String) name = ne.GetString();
                if (el.TryGetProperty("used_by_heroes", out var ue) && ue.ValueKind == JsonValueKind.String) used = ue.GetString();
                if (el.TryGetProperty("prefab", out var pe) && pe.ValueKind == JsonValueKind.String) prefab = pe.GetString();
                if (el.TryGetProperty("primary_attr", out var pae) && pae.ValueKind == JsonValueKind.String)
                    primaryAttr = pae.GetString() ?? "universal";
                var basePriority = ParseBasePriority(el);

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
                            else if (valueEl.ValueKind == JsonValueKind.Object)
                            {
                                ParseStyledSet(setName, valueEl, sets, setStyles);
                            }
                            else
                            {
                            }
                        }
                    }
                }

                if (el.TryGetProperty("set_names", out var namesEl) && namesEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in namesEl.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.String) continue;
                        var display = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(prop.Name) && !string.IsNullOrWhiteSpace(display))
                            setNames[prop.Name.Trim()] = display!.Trim();
                    }
                }

                if (el.TryGetProperty("incompatible", out var incEl) && incEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ruleEl in incEl.EnumerateArray())
                    {
                        if (ruleEl.ValueKind != JsonValueKind.Object) continue;
                        var left = ReadStemArray(ruleEl, "sets");
                        if (left.Length == 0) continue;
                        incompatible.Add(new SetIncompatibilityRule
                        {
                            Sets = left,
                            With = ReadStemArray(ruleEl, "with")
                        });
                    }
                }

                var notices = el.TryGetProperty("notices", out var noticesEl)
                    ? ReadNotices(noticesEl)
                    : new Dictionary<string, List<SetNotice>>(StringComparer.OrdinalIgnoreCase);

                var baseLocks = el.TryGetProperty("base_locks", out var locksEl)
                    ? ReadBaseLocks(locksEl)
                    : new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                var abilityNames = el.TryGetProperty("ability_names", out var abilityEl)
                    ? ReadAbilityNames(abilityEl)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var abilityIcons = el.TryGetProperty("ability_icons", out var abilityIconsEl)
                    ? ReadIconUrls(abilityIconsEl, AbilityEffectGuard.IsValidTag)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var effectIcons = el.TryGetProperty("effect_icons", out var effectIconsEl)
                    ? ReadIconUrls(effectIconsEl, IsEffectStem)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(name)) return null;

                return new HeroSummary
                {
                    Name = name!,
                    UsedByHeroes = used ?? "",
                    Prefab = prefab ?? "",
                    PrimaryAttr = string.IsNullOrWhiteSpace(primaryAttr) ? "universal" : primaryAttr.ToLowerInvariant(),
                    Ids = ids,
                    Sets = sets,
                    SetStyles = setStyles,
                    SetNames = setNames,
                    Incompatible = incompatible,
                    Notices = notices,
                    BaseLocks = baseLocks,
                    AbilityNames = abilityNames,
                    AbilityIcons = abilityIcons,
                    EffectIcons = effectIcons,
                    BasePriority = basePriority
                };
            }
            catch
            {
                return null;
            }
        }

        internal const int MaxNoticesPerSet = 4;
        internal const int MaxNoticeTextLength = 160;

        internal static Dictionary<string, List<SetNotice>> ReadNotices(JsonElement noticesEl)
        {
            var result = new Dictionary<string, List<SetNotice>>(StringComparer.OrdinalIgnoreCase);
            if (noticesEl.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in noticesEl.EnumerateObject())
            {
                var stem = prop.Name.Trim();
                if (stem.Length == 0 || prop.Value.ValueKind != JsonValueKind.Array) continue;

                if (!result.TryGetValue(stem, out var list))
                    list = new List<SetNotice>();

                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (list.Count >= MaxNoticesPerSet) break;
                    var notice = ReadNotice(item);
                    if (notice != null && !list.Contains(notice))
                        list.Add(notice);
                }

                if (list.Count > 0)
                    result[stem] = list;
            }

            return result;
        }

        internal const int MaxLockedSlotsPerBase = 8;

        internal static Dictionary<string, List<string>> ReadBaseLocks(JsonElement locksEl)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (locksEl.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in locksEl.EnumerateObject())
            {
                var stem = prop.Name.Trim();
                if (!stem.StartsWith("base_", StringComparison.OrdinalIgnoreCase)) continue;

                var raw = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => new[] { prop.Value },
                    JsonValueKind.Array => prop.Value.EnumerateArray().ToArray(),
                    _ => Array.Empty<JsonElement>()
                };

                if (!result.TryGetValue(stem, out var slots))
                    slots = new List<string>();
                foreach (var v in raw)
                {
                    if (slots.Count >= MaxLockedSlotsPerBase) break;
                    if (v.ValueKind != JsonValueKind.String) continue;
                    var slot = v.GetString()?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(slot) && !slots.Contains(slot))
                        slots.Add(slot);
                }

                if (slots.Count > 0)
                    result[stem] = slots;
            }

            return result;
        }

        internal const int MaxAbilityNameLength = 64;

        internal static Dictionary<string, string> ReadAbilityNames(JsonElement namesEl)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (namesEl.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in namesEl.EnumerateObject())
            {
                var tag = prop.Name.Trim().ToLowerInvariant();
                if (!AbilityEffectGuard.IsValidTag(tag) || prop.Value.ValueKind != JsonValueKind.String) continue;
                var label = prop.Value.GetString()?.Trim();
                if (string.IsNullOrEmpty(label)) continue;
                result[tag] = label.Length > MaxAbilityNameLength ? label[..MaxAbilityNameLength] : label;
            }

            return result;
        }

        private static readonly string[] IconExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

        private static bool IsEffectStem(string stem) =>
            stem.Length > AbilityEffectGuard.ArchivePrefix.Length
            && stem.StartsWith(AbilityEffectGuard.ArchivePrefix, StringComparison.Ordinal);

        internal static Dictionary<string, string> ReadIconUrls(JsonElement mapEl, Func<string, bool> isValidKey)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (mapEl.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in mapEl.EnumerateObject())
            {
                var key = prop.Name.Trim().ToLowerInvariant();
                if (!isValidKey(key) || prop.Value.ValueKind != JsonValueKind.String) continue;
                var url = prop.Value.GetString()?.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                    || !IconExtensions.Any(ext => uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    continue;
                result[key] = uri.AbsoluteUri;
            }

            return result;
        }

        private static SetNotice? ReadNotice(JsonElement item)
        {
            string? type, anim = null, text = null;
            if (item.ValueKind == JsonValueKind.String)
            {
                type = item.GetString();
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                type = ReadNoticeString(item, "type");
                anim = ReadNoticeString(item, "anim");
                text = ReadNoticeString(item, "text");
            }
            else
            {
                return null;
            }

            type = type?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type)) return null;

            if (!SetNotice.IsKnownType(type))
                return text == null ? null : new SetNotice(SetNotice.Custom, Text: text);

            return type switch
            {
                SetNotice.Custom => text == null ? null : new SetNotice(SetNotice.Custom, Text: text),
                SetNotice.AnimBug => new SetNotice(SetNotice.AnimBug, Anim: anim),
                _ => new SetNotice(type)
            };
        }

        private static string? ReadNoticeString(JsonElement obj, string name)
        {
            if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            var s = v.GetString()?.Trim();
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length > MaxNoticeTextLength ? s[..MaxNoticeTextLength].TrimEnd() : s;
        }

        private static string[] ReadStemArray(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return arr.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .ToArray();
        }

        private static BasePriorityPolicy ParseBasePriority(JsonElement el)
        {
            var policy = new BasePriorityPolicy();

            if (!el.TryGetProperty("method", out var me))
                return policy;

            if (me.ValueKind == JsonValueKind.Number)
            {
                if (me.TryGetInt32(out var scalar)) policy.Default = scalar;
                return policy;
            }

            if (me.ValueKind != JsonValueKind.Object)
                return policy;

            if (me.TryGetProperty("default", out var de) && TryReadMethod(de, out var dm))
                policy.Default = dm;

            if (me.TryGetProperty("sets", out var se) && se.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in se.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(prop.Name) && TryReadMethod(prop.Value, out var sm))
                        policy.Sets[prop.Name] = sm;
                }
            }

            if (me.TryGetProperty("items", out var ie) && ie.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in ie.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out var itemId) && TryReadMethod(prop.Value, out var im))
                        policy.Items[itemId] = im;
                }
            }

            return policy;
        }

        private static bool TryReadMethod(JsonElement value, out int method)
        {
            method = 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
            {
                method = n;
                return true;
            }
            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out var s))
            {
                method = s;
                return true;
            }
            return false;
        }

        private static void ParseStyledSet(
            string setName,
            JsonElement setObject,
            Dictionary<string, string[]> sets,
            Dictionary<string, SetStyleInfo> setStyles)
        {
            if (!setObject.TryGetProperty("styles", out var stylesEl) || stylesEl.ValueKind != JsonValueKind.Object)
                return;

            string? groupThumbnail = null;
            if (setObject.TryGetProperty("thumbnail", out var thumbEl) &&
                thumbEl.ValueKind == JsonValueKind.String)
            {
                var t = thumbEl.GetString();
                if (!string.IsNullOrWhiteSpace(t)) groupThumbnail = t;
            }

            foreach (var styleProp in stylesEl.EnumerateObject())
            {
                var label = styleProp.Name;
                if (string.IsNullOrWhiteSpace(label) || styleProp.Value.ValueKind != JsonValueKind.Array)
                    continue;

                var urls = styleProp.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (urls.Length == 0)
                    continue;

                var flatKey = $"{setName} ({label})";
                if (sets.ContainsKey(flatKey))
                    continue;

                sets[flatKey] = urls;
                setStyles[flatKey] = new SetStyleInfo { Group = setName, Label = label, GroupThumbnail = groupThumbnail };
            }
        }

        private static string NormalizeJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            if (raw[0] == '\uFEFF') raw = raw.Substring(1);
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            return raw;
        }
    }
}

