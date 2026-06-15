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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// A single GitHub release rendered in the in-app "What's New" changelog.
    /// </summary>
    public sealed class ReleaseNote
    {
        /// <summary>Release tag (e.g. <c>2.1.27-beta</c>).</summary>
        public string Tag { get; set; } = "";

        /// <summary>Release title; falls back to the tag when empty.</summary>
        public string Name { get; set; } = "";

        /// <summary>Publish date (UTC), if available.</summary>
        public DateTime? Date { get; set; }

        /// <summary>Release notes body (GitHub-flavored markdown).</summary>
        public string Body { get; set; } = "";

        /// <summary>Web URL of the release on GitHub.</summary>
        public string HtmlUrl { get; set; } = "";
    }
}
