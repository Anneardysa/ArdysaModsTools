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
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;

namespace ArdysaModsTools.Core.Services.Config
{
    public sealed class FeatureCheckResult
    {
        public bool IsAllowed { get; init; }

        public bool IsDevModeBypass { get; init; }

        public string FeatureDisplayName { get; init; } = "";

        public string? BlockedMessage { get; init; }

        public static FeatureCheckResult Allowed(
            string displayName, bool devModeBypass = false) => new()
        {
            IsAllowed = true,
            IsDevModeBypass = devModeBypass,
            FeatureDisplayName = displayName
        };

        public static FeatureCheckResult Blocked(
            string displayName, string message) => new()
        {
            IsAllowed = false,
            FeatureDisplayName = displayName,
            BlockedMessage = message
        };
    }

    public static class FeatureAccessService
    {
        #region Constants

        private const string ConfigPath = "config/feature_access.json";

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        #endregion

        #region Private Fields

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = RequestTimeout
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly object _lock = new();

        private static FeatureAccessConfig? _cachedConfig;
        private static DateTime _cacheTime = DateTime.MinValue;

        #endregion

        #region Public API

        public static async Task<FeatureAccessConfig> GetConfigAsync()
        {
            if (IsCacheValid())
            {
                return _cachedConfig!;
            }

            try
            {
                var url = $"{CdnConfig.R2BaseUrl}/{ConfigPath}";
                using var cts = new CancellationTokenSource(RequestTimeout);
                var json = await CdnFallbackService.Instance.DownloadStringWithFallbackAsync(url, cts.Token).ConfigureAwait(false);

                if (string.IsNullOrEmpty(json))
                {
                    System.Diagnostics.Debug.WriteLine("[FeatureAccess] Failed to download config from all CDNs");
                    return GetCachedOrDefault();
                }

                var config = JsonSerializer.Deserialize<FeatureAccessConfig>(json, _jsonOptions);

                if (config != null)
                {
                    UpdateCache(config);
                    System.Diagnostics.Debug.WriteLine(
                        $"[FeatureAccess] Loaded: SkinSelector={config.SkinSelector.Enabled}, " +
                        $"Miscellaneous={config.Miscellaneous.Enabled}, " +
                        $"InstallModsPack={config.InstallModsPack.Enabled}");
                    return config;
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[FeatureAccess] Request timed out");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeatureAccess] Network error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeatureAccess] Invalid JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeatureAccess] Unexpected error: {ex.Message}");
            }

            return GetCachedOrDefault();
        }

        public static async Task<bool> IsFeatureEnabledAsync(string featureName)
        {
            var config = await GetConfigAsync().ConfigureAwait(false);
            return GetFeatureAccess(config, featureName).Enabled;
        }

        public static async Task<string> GetFeatureMessageAsync(string featureName)
        {
            var config = await GetConfigAsync().ConfigureAwait(false);
            return GetFeatureAccess(config, featureName).GetDisplayMessage();
        }

        public static async Task<FeatureCheckResult> CheckFeatureAsync(string featureName)
        {
            var displayName = featureName switch
            {
                SkinSelectorFeature => "Skin Selector",
                MiscellaneousFeature => "Miscellaneous",
                InstallModsPackFeature => "Install ModsPack",
                _ => featureName
            };

            if (IsDevMode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DEV] Bypassing feature gate for {displayName}");
                return FeatureCheckResult.Allowed(displayName, devModeBypass: true);
            }

            try
            {
                var config = await GetConfigAsync().ConfigureAwait(false);
                var feature = GetFeatureAccess(config, featureName);

                if (!feature.Enabled)
                {
                    return FeatureCheckResult.Blocked(
                        displayName, feature.GetDisplayMessage());
                }

                return FeatureCheckResult.Allowed(displayName);
            }
            catch
            {
                return FeatureCheckResult.Allowed(displayName);
            }
        }

        public static bool IsDevMode => EnvironmentConfig.IsDevMode;

        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedConfig = null;
                _cacheTime = DateTime.MinValue;
            }
        }

        public static FeatureAccessConfig? CurrentConfig
        {
            get
            {
                lock (_lock)
                {
                    return _cachedConfig;
                }
            }
        }

        #endregion

        #region Feature Name Constants

        public const string SkinSelectorFeature = "SkinSelector";

        public const string MiscellaneousFeature = "Miscellaneous";

        public const string InstallModsPackFeature = "InstallModsPack";

        #endregion

        #region Private Helpers

        private static bool IsCacheValid()
        {
            lock (_lock)
            {
                return _cachedConfig != null &&
                       DateTime.UtcNow - _cacheTime < CacheDuration;
            }
        }

        private static void UpdateCache(FeatureAccessConfig config)
        {
            lock (_lock)
            {
                _cachedConfig = config;
                _cacheTime = DateTime.UtcNow;
            }
        }

        private static FeatureAccessConfig GetCachedOrDefault()
        {
            lock (_lock)
            {
                if (_cachedConfig != null)
                {
                    System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using stale cache");
                    return _cachedConfig;
                }
            }

            System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using fail-open defaults");
            return FeatureAccessConfig.CreateDefault();
        }

        private static FeatureAccess GetFeatureAccess(FeatureAccessConfig config, string featureName)
        {
            return featureName switch
            {
                SkinSelectorFeature => config.SkinSelector,
                MiscellaneousFeature => config.Miscellaneous,
                InstallModsPackFeature => config.InstallModsPack,
                _ => new FeatureAccess()
            };
        }

        #endregion
    }
}
