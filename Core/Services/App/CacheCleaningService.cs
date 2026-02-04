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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Core.Services.App
{
    /// <summary>
    /// Service for cleaning application cache and temporary files.
    /// Only cleans ArdysaModsTools-specific folders, never touches unrelated files.
    /// </summary>
    public class CacheCleaningService
    {
        // Constants for identifying app-specific cache folders
        private const string AppCachePrefix = "Ardysa";
        private const string FallbackTempRoot = @"C:\Users\Public\ArdysaModsTemp";
        
        // Folders to skip during cleanup (still in use by the app)
        private static readonly HashSet<string> ProtectedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "ArdysaModsTools.WebView2"  // WebView2 user data - locked while app is running
        };

        /// <summary>
        /// Result of a cache cleaning operation.
        /// </summary>
        public class CleaningResult
        {
            public bool Success { get; set; }
            public int FilesDeleted { get; set; }
            public int DirectoriesDeleted { get; set; }
            public long BytesFreed { get; set; }
            public int FilesSkipped { get; set; }
            public string? ErrorMessage { get; set; }
            public List<string> Errors { get; } = new();
        }

        /// <summary>
        /// Clears all application-related cache and temp files.
        /// This includes:
        /// - C:\Users\Public\ArdysaModsTemp (fallback temp for non-ASCII usernames)
        /// - All Ardysa* folders in %TEMP% (except protected folders like WebView2)
        /// </summary>
        public async Task<CleaningResult> ClearAllCacheAsync()
        {
            var result = new CleaningResult { Success = true };

            try
            {
                var cacheFolders = GetCacheFolders();
                
                await Task.Run(() =>
                {
                    foreach (var folder in cacheFolders)
                    {
                        CleanDirectory(folder, result, deleteRoot: folder.DeleteRoot);
                    }
                });

                FallbackLogger.Log($"[CacheCleaningService] Cleaned {result.FilesDeleted} files, " +
                    $"{result.DirectoriesDeleted} directories, freed {FormatBytes(result.BytesFreed)}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Errors.Add(ex.Message);
                FallbackLogger.Log($"[CacheCleaningService] ClearAllCacheAsync failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets the list of cache folders to clean/calculate.
        /// </summary>
        private List<CacheFolder> GetCacheFolders()
        {
            var folders = new List<CacheFolder>();
            string systemTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 1. Add fallback temp root if it exists (for non-ASCII username support)
            if (Directory.Exists(FallbackTempRoot))
            {
                folders.Add(new CacheFolder(FallbackTempRoot, "Fallback Temp", deleteRoot: false));
            }

            // 2. Add all Ardysa* folders in system temp
            try
            {
                foreach (var dir in Directory.GetDirectories(systemTemp, $"{AppCachePrefix}*", SearchOption.TopDirectoryOnly))
                {
                    string folderName = Path.GetFileName(dir);
                    
                    // Skip protected folders (WebView2, etc.)
                    bool isProtected = ProtectedFolderNames.Contains(folderName);
                    
                    folders.Add(new CacheFolder(dir, folderName, deleteRoot: true, isProtected: isProtected));
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] Error scanning temp: {ex.Message}");
            }

            return folders;
        }

        /// <summary>
        /// Cleans a specific directory, deleting all files and subdirectories.
        /// </summary>
        private void CleanDirectory(CacheFolder folder, CleaningResult result, bool deleteRoot = false)
        {
            if (!Directory.Exists(folder.Path))
                return;

            // Skip protected folders entirely
            if (folder.IsProtected)
            {
                FallbackLogger.Log($"[CacheCleaningService] Skipping protected folder: {folder.Name}");
                return;
            }

            try
            {
                // Get all files with their sizes first (before deletion)
                var files = new List<(string Path, long Size)>();
                try
                {
                    foreach (var file in Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            files.Add((file, new FileInfo(file).Length));
                        }
                        catch
                        {
                            // Skip files we can't access
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error enumerating {folder.Name}: {ex.Message}");
                }

                // Delete files
                foreach (var (path, size) in files)
                {
                    try
                    {
                        File.Delete(path);
                        result.FilesDeleted++;
                        result.BytesFreed += size;
                    }
                    catch
                    {
                        result.FilesSkipped++;
                    }
                }

                // Delete subdirectories (bottom-up by path length)
                try
                {
                    var dirs = Directory.GetDirectories(folder.Path, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length)
                        .ToList();

                    foreach (var dir in dirs)
                    {
                        TryDeleteEmptyDirectory(dir, result);
                    }
                }
                catch
                {
                    // Ignore enumeration errors
                }

                // Delete root if requested and empty
                if (deleteRoot)
                {
                    TryDeleteEmptyDirectory(folder.Path, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error cleaning {folder.Name}: {ex.Message}");
                FallbackLogger.Log($"[CacheCleaningService] CleanDirectory error for {folder.Path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to delete an empty directory.
        /// </summary>
        private void TryDeleteEmptyDirectory(string path, CleaningResult result)
        {
            try
            {
                // Only delete if truly empty
                if (Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
                {
                    Directory.Delete(path, false);
                    result.DirectoriesDeleted++;
                }
            }
            catch
            {
                // Directory might be in use or have permission issues
            }
        }

        /// <summary>
        /// Gets the total size of cache files in bytes.
        /// Only counts ArdysaModsTools-specific cache folders that CAN be deleted.
        /// Protected folders (like WebView2) are excluded from this count.
        /// </summary>
        public long GetCacheSizeBytes()
        {
            long totalSize = 0;

            try
            {
                var cacheFolders = GetCacheFolders();

                foreach (var folder in cacheFolders)
                {
                    // Skip protected folders - they won't be deleted so don't count them
                    if (folder.IsProtected)
                        continue;
                        
                    if (Directory.Exists(folder.Path))
                    {
                        totalSize += GetDirectorySizeQuick(folder.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] GetCacheSizeBytes error: {ex.Message}");
            }

            return totalSize;
        }

        /// <summary>
        /// Gets the total size of a directory in bytes using optimized enumeration.
        /// </summary>
        private long GetDirectorySizeQuick(string path)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += file.Length;
                    }
                    catch
                    {
                        // Skip inaccessible files
                    }
                }
            }
            catch
            {
                // Skip inaccessible directories
            }
            return size;
        }

        /// <summary>
        /// Formats bytes into human-readable string (KB, MB, GB).
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F1} {suffixes[suffixIndex]}";
        }

        /// <summary>
        /// Represents a cache folder with its metadata.
        /// </summary>
        private class CacheFolder
        {
            public string Path { get; }
            public string Name { get; }
            public bool DeleteRoot { get; }
            public bool IsProtected { get; }

            public CacheFolder(string path, string name, bool deleteRoot = false, bool isProtected = false)
            {
                Path = path;
                Name = name;
                DeleteRoot = deleteRoot;
                IsProtected = isProtected;
            }
        }
    }
}
