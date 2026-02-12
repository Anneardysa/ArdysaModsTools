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

namespace ArdysaModsTools.Core.Services.Config
{
    /// <summary>
    /// Service to fetch and cache feature access configuration from R2 CDN.
    /// Controls which features (Skin Selector, Miscellaneous) are accessible.
    /// 
    /// Design decisions:
    /// - Fail-open: defaults to all-enabled if R2 is unreachable
    /// - 5-minute cache to balance freshness vs. request volume
    /// - Static service (same pattern as SubsGoalService)
    /// - Thread-safe via lock on cache operations
    /// </summary>
    public static class FeatureAccessService
    {
        #region Constants

        /// <summary>
        /// Path to the feature access config on R2 CDN.
        /// Full URL: {CdnConfig.R2BaseUrl}/config/feature_access.json
        /// </summary>
        private const string ConfigPath = "config/feature_access.json";

        /// <summary>
        /// HTTP request timeout. Short timeout ensures the app doesn't hang
        /// if R2 is unreachable — features stay enabled via fail-open default.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How long to keep cached config before re-fetching.
        /// 5 minutes balances real-time control vs. CDN request volume.
        /// </summary>
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

        /// <summary>
        /// Gets the current feature access configuration.
        /// Fetches from R2 CDN if cache is stale, falls back to defaults on error.
        /// </summary>
        /// <returns>Feature access config (never null — fail-open design).</returns>
        public static async Task<FeatureAccessConfig> GetConfigAsync()
        {
            // Return cached if still fresh
            if (IsCacheValid())
            {
                return _cachedConfig!;
            }

            try
            {
                var url = $"{CdnConfig.R2BaseUrl}/{ConfigPath}";
                using var cts = new CancellationTokenSource(RequestTimeout);
                var response = await _httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FeatureAccess] R2 returned {response.StatusCode}");
                    return GetCachedOrDefault();
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<FeatureAccessConfig>(json, _jsonOptions);

                if (config != null)
                {
                    UpdateCache(config);
                    System.Diagnostics.Debug.WriteLine(
                        $"[FeatureAccess] Loaded: SkinSelector={config.SkinSelector.Enabled}, " +
                        $"Miscellaneous={config.Miscellaneous.Enabled}");
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

        /// <summary>
        /// Checks if a specific feature is enabled.
        /// Convenience method that fetches config and checks the feature flag.
        /// </summary>
        /// <param name="featureName">Feature name: "SkinSelector" or "Miscellaneous"</param>
        /// <returns>True if the feature is enabled (default: true).</returns>
        public static async Task<bool> IsFeatureEnabledAsync(string featureName)
        {
            var config = await GetConfigAsync().ConfigureAwait(false);
            return GetFeatureAccess(config, featureName).Enabled;
        }

        /// <summary>
        /// Gets the disabled message for a specific feature.
        /// Returns a sensible default if no custom message is configured.
        /// </summary>
        /// <param name="featureName">Feature name: "SkinSelector" or "Miscellaneous"</param>
        /// <returns>The display message for the disabled feature.</returns>
        public static async Task<string> GetFeatureMessageAsync(string featureName)
        {
            var config = await GetConfigAsync().ConfigureAwait(false);
            return GetFeatureAccess(config, featureName).GetDisplayMessage();
        }

        /// <summary>
        /// Force cache invalidation to re-fetch on next access.
        /// Useful when the app needs to check for policy changes immediately.
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedConfig = null;
                _cacheTime = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Gets the current cached config without fetching.
        /// Returns null if no config has been fetched yet.
        /// </summary>
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

        /// <summary>Feature name constant for Skin Selector.</summary>
        public const string SkinSelectorFeature = "SkinSelector";

        /// <summary>Feature name constant for Miscellaneous.</summary>
        public const string MiscellaneousFeature = "Miscellaneous";

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
                // Return stale cache if available (better than nothing)
                if (_cachedConfig != null)
                {
                    System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using stale cache");
                    return _cachedConfig;
                }
            }

            // No cache at all — fail open with all features enabled
            System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using fail-open defaults");
            return FeatureAccessConfig.CreateDefault();
        }

        /// <summary>
        /// Maps a feature name string to the corresponding FeatureAccess object.
        /// </summary>
        private static FeatureAccess GetFeatureAccess(FeatureAccessConfig config, string featureName)
        {
            return featureName switch
            {
                SkinSelectorFeature => config.SkinSelector,
                MiscellaneousFeature => config.Miscellaneous,
                _ => new FeatureAccess() // Unknown feature = enabled (fail-open)
            };
        }

        #endregion
    }
}
