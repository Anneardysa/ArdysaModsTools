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
using System.Collections.Generic;

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Banner carousel manifest fetched from the R2 CDN (<c>config/banners.json</c>).
    /// Drives the main-shell banner; when unavailable the shell falls back to the bundled banner image.
    /// </summary>
    public sealed class BannerConfig
    {
        /// <summary>Ordered carousel slides.</summary>
        public List<BannerSlide> Slides { get; set; } = new();

        /// <summary>
        /// Current ModsPack release version (e.g. <c>2.6</c>), shown on the What's New card.
        /// Optional: when absent the shell shows its bundled fallback. Served from the same R2
        /// manifest (<c>config/banner.json</c>) so it can be bumped without an app release.
        /// </summary>
        public string? ModspackVersion { get; set; }
    }

    /// <summary>
    /// A single carousel slide.
    /// </summary>
    public sealed class BannerSlide
    {
        /// <summary>
        /// Image path relative to the CDN base (e.g. <c>Assets/image/banner/slide1.jpg</c>),
        /// or a full URL. Resolved against the CDN base before being shown.
        /// </summary>
        public string Image { get; set; } = "";

        /// <summary>Optional URL opened when the slide is clicked.</summary>
        public string? Link { get; set; }

        /// <summary>Optional caption shown over the slide.</summary>
        public string? Title { get; set; }
    }
}
