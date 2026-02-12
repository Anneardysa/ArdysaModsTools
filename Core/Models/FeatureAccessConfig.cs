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
    /// Root configuration model for remote feature access control.
    /// Fetched from R2 CDN: config/feature_access.json
    /// Controls which features are accessible to end users.
    /// </summary>
    /// <remarks>
    /// Design: Fail-open — all features default to enabled.
    /// If R2 is unreachable or JSON is invalid, users can still use all features.
    /// This prevents a CDN outage from locking users out of functionality.
    /// </remarks>
    public class FeatureAccessConfig
    {
        /// <summary>
        /// Access control for the Skin Selector (Hero Gallery) feature.
        /// </summary>
        [JsonPropertyName("skinSelector")]
        public FeatureAccess SkinSelector { get; set; } = new();

        /// <summary>
        /// Access control for the Miscellaneous options feature.
        /// </summary>
        [JsonPropertyName("miscellaneous")]
        public FeatureAccess Miscellaneous { get; set; } = new();

        /// <summary>
        /// Creates a default config with all features enabled.
        /// Used as fallback when remote fetch fails.
        /// </summary>
        public static FeatureAccessConfig CreateDefault() => new();
    }

    /// <summary>
    /// Access control settings for a single feature.
    /// </summary>
    public class FeatureAccess
    {
        /// <summary>
        /// Whether the feature is currently accessible.
        /// Default: true (fail-open design).
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Message shown to users when the feature is disabled.
        /// Should explain why and when it will be back.
        /// </summary>
        [JsonPropertyName("disabledMessage")]
        public string? DisabledMessage { get; set; }

        /// <summary>
        /// Gets the message to display, with a sensible fallback.
        /// </summary>
        public string GetDisplayMessage() =>
            !string.IsNullOrWhiteSpace(DisabledMessage)
                ? DisabledMessage
                : "This feature is temporarily unavailable. Please try again later.";
    }
}
