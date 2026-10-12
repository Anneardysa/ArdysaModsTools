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
using System.Security.Cryptography;
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

        public bool IsOffline { get; init; }

        public string FeatureName { get; init; } = "";

        public static FeatureCheckResult Allowed(
            string displayName, bool devModeBypass = false, string featureName = "") => new()
        {
            IsAllowed = true,
            IsDevModeBypass = devModeBypass,
            FeatureName = featureName,
            FeatureDisplayName = displayName
        };

        public static FeatureCheckResult Blocked(
            string displayName, string message, string featureName = "") => new()
        {
            IsAllowed = false,
            FeatureName = featureName,
            FeatureDisplayName = displayName,
            BlockedMessage = message
        };

        public static FeatureCheckResult Offline(
            string featureName, string displayName, string message) => new()
        {
            IsAllowed = false,
            IsOffline = true,
            FeatureName = featureName,
            FeatureDisplayName = displayName,
            BlockedMessage = message
        };

        public static FeatureCheckResult Outdated(
            string displayName, string message, string requiredVersion, string featureName = "") => new()
        {
            IsAllowed = false,
            IsOutdated = true,
            FeatureName = featureName,
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

        internal static readonly string DefaultCacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools", "feature_access_cache.dat");

        internal const string LegacyCacheFileName = "feature_access_cache.json";

        private static readonly byte[] DiskCacheEntropy = System.Text.Encoding.UTF8.GetBytes("AMT.FeatureAccess.Cache.v1");

        internal static readonly TimeSpan DiskCacheMaxAge = TimeSpan.FromDays(7);

        private static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(1);

        public static string CacheFilePath { get; internal set; } = DefaultCacheFilePath;

        internal static readonly Func<CancellationToken, Task<string?>> DefaultFetcher = ct =>
            CdnFallbackService.Instance.DownloadStringWithFallbackAsync(
                $"{CdnConfig.R2BaseUrl}/{ConfigPath}", ct, AttemptTimeout);

        internal static Func<CancellationToken, Task<string?>> Fetcher { get; set; } = DefaultFetcher;

        internal static string? LastFailureReason { get; private set; }

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(7);

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

        private static Task<FeatureAccessConfig?>? _inflight;

        #endregion

        #region Public API

        public static async Task<FeatureAccessConfig> GetConfigAsync()
            => await TryGetConfigAsync().ConfigureAwait(false) ?? FeatureAccessConfig.CreateDefault();

        internal static Task<FeatureAccessConfig?> TryGetConfigAsync()
        {
            lock (_lock)
            {
                if (_cachedConfig != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                    return Task.FromResult<FeatureAccessConfig?>(_cachedConfig);

                return _inflight ??= Task.Run(FetchAsync);
            }
        }

        private static async Task<FeatureAccessConfig?> FetchAsync()
        {
            try
            {
                if (IsInFailureBackoff() && GetFallbackPolicy() is { } fallback)
                    return fallback;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                string reason;
                try
                {
                    using var cts = new CancellationTokenSource(RequestTimeout);
                    var json = await Fetcher(cts.Token).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(json))
                    {
                        reason = "no response from any CDN host";
                    }
                    else if (JsonSerializer.Deserialize<FeatureAccessConfig>(json, _jsonOptions) is { } config)
                    {
                        UpdateCache(config);
                        LastFailureReason = null;
                        await SaveCacheAsync(json).ConfigureAwait(false);
                        return config;
                    }
                    else
                    {
                        reason = "policy JSON is null";
                    }
                }
                catch (OperationCanceledException)
                {
                    reason = $"timed out after {RequestTimeout.TotalSeconds:0}s";
                }
                catch (HttpRequestException ex)
                {
                    reason = $"network error: {ex.Message}";
                }
                catch (JsonException ex)
                {
                    reason = $"invalid policy JSON: {ex.Message}";
                }
                catch (Exception ex)
                {
                    reason = $"{ex.GetType().Name}: {ex.Message}";
                }

                return OnFetchFailed(reason, sw.ElapsedMilliseconds);
            }
            finally
            {
                lock (_lock) { _inflight = null; }
            }
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

        public static async Task<bool> IsGreyedOutAsync(string featureName)
        {
            if (IsDevMode) return false;
            var config = await GetConfigAsync().ConfigureAwait(false);
            return GetFeatureAccess(config, featureName).GreyOut;
        }

        public static async Task<FeatureCheckResult> CheckFeatureAsync(string featureName)
        {
            if (IsDevMode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DEV] Bypassing feature gate for {DisplayNameOf(featureName)}");
                return FeatureCheckResult.Allowed(DisplayNameOf(featureName), devModeBypass: true, featureName);
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
                return FeatureCheckResult.Offline(featureName, DisplayNameOf(featureName), OfflineMessage());
            }
        }

        internal static FeatureCheckResult Evaluate(
            FeatureAccessConfig? config, string featureName, AppVersion current)
        {
            var displayName = DisplayNameOf(featureName);

            if (config == null)
                return FeatureCheckResult.Offline(featureName, displayName, OfflineMessage());

            var feature = GetFeatureAccess(config, featureName);

            if (!feature.Enabled || feature.GreyOut)
                return FeatureCheckResult.Blocked(displayName, feature.GetDisplayMessage(), featureName);

            if (!MeetsMinimumVersion(feature, current, out string required))
            {
                string fallback = Loc.T(OutdatedMessageKey);
                if (string.Equals(fallback, OutdatedMessageKey, StringComparison.Ordinal))
                    fallback = "";

                return FeatureCheckResult.Outdated(
                    displayName,
                    feature.GetOutdatedMessage(required, current.ToString(), fallback),
                    required,
                    featureName);
            }

            return FeatureCheckResult.Allowed(displayName, featureName: featureName);
        }

        private static string DisplayNameOf(string featureName) => featureName switch
        {
            SkinSelectorFeature => "Skin Selector",
            MiscellaneousFeature => "Miscellaneous",
            InstallModsPackFeature => "Install ModsPack",
            UtilitiesFeature => "Utilities",
            _ => featureName
        };

        private static string OfflineMessage()
        {
            string text = Loc.T(OfflineMessageKey);
            if (string.Equals(text, OfflineMessageKey, StringComparison.Ordinal))
                text = "This feature checks with the Ardysa server before it runs, and that check " +
                       "couldn't get through.\n\nYour internet may be working fine — some ISPs and " +
                       "networks block our server.";
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

        internal static void ResetForTests()
        {
            InvalidateCache();
            lock (_lock) { _inflight = null; }
            LastFailureReason = null;
            Fetcher = DefaultFetcher;
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

        public const string UtilitiesFeature = "Utilities";

        #endregion

        #region Private Helpers

        private static void UpdateCache(FeatureAccessConfig config)
        {
            lock (_lock)
            {
                _cachedConfig = config;
                _cacheTime = DateTime.UtcNow;
                _lastFailedFetch = DateTime.MinValue;
            }
        }

        private static FeatureAccessConfig? OnFetchFailed(string reason, long elapsedMs)
        {
            lock (_lock) { _lastFailedFetch = DateTime.UtcNow; }
            LastFailureReason = reason;

            var fallback = GetFallbackPolicy();
            FallbackLogger.LogFileOnly(
                $"[FeatureAccess] Policy fetch failed after {elapsedMs} ms ({reason}); " +
                (fallback != null ? "using last-known-good policy." : "no policy from any source, gated features blocked."));
            return fallback;
        }

        private static FeatureAccessConfig? GetFallbackPolicy()
        {
            lock (_lock)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;
            }

            var fromDisk = LoadDiskCache();
            if (fromDisk != null)
            {
                lock (_lock) { _cachedConfig ??= fromDisk; }
            }
            return fromDisk;
        }

        private static bool IsInFailureBackoff()
        {
            lock (_lock)
            {
                return _lastFailedFetch != DateTime.MinValue &&
                       DateTime.UtcNow - _lastFailedFetch < FailureBackoff;
            }
        }

        private sealed record DiskCacheEnvelope(DateTime SavedUtc, string Policy);

        private static FeatureAccessConfig? LoadDiskCache()
        {
            DeleteLegacyCache();
            try
            {
                if (!File.Exists(CacheFilePath)) return null;

                var plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(CacheFilePath), DiskCacheEntropy, DataProtectionScope.CurrentUser);
                var envelope = JsonSerializer.Deserialize<DiskCacheEnvelope>(plain);
                if (envelope == null) return null;

                var age = DateTime.UtcNow - envelope.SavedUtc;
                if (age < -ClockTolerance || age > DiskCacheMaxAge)
                {
                    FallbackLogger.LogFileOnly($"[FeatureAccess] Ignoring cached policy saved {envelope.SavedUtc:u} (age {age}).");
                    return null;
                }

                return JsonSerializer.Deserialize<FeatureAccessConfig>(envelope.Policy, _jsonOptions);
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[FeatureAccess] Cached policy rejected: {ex.GetType().Name}");
                return null;
            }
        }

        private static Task SaveCacheAsync(string json)
        {
            WriteDiskCache(json, DateTime.UtcNow);
            return Task.CompletedTask;
        }

        internal static void WriteDiskCache(string json, DateTime savedUtc)
        {
            string tempPath = CacheFilePath + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var blob = ProtectedData.Protect(
                    JsonSerializer.SerializeToUtf8Bytes(new DiskCacheEnvelope(savedUtc, json)),
                    DiskCacheEntropy, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(tempPath, blob);
                if (File.Exists(CacheFilePath))
                    File.SetAttributes(CacheFilePath, FileAttributes.Normal);
                File.Move(tempPath, CacheFilePath, overwrite: true);
                File.SetAttributes(CacheFilePath, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[FeatureAccess] Failed to save cached policy: {ex.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }

            DeleteLegacyCache();
        }

        private static void DeleteLegacyCache()
        {
            try
            {
                string legacy = Path.Combine(Path.GetDirectoryName(CacheFilePath) ?? "", LegacyCacheFileName);
                if (File.Exists(legacy) && !string.Equals(
                        Path.GetFullPath(legacy), Path.GetFullPath(CacheFilePath), StringComparison.OrdinalIgnoreCase))
                    File.Delete(legacy);
            }
            catch { }
        }

        private static FeatureAccess GetFeatureAccess(FeatureAccessConfig config, string featureName)
        {
            return featureName switch
            {
                SkinSelectorFeature => config.SkinSelector,
                MiscellaneousFeature => config.Miscellaneous,
                InstallModsPackFeature => config.InstallModsPack,
                UtilitiesFeature => config.Utilities,
                _ => null
            } ?? new FeatureAccess();
        }

        #endregion
    }
}
