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
using System.Threading;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Update.Models;

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

    public class CdnAuthHandler : DelegatingHandler
    {
        private static long _clockOffsetSeconds;

        public CdnAuthHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (!TrySign(request))
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            long previousOffset = Interlocked.Read(ref _clockOffsetSeconds);

            if (!TryAdoptServerClock(response, out long adoptedOffset))
                return response;

            var retry = CloneForRetry(request);
            if (retry == null || !TrySign(retry))
            {
                RollBackClock(previousOffset, adoptedOffset);
                return response;
            }

            var corrected = await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);

            if (corrected.StatusCode == HttpStatusCode.Unauthorized)
            {
                RollBackClock(previousOffset, adoptedOffset);
                corrected.Dispose();
                return response;
            }

            response.Dispose();
            return corrected;
        }

        private static void RollBackClock(long previousOffset, long adoptedOffset) =>
            Interlocked.CompareExchange(ref _clockOffsetSeconds, previousOffset, adoptedOffset);

        private static bool TrySign(HttpRequestMessage request)
        {
            var uri = request.RequestUri;
            if (uri == null || !IsOurCdn(uri))
                return false;

            var key = CdnSigningKey.Key;
            if (key == null)
                return false;

            string path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            if (!CdnConfig.IsProtectedPath(path))
                return false;

            long expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + Interlocked.Read(ref _clockOffsetSeconds)
                + CdnRequestSigner.LifetimeSeconds;

            request.Headers.Remove(CdnRequestSigner.HeaderName);
            request.Headers.TryAddWithoutValidation(
                CdnRequestSigner.HeaderName,
                CdnRequestSigner.BuildHeaderValue(key, path, expiry));

            return true;
        }

        private static bool IsOurCdn(Uri uri)
        {
            foreach (var baseUrl in CdnConfig.GetCdnBaseUrls())
            {
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                    string.Equals(baseUri.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryAdoptServerClock(HttpResponseMessage response, out long adoptedOffset)
        {
            adoptedOffset = 0;

            if (!response.Headers.TryGetValues(CdnRequestSigner.ServerTimeHeaderName, out var values))
                return false;

            foreach (var value in values)
            {
                if (!long.TryParse(value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out long serverNow))
                    continue;

                long offset = serverNow - DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (Math.Abs(offset - Interlocked.Read(ref _clockOffsetSeconds)) < 2)
                    return false;

                Interlocked.Exchange(ref _clockOffsetSeconds, offset);
                adoptedOffset = offset;
                return true;
            }

            return false;
        }

        private static HttpRequestMessage? CloneForRetry(HttpRequestMessage request)
        {
            if (request.Content != null || request.RequestUri == null)
                return null;

            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, CdnRequestSigner.HeaderName, StringComparison.OrdinalIgnoreCase))
                    continue;
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }

    public static class HttpClientProvider
    {
        public static string UserAgent { get; } = BuildUserAgent();

        private static string BuildUserAgent()
        {
            var v = AppVersion.Current;
            return $"ArdysaModsTools/{v.Version} (build {v.BuildNumber}; Windows NT)";
        }

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

            var cdnHandler = new CdnAuthHandler(new GitHubTokenHandler(handler));

            var c = new HttpClient(cdnHandler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);

            return c;
        });

        public static HttpClient Client => _clientLazy.Value;
    }
}

