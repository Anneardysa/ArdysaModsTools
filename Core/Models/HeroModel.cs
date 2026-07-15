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
using System.Linq;
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Models
{
    public sealed class HeroModel
    {
        [JsonPropertyName("id")]
        public List<int> ItemIds { get; set; } = new List<int>();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("used_by_heroes")]
        public string HeroId { get; set; } = string.Empty;

        [JsonPropertyName("localized_name")]
        public string LocalizedName { get; set; } = string.Empty;

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

        [JsonPropertyName("primary_attr")]
        public string PrimaryAttribute { get; set; } = "universal";

        [JsonPropertyName("method")]
        public int? Method { get; set; }

        [JsonPropertyName("sets")]
        public Dictionary<string, List<string>> Sets { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public Dictionary<string, SetStyleInfo> SetStyles { get; set; } = new Dictionary<string, SetStyleInfo>(StringComparer.OrdinalIgnoreCase);

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

        [JsonIgnore]
        public string Id => !string.IsNullOrWhiteSpace(HeroId) ? HeroId : (Name ?? string.Empty);

        public HeroModel() { }

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

