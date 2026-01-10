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
        /// Get thumbnail URL for a specific choice.
        /// </summary>
        public string? GetThumbnailUrl(string choice)
        {
            if (string.IsNullOrEmpty(ThumbnailUrlPattern)) return null;
            
            // Convert choice to URL-safe format (lowercase, replace spaces with underscores)
            var safeChoice = choice.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
            
            return ThumbnailUrlPattern.Replace("{choice}", safeChoice);
        }
    }
}

