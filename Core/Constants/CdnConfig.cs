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
    public static class CdnConfig
    {
        #region CDN Base URLs

        public const string R2BaseUrl = "https://cdn.ardysamods.my.id";

        public const string Cdn2BaseUrl = "https://cdn2.ardysamods.my.id";

        #endregion

        #region Asset Path Markers

        private const string AssetsMarker = "/Assets/";

        public const string ModelsPath = "Assets/models";

        public const string ImagesPath = "Assets/image";

        public const string MiscPath = "Assets/misc";

        #endregion

        #region Configuration

        public static bool IsR2Enabled { get; set; } = true;

        public static string CdnServerPreference { get; set; } = "auto";

        public const int TimeoutSeconds = 30;

        public const int MaxRetryPerCdn = 2;

        public const int RetryBaseDelayMs = 400;

        public const int RetryMaxDelayMs = 5000;

        public const int MaxRetryAfterSeconds = 15;

        public const int ChainRetryPasses = 2;

        public const int CdnFailureThreshold = 3;

        public const int CdnCooldownSeconds = 120;

        #endregion

        #region Release Mirror

        public const string ReleaseManifestUrl = R2BaseUrl + "/releases/releases.json";

        public const string ModsPackReleasesManifestUrl = R2BaseUrl + "/modspack-releases/modspack-releases.json";

        public const string BannerManifestUrl = R2BaseUrl + "/config/banner.json";

        public const string ModsDownloadManifestUrl = R2BaseUrl + "/config/mods_download.json";

        public const string ReleasesBasePath = "releases";

        public static string BuildReleaseUrl(string version, string filename)
        {
            return $"{R2BaseUrl}/{ReleasesBasePath}/{version}/{filename}";
        }

        #endregion

        #region URL Helpers

        public static string[] GetCdnBaseUrls()
        {
            if (!IsR2Enabled)
            {
                return [Cdn2BaseUrl];
            }

            if (string.Equals(CdnServerPreference, "eu_us", StringComparison.OrdinalIgnoreCase))
            {
                return [Cdn2BaseUrl, R2BaseUrl];
            }

            return [R2BaseUrl, Cdn2BaseUrl];
        }

        private static readonly string[] LegacyBaseUrls =
        [
            "https://ghfast.top/https://raw.githubusercontent.com/Anneardysa/ModsPack/main",
            "https://gh-proxy.com/https://raw.githubusercontent.com/Anneardysa/ModsPack/main",
            "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main",
            "https://raw.githubusercontent.com/Anneardysa/ModsPack/main",
            "https://raw.githubusercontent.com/Anneardysa/ModsPack/refs/heads/main"
        ];

        public static string? ExtractAssetPath(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int queryIndex = url.IndexOf('?');
            string urlWithoutQuery = queryIndex != -1 ? url.Substring(0, queryIndex) : url;

            foreach (var baseUrl in GetCdnBaseUrls()
                         .Concat(LegacyBaseUrls)
                         .OrderByDescending(b => b.Length))
            {
                if (urlWithoutQuery.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    string path = urlWithoutQuery.Substring(baseUrl.Length).TrimStart('/');
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }

            int assetsIndex = urlWithoutQuery.IndexOf(AssetsMarker, StringComparison.OrdinalIgnoreCase);
            if (assetsIndex != -1)
                return urlWithoutQuery.Substring(assetsIndex + 1);

            return null;
        }

        public static string? ExtractBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            foreach (var baseUrl in GetCdnBaseUrls().OrderByDescending(b => b.Length))
            {
                if (url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                    return baseUrl;
            }

            return null;
        }

        public static string ConvertToCdn(string originalUrl, string newBaseUrl)
        {
            var assetPath = ExtractAssetPath(originalUrl);
            if (assetPath == null)
                return originalUrl;

            string baseUrl = newBaseUrl.TrimEnd('/');
            return $"{baseUrl}/{assetPath}";
        }

        public static string BuildUrl(string assetPath)
        {
            string baseUrl = IsR2Enabled ? R2BaseUrl : Cdn2BaseUrl;
            baseUrl = baseUrl.TrimEnd('/');
            assetPath = assetPath.TrimStart('/');
            return $"{baseUrl}/{assetPath}";
        }

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

        public static bool IsProxyUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.Contains("cdn2.ardysamods.my.id") ||
                   url.Contains("ghfast.top") ||
                   url.Contains("gh-proxy.com");
        }

        #endregion
    }
}
