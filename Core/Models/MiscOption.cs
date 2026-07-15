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
    public class MiscOption
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public List<string> Choices { get; set; } = new();

        public string? SelectedChoice { get; set; }

        public string? ThumbnailUrlPattern { get; set; }

        public Dictionary<string, string> ChoiceThumbnails { get; set; } = new();

        public Dictionary<string, string> ChoiceThumbnailIds { get; set; } = new();

        public bool IsSpecialVpk { get; set; }

        public List<string> ExcludesWith { get; set; } = new();

        public Dictionary<string, List<string>> ChoiceStyles { get; set; } = new();

        public string? GetThumbnailUrl(string choice)
        {
            if (ChoiceThumbnails.TryGetValue(choice, out var thumbUrl))
                return thumbUrl;

            if (IsDefaultChoice(choice)) return null;

            if (string.IsNullOrEmpty(ThumbnailUrlPattern)) return null;

            var token = ChoiceThumbnailIds.TryGetValue(choice, out var overrideId) && !string.IsNullOrEmpty(overrideId)
                ? overrideId
                : SanitizeChoice(choice);

            return ThumbnailUrlPattern.Replace("{choice}", token);
        }

        public static bool IsDefaultChoice(string choice)
        {
            if (string.IsNullOrEmpty(choice)) return false;
            var n = choice.TrimStart().ToLowerInvariant();
            return n.StartsWith("default") || n.StartsWith("disable");
        }

        public static string SanitizeChoice(string choice)
        {
            if (string.IsNullOrEmpty(choice)) return string.Empty;

            var stripped = System.Text.RegularExpressions.Regex.Replace(choice, @"[^A-Za-z0-9_\s-]", "");
            return stripped.Trim().ToLowerInvariant().Replace(" ", "_");
        }
    }
}


