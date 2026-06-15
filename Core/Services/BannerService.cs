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

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Fetches the main-shell banner carousel manifest from the R2 CDN
    /// (<see cref="CdnConfig.BannerManifestUrl"/>). Mirrors <see cref="SupportGoalsService"/>:
    /// short timeout, multi-CDN fallback via <see cref="CdnFallbackService"/>, and a short-lived
    /// cache. Returns <c>null</c> on any failure so the caller can fall back to the bundled banner.
    /// </summary>
    public sealed class BannerService
    {
        private static BannerConfig? _cachedConfig;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Fetches the banner manifest from R2 (with CDN fallback). Returns a fresh cached copy when
        /// available, the parsed manifest on success, or the stale cache / <c>null</c> on failure.
        /// </summary>
        public async Task<BannerConfig?> GetConfigAsync(CancellationToken ct = default)
        {
            if (_cachedConfig != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return _cachedConfig;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var json = await CdnFallbackService.Instance
                    .DownloadStringWithFallbackAsync(CdnConfig.BannerManifestUrl, cts.Token)
                    .ConfigureAwait(false);

                var config = Parse(json);
                if (config != null)
                {
                    _cachedConfig = config;
                    _cacheTime = DateTime.UtcNow;
                    return config;
                }

                return _cachedConfig; // keep stale cache on parse failure
            }
            catch (OperationCanceledException)
            {
                return _cachedConfig;
            }
            catch (HttpRequestException)
            {
                return _cachedConfig;
            }
        }

        /// <summary>
        /// Parses a banner manifest JSON string into a <see cref="BannerConfig"/>, keeping only slides
        /// that have a non-empty image. Returns <c>null</c> for empty/invalid input or no usable slides.
        /// Pure and side-effect free — the unit-test seam for this service.
        /// </summary>
        public static BannerConfig? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<BannerConfig>(json, _jsonOptions);
                if (config?.Slides == null)
                    return null;

                config.Slides.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Image));
                return config.Slides.Count > 0 ? config : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
