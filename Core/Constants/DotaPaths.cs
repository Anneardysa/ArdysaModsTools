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
namespace ArdysaModsTools.Core.Constants
{
    /// <summary>
    /// Centralized path constants for Dota 2 mod files.
    /// All paths are relative to the Dota 2 installation folder.
    /// Use forward slashes for cross-platform compatibility.
    /// </summary>
    public static class DotaPaths
    {
        #region Mod Files

        /// <summary>Mods installation folder.</summary>
        public const string ModsFolder = "game/_ArdysaMods";

        /// <summary>Main mod VPK file.</summary>
        public const string ModsVpk = "game/_ArdysaMods/pak01_dir.vpk";

        /// <summary>Mod version file.</summary>
        public const string ModsVersion = "game/_ArdysaMods/version.txt";

        /// <summary>Temporary files folder.</summary>
        public const string TempFolder = "game/_ArdysaMods/_temp";

        /// <summary>Version cache file (legacy format).</summary>
        public const string VersionCache = "game/_ArdysaMods/_temp/version_cache.txt";

        /// <summary>Version JSON file (new format).</summary>
        public const string VersionJson = "game/_ArdysaMods/_temp/version.json";

        #endregion

        #region Game Files

        /// <summary>Steam info file containing version data.</summary>
        public const string SteamInf = "game/dota/steam.inf";

        /// <summary>Dota 2 signatures file.</summary>
        public const string Signatures = "game/bin/win64/dota.signatures";

        /// <summary>Game info configuration file.</summary>
        public const string GameInfo = "game/dota/gameinfo_branchspecific.gi";

        /// <summary>Main Dota 2 executable.</summary>
        public const string Dota2Exe = "game/bin/win64/dota2.exe";

        #endregion

        #region Windows-Style Paths (for FileSystemWatcher)

        /// <summary>Steam info file (Windows path style).</summary>
        public const string SteamInfWindows = @"game\dota\steam.inf";

        /// <summary>Signatures file (Windows path style).</summary>
        public const string SignaturesWindows = @"game\bin\win64\dota.signatures";

        #endregion
    }
}
