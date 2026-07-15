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
    public sealed class CdnDownloadResult
    {
        public byte[]? Data { get; init; }

        public bool Success => Data != null && Data.Length > 0;

        public string? SuccessfulUrl { get; init; }

        public string? ETag { get; init; }

        public string? LastModified { get; init; }

        public string? ErrorMessage { get; init; }

        public int FallbacksAttempted { get; init; }

        public long ElapsedMs { get; init; }

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

        public static CdnDownloadResult Fail(string error, int fallbacks, long elapsed) =>
            new()
            {
                ErrorMessage = error,
                FallbacksAttempted = fallbacks,
                ElapsedMs = elapsed
            };
    }

    public sealed class CdnFallbackService
    {
        #region Singleton

        private static readonly Lazy<CdnFallbackService> _instance =
            new Lazy<CdnFallbackService>(() => new CdnFallbackService());

        public static CdnFallbackService Instance => _instance.Value;

        private CdnFallbackService() { }

        #endregion

        #region Statistics

        private long _totalDownloads;
        private long _r2Successes;
        private long _jsdelivrSuccesses;
        private long _githubSuccesses;
        private long _proxySuccesses;
        private long _totalFailures;

        public (long total, long r2, long jsdelivr, long github, long proxy, long failures) GetStats() =>
            (_totalDownloads, _r2Successes, _jsdelivrSuccesses, _githubSuccesses, _proxySuccesses, _totalFailures);

        public void ResetStats()
        {
            Interlocked.Exchange(ref _totalDownloads, 0);
            Interlocked.Exchange(ref _r2Successes, 0);
            Interlocked.Exchange(ref _jsdelivrSuccesses, 0);
            Interlocked.Exchange(ref _githubSuccesses, 0);
            Interlocked.Exchange(ref _proxySuccesses, 0);
            Interlocked.Exchange(ref _totalFailures, 0);
        }

        #endregion

        #region Public API

        public async Task<string?> DownloadStringWithFallbackAsync(string url, CancellationToken ct = default)
        {
            var result = await DownloadWithFallbackAsync(url, ct).ConfigureAwait(false);
            if (result.Success && result.Data != null)
            {
                if (result.Data.Length >= 3 && result.Data[0] == 0xEF && result.Data[1] == 0xBB && result.Data[2] == 0xBF)
                    return System.Text.Encoding.UTF8.GetString(result.Data, 3, result.Data.Length - 3);
                return System.Text.Encoding.UTF8.GetString(result.Data);
            }
            return null;
        }

        public async Task<CdnDownloadResult> DownloadWithFallbackAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                return CdnDownloadResult.Fail("URL is empty", 0, 0);

            var stopwatch = Stopwatch.StartNew();
            int fallbackCount = 0;
            string? lastError = null;

            Interlocked.Increment(ref _totalDownloads);

            int passes = Math.Max(1, CdnConfig.ChainRetryPasses);
            for (int pass = 0; pass < passes; pass++)
            {
                if (pass > 0)
                {
                    await Task.Delay(DownloadRetryPolicy.GetBackoffDelay(pass), ct).ConfigureAwait(false);
                    Debug.WriteLine($"[CdnFallback] Retrying full CDN chain (pass {pass + 1}/{passes}) for: {url}");
                }

                var cdnUrls = SmartCdnSelector.Instance.GetOrderedCdnUrls();

                bool allNotFoundThisPass = true;

                foreach (var cdnBase in cdnUrls)
                {
                    ct.ThrowIfCancellationRequested();

                    string targetUrl = CdnConfig.ConvertToCdn(url, cdnBase);

                    try
                    {
                        var result = await DownloadRetryPolicy.ExecuteWithRetryAsync(
                            token => TryDownloadAsync(targetUrl, token),
                            log: null,
                            ct).ConfigureAwait(false);

                        stopwatch.Stop();
                        SmartCdnSelector.Instance.ReportSuccess(cdnBase);
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
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;

                        bool isNotFound = ex is HttpRequestException hre &&
                            (hre.StatusCode == System.Net.HttpStatusCode.NotFound ||
                             hre.StatusCode == System.Net.HttpStatusCode.Forbidden);

                        if (!isNotFound)
                        {
                            allNotFoundThisPass = false;
                            SmartCdnSelector.Instance.ReportFailure(cdnBase);
                        }

                        Debug.WriteLine($"[CdnFallback] Failed: {targetUrl} -> {ex.Message}");
                    }

                    fallbackCount++;
                }

                if (allNotFoundThisPass)
                    break;
            }

            stopwatch.Stop();
            Interlocked.Increment(ref _totalFailures);

            Debug.WriteLine($"[CdnFallback] All CDNs failed for: {url} -> {lastError}");
            return CdnDownloadResult.Fail(lastError ?? "All CDNs failed", fallbackCount, stopwatch.ElapsedMilliseconds);
        }

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

                return CdnDownloadResult.Ok(
                    result.Data,
                    targetUrl,
                    result.ETag,
                    result.LastModified,
                    0,
                    stopwatch.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CdnDownloadResult.Fail(ex.Message, 0, stopwatch.ElapsedMilliseconds);
            }
        }

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

        private async Task<(byte[] Data, string? ETag, string? LastModified)> TryDownloadAsync(
            string url, CancellationToken ct)
        {
            var client = HttpClientProvider.Client;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(CdnConfig.TimeoutSeconds));

            using var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (Core.Helpers.RetryHelper.IsTransientStatusCode(response.StatusCode))
                    throw new TransientDownloadException($"HTTP {(int)response.StatusCode}", DownloadRetryPolicy.GetRetryAfter(response));
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}", null, response.StatusCode);
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);

            if (data.Length == 0)
                throw new TransientDownloadException("Empty response body");

            long? declared = response.Content.Headers.ContentLength;
            if (declared.HasValue && declared.Value > 0 && data.Length != declared.Value)
                throw new TransientDownloadException($"Truncated response: got {data.Length} of {declared.Value} bytes");

            string? etag = response.Headers.ETag?.Tag;
            string? lastModified = response.Content.Headers.LastModified?.ToString("R");

            return (data, etag, lastModified);
        }

        private void UpdateSuccessStats(string cdnBase)
        {
            if (cdnBase.Contains("r2.dev") || cdnBase.Contains("ardysamods.my.id"))
                Interlocked.Increment(ref _r2Successes);
            else if (cdnBase.Contains("jsdelivr"))
                Interlocked.Increment(ref _jsdelivrSuccesses);
            else if (CdnConfig.IsProxyUrl(cdnBase))
                Interlocked.Increment(ref _proxySuccesses);
            else if (cdnBase.Contains("githubusercontent") || cdnBase.Contains("github"))
                Interlocked.Increment(ref _githubSuccesses);
        }

        #endregion
    }
}
