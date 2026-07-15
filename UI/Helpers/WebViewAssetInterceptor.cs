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
    public static class WebViewAssetInterceptor
    {
        private static readonly TimeSpan KnownMissingTtl = TimeSpan.FromDays(7);

        private static readonly TimeSpan ImageFetchTimeout = TimeSpan.FromSeconds(45);

        public static void Attach(CoreWebView2 core, CoreWebView2Environment env, string cdnBaseUrl)
        {
            if (core == null || env == null || string.IsNullOrWhiteSpace(cdnBaseUrl))
                return;

            core.AddWebResourceRequestedFilter(BuildImageFilter(cdnBaseUrl), CoreWebView2WebResourceContext.Image);

            core.WebResourceRequested += async (sender, e) =>
            {
                CoreWebView2Deferral? deferral = null;
                try
                {
                    deferral = e.GetDeferral();
                    string url = e.Request.Uri;

                    if (AssetCacheService.Instance.IsKnownMissing(url, KnownMissingTtl))
                    {
                        e.Response = env.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", string.Empty);
                        return;
                    }

                    using var fetchCts = new CancellationTokenSource(ImageFetchTimeout);
                    byte[]? bytes = await AssetCacheService.Instance
                        .GetAssetBytesAsync(url, fetchCts.Token)
                        .ConfigureAwait(true);

                    if (bytes != null && bytes.Length > 0)
                    {
                        var stream = new MemoryStream(bytes, writable: false);
                        string headers =
                            $"Content-Type: {GetContentType(url)}\r\n" +
                            "Cache-Control: public, max-age=31536000";
                        e.Response = env.CreateWebResourceResponse(stream, 200, "OK", headers);
                    }
                    else if (AssetCacheService.Instance.IsKnownMissing(url, KnownMissingTtl))
                    {
                        e.Response = env.CreateWebResourceResponse(new MemoryStream(), 404, "Not Found", string.Empty);
                    }
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

        public static string BuildImageFilter(string cdnBaseUrl)
        {
            return cdnBaseUrl.TrimEnd('/') + "/*";
        }

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
