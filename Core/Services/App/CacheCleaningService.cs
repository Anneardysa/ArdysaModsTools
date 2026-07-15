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
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Core.Services.App
{
    public class CacheCleaningService
    {
        private const string AppCachePrefix = "Ardysa";
        private const string FallbackTempRoot = @"C:\Users\Public\ArdysaModsTemp";
        
        private static readonly HashSet<string> ProtectedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "ArdysaModsTools.WebView2"
        };

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

                    AssetCacheService.Instance.ClearCache();

                    long configFreed = RemoteMiscConfigService.DeleteCache();
                    if (configFreed > 0)
                    {
                        result.FilesDeleted++;
                        result.BytesFreed += configFreed;
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

        private List<CacheFolder> GetCacheFolders()
        {
            var folders = new List<CacheFolder>();
            string systemTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (Directory.Exists(FallbackTempRoot))
            {
                folders.Add(new CacheFolder(FallbackTempRoot, "Fallback Temp", deleteRoot: false));
            }

            try
            {
                foreach (var dir in Directory.GetDirectories(systemTemp, $"{AppCachePrefix}*", SearchOption.TopDirectoryOnly))
                {
                    string folderName = Path.GetFileName(dir);
                    
                    bool isProtected = ProtectedFolderNames.Contains(folderName);
                    
                    folders.Add(new CacheFolder(dir, folderName, deleteRoot: true, isProtected: isProtected));
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] Error scanning temp: {ex.Message}");
            }

            try
            {
                string assetCacheDir = AssetCacheService.Instance.CacheDirectory;
                if (Directory.Exists(assetCacheDir))
                {
                    folders.Add(new CacheFolder(assetCacheDir, "Asset Cache", deleteRoot: false));
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] Error accessing AssetCacheService: {ex.Message}");
            }

            try
            {
                if (Directory.Exists(WebView2EnvironmentHelper.UserDataFolder))
                {
                    folders.Add(new CacheFolder(WebView2EnvironmentHelper.UserDataFolder, "WebView2", deleteRoot: false));
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] Error accessing WebView2 folder: {ex.Message}");
            }

            return folders;
        }

        private void CleanDirectory(CacheFolder folder, CleaningResult result, bool deleteRoot = false)
        {
            if (!Directory.Exists(folder.FolderPath))
                return;

            if (folder.IsProtected)
            {
                FallbackLogger.Log($"[CacheCleaningService] Skipping protected folder: {folder.Name}");
                return;
            }

            try
            {
                var files = new List<(string Path, long Size)>();
                try
                {
                    foreach (var file in Directory.GetFiles(folder.FolderPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            files.Add((file, new FileInfo(file).Length));
                        }
                        catch
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error enumerating {folder.Name}: {ex.Message}");
                }

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

                try
                {
                    var dirs = Directory.GetDirectories(folder.FolderPath, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length)
                        .ToList();

                    foreach (var dir in dirs)
                    {
                        TryDeleteEmptyDirectory(dir, result);
                    }
                }
                catch
                {
                }

                if (deleteRoot)
                {
                    TryDeleteEmptyDirectory(folder.FolderPath, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error cleaning {folder.Name}: {ex.Message}");
                FallbackLogger.Log($"[CacheCleaningService] CleanDirectory error for {folder.FolderPath}: {ex.Message}");
            }
        }

        private void TryDeleteEmptyDirectory(string path, CleaningResult result)
        {
            try
            {
                if (Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
                {
                    Directory.Delete(path, false);
                    result.DirectoriesDeleted++;
                }
            }
            catch
            {
            }
        }

        public long GetCacheSizeBytes()
        {
            long totalSize = 0;

            try
            {
                var cacheFolders = GetCacheFolders();

                foreach (var folder in cacheFolders)
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        totalSize += GetDirectorySizeQuick(folder.FolderPath);
                    }
                }

                try
                {
                    if (File.Exists(RemoteMiscConfigService.CacheFilePath))
                        totalSize += new FileInfo(RemoteMiscConfigService.CacheFilePath).Length;
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[CacheCleaningService] GetCacheSizeBytes error: {ex.Message}");
            }

            return totalSize;
        }

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
                    }
                }
            }
            catch
            {
            }
            return size;
        }

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

        private class CacheFolder
        {
            public string FolderPath { get; }
            public string Name { get; }
            public bool DeleteRoot { get; }
            public bool IsProtected { get; }

            public CacheFolder(string path, string name, bool deleteRoot = false, bool isProtected = false)
            {
                FolderPath = path;
                Name = name;
                DeleteRoot = deleteRoot;
                IsProtected = isProtected;
            }
        }
    }
}
