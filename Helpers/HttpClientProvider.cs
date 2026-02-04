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
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Helpers
{
    /// <summary>
    /// Centralized, reusable HttpClient configuration.
    /// Use HttpClientProvider.Client anywhere instead of creating new HttpClient.
    /// </summary>
    public static class HttpClientProvider
    {
        private static readonly Lazy<HttpClient> _clientLazy = new(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                // Inherit system proxy settings (like browsers do)
                UseProxy = true,
                Proxy = WebRequest.GetSystemWebProxy(),
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            };

            var c = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            c.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools/1.0");
            
            // Add GitHub token if configured (for higher rate limits: 5000/hour vs 60/hour)
            var token = EnvironmentConfig.GitHubToken;
            if (!string.IsNullOrEmpty(token))
            {
                c.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            }
            
            return c;
        });

        public static HttpClient Client => _clientLazy.Value;
    }
}

