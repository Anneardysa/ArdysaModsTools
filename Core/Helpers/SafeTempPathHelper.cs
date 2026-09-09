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
        
        private static readonly string[] StaleWorkDirPrefixes =
        {
            "ArdysaHero_", "ArdysaMisc_", "ArdysaMods_", "ArdysaModsPack_", "ArdysaMerge_",
            "ArdysaItemsGame_", "ArdysaSkinManifest_", "ArdysaSync_", "ArdysaSyncReport_",
            "amt_manual_split_", "amt_probe_",
        };

        private static readonly string[] SelectHeroDecryptedSubdirs = { "HeroSets", "dec" };

        public static int SweepStaleWorkDirs() => SweepStaleWorkDirs(GetSafeTempPath());

        public static int SweepStaleWorkDirs(string root)
        {
            int removed = 0;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return 0;

            foreach (var dir in SafeEnumerateDirectories(root))
            {
                string name = Path.GetFileName(dir);
                bool stale = false;
                foreach (var prefix in StaleWorkDirPrefixes)
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { stale = true; break; }
                }
                if (stale && TryDeleteTree(dir)) removed++;
            }

            string selectHero = Path.Combine(root, "ArdysaSelectHero");
            if (Directory.Exists(selectHero))
            {
                foreach (var sub in SelectHeroDecryptedSubdirs)
                {
                    string path = Path.Combine(selectHero, sub);
                    if (Directory.Exists(path) && TryDeleteTree(path)) removed++;
                }
            }

            return removed;
        }

        private static string[] SafeEnumerateDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return Array.Empty<string>(); }
        }

        private static bool TryDeleteTree(string path)
        {
            try
            {
                NormalizeAttributes(path);
                Directory.Delete(path, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void NormalizeAttributes(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                dir.Attributes = FileAttributes.Normal;
                foreach (var entry in dir.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    try { entry.Attributes = FileAttributes.Normal; } catch { }
                }
            }
            catch {  }
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

        public static bool IsSafeExtractionPath(string targetDirectory, string relativePathOrFileName, out string safeDestinationPath)
        {
            safeDestinationPath = string.Empty;
            if (string.IsNullOrWhiteSpace(targetDirectory) || string.IsNullOrWhiteSpace(relativePathOrFileName))
                return false;

            try
            {
                string fullTargetDir = Path.GetFullPath(targetDirectory);
                if (!fullTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    fullTargetDir += Path.DirectorySeparatorChar;
                }

                string combinedPath = Path.Combine(targetDirectory, relativePathOrFileName);
                string fullDestPath = Path.GetFullPath(combinedPath);

                if (fullDestPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    safeDestinationPath = fullDestPath;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsAsciiOnly(string text)
        {
            return text.All(c => c <= 127);
        }
    }
}
