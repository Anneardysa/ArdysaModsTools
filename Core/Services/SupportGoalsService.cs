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
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services;

/// <summary>
/// Service to fetch Support dialog goals configuration from R2 CDN.
/// Endpoint: {R2BaseUrl}/config/support_goals.json
/// Contains both Ko-fi donation goal and YouTube subscriber goal.
/// </summary>
public sealed class SupportGoalsService
{
    private const string ConfigPath = "config/support_goals.json";

    // Cache to avoid repeated requests
    private static SupportGoalsConfig? _cachedConfig;
    private static DateTime _cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fetch support goals config from R2 CDN.
    /// Returns cached config if available and fresh.
    /// </summary>
    public async Task<SupportGoalsConfig?> GetConfigAsync(CancellationToken ct = default)
    {
        // Return cached if still fresh
        if (_cachedConfig != null && DateTime.UtcNow - _cacheTime < CacheDuration)
        {
            return _cachedConfig;
        }

        try
        {
            string url = $"{CdnConfig.R2BaseUrl}/{ConfigPath}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await HttpClientProvider.Client
                .GetAsync(url, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return _cachedConfig; // Return stale cache on error
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<SupportGoalsConfig>(json, options);

            if (config != null)
            {
                _cachedConfig = config;
                _cacheTime = DateTime.UtcNow;
            }

            return config;
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
}
