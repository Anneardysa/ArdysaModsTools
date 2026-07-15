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
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    public sealed class ModsPackUpdatesService
    {
        private static readonly HttpClient _http = CreateClient();

        private static List<ModsPackUpdate>? _cached;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArdysaModsTools");
            return client;
        }

        public async Task<List<ModsPackUpdate>?> GetUpdatesAsync(CancellationToken ct = default)
        {
            if (_cached != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return _cached;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var json = await _http.GetStringAsync(EnvironmentConfig.ModsPackUpdatesUrl, cts.Token)
                    .ConfigureAwait(false);

                var updates = Parse(json, EnvironmentConfig.WebsiteBase);
                if (updates != null && updates.Count > 0)
                {
                    _cached = updates;
                    _cacheTime = DateTime.UtcNow;
                    return updates;
                }

                return _cached;
            }
            catch (OperationCanceledException)
            {
                return _cached;
            }
            catch (HttpRequestException)
            {
                return _cached;
            }
        }

        public static List<ModsPackUpdate>? Parse(string? json, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var items = JsonSerializer.Deserialize<List<ModsPackUpdate>>(json, _jsonOptions);
                if (items == null)
                    return null;

                items.RemoveAll(u => u == null || string.IsNullOrWhiteSpace(u.Hero));
                foreach (var u in items)
                    u.Image = ResolveImage(u.Image, baseUrl);

                return items.Count > 0 ? items : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ResolveImage(string? image, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(image))
                return "";

            if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return image;

            return $"{baseUrl.TrimEnd('/')}/{image.TrimStart('/')}";
        }
    }
}
