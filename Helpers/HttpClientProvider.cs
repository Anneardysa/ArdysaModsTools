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
    public class GitHubTokenHandler : DelegatingHandler
    {
        public GitHubTokenHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var token = EnvironmentConfig.GitHubToken;
            bool tokenAttached = false;

            if (!string.IsNullOrEmpty(token) && request.RequestUri != null)
            {
                var host = request.RequestUri.Host.ToLowerInvariant();
                if (host == "github.com" || host == "api.github.com" || host.EndsWith(".githubusercontent.com"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                    tokenAttached = true;
                }
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (tokenAttached &&
                (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden))
            {
                var anonymousRequest = CloneRequestWithoutAuth(request);
                if (anonymousRequest != null)
                {
                    response.Dispose();
                    return await base.SendAsync(anonymousRequest, cancellationToken).ConfigureAwait(false);
                }
            }

            return response;
        }

        private static HttpRequestMessage? CloneRequestWithoutAuth(HttpRequestMessage request)
        {
            if (request.Content != null || request.RequestUri == null)
                return null;

            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    continue;
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }

    public static class HttpClientProvider
    {
        private static readonly Lazy<HttpClient> _clientLazy = new(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip
                    | DecompressionMethods.Deflate
                    | DecompressionMethods.Brotli,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                UseProxy = true,
                Proxy = WebRequest.GetSystemWebProxy(),
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            };

            var gitHubHandler = new GitHubTokenHandler(handler);

            var c = new HttpClient(gitHubHandler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            c.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools/1.0");
            
            return c;
        });

        public static HttpClient Client => _clientLazy.Value;
    }
}

