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
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// Result of a CDN download attempt.
    /// </summary>
    public sealed class CdnDownloadResult
    {
        /// <summary>Downloaded data bytes.</summary>
        public byte[]? Data { get; init; }

        /// <summary>Whether the download was successful.</summary>
        public bool Success => Data != null && Data.Length > 0;

        /// <summary>The CDN URL that succeeded.</summary>
        public string? SuccessfulUrl { get; init; }

        /// <summary>ETag header from response (for cache validation).</summary>
        public string? ETag { get; init; }

        /// <summary>Last-Modified header from response.</summary>
        public string? LastModified { get; init; }

        /// <summary>Error message if all CDNs failed.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Number of CDN fallbacks attempted.</summary>
        public int FallbacksAttempted { get; init; }

        /// <summary>Total download time in milliseconds.</summary>
        public long ElapsedMs { get; init; }

        /// <summary>Create a successful result.</summary>
        public static CdnDownloadResult Ok(byte[] data, string url, string? etag, string? lastModified, int fallbacks, long elapsed) =>
            new()
            {
                Data = data,
                SuccessfulUrl = url,
                ETag = etag,
                LastModified = lastModified,
                FallbacksAttempted = fallbacks,
                ElapsedMs = elapsed
            };

        /// <summary>Create a failed result.</summary>
        public static CdnDownloadResult Fail(string error, int fallbacks, long elapsed) =>
            new()
            {
                ErrorMessage = error,
                FallbacksAttempted = fallbacks,
                ElapsedMs = elapsed
            };
    }

    /// <summary>
    /// Service for downloading assets with automatic CDN fallback.
    /// Tries multiple CDN providers in priority order until one succeeds.
    /// Thread-safe singleton.
    /// </summary>
    public sealed class CdnFallbackService
    {
        #region Singleton

        private static readonly Lazy<CdnFallbackService> _instance =
            new Lazy<CdnFallbackService>(() => new CdnFallbackService());

        /// <summary>Gets the singleton instance.</summary>
        public static CdnFallbackService Instance => _instance.Value;

        private CdnFallbackService() { }

        #endregion

        #region Statistics

        private long _totalDownloads;
        private long _r2Successes;
        private long _jsdelivrSuccesses;
        private long _githubSuccesses;
        private long _totalFailures;

        /// <summary>
        /// Get download statistics.
        /// </summary>
        public (long total, long r2, long jsdelivr, long github, long failures) GetStats() =>
            (_totalDownloads, _r2Successes, _jsdelivrSuccesses, _githubSuccesses, _totalFailures);

        /// <summary>Reset statistics.</summary>
        public void ResetStats()
        {
            Interlocked.Exchange(ref _totalDownloads, 0);
            Interlocked.Exchange(ref _r2Successes, 0);
            Interlocked.Exchange(ref _jsdelivrSuccesses, 0);
            Interlocked.Exchange(ref _githubSuccesses, 0);
            Interlocked.Exchange(ref _totalFailures, 0);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Download an asset with automatic CDN fallback.
        /// Tries each CDN in priority order until one succeeds.
        /// </summary>
        /// <param name="url">Original asset URL (any CDN format).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Download result with data and metadata.</returns>
        public async Task<CdnDownloadResult> DownloadWithFallbackAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                return CdnDownloadResult.Fail("URL is empty", 0, 0);

            var stopwatch = Stopwatch.StartNew();
            
            // Use SmartCdnSelector for speed-optimized CDN order
            var cdnUrls = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            int fallbackCount = 0;
            string? lastError = null;

            Interlocked.Increment(ref _totalDownloads);

            foreach (var cdnBase in cdnUrls)
            {
                ct.ThrowIfCancellationRequested();

                string targetUrl = CdnConfig.ConvertToCdn(url, cdnBase);

                try
                {
                    var result = await TryDownloadAsync(targetUrl, ct).ConfigureAwait(false);

                    if (result.Data != null && result.Data.Length > 0)
                    {
                        stopwatch.Stop();
                        UpdateSuccessStats(cdnBase);

                        Debug.WriteLine($"[CdnFallback] Success: {targetUrl} ({result.Data.Length} bytes, {stopwatch.ElapsedMilliseconds}ms, fallbacks: {fallbackCount})");

                        return CdnDownloadResult.Ok(
                            result.Data,
                            targetUrl,
                            result.ETag,
                            result.LastModified,
                            fallbackCount,
                            stopwatch.ElapsedMilliseconds
                        );
                    }

                    lastError = result.ErrorMessage;
                    // Report failure to SmartCdnSelector for future reordering
                    SmartCdnSelector.Instance.ReportFailure(cdnBase);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    SmartCdnSelector.Instance.ReportFailure(cdnBase);
                    Debug.WriteLine($"[CdnFallback] Failed: {targetUrl} -> {ex.Message}");
                }

                fallbackCount++;
            }

            stopwatch.Stop();
            Interlocked.Increment(ref _totalFailures);

            Debug.WriteLine($"[CdnFallback] All CDNs failed for: {url} -> {lastError}");
            return CdnDownloadResult.Fail(lastError ?? "All CDNs failed", fallbackCount, stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Download using only the primary CDN (no fallback).
        /// Useful for testing CDN connectivity.
        /// </summary>
        public async Task<CdnDownloadResult> DownloadFromPrimaryAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                return CdnDownloadResult.Fail("URL is empty", 0, 0);

            var stopwatch = Stopwatch.StartNew();
            var cdnUrls = CdnConfig.GetCdnBaseUrls();

            if (cdnUrls.Length == 0)
                return CdnDownloadResult.Fail("No CDN configured", 0, 0);

            string targetUrl = CdnConfig.ConvertToCdn(url, cdnUrls[0]);

            try
            {
                var result = await TryDownloadAsync(targetUrl, ct).ConfigureAwait(false);
                stopwatch.Stop();

                if (result.Data != null && result.Data.Length > 0)
                {
                    return CdnDownloadResult.Ok(
                        result.Data,
                        targetUrl,
                        result.ETag,
                        result.LastModified,
                        0,
                        stopwatch.ElapsedMilliseconds
                    );
                }

                return CdnDownloadResult.Fail(result.ErrorMessage ?? "Download failed", 0, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CdnDownloadResult.Fail(ex.Message, 0, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Test connectivity to all CDNs.
        /// </summary>
        public async Task<(string cdn, bool success, long latencyMs)[]> TestAllCdnsAsync(string testAssetPath = "Assets/heroes.json", CancellationToken ct = default)
        {
            var cdnUrls = CdnConfig.GetCdnBaseUrls();
            var results = new (string cdn, bool success, long latencyMs)[cdnUrls.Length];

            for (int i = 0; i < cdnUrls.Length; i++)
            {
                var cdnBase = cdnUrls[i];
                string testUrl = $"{cdnBase.TrimEnd('/')}/{testAssetPath}";

                var sw = Stopwatch.StartNew();
                bool success = false;

                try
                {
                    var client = HttpClientProvider.Client;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
                    var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
                    success = response.IsSuccessStatusCode;
                }
                catch
                {
                    success = false;
                }

                sw.Stop();
                results[i] = (cdnBase, success, sw.ElapsedMilliseconds);
            }

            return results;
        }

        #endregion

        #region Private Helpers

        private async Task<(byte[]? Data, string? ETag, string? LastModified, string? ErrorMessage)> TryDownloadAsync(
            string url, CancellationToken ct)
        {
            var client = HttpClientProvider.Client;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(CdnConfig.TimeoutSeconds));

            var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return (null, null, null, $"HTTP {(int)response.StatusCode}");
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            if (data.Length == 0)
            {
                return (null, null, null, "Empty response");
            }

            string? etag = response.Headers.ETag?.Tag;
            string? lastModified = response.Content.Headers.LastModified?.ToString("R");

            return (data, etag, lastModified, null);
        }

        private void UpdateSuccessStats(string cdnBase)
        {
            if (cdnBase.Contains("r2.dev") || cdnBase.Contains("ardysamods.my.id"))
                Interlocked.Increment(ref _r2Successes);
            else if (cdnBase.Contains("jsdelivr"))
                Interlocked.Increment(ref _jsdelivrSuccesses);
            else if (cdnBase.Contains("githubusercontent") || cdnBase.Contains("github"))
                Interlocked.Increment(ref _githubSuccesses);
        }

        #endregion
    }
}
