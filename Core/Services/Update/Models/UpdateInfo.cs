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
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;

        public string? InstallerDownloadUrl { get; set; }

        public string? PortableDownloadUrl { get; set; }

        public string? ReleaseNotes { get; set; }

        public bool IsUpdateAvailable { get; set; }

        public string CurrentVersion { get; set; } = string.Empty;

        public int BuildNumber { get; set; }

        public int CurrentBuildNumber { get; set; }

        public string? MirrorInstallerUrl { get; set; }

        public string? MirrorPortableUrl { get; set; }

        public string? InstallerSha256 { get; set; }

        public string? PortableSha256 { get; set; }

        public string? FilesManifestUrl { get; set; }
    }
}

