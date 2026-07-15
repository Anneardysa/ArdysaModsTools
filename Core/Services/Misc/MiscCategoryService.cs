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
    public static class MiscCategoryService
    {
        public static List<string> GetCategories()
        {
            return RemoteMiscConfigService.GetCategories();
        }

        public static List<MiscOption> GetAllOptions()
        {
            var config = RemoteMiscConfigService.GetConfigSync();
            var options = new List<MiscOption>();

            var thumbnailBasePath = GetThumbnailBasePath(config.ThumbnailBaseUrl);
            var cdnThumbnailBase = $"{EnvironmentConfig.ContentBase}/{thumbnailBasePath}";

            foreach (var remoteOption in config.Options)
            {
                string? thumbPattern = null;
                var ext = string.IsNullOrEmpty(remoteOption.ThumbnailExtension) 
                    ? "webp" 
                    : remoteOption.ThumbnailExtension;
                    
                if (!string.IsNullOrEmpty(remoteOption.ThumbnailFolder))
                {
                    thumbPattern = $"{cdnThumbnailBase}/{remoteOption.ThumbnailFolder}/{{choice}}.{ext}";
                }

                var choiceNames = new List<string>();
                var choiceStyles = new Dictionary<string, List<string>>();
                var choiceThumbnailIds = new Dictionary<string, string>();

                foreach (var choice in remoteOption.Choices)
                {
                    choiceNames.Add(choice.Name);

                    if (!string.IsNullOrEmpty(choice.ThumbnailId))
                        choiceThumbnailIds[choice.Name] = choice.ThumbnailId;

                    if (choice.Styles != null && choice.Styles.Count > 0)
                    {
                        var styleNames = choice.Styles.Select(s => s.Name).ToList();
                        choiceStyles[choice.Name] = styleNames;

                        foreach (var style in choice.Styles)
                        {
                            if (!string.IsNullOrEmpty(style.ThumbnailId))
                                choiceThumbnailIds[style.Name] = style.ThumbnailId;
                        }
                    }
                }

                options.Add(new MiscOption
                {
                    Id = remoteOption.Id,
                    IsSpecialVpk = remoteOption.IsSpecialVpk,
                    ExcludesWith = remoteOption.ExcludesWith,
                    DisplayName = remoteOption.DisplayName,
                    Category = remoteOption.Category,
                    Choices = choiceNames,
                    ThumbnailUrlPattern = thumbPattern,
                    ChoiceThumbnails = new Dictionary<string, string>(),
                    ChoiceThumbnailIds = choiceThumbnailIds,
                    ChoiceStyles = choiceStyles
                });
            }

            return options;
        }


        public static async Task PreloadConfigAsync()
        {
            await RemoteMiscConfigService.LoadConfigAsync().ConfigureAwait(false);
        }

        private static string GetThumbnailBasePath(string? thumbnailBaseUrl)
        {
            const string defaultPath = "Assets/misc";
            
            if (string.IsNullOrEmpty(thumbnailBaseUrl))
                return defaultPath;

            try
            {
                var uri = new Uri(thumbnailBaseUrl);
                var path = uri.AbsolutePath.TrimStart('/');
                
                if (uri.Host.Contains("githubusercontent.com"))
                {
                    var parts = path.Split('/');
                    if (parts.Length > 3)
                    {
                        return string.Join("/", parts.Skip(3));
                    }
                }
                
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
                
                return path.Length > 0 ? path : defaultPath;
            }
            catch
            {
                return defaultPath;
            }
        }
    }
}

