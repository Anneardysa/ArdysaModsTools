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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Core.Services

{
    /// <summary>
    /// Provides structured misc option data grouped by category.
    /// Now uses remote configuration from GitHub.
    /// </summary>
    public static class MiscCategoryService
    {
        /// <summary>
        /// Get ordered list of category names for section headers.
        /// </summary>
        public static List<string> GetCategories()
        {
            return RemoteMiscConfigService.GetCategories();
        }

        /// <summary>
        /// Get all misc options with their choices, ordered by category.
        /// Converts RemoteMiscOption to MiscOption for UI compatibility.
        /// </summary>
        public static List<MiscOption> GetAllOptions()
        {
            var config = RemoteMiscConfigService.GetConfigSync();
            var options = new List<MiscOption>();

            // Use R2 CDN base URL for thumbnails (faster and more reliable)
            // Extract relative path from config's thumbnailBaseUrl or use default
            var thumbnailBasePath = GetThumbnailBasePath(config.ThumbnailBaseUrl);
            var cdnThumbnailBase = $"{EnvironmentConfig.ContentBase}/{thumbnailBasePath}";

            foreach (var remoteOption in config.Options)
            {
                // Only set thumbnail pattern if the option has a valid thumbnail folder
                // Use extension from config (default: webp)
                string? thumbPattern = null;
                var ext = string.IsNullOrEmpty(remoteOption.ThumbnailExtension) 
                    ? "webp" 
                    : remoteOption.ThumbnailExtension;
                    
                if (!string.IsNullOrEmpty(remoteOption.ThumbnailFolder))
                {
                    thumbPattern = $"{cdnThumbnailBase}/{remoteOption.ThumbnailFolder}/{{choice}}.{ext}";
                }

                options.Add(new MiscOption
                {
                    Id = remoteOption.Id,
                    DisplayName = remoteOption.DisplayName,
                    Category = remoteOption.Category,
                    Choices = remoteOption.GetChoiceNames(),
                    ThumbnailUrlPattern = thumbPattern,
                    ChoiceThumbnails = new Dictionary<string, string>() // Not used - pattern handles all cases
                });
            }

            return options;
        }


        /// <summary>
        /// Get options filtered by category.
        /// </summary>
        public static List<MiscOption> GetOptionsByCategory(string category)
        {
            return GetAllOptions().Where(o => o.Category == category).ToList();
        }

        /// <summary>
        /// Pre-load config from remote (call on app startup).
        /// </summary>
        public static async Task PreloadConfigAsync()
        {
            await RemoteMiscConfigService.LoadConfigAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Extract the relative path from a thumbnail base URL.
        /// Handles both GitHub raw URLs and R2 CDN URLs.
        /// Example: "https://raw.githubusercontent.com/Owner/Repo/main/Assets/misc" -> "Assets/misc"
        /// </summary>
        private static string GetThumbnailBasePath(string? thumbnailBaseUrl)
        {
            const string defaultPath = "Assets/misc";
            
            if (string.IsNullOrEmpty(thumbnailBaseUrl))
                return defaultPath;

            try
            {
                var uri = new Uri(thumbnailBaseUrl);
                var path = uri.AbsolutePath.TrimStart('/');
                
                // For GitHub raw URLs: /Owner/Repo/Branch/path -> extract path after branch
                // Format: raw.githubusercontent.com/Owner/Repo/Branch/...
                if (uri.Host.Contains("githubusercontent.com"))
                {
                    var parts = path.Split('/');
                    // Skip: owner, repo, branch (first 3 parts)
                    if (parts.Length > 3)
                    {
                        return string.Join("/", parts.Skip(3));
                    }
                }
                
                // For jsDelivr: /gh/Owner/Repo@Branch/path
                if (uri.Host.Contains("jsdelivr.net"))
                {
                    var atIndex = path.IndexOf('@');
                    if (atIndex >= 0)
                    {
                        var afterAt = path.Substring(atIndex + 1);
                        var slashIndex = afterAt.IndexOf('/');
                        if (slashIndex >= 0)
                        {
                            return afterAt.Substring(slashIndex + 1);
                        }
                    }
                }
                
                // For R2 CDN or other URLs, just use the path directly
                return path.Length > 0 ? path : defaultPath;
            }
            catch
            {
                return defaultPath;
            }
        }
    }
}

