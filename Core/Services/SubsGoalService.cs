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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Service to fetch YouTube subscriber goal configuration from R2 CDN.
    /// Endpoint: {R2BaseUrl}/config/subs_goal.json
    /// </summary>
    public sealed class SubsGoalService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        private const string ConfigPath = "config/subs_goal.json";
        
        // Cache to avoid repeated requests
        private static SubsGoalConfig? _cachedConfig;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Fetch subscriber goal config from R2 CDN.
        /// Returns cached config if available and fresh.
        /// </summary>
        public async Task<SubsGoalConfig?> GetConfigAsync()
        {
            // Return cached if still fresh
            if (_cachedConfig != null && DateTime.UtcNow - _cacheTime < CacheDuration)
            {
                return _cachedConfig;
            }
            
            try
            {
                string url = $"{CdnConfig.R2BaseUrl}/{ConfigPath}";
                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    return _cachedConfig; // Return stale cache on error
                }
                
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var config = JsonSerializer.Deserialize<SubsGoalConfig>(json, options);
                
                if (config != null)
                {
                    _cachedConfig = config;
                    _cacheTime = DateTime.UtcNow;
                }
                
                return config;
            }
            catch
            {
                // Silent fail - return cached or null
                return _cachedConfig;
            }
        }
        
        /// <summary>
        /// Get default config for when remote fetch fails.
        /// </summary>
        public static SubsGoalConfig GetDefault()
        {
            return new SubsGoalConfig
            {
                CurrentSubs = 0,
                GoalSubs = 0,
                ChannelUrl = "https://youtube.com/@Ardysa",
                Enabled = false // Don't show if no data, but will show defaults while loading
            };
        }
    }
}
