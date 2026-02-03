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
using System.IO;
using System.Linq;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Provides safe temp paths for external tools that may not support Unicode/non-ASCII paths.
    /// This is critical for users with non-ASCII usernames (Chinese, Japanese, Korean, etc).
    /// </summary>
    public static class SafeTempPathHelper
    {
        // Fallback path at system root - guaranteed ASCII-safe
        private const string FallbackTempRoot = @"C:\Users\Public\ArdysaModsTemp";
        
        /// <summary>
        /// Gets a temp path that is safe for external tools like vpk.exe.
        /// Falls back to C:\ArdysaTemp if the user's temp path contains non-ASCII characters.
        /// </summary>
        public static string GetSafeTempPath()
        {
            string systemTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            // Check if the temp path contains any non-ASCII characters
            if (IsAsciiOnly(systemTemp))
            {
                return systemTemp;
            }
            
            // Use fallback path for non-ASCII usernames (Chinese, Japanese, Korean, etc.)
            try
            {
                if (!Directory.Exists(FallbackTempRoot))
                {
                    Directory.CreateDirectory(FallbackTempRoot);
                }
                return FallbackTempRoot;
            }
            catch
            {
                // If we can't create the fallback, return system temp as last resort
                return systemTemp;
            }
        }
        
        /// <summary>
        /// Creates a unique temp directory with a safe ASCII-only path.
        /// </summary>
        /// <param name="prefix">Prefix for the temp folder name</param>
        /// <returns>Full path to the created directory</returns>
        public static string CreateSafeTempDirectory(string prefix)
        {
            string basePath = GetSafeTempPath();
            string dirName = $"{prefix}_{Guid.NewGuid():N}";
            string fullPath = Path.Combine(basePath, dirName);
            
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }
        
        /// <summary>
        /// Checks if a string contains only ASCII characters (basic Latin alphabet, digits, common symbols).
        /// </summary>
        private static bool IsAsciiOnly(string text)
        {
            return text.All(c => c <= 127);
        }
    }
}
