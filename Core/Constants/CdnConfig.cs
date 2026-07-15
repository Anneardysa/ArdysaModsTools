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

        public const string JsDelivrBaseUrl = "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main";

        public const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/Anneardysa/ModsPack/main";

        #endregion

        #region GitHub Proxy Mirrors (GFW Bypass)

        public const string GitHubProxyPrimaryUrl = "https://ghfast.top/" + GitHubRawBaseUrl;

        public const string GitHubProxySecondaryUrl = "https://gh-proxy.com/" + GitHubRawBaseUrl;

        #endregion

        #region Asset Path Markers

        private const string AssetsMarker = "/Assets/";

        public const string ModelsPath = "Assets/models";

        public const string ImagesPath = "Assets/image";

        public const string MiscPath = "Assets/misc";

        #endregion

        #region Configuration

        public static bool IsR2Enabled { get; set; } = true;

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
            if (IsR2Enabled)
            {
                return
                [
                    R2BaseUrl,
                    JsDelivrBaseUrl,
                    GitHubRawBaseUrl,
                    GitHubProxyPrimaryUrl,
                    GitHubProxySecondaryUrl
                ];
            }

            return
            [
                JsDelivrBaseUrl,
                GitHubRawBaseUrl,
                GitHubProxyPrimaryUrl,
                GitHubProxySecondaryUrl
            ];
        }

        public static string? ExtractAssetPath(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int assetsIndex = url.IndexOf(AssetsMarker, StringComparison.OrdinalIgnoreCase);
            if (assetsIndex != -1)
            {
                var raw = url.Substring(assetsIndex + 1);
                int q = raw.IndexOf('?');
                return q >= 0 ? raw.Substring(0, q) : raw;
            }

            var baseUrls = GetCdnBaseUrls().OrderByDescending(b => b.Length).ToList();
            
            int queryIndex = url.IndexOf('?');
            string urlWithoutQuery = queryIndex != -1 ? url.Substring(0, queryIndex) : url;

            foreach (var baseUrl in baseUrls)
            {
                if (urlWithoutQuery.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    string path = url.Substring(baseUrl.Length).TrimStart('/');
                    int qp = path.IndexOf('?');
                    if (qp >= 0) path = path.Substring(0, qp);
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }

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
            string baseUrl = IsR2Enabled ? R2BaseUrl : JsDelivrBaseUrl;
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

            return url.Contains("ghfast.top") ||
                   url.Contains("gh-proxy.com");
        }

        #endregion
    }
}
