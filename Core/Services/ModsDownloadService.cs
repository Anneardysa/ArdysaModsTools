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

namespace ArdysaModsTools.Core.Services
{
    public sealed class ModsDownloadService
    {
        private static ModsDownloadConfig? _cachedConfig;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static ModsDownloadConfig GetBundled()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "config", "mods_download.json");
                if (File.Exists(path))
                    return Parse(File.ReadAllText(path)) ?? new ModsDownloadConfig();
            }
            catch (Exception) {  }
            return new ModsDownloadConfig();
        }

        public async Task<ModsDownloadConfig> GetConfigAsync(CancellationToken ct = default)
        {
            if (_cachedConfig != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return _cachedConfig;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var json = await CdnFallbackService.Instance
                    .DownloadStringWithFallbackAsync(CdnConfig.ModsDownloadManifestUrl, cts.Token)
                    .ConfigureAwait(false);

                var config = Parse(json);
                if (config != null)
                {
                    _cachedConfig = config;
                    _cacheTime = DateTime.UtcNow;
                    return config;
                }
            }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { }

            return _cachedConfig ?? GetBundled();
        }

        public static ModsDownloadConfig? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var config = JsonSerializer.Deserialize<ModsDownloadConfig>(json, _jsonOptions);
                if (config == null)
                    return null;

                config.Mega = Sanitize(config.Mega);
                config.Mediafire = Sanitize(config.Mediafire);
                return (config.Mega != null || config.Mediafire != null) ? config : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Sanitize(string? url)
            => Uri.TryCreate(url, UriKind.Absolute, out var u)
               && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
                ? url
                : null;
    }
}
