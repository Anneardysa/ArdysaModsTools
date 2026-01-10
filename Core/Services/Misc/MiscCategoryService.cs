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
