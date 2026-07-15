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
    public static class SafeTempPathHelper
    {
        private const string FallbackTempRoot = @"C:\Users\Public\ArdysaModsTemp";
        
        public static string GetSafeTempPath()
        {
            string systemTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            if (IsAsciiOnly(systemTemp))
            {
                return systemTemp;
            }
            
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
                return systemTemp;
            }
        }
        
        public static string CreateSafeTempDirectory(string prefix)
        {
            string basePath = GetSafeTempPath();
            string dirName = $"{prefix}_{Guid.NewGuid():N}";
            string fullPath = Path.Combine(basePath, dirName);
            
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }
        
        public static void HideDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    new DirectoryInfo(path).Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }
            catch {  }
        }

        private static bool IsAsciiOnly(string text)
        {
            return text.All(c => c <= 127);
        }
    }
}
