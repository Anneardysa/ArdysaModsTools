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
using System.Linq;

namespace ArdysaModsTools.Core.Constants
{
    /// <summary>
    /// CDN configuration for ModsPack assets.
    /// Supports multiple CDN providers with automatic fallback.
    /// Priority: R2 (Primary) → jsDelivr (Fallback 1) → GitHub Raw (Fallback 2)
    /// </summary>
    public static class CdnConfig
    {
        #region CDN Base URLs

        /// <summary>
        /// Cloudflare R2 with custom domain (Primary CDN).
        /// Custom domain bypasses ISP blocking of *.r2.dev in some regions.
        /// </summary>
        public const string R2BaseUrl = "https://cdn.ardysamods.my.id";

        /// <summary>
        /// jsDelivr CDN URL (Fallback 1).
        /// Uses GitHub repo as source, provides global CDN caching.
        /// </summary>
        public const string JsDelivrBaseUrl = "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main";

        /// <summary>
        /// Raw GitHub URL (Fallback 2).
        /// Direct access to repository files, no CDN caching.
        /// </summary>
        public const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/Anneardysa/ModsPack/main";

        #endregion

        #region GitHub Proxy Mirrors (GFW Bypass)

        /// <summary>
        /// GitHub proxy mirror via ghfast.top (Fallback 3).
        /// Enables access from regions where GitHub/Cloudflare/jsDelivr are blocked
        /// (e.g., China Mobile, China Telecom behind the Great Firewall).
        /// Proxies the full GitHub Raw URL through an accessible relay.
        /// </summary>
        public const string GitHubProxyPrimaryUrl = "https://ghfast.top/" + GitHubRawBaseUrl;

        /// <summary>
        /// GitHub proxy mirror via gh-proxy.com (Fallback 4).
        /// Secondary proxy in case the primary proxy is also unavailable.
        /// </summary>
        public const string GitHubProxySecondaryUrl = "https://gh-proxy.com/" + GitHubRawBaseUrl;

        #endregion

        #region Asset Path Markers

        /// <summary>Assets folder marker in URL paths.</summary>
        private const string AssetsMarker = "/Assets/";

        /// <summary>Models folder for hero skins.</summary>
        public const string ModelsPath = "Assets/models";

        /// <summary>Images folder for thumbnails.</summary>
        public const string ImagesPath = "Assets/image";

        /// <summary>Misc mods folder.</summary>
        public const string MiscPath = "Assets/misc";

        #endregion

        #region Configuration

        /// <summary>
        /// Whether R2 CDN is enabled.
        /// </summary>
        public static bool IsR2Enabled { get; set; } = true;

        /// <summary>
        /// Connection timeout for CDN requests in seconds.
        /// </summary>
        public const int TimeoutSeconds = 30;

        /// <summary>
        /// Maximum retry attempts per CDN before falling back.
        /// </summary>
        public const int MaxRetryPerCdn = 2;

        /// <summary>
        /// Base delay (milliseconds) for exponential backoff between per-CDN retries.
        /// </summary>
        public const int RetryBaseDelayMs = 400;

        /// <summary>
        /// Ceiling (milliseconds) for the exponential backoff delay between retries.
        /// </summary>
        public const int RetryMaxDelayMs = 5000;

        /// <summary>
        /// Maximum honoured <c>Retry-After</c> wait (seconds). Caps hostile or excessive
        /// rate-limit headers so a CDN cannot stall the whole download.
        /// </summary>
        public const int MaxRetryAfterSeconds = 15;

        /// <summary>
        /// Number of full passes over the CDN chain (R2 → … → proxy) before giving up.
        /// A second pass recovers from a transient total-network blip.
        /// </summary>
        public const int ChainRetryPasses = 2;

        /// <summary>
        /// Consecutive failures before a CDN is temporarily tripped (circuit breaker).
        /// </summary>
        public const int CdnFailureThreshold = 3;

        /// <summary>
        /// Cooldown (seconds) a tripped CDN is deprioritized before it is retried at
        /// its normal benchmark position again.
        /// </summary>
        public const int CdnCooldownSeconds = 120;

        #endregion

        #region Release Mirror

        /// <summary>
        /// URL to releases.json manifest on R2 CDN.
        /// Contains version info and download URLs for app updates.
        /// </summary>
        public const string ReleaseManifestUrl = R2BaseUrl + "/releases/releases.json";

        /// <summary>
        /// Base path for release files on R2 CDN.
        /// Format: {R2BaseUrl}/releases/{version}/{filename}
        /// </summary>
        public const string ReleasesBasePath = "releases";

        /// <summary>
        /// Build release file URL for a specific version and filename.
        /// </summary>
        /// <param name="version">Version string (e.g., "2.1.2")</param>
        /// <param name="filename">File name (e.g., "ArdysaModsTools_Setup_2.1.2.exe")</param>
        /// <returns>Full CDN URL for the release file</returns>
        public static string BuildReleaseUrl(string version, string filename)
        {
            return $"{R2BaseUrl}/{ReleasesBasePath}/{version}/{filename}";
        }

        #endregion

        #region URL Helpers

        /// <summary>
        /// Get all CDN base URLs in priority order.
        /// </summary>
        /// <returns>Array of CDN base URLs ordered by priority.</returns>
        public static string[] GetCdnBaseUrls()
        {
            if (IsR2Enabled)
            {
                return
                [
                    R2BaseUrl,             // Priority 1: Cloudflare R2 (fastest)
                    JsDelivrBaseUrl,       // Priority 2: jsDelivr CDN
                    GitHubRawBaseUrl,      // Priority 3: GitHub Raw
                    GitHubProxyPrimaryUrl, // Priority 4: ghfast.top proxy (GFW bypass)
                    GitHubProxySecondaryUrl // Priority 5: gh-proxy.com proxy (GFW bypass)
                ];
            }

            // R2 not configured, use GitHub CDNs only
            return
            [
                JsDelivrBaseUrl,
                GitHubRawBaseUrl,
                GitHubProxyPrimaryUrl,
                GitHubProxySecondaryUrl
            ];
        }

        /// <summary>
        /// Extract the asset or configuration path from a full CDN URL.
        /// Example 1: "https://cdn.jsdelivr.net/gh/.../Assets/models/Abaddon/skin.zip" → "Assets/models/Abaddon/skin.zip"
        /// Example 2: "https://cdn.ardysamods.my.id/config/misc_config.json" → "config/misc_config.json"
        /// </summary>
        /// <param name="url">Full CDN URL.</param>
        /// <returns>Relative path, or null if not a valid URL.</returns>
        public static string? ExtractAssetPath(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // 1. Try legacy marker approach first for fast path matching
            int assetsIndex = url.IndexOf(AssetsMarker, StringComparison.OrdinalIgnoreCase);
            if (assetsIndex != -1)
            {
                return url.Substring(assetsIndex + 1); // Skip the leading "/"
            }

            // 2. Match against known base URLs (longest first to avoid partial matches)
            var baseUrls = GetCdnBaseUrls().OrderByDescending(b => b.Length).ToList();
            
            // We want to match the URL without query params, but return the path WITH query params
            int queryIndex = url.IndexOf('?');
            string urlWithoutQuery = queryIndex != -1 ? url.Substring(0, queryIndex) : url;

            foreach (var baseUrl in baseUrls)
            {
                if (urlWithoutQuery.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    string path = url.Substring(baseUrl.Length).TrimStart('/');
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract the known CDN base URL from a full asset URL.
        /// Used to key circuit-breaker state consistently regardless of asset path.
        /// </summary>
        /// <param name="url">Full asset URL (e.g., "https://cdn.ardysamods.my.id/Assets/...").</param>
        /// <returns>The matching CDN base URL, or null if none matches.</returns>
        public static string? ExtractBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Longest base first so proxy bases (which embed the GitHub Raw URL) match before it.
            foreach (var baseUrl in GetCdnBaseUrls().OrderByDescending(b => b.Length))
            {
                if (url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                    return baseUrl;
            }

            return null;
        }

        /// <summary>
        /// Convert a URL to use a different CDN base.
        /// Preserves the asset path while changing the CDN provider.
        /// </summary>
        /// <param name="originalUrl">Original CDN URL.</param>
        /// <param name="newBaseUrl">New CDN base URL.</param>
        /// <returns>URL with new CDN base, or original if conversion fails.</returns>
        public static string ConvertToCdn(string originalUrl, string newBaseUrl)
        {
            var assetPath = ExtractAssetPath(originalUrl);
            if (assetPath == null)
                return originalUrl;

            // Ensure base URL doesn't end with slash
            string baseUrl = newBaseUrl.TrimEnd('/');
            return $"{baseUrl}/{assetPath}";
        }

        /// <summary>
        /// Build a full CDN URL from an asset path.
        /// Uses the primary enabled CDN.
        /// </summary>
        /// <param name="assetPath">Asset path (e.g., "Assets/models/Abaddon/skin.zip").</param>
        /// <returns>Full CDN URL.</returns>
        public static string BuildUrl(string assetPath)
        {
            string baseUrl = IsR2Enabled ? R2BaseUrl : JsDelivrBaseUrl;
            baseUrl = baseUrl.TrimEnd('/');
            assetPath = assetPath.TrimStart('/');
            return $"{baseUrl}/{assetPath}";
        }

        /// <summary>
        /// Check if a URL is a ModsPack asset URL.
        /// </summary>
        public static bool IsModsPackUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.Contains("ModsPack") || 
                   url.Contains("r2.dev") ||
                   url.Contains("ardysamods.my.id") ||
                   url.Contains("Anneardysa") ||
                   IsProxyUrl(url);
        }

        /// <summary>
        /// Check if a URL is served through a GitHub proxy mirror.
        /// </summary>
        public static bool IsProxyUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.Contains("ghfast.top") ||
                   url.Contains("gh-proxy.com");
        }

        #endregion
    }
}
