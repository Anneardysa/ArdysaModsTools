using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;

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
                // Other handler config if needed
            };

            var c = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            c.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools/1.0");
            return c;
        });

        public static HttpClient Client => _clientLazy.Value;
    }
}
