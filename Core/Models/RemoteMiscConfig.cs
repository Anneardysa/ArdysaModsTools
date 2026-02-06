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
    /// <summary>
    /// Root configuration model for remote misc config JSON.
    /// </summary>
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

    /// <summary>
    /// A single misc option (e.g., Weather, Terrain) from remote config.
    /// </summary>
    public class RemoteMiscOption
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("thumbnailFolder")]
        public string ThumbnailFolder { get; set; } = "";

        /// <summary>
        /// File extension for thumbnails (default: webp). Do not include the dot.
        /// </summary>
        [JsonPropertyName("thumbnailExtension")]
        public string ThumbnailExtension { get; set; } = "webp";

        [JsonPropertyName("choices")]
        public List<RemoteMiscChoice> Choices { get; set; } = new();

        /// <summary>
        /// Gets the list of choice names for UI display.
        /// </summary>
        public List<string> GetChoiceNames() => Choices.Select(c => c.Name).ToList();

        /// <summary>
        /// Gets the primary URL for a specific choice by name.
        /// </summary>
        public string? GetChoiceUrl(string choiceName) =>
            Choices.FirstOrDefault(c => c.Name == choiceName)?.GetPrimaryUrl();

        /// <summary>
        /// Gets all URLs for a specific choice by name (for multi-file downloads).
        /// </summary>
        public List<string> GetChoiceUrls(string choiceName) =>
            Choices.FirstOrDefault(c => c.Name == choiceName)?.GetAllUrls() ?? new List<string>();
    }

    /// <summary>
    /// A single choice within a misc option (e.g., "Weather Ash").
    /// Supports both single URL and multiple URLs for downloads.
    /// </summary>
    public class RemoteMiscChoice
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Single URL for simple downloads (use this OR urls, not both).
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Multiple URLs for choices that require downloading several files.
        /// </summary>
        [JsonPropertyName("urls")]
        public List<string>? Urls { get; set; }

        /// <summary>
        /// Gets all URLs for this choice (works with both url and urls).
        /// </summary>
        public List<string> GetAllUrls()
        {
            if (Urls != null && Urls.Count > 0)
                return Urls;
            if (!string.IsNullOrEmpty(Url))
                return new List<string> { Url };
            return new List<string>();
        }

        /// <summary>
        /// Gets the primary URL (first URL for backward compatibility).
        /// </summary>
        public string GetPrimaryUrl() => GetAllUrls().FirstOrDefault() ?? "";
    }
}

