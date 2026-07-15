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
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models
{
    public class RemoteMiscConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("thumbnailBaseUrl")]
        public string ThumbnailBaseUrl { get; set; } = "";

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();

        [JsonPropertyName("options")]
        public List<RemoteMiscOption> Options { get; set; } = new();
    }

    public class RemoteMiscOption
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonIgnore]
        public bool IsSpecialVpk => string.Equals(Type, "vpk", StringComparison.OrdinalIgnoreCase);

        [JsonPropertyName("excludesWith")]
        public List<string> ExcludesWith { get; set; } = new();

        [JsonPropertyName("thumbnailFolder")]
        public string ThumbnailFolder { get; set; } = "";

        [JsonPropertyName("thumbnailExtension")]
        public string ThumbnailExtension { get; set; } = "webp";

        [JsonPropertyName("choices")]
        public List<RemoteMiscChoice> Choices { get; set; } = new();

        public List<string> GetChoiceNames() => Choices.Select(c => c.Name).ToList();

        public string? GetChoiceUrl(string choiceName)
        {
            foreach (var choice in Choices)
            {
                if (choice.Name == choiceName)
                    return choice.GetPrimaryUrl();

                if (choice.Styles != null)
                {
                    var style = choice.Styles.FirstOrDefault(s => s.Name == choiceName);
                    if (style != null)
                        return style.GetPrimaryUrl();
                }
            }
            return null;
        }

        public List<string> GetChoiceUrls(string choiceName)
        {
            foreach (var choice in Choices)
            {
                if (choice.Name == choiceName)
                    return choice.GetAllUrls();

                if (choice.Styles != null)
                {
                    var style = choice.Styles.FirstOrDefault(s => s.Name == choiceName);
                    if (style != null)
                        return style.GetAllUrls();
                }
            }
            return new List<string>();
        }
    }

    public class RemoteMiscChoice
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("urls")]
        public List<string>? Urls { get; set; }

        [JsonPropertyName("thumbnailId")]
        public string? ThumbnailId { get; set; }

        [JsonPropertyName("styles")]
        public List<RemoteMiscChoice>? Styles { get; set; }

        public List<string> GetAllUrls()
        {
            if (Urls != null && Urls.Count > 0)
                return Urls;
            if (!string.IsNullOrEmpty(Url))
                return new List<string> { Url };
            if (Styles != null && Styles.Count > 0)
                return Styles[0].GetAllUrls();
            return new List<string>();
        }

        public string GetPrimaryUrl() => GetAllUrls().FirstOrDefault() ?? "";
    }
}

