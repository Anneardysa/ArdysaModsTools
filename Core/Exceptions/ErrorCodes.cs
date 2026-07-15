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
namespace ArdysaModsTools.Core.Exceptions
{
    public static class ErrorCodes
    {
        #region VPK Errors (VPK_XXX) - VPK file operations

        public const string VPK_EXTRACT_FAILED = "VPK_001";

        public const string VPK_RECOMPILE_FAILED = "VPK_002";

        public const string VPK_REPLACE_FAILED = "VPK_003";

        public const string VPK_FILE_NOT_FOUND = "VPK_004";

        public const string VPK_INVALID_FORMAT = "VPK_005";

        public const string VPK_TOOL_NOT_FOUND = "VPK_006";

        public const string VPK_REORGANIZE_FAILED = "VPK_007";

        public const string VPK_ITEMS_GAME_MISSING = "VPK_008";

        #endregion

        #region Download Errors (DL_XXX) - Network and file download operations

        public const string DL_NETWORK_ERROR = "DL_001";

        public const string DL_TIMEOUT = "DL_002";

        public const string DL_INVALID_URL = "DL_003";

        public const string DL_FILE_NOT_FOUND = "DL_004";

        public const string DL_SERVER_ERROR = "DL_005";

        public const string DL_HASH_MISMATCH = "DL_006";

        public const string DL_EXTRACT_FAILED = "DL_007";

        public const string DL_INVALID_FILE = "DL_008";

        #endregion

        #region Patch Errors (PATCH_XXX) - File patching operations

        public const string PATCH_ITEMS_GAME_FAILED = "PATCH_001";

        public const string PATCH_SIGNATURE_FAILED = "PATCH_002";

        public const string PATCH_BLOCK_NOT_FOUND = "PATCH_003";

        public const string PATCH_PARSE_FAILED = "PATCH_004";

        public const string PATCH_AMBIGUOUS_MATCH = "PATCH_005";

        public const string PATCH_VALIDATION_FAILED = "PATCH_006";

        public const string PATCH_WRITE_FAILED = "PATCH_007";

        public const string PATCH_GAMEINFO_FAILED = "PATCH_008";

        #endregion

        #region Configuration Errors (CFG_XXX) - Settings and detection

        public const string CFG_DOTA_NOT_FOUND = "CFG_001";

        public const string CFG_INVALID_PATH = "CFG_002";

        public const string CFG_STEAM_NOT_FOUND = "CFG_003";

        public const string CFG_READ_FAILED = "CFG_004";

        public const string CFG_WRITE_FAILED = "CFG_005";

        public const string CFG_MODSPACK_NOT_FOUND = "CFG_006";

        #endregion

        #region Generation Errors (GEN_XXX) - Hero set generation

        public const string GEN_FAILED = "GEN_001";

        public const string GEN_HERO_NOT_FOUND = "GEN_002";

        public const string GEN_SET_NOT_FOUND = "GEN_003";

        public const string GEN_DOWNLOAD_FAILED = "GEN_004";

        public const string GEN_MERGE_FAILED = "GEN_005";

        public const string GEN_NO_HEROES_DATA = "GEN_006";

        public const string GEN_INDEX_NOT_FOUND = "GEN_007";

        public const string GEN_PARTIAL_FAILURE = "GEN_008";

        #endregion

        #region Miscellaneous Errors (MISC_XXX) - Misc mod operations

        public const string MISC_GEN_FAILED = "MISC_001";

        public const string MISC_CONFIG_FAILED = "MISC_002";

        public const string MISC_INVALID_OPTION = "MISC_003";

        public const string MISC_APPLY_FAILED = "MISC_004";

        #endregion

        #region Security Errors (SEC_XXX) - Security operations

        public const string SEC_DEBUGGER_DETECTED = "SEC_001";

        public const string SEC_INTEGRITY_FAILED = "SEC_002";

        public const string SEC_TAMPERING = "SEC_003";

        #endregion

        #region Update Errors (UPD_XXX) - Update operations

        public const string UPD_CHECK_FAILED = "UPD_001";

        public const string UPD_DOWNLOAD_FAILED = "UPD_002";

        public const string UPD_APPLY_FAILED = "UPD_003";

        public const string UPD_VERIFY_FAILED = "UPD_004";

        #endregion
    }
}

