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
using System.Threading;
using ArdysaModsTools.Core.Services.Cache;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.UI.Helpers
{
    /// <summary>
    /// Bridges WebView2 image requests to the persistent <see cref="AssetCacheService"/>.
    /// Without this, the embedded browser fetches every CDN thumbnail from the network on
    /// each open (its own cache is volatile). With it, image requests to the CDN host are
    /// served from the local cache — one download ever, surviving temp cleanup and updates.
    /// The HTML is unchanged: it still references CDN URLs; serving happens transparently here.
    /// </summary>
    public static class WebViewAssetInterceptor
    {
        // Mirrors AssetCacheService's known-missing window: skip assets the CDN reported as
        // not-found so the browser never re-requests them (no network, no CDN-chain hammering).
        private static readonly TimeSpan KnownMissingTtl = TimeSpan.FromDays(7);

        // Hard ceiling for a single intercepted image fetch. Without it, one slow/blocked CDN can let
        // the full fallback chain (multiple CDNs × retries × passes) hold a WebView2 deferral open for
        // minutes, so the whole gallery appears to "hang" loading thumbnails for impaired users. On
        // timeout the cache returns null, the request falls through, and the JS onerror placeholder shows.
        private static readonly TimeSpan ImageFetchTimeout = TimeSpan.FromSeconds(45);

        /// <summary>
        /// Attaches a resource interceptor that serves images under <paramref name="cdnBaseUrl"/>
        /// from the local asset cache, falling back to the network when not cached.
        /// </summary>
        /// <param name="core">The initialized CoreWebView2 instance.</param>
        /// <param name="env">The environment used to build cached responses.</param>
        /// <param name="cdnBaseUrl">CDN base URL whose images should be served from cache (e.g. ContentBase).</param>
        public static void Attach(CoreWebView2 core, CoreWebView2Environment env, string cdnBaseUrl)
        {
            if (core == null || env == null || string.IsNullOrWhiteSpace(cdnBaseUrl))
                return;

            // Only intercept image requests under the CDN host; everything else (Tailwind on a
            // different host, navigation, scripts) is left to WebView2's normal handling.
            core.AddWebResourceRequestedFilter(BuildImageFilter(cdnBaseUrl), CoreWebView2WebResourceContext.Image);

            // [AMT:PRO] WebView2 bridge handler — paired with the CDN <img> URLs in the misc/hero
            // HTML assets. Must always complete the deferral and never throw into the bridge.
            core.WebResourceRequested += async (sender, e) =>
            {
                CoreWebView2Deferral? deferral = null;
                try
                {
                    deferral = e.GetDeferral();
                    string url = e.Request.Uri;

                    // Known-missing: respond 404 instantly. No network, no CDN-fallback chain —
                    // the browser's onerror shows the placeholder. Prevents the request storm
                    // for permanently-absent thumbnails on every selector open.
                    if (AssetCacheService.Instance.IsKnownMissing(url, KnownMissingTtl))
                    {
                        e.Response = env.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", string.Empty);
                        return;
                    }

                    // Memory -> disk -> CDN-fallback download (with request coalescing). Bounded so a
                    // single slow/blocked CDN can never stall this thumbnail's deferral indefinitely.
                    using var fetchCts = new CancellationTokenSource(ImageFetchTimeout);
                    byte[]? bytes = await AssetCacheService.Instance
                        .GetAssetBytesAsync(url, fetchCts.Token)
                        .ConfigureAwait(true);

                    if (bytes != null && bytes.Length > 0)
                    {
                        // Stream is read by WebView2 after we return; do not dispose it here.
                        var stream = new MemoryStream(bytes, writable: false);
                        string headers =
                            $"Content-Type: {GetContentType(url)}\r\n" +
                            "Cache-Control: public, max-age=31536000";
                        e.Response = env.CreateWebResourceResponse(stream, 200, "OK", headers);
                    }
                    else if (AssetCacheService.Instance.IsKnownMissing(url, KnownMissingTtl))
                    {
                        // The fetch just marked it not-found — return 404 so the browser stops
                        // round-tripping through us for it.
                        e.Response = env.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", string.Empty);
                    }
                    // Otherwise leave e.Response null so WebView2 performs its normal network
                    // fetch — this preserves the JS tryAltFormats/handleImgError fallback flow.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WebViewAssetInterceptor] Serve failed: {ex.Message}");
                }
                finally
                {
                    deferral?.Complete();
                }
            };
        }

        /// <summary>
        /// Builds the image-only request filter for a CDN base URL (e.g. "https://cdn.host/*").
        /// </summary>
        public static string BuildImageFilter(string cdnBaseUrl)
        {
            return cdnBaseUrl.TrimEnd('/') + "/*";
        }

        /// <summary>
        /// Resolves an HTTP Content-Type from a URL's file extension.
        /// Mirrors the mime mapping used by <see cref="AssetCacheService"/>.
        /// </summary>
        public static string GetContentType(string url)
        {
            string ext;
            try
            {
                ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            }
            catch
            {
                ext = Path.GetExtension(url ?? string.Empty).ToLowerInvariant();
            }

            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }
    }
}
