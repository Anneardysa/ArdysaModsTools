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
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Config
{
    public sealed class FeatureCheckResult
    {
        public bool IsAllowed { get; init; }

        public bool IsDevModeBypass { get; init; }

        public string FeatureDisplayName { get; init; } = "";

        public string? BlockedMessage { get; init; }

        public bool IsOutdated { get; init; }

        public string RequiredVersion { get; init; } = "";

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

        public static FeatureCheckResult Outdated(
            string displayName, string message, string requiredVersion) => new()
        {
            IsAllowed = false,
            IsOutdated = true,
            FeatureDisplayName = displayName,
            BlockedMessage = message,
            RequiredVersion = requiredVersion
        };
    }

    public static class FeatureAccessService
    {
        #region Constants

        private const string ConfigPath = "config/feature_access.json";

        private const string OutdatedMessageKey = "update.required.default";

        private const string OfflineMessageKey = "feature.blocked.offline";

        public static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools", "feature_access_cache.json");

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(60);

        #endregion

        #region Private Fields

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly object _lock = new();

        private static FeatureAccessConfig? _cachedConfig;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static DateTime _lastFailedFetch = DateTime.MinValue;

        #endregion

        #region Public API

        public static async Task<FeatureAccessConfig> GetConfigAsync()
            => await TryGetConfigAsync().ConfigureAwait(false) ?? FeatureAccessConfig.CreateDefault();

        private static async Task<FeatureAccessConfig?> TryGetConfigAsync()
        {
            if (IsCacheValid())
            {
                return _cachedConfig!;
            }

            if (IsInFailureBackoff())
            {
                return GetCachedOrDefault();
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
                    await SaveCacheAsync(json).ConfigureAwait(false);
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
            if (IsDevMode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DEV] Bypassing feature gate for {DisplayNameOf(featureName)}");
                return FeatureCheckResult.Allowed(DisplayNameOf(featureName), devModeBypass: true);
            }

            try
            {
                var config = await TryGetConfigAsync().ConfigureAwait(false);
                return Evaluate(config, featureName, AppVersion.Current);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FeatureAccess] Check failed for {DisplayNameOf(featureName)}: {ex.Message}");
                return FeatureCheckResult.Blocked(DisplayNameOf(featureName), OfflineMessage());
            }
        }

        internal static FeatureCheckResult Evaluate(
            FeatureAccessConfig? config, string featureName, AppVersion current)
        {
            var displayName = DisplayNameOf(featureName);

            if (config == null)
                return FeatureCheckResult.Blocked(displayName, OfflineMessage());

            var feature = GetFeatureAccess(config, featureName);

            if (!feature.Enabled)
                return FeatureCheckResult.Blocked(displayName, feature.GetDisplayMessage());

            if (!MeetsMinimumVersion(feature, current, out string required))
            {
                string fallback = Loc.T(OutdatedMessageKey);
                if (string.Equals(fallback, OutdatedMessageKey, StringComparison.Ordinal))
                    fallback = "";

                return FeatureCheckResult.Outdated(
                    displayName,
                    feature.GetOutdatedMessage(required, current.ToString(), fallback),
                    required);
            }

            return FeatureCheckResult.Allowed(displayName);
        }

        private static string DisplayNameOf(string featureName) => featureName switch
        {
            SkinSelectorFeature => "Skin Selector",
            MiscellaneousFeature => "Miscellaneous",
            InstallModsPackFeature => "Install ModsPack",
            _ => featureName
        };

        private static string OfflineMessage()
        {
            string text = Loc.T(OfflineMessageKey);
            if (string.Equals(text, OfflineMessageKey, StringComparison.Ordinal))
                text = "This feature needs to check for updates before it can run. " +
                       "Connect to the internet and try again.";
            return text;
        }

        public static bool MeetsMinimumVersion(
            FeatureAccess? feature, AppVersion current, out string required)
        {
            required = "";

            if (feature == null || !feature.HasVersionRequirement)
                return true;

            var minimum = new AppVersion(
                string.IsNullOrWhiteSpace(feature.MinVersion) ? current.Version : feature.MinVersion!,
                feature.MinBuild);

            required = minimum.ToString();
            return !current.ShouldUpdateTo(minimum);
        }

        public static bool IsDevMode => EnvironmentConfig.IsDevMode;

        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedConfig = null;
                _cacheTime = DateTime.MinValue;
                _lastFailedFetch = DateTime.MinValue;
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

        private static FeatureAccessConfig? GetCachedOrDefault()
        {
            lock (_lock)
            {
                _lastFailedFetch = DateTime.UtcNow;

                if (_cachedConfig != null)
                {
                    System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using stale cache");
                    return _cachedConfig;
                }
            }

            var fromDisk = LoadDiskCache();
            if (fromDisk != null)
            {
                System.Diagnostics.Debug.WriteLine("[FeatureAccess] Using on-disk last-known-good");
                lock (_lock) { _cachedConfig ??= fromDisk; }
                return fromDisk;
            }

            System.Diagnostics.Debug.WriteLine("[FeatureAccess] No policy available from any source");
            return null;
        }

        private static bool IsInFailureBackoff()
        {
            lock (_lock)
            {
                return _lastFailedFetch != DateTime.MinValue &&
                       DateTime.UtcNow - _lastFailedFetch < FailureBackoff;
            }
        }

        private static FeatureAccessConfig? LoadDiskCache()
        {
            try
            {
                if (!File.Exists(CacheFilePath)) return null;
                return JsonSerializer.Deserialize<FeatureAccessConfig>(
                    File.ReadAllText(CacheFilePath), _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeatureAccess] Disk cache unreadable: {ex.Message}");
                return null;
            }
        }

        private static async Task SaveCacheAsync(string json)
        {
            string tempPath = CacheFilePath + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                File.Move(tempPath, CacheFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeatureAccess] Failed to save cache: {ex.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private static FeatureAccess GetFeatureAccess(FeatureAccessConfig config, string featureName)
        {
            return featureName switch
            {
                SkinSelectorFeature => config.SkinSelector,
                MiscellaneousFeature => config.Miscellaneous,
                InstallModsPackFeature => config.InstallModsPack,
                _ => null
            } ?? new FeatureAccess();
        }

        #endregion
    }
}
