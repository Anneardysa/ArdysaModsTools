namespace ArdysaModsTools.Core.Exceptions
{
    /// <summary>
    /// Centralized error codes for consistent error reporting and debugging.
    /// All errors follow the pattern: CATEGORY_XXX where XXX is a 3-digit number.
    /// </summary>
    public static class ErrorCodes
    {
        #region VPK Errors (VPK_XXX) - VPK file operations

        /// <summary>VPK extraction failed - HLExtract.exe returned an error</summary>
        public const string VPK_EXTRACT_FAILED = "VPK_001";

        /// <summary>VPK recompilation failed - vpk.exe returned an error</summary>
        public const string VPK_RECOMPILE_FAILED = "VPK_002";

        /// <summary>VPK replacement failed - could not copy VPK to game directory</summary>
        public const string VPK_REPLACE_FAILED = "VPK_003";

        /// <summary>VPK file not found at expected path</summary>
        public const string VPK_FILE_NOT_FOUND = "VPK_004";

        /// <summary>VPK file is corrupted or invalid</summary>
        public const string VPK_INVALID_FORMAT = "VPK_005";

        /// <summary>Required tool (HLExtract.exe or vpk.exe) not found</summary>
        public const string VPK_TOOL_NOT_FOUND = "VPK_006";

        /// <summary>File reorganization after extraction failed</summary>
        public const string VPK_REORGANIZE_FAILED = "VPK_007";

        /// <summary>items_game.txt not found after extraction</summary>
        public const string VPK_ITEMS_GAME_MISSING = "VPK_008";

        #endregion

        #region Download Errors (DL_XXX) - Network and file download operations

        /// <summary>Network connection failed</summary>
        public const string DL_NETWORK_ERROR = "DL_001";

        /// <summary>Download timed out</summary>
        public const string DL_TIMEOUT = "DL_002";

        /// <summary>Invalid or malformed URL</summary>
        public const string DL_INVALID_URL = "DL_003";

        /// <summary>File not found on server (404)</summary>
        public const string DL_FILE_NOT_FOUND = "DL_004";

        /// <summary>Server returned an error response</summary>
        public const string DL_SERVER_ERROR = "DL_005";

        /// <summary>Downloaded file hash does not match expected</summary>
        public const string DL_HASH_MISMATCH = "DL_006";

        /// <summary>Failed to extract downloaded archive</summary>
        public const string DL_EXTRACT_FAILED = "DL_007";

        /// <summary>Downloaded file is empty or corrupted</summary>
        public const string DL_INVALID_FILE = "DL_008";

        #endregion

        #region Patch Errors (PATCH_XXX) - File patching operations

        /// <summary>Failed to patch items_game.txt</summary>
        public const string PATCH_ITEMS_GAME_FAILED = "PATCH_001";

        /// <summary>Failed to patch dota.signatures</summary>
        public const string PATCH_SIGNATURE_FAILED = "PATCH_002";

        /// <summary>Block/entry not found in target file</summary>
        public const string PATCH_BLOCK_NOT_FOUND = "PATCH_003";

        /// <summary>Failed to parse source file format</summary>
        public const string PATCH_PARSE_FAILED = "PATCH_004";

        /// <summary>Multiple blocks found when expecting one</summary>
        public const string PATCH_AMBIGUOUS_MATCH = "PATCH_005";

        /// <summary>Block validation failed - IDs don't match</summary>
        public const string PATCH_VALIDATION_FAILED = "PATCH_006";

        /// <summary>Failed to write patched file</summary>
        public const string PATCH_WRITE_FAILED = "PATCH_007";

        /// <summary>Failed to patch gameinfo_branchspecific.gi</summary>
        public const string PATCH_GAMEINFO_FAILED = "PATCH_008";

        #endregion

        #region Configuration Errors (CFG_XXX) - Settings and detection

        /// <summary>Dota 2 installation not found</summary>
        public const string CFG_DOTA_NOT_FOUND = "CFG_001";

        /// <summary>Invalid Dota 2 path selected</summary>
        public const string CFG_INVALID_PATH = "CFG_002";

        /// <summary>Steam not installed or not found</summary>
        public const string CFG_STEAM_NOT_FOUND = "CFG_003";

        /// <summary>Failed to read configuration file</summary>
        public const string CFG_READ_FAILED = "CFG_004";

        /// <summary>Failed to write configuration file</summary>
        public const string CFG_WRITE_FAILED = "CFG_005";

        /// <summary>ModsPack not found or not installed</summary>
        public const string CFG_MODSPACK_NOT_FOUND = "CFG_006";

        #endregion

        #region Generation Errors (GEN_XXX) - Hero set generation

        /// <summary>Hero set generation failed</summary>
        public const string GEN_FAILED = "GEN_001";

        /// <summary>Hero not found in heroes.json</summary>
        public const string GEN_HERO_NOT_FOUND = "GEN_002";

        /// <summary>Set not found for specified hero</summary>
        public const string GEN_SET_NOT_FOUND = "GEN_003";

        /// <summary>Failed to download hero set files</summary>
        public const string GEN_DOWNLOAD_FAILED = "GEN_004";

        /// <summary>Failed to merge hero set files</summary>
        public const string GEN_MERGE_FAILED = "GEN_005";

        /// <summary>No valid heroes.json available</summary>
        public const string GEN_NO_HEROES_DATA = "GEN_006";

        /// <summary>index.txt not found in hero set</summary>
        public const string GEN_INDEX_NOT_FOUND = "GEN_007";

        /// <summary>Batch generation partially failed</summary>
        public const string GEN_PARTIAL_FAILURE = "GEN_008";

        #endregion

        #region Miscellaneous Errors (MISC_XXX) - Misc mod operations

        /// <summary>Miscellaneous mod generation failed</summary>
        public const string MISC_GEN_FAILED = "MISC_001";

        /// <summary>Failed to load remote configuration</summary>
        public const string MISC_CONFIG_FAILED = "MISC_002";

        /// <summary>Invalid mod option selected</summary>
        public const string MISC_INVALID_OPTION = "MISC_003";

        /// <summary>Failed to apply mod files</summary>
        public const string MISC_APPLY_FAILED = "MISC_004";

        #endregion

        #region Security Errors (SEC_XXX) - Security operations

        /// <summary>Debugger detected</summary>
        public const string SEC_DEBUGGER_DETECTED = "SEC_001";

        /// <summary>Integrity check failed</summary>
        public const string SEC_INTEGRITY_FAILED = "SEC_002";

        /// <summary>Tampering detected</summary>
        public const string SEC_TAMPERING = "SEC_003";

        #endregion

        #region Update Errors (UPD_XXX) - Update operations

        /// <summary>Failed to check for updates</summary>
        public const string UPD_CHECK_FAILED = "UPD_001";

        /// <summary>Failed to download update</summary>
        public const string UPD_DOWNLOAD_FAILED = "UPD_002";

        /// <summary>Failed to apply update</summary>
        public const string UPD_APPLY_FAILED = "UPD_003";

        /// <summary>Update file verification failed</summary>
        public const string UPD_VERIFY_FAILED = "UPD_004";

        #endregion
    }
}
