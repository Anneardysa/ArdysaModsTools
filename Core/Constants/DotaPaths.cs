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
    public static class DotaPaths
    {
        #region Mod Files

        public const string ModsFolder = "game/_ArdysaMods";

        public const string ModsVpk = "game/_ArdysaMods/pak01_dir.vpk";
        public const string ModsVersion = "game/_ArdysaMods/version.txt";

        public const string TempFolder = "game/_ArdysaMods/_temp";

        public const string VersionCache = "game/_ArdysaMods/_temp/version_cache.txt";

        public const string VersionJson = "game/_ArdysaMods/_temp/version.json";

        #endregion

        #region Game Files

        public const string SteamInf = "game/dota/steam.inf";

        public const string Signatures = "game/bin/win64/dota.signatures";

        public const string GameInfo = "game/dota/gameinfo_branchspecific.gi";

        public const string Dota2Exe = "game/bin/win64/dota2.exe";

        #endregion

        #region Windows-Style Paths (for FileSystemWatcher)

        public const string SteamInfWindows = @"game\dota\steam.inf";

        public const string SignaturesWindows = @"game\bin\win64\dota.signatures";

        #endregion
    }
}
