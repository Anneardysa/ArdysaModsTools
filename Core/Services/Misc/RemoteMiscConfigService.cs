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
using System.Net.Http;
using System.Text.Json;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Misc
{
    public static class RemoteMiscConfigService
    {
        private static string ConfigUrl => EnvironmentConfig.BuildRawUrl("config/misc_config.json");

        public static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools", "misc_config_cache.json");

        private static RemoteMiscConfig? _cachedConfig;
        private static readonly object _lock = new();


        public static RemoteMiscConfig? CurrentConfig => _cachedConfig;

        public static async Task<RemoteMiscConfig> LoadConfigAsync()
        {
            RemoteMiscConfig? config = null;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var json = await CdnFallbackService.Instance.DownloadStringWithFallbackAsync(ConfigUrl, cts.Token).ConfigureAwait(false);
                
                if (!string.IsNullOrEmpty(json))
                {
                    config = JsonSerializer.Deserialize<RemoteMiscConfig>(json);

                    if (config != null)
                    {
                        await SaveCacheAsync(json).ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine("RemoteMiscConfigService: Loaded config from remote");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteMiscConfigService: Failed to load from remote: {ex.Message}");
            }

            if (config == null)
            {
                config = await LoadCacheAsync().ConfigureAwait(false);
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine("RemoteMiscConfigService: Loaded config from cache");
                }
            }

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

        public static RemoteMiscConfig GetConfigSync()
        {
            if (_cachedConfig != null)
                return _cachedConfig;

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

        public static string? GetUrl(string optionId, string choiceName)
        {
            var config = _cachedConfig ?? GetConfigSync();
            var option = config.Options.FirstOrDefault(o => o.Id == optionId);
            return option?.GetChoiceUrl(choiceName);
        }

        public static List<string> GetCategories()
        {
            var config = _cachedConfig ?? GetConfigSync();
            return config.Categories;
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

        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedConfig = null;
            }
        }

        public static long DeleteCache()
        {
            long freed = 0;
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    freed = new FileInfo(CacheFilePath).Length;
                    File.Delete(CacheFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteMiscConfigService: Failed to delete cache: {ex.Message}");
                freed = 0;
            }

            InvalidateCache();
            return freed;
        }
    }
}

