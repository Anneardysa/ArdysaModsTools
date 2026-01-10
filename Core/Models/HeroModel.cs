using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Models
{
    public sealed class HeroModel
    {
        // numeric item ids (used for items_game kv blocks)
        [JsonPropertyName("id")]
        public List<int> ItemIds { get; set; } = new List<int>();

        // JSON "name" - usually a friendly short name (kept for compatibility)
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        // JSON "used_by_heroes" - internal npc id like "npc_dota_hero_abaddon"
        [JsonPropertyName("used_by_heroes")]
        public string HeroId { get; set; } = string.Empty;

        // Optional localized/display name field (if your JSON uses localized_name)
        [JsonPropertyName("localized_name")]
        public string LocalizedName { get; set; } = string.Empty;

        // UI display name prefers localized_name, then Name, then HeroId
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LocalizedName)) return LocalizedName;
                if (!string.IsNullOrWhiteSpace(Name)) return Name;
                return HeroId ?? string.Empty;
            }
        }

        // primary attribute
        [JsonPropertyName("primary_attr")]
        public string PrimaryAttribute { get; set; } = "universal";

        // sets map: setName -> list of asset URLs/paths
        [JsonPropertyName("sets")]
        public Dictionary<string, List<string>> Sets { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public IReadOnlyList<string> Skins
        {
            get
            {
                if (Sets != null && Sets.Count > 0)
                    return Sets.Keys.ToList().AsReadOnly();
                return Array.Empty<string>();
            }
        }

        // Backwards-compatible Id property (used across UI): return HeroId if available
        [JsonIgnore]
        public string Id => !string.IsNullOrWhiteSpace(HeroId) ? HeroId : (Name ?? string.Empty);

        public HeroModel() { }

        // Convenience ctor: heroId first, display name second, then skins
        public HeroModel(string heroId, string displayName, IEnumerable<string>? skins = null, string primaryAttribute = "universal")
        {
            if (string.IsNullOrWhiteSpace(heroId)) throw new ArgumentException("heroId must be provided", nameof(heroId));
            HeroId = heroId;
            Name = displayName ?? heroId;
            LocalizedName = displayName ?? Name;
            PrimaryAttribute = string.IsNullOrWhiteSpace(primaryAttribute) ? "universal" : primaryAttribute.ToLowerInvariant();

            if (skins != null)
            {
                Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default Set", new List<string>(skins) }
                };
            }
        }

        public override string ToString() => $"{DisplayName} ({Id})";
    }
}
