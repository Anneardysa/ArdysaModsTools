using System.Net.Http;
using System.Text.Json;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services.Misc
{
    /// <summary>
    /// Service for downloading and caching remote misc configuration from GitHub.
    /// </summary>
    public static class RemoteMiscConfigService
    {
        // URL now loaded from environment configuration
        private static string ConfigUrl => EnvironmentConfig.BuildRawUrl("config/misc_config.json");
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools", "misc_config_cache.json");

        private static RemoteMiscConfig? _cachedConfig;
        private static readonly object _lock = new();


        /// <summary>
        /// Gets the current loaded config. Returns null if not yet loaded.
        /// </summary>
        public static RemoteMiscConfig? CurrentConfig => _cachedConfig;

        /// <summary>
        /// Loads the config from remote URL, falling back to cache if network fails.
        /// Always attempts to fetch fresh data from remote.
        /// </summary>
        public static async Task<RemoteMiscConfig> LoadConfigAsync()
        {
            RemoteMiscConfig? config = null;

            // Try to load from remote
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var json = await client.GetStringAsync(ConfigUrl).ConfigureAwait(false);
                config = JsonSerializer.Deserialize<RemoteMiscConfig>(json);

                if (config != null)
                {
                    // Save to cache for offline use
                    await SaveCacheAsync(json).ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("RemoteMiscConfigService: Loaded config from remote");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteMiscConfigService: Failed to load from remote: {ex.Message}");
            }

            // Fallback to cache if remote failed
            if (config == null)
            {
                config = await LoadCacheAsync().ConfigureAwait(false);
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine("RemoteMiscConfigService: Loaded config from cache");
                }
            }

            // Fallback to default if both failed
            if (config == null)
            {
                config = GetDefaultConfig();
                System.Diagnostics.Debug.WriteLine("RemoteMiscConfigService: Using default config");
            }

            lock (_lock)
            {
                _cachedConfig = config;
            }

            return config;
        }

        /// <summary>
        /// Gets synchronous access to cached config. Call LoadConfigAsync first.
        /// </summary>
        public static RemoteMiscConfig GetConfigSync()
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            // Try cache file synchronously
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    var json = File.ReadAllText(CacheFilePath);
                    var config = JsonSerializer.Deserialize<RemoteMiscConfig>(json);
                    if (config != null)
                    {
                        _cachedConfig = config;
                        return config;
                    }
                }
                catch { }
            }

            return GetDefaultConfig();
        }

        /// <summary>
        /// Get the primary URL for a specific option and choice.
        /// </summary>
        public static string? GetUrl(string optionId, string choiceName)
        {
            var config = _cachedConfig ?? GetConfigSync();
            var option = config.Options.FirstOrDefault(o => o.Id == optionId);
            return option?.GetChoiceUrl(choiceName);
        }

        /// <summary>
        /// Get all URLs for a specific option and choice (for multi-file downloads).
        /// </summary>
        public static List<string> GetUrls(string optionId, string choiceName)
        {
            var config = _cachedConfig ?? GetConfigSync();
            var option = config.Options.FirstOrDefault(o => o.Id == optionId);
            return option?.GetChoiceUrls(choiceName) ?? new List<string>();
        }

        /// <summary>
        /// Get all categories from config.
        /// </summary>
        public static List<string> GetCategories()
        {
            var config = _cachedConfig ?? GetConfigSync();
            return config.Categories;
        }

        /// <summary>
        /// Get all options from config.
        /// </summary>
        public static List<RemoteMiscOption> GetOptions()
        {
            var config = _cachedConfig ?? GetConfigSync();
            return config.Options;
        }

        /// <summary>
        /// Get options filtered by category.
        /// </summary>
        public static List<RemoteMiscOption> GetOptionsByCategory(string category)
        {
            return GetOptions().Where(o => o.Category == category).ToList();
        }

        /// <summary>
        /// Get thumbnail URL for a choice.
        /// </summary>
        public static string GetThumbnailUrl(string thumbnailFolder, string choiceName)
        {
            var config = _cachedConfig ?? GetConfigSync();
            var safeChoice = choiceName.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
            return $"{config.ThumbnailBaseUrl}/{thumbnailFolder}/{safeChoice}.png";
        }

        private static async Task SaveCacheAsync(string json)
        {
            try
            {
                var dir = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(CacheFilePath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteMiscConfigService: Failed to save cache: {ex.Message}");
            }
        }

        private static async Task<RemoteMiscConfig?> LoadCacheAsync()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(CacheFilePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<RemoteMiscConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a minimal default config for offline/error scenarios.
        /// </summary>
        private static RemoteMiscConfig GetDefaultConfig()
        {
            return new RemoteMiscConfig
            {
                Version = "0.0",
                ThumbnailBaseUrl = EnvironmentConfig.BuildRawUrl("Assets/misc"),
                Categories = new List<string> { "Environment", "Audio & Visual", "Interface", "Radiant Units", "Dire Units" },
                Options = new List<RemoteMiscOption>()
            };
        }

        /// <summary>
        /// Force reload from remote on next access.
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedConfig = null;
            }
        }
    }
}
