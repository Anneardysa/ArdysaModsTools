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
    /// A single hero-skin entry in the in-app "ModsPack" updates grid — the native equivalent of a
    /// card on the website's <c>updates.html</c> page. Sourced from the site's <c>updates.json</c>.
    /// </summary>
    public sealed class ModsPackUpdate
    {
        /// <summary>Hero display name (e.g. <c>Anti-Mage</c>).</summary>
        public string Hero { get; set; } = "";

        /// <summary>Absolute thumbnail URL (relative paths are resolved against the site base).</summary>
        public string Image { get; set; } = "";

        /// <summary>Publish date (ISO <c>yyyy-MM-dd</c>) as authored in the manifest, if present.</summary>
        public string? Date { get; set; }

        /// <summary>Primary attribute: <c>Strength</c>, <c>Agility</c>, <c>Intelligence</c> or <c>Universal</c>.</summary>
        public string? Attribute { get; set; }
    }
}
