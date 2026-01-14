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
namespace ArdysaModsTools.Core.Services.Update.Models
{
    /// <summary>
    /// Contains information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// The version string of the latest release (e.g., "2.1.0").
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Download URL for the installer version (*_Setup_*.exe).
        /// </summary>
        public string? InstallerDownloadUrl { get; set; }

        /// <summary>
        /// Download URL for the portable version (*_Portable_*.zip).
        /// </summary>
        public string? PortableDownloadUrl { get; set; }

        /// <summary>
        /// Release notes / changelog from GitHub.
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// Whether an update is available compared to current version.
        /// </summary>
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// The current application version for reference.
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;
    }
}

