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

            foreach (var remoteOption in config.Options)
            {
                options.Add(new MiscOption
                {
                    Id = remoteOption.Id,
                    DisplayName = remoteOption.DisplayName,
                    Category = remoteOption.Category,
                    Choices = remoteOption.GetChoiceNames(),
                    ThumbnailUrlPattern = $"{config.ThumbnailBaseUrl}/{remoteOption.ThumbnailFolder}/{{choice}}.png"
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
    }
}

