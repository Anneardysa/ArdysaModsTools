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
namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents a miscellaneous mod option with its available choices.
    /// </summary>
    public class MiscOption
    {
        /// <summary>
        /// Internal ID used for generation dictionary key (e.g., "Weather", "Map").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name shown in UI (e.g., "Weather", "Terrain").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Category grouping for section separator (e.g., "Environment", "Audio & Visual").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// List of available choices for this option.
        /// </summary>
        public List<string> Choices { get; set; } = new();

        /// <summary>
        /// Currently selected choice (null = first/default).
        /// </summary>
        public string? SelectedChoice { get; set; }

        /// <summary>
        /// URL pattern for thumbnails. Use {choice} as placeholder.
        /// Example: "https://example.com/misc/weather/{choice}.png"
        /// </summary>
        public string? ThumbnailUrlPattern { get; set; }

        /// <summary>
        /// Per-choice thumbnail URLs (derived from asset URLs when available).
        /// Takes precedence over ThumbnailUrlPattern.
        /// </summary>
        public Dictionary<string, string> ChoiceThumbnails { get; set; } = new();

        /// <summary>
        /// Get thumbnail URL for a specific choice.
        /// Priority: ChoiceThumbnails (derived from zip URL) -> ThumbnailUrlPattern (legacy)
        /// </summary>
        public string? GetThumbnailUrl(string choice)
        {
            // First check if we have a derived thumbnail URL for this choice
            if (ChoiceThumbnails.TryGetValue(choice, out var thumbUrl))
                return thumbUrl;
            
            // Fall back to pattern-based URL
            if (string.IsNullOrEmpty(ThumbnailUrlPattern)) return null;
            
            // Convert choice to URL-safe format (lowercase, replace spaces with underscores)
            var safeChoice = choice.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
            
            return ThumbnailUrlPattern.Replace("{choice}", safeChoice);
        }
    }
}


