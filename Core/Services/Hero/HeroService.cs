// Services/HeroService.cs
using ArdysaModsTools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
    }

    public class HeroService
    {
        private readonly string _baseFolder;
        private readonly string _heroesJsonPath;
        
        // URL now loaded from environment configuration
        private static string GitHubHeroesUrl => EnvironmentConfig.BuildRawUrl("Assets/heroes.json");

        public HeroService(string baseFolder)
        {
            _baseFolder = baseFolder ?? throw new ArgumentNullException(nameof(baseFolder));
            _heroesJsonPath = Path.Combine(_baseFolder, "heroes.json");
        }

        public async Task<List<HeroSummary>> LoadHeroesAsync()
        {
            string raw;
            
            // Try loading from GitHub first, fallback to local
            try
            {
                raw = await LoadFromGitHubAsync();
            }
            catch
            {
                // Fallback to local file
                if (!File.Exists(_heroesJsonPath))
                    throw new FileNotFoundException("heroes.json not found locally and GitHub fetch failed", _heroesJsonPath);
                
                raw = await File.ReadAllTextAsync(_heroesJsonPath, Encoding.UTF8).ConfigureAwait(false);
            }
            
            raw = NormalizeJson(raw);
            return ParseHeroesJson(raw);
        }

        /// <summary>
        /// Load heroes.json from GitHub URL.
        /// </summary>
        private async Task<string> LoadFromGitHubAsync()
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools");
            
            var response = await client.GetAsync(GitHubHeroesUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

                if (el.TryGetProperty("name", out var ne) && ne.ValueKind == JsonValueKind.String) name = ne.GetString();
                if (el.TryGetProperty("used_by_heroes", out var ue) && ue.ValueKind == JsonValueKind.String) used = ue.GetString();
                if (el.TryGetProperty("prefab", out var pe) && pe.ValueKind == JsonValueKind.String) prefab = pe.GetString();
                if (el.TryGetProperty("primary_attr", out var pae) && pae.ValueKind == JsonValueKind.String) 
                    primaryAttr = pae.GetString() ?? "universal";

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
                    Sets = sets
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
