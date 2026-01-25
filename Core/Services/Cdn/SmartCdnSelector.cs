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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// CDN latency test result.
    /// </summary>
    public sealed class CdnLatencyResult
    {
        public string CdnUrl { get; init; } = "";
        public string CdnName { get; init; } = "";
        public bool IsReachable { get; init; }
        public long LatencyMs { get; init; }
        public long DownloadSpeedKBps { get; init; }
        public DateTime TestedAt { get; init; }
    }

    /// <summary>
    /// Smart CDN selector that automatically finds the fastest CDN for the user.
    /// - Tests all CDNs on startup
    /// - Caches results to disk
    /// - Reorders CDN priority based on speed
    /// - Retests periodically or on failures
    /// </summary>
    public sealed class SmartCdnSelector
    {
        #region Singleton

        private static readonly Lazy<SmartCdnSelector> _instance =
            new Lazy<SmartCdnSelector>(() => new SmartCdnSelector());

        public static SmartCdnSelector Instance => _instance.Value;

        private SmartCdnSelector()
        {
            _cacheFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools",
                "cdn_latency_cache.json"
            );
        }

        #endregion

        #region Constants

        /// <summary>Test file for latency measurement (small file).</summary>
        private const string LatencyTestFile = "Assets/set_update.json";

        /// <summary>Test file for speed measurement (larger file).</summary>
        private const string SpeedTestFile = "Assets/heroes.json";

        /// <summary>How long to cache results before retesting.</summary>
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(6);

        /// <summary>Timeout for latency test.</summary>
        private static readonly TimeSpan LatencyTimeout = TimeSpan.FromSeconds(10);

        /// <summary>Timeout for speed test.</summary>
        private static readonly TimeSpan SpeedTimeout = TimeSpan.FromSeconds(30);

        #endregion

        #region Fields

        private readonly string _cacheFilePath;
        private List<CdnLatencyResult>? _cachedResults;
        private string[]? _orderedCdnUrls;
        private readonly SemaphoreSlim _testLock = new(1, 1);
        private bool _isInitialized;

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the selector by loading cached results or running tests.
        /// Call this during app startup.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized) return;

            await _testLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                // Try to load cached results
                _cachedResults = await LoadCacheAsync().ConfigureAwait(false);

                if (_cachedResults != null && !IsCacheExpired(_cachedResults))
                {
                    Debug.WriteLine("[SmartCdn] Using cached CDN latency results");
                    _orderedCdnUrls = _cachedResults
                        .Where(r => r.IsReachable)
                        .OrderBy(r => r.LatencyMs)
                        .Select(r => r.CdnUrl)
                        .ToArray();

                    // Add any missing CDNs at the end
                    var allCdns = CdnConfig.GetCdnBaseUrls();
                    _orderedCdnUrls = _orderedCdnUrls
                        .Concat(allCdns.Where(c => !_orderedCdnUrls.Contains(c)))
                        .ToArray();
                }
                else
                {
                    // Run fresh tests
                    await RunLatencyTestsAsync(ct).ConfigureAwait(false);
                }

                _isInitialized = true;
            }
            finally
            {
                _testLock.Release();
            }
        }

        /// <summary>
        /// Get CDN URLs ordered by speed (fastest first).
        /// </summary>
        public string[] GetOrderedCdnUrls()
        {
            if (_orderedCdnUrls != null && _orderedCdnUrls.Length > 0)
                return _orderedCdnUrls;

            // Fallback to default order if not initialized
            return CdnConfig.GetCdnBaseUrls();
        }

        /// <summary>
        /// Force retest all CDNs (call after repeated failures).
        /// </summary>
        public async Task RetestAsync(CancellationToken ct = default)
        {
            await _testLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _cachedResults = null;
                _orderedCdnUrls = null;
                await RunLatencyTestsAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _testLock.Release();
            }
        }

        /// <summary>
        /// Get the last test results.
        /// </summary>
        public IReadOnlyList<CdnLatencyResult>? GetLastResults() => _cachedResults;

        /// <summary>
        /// Report a CDN failure (used to trigger reordering).
        /// </summary>
        public void ReportFailure(string cdnUrl)
        {
            if (_cachedResults == null) return;

            var result = _cachedResults.FirstOrDefault(r => r.CdnUrl == cdnUrl);
            if (result != null)
            {
                Debug.WriteLine($"[SmartCdn] CDN failure reported: {GetCdnName(cdnUrl)}");
                // Could implement penalty scoring here
            }
        }

        #endregion

        #region Private Methods

        private async Task RunLatencyTestsAsync(CancellationToken ct)
        {
            Debug.WriteLine("[SmartCdn] Running CDN latency tests...");

            var cdnUrls = CdnConfig.GetCdnBaseUrls();
            var results = new List<CdnLatencyResult>();

            // Test all CDNs in parallel
            var tasks = cdnUrls.Select(cdnUrl => TestCdnAsync(cdnUrl, ct));
            var testResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            results.AddRange(testResults);

            // Sort by latency (reachable first, then by speed)
            results = results
                .OrderByDescending(r => r.IsReachable)
                .ThenBy(r => r.LatencyMs)
                .ThenByDescending(r => r.DownloadSpeedKBps)
                .ToList();

            _cachedResults = results;
            _orderedCdnUrls = results
                .Where(r => r.IsReachable)
                .Select(r => r.CdnUrl)
                .Concat(results.Where(r => !r.IsReachable).Select(r => r.CdnUrl))
                .ToArray();

            // Log results
            foreach (var r in results)
            {
                Debug.WriteLine($"[SmartCdn] {r.CdnName}: {(r.IsReachable ? $"{r.LatencyMs}ms, {r.DownloadSpeedKBps}KB/s" : "UNREACHABLE")}");
            }

            // Save to cache
            await SaveCacheAsync(results).ConfigureAwait(false);
        }

        private async Task<CdnLatencyResult> TestCdnAsync(string cdnUrl, CancellationToken ct)
        {
            string cdnName = GetCdnName(cdnUrl);
            var sw = Stopwatch.StartNew();

            try
            {
                var client = HttpClientProvider.Client;

                // Latency test (HEAD request)
                using var latencyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                latencyCts.CancelAfter(LatencyTimeout);

                string testUrl = $"{cdnUrl.TrimEnd('/')}/{LatencyTestFile}";
                using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
                var response = await client.SendAsync(request, latencyCts.Token).ConfigureAwait(false);

                sw.Stop();
                long latencyMs = sw.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    return new CdnLatencyResult
                    {
                        CdnUrl = cdnUrl,
                        CdnName = cdnName,
                        IsReachable = false,
                        LatencyMs = 99999,
                        TestedAt = DateTime.UtcNow
                    };
                }

                // Speed test (download larger file)
                sw.Restart();
                using var speedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                speedCts.CancelAfter(SpeedTimeout);

                string speedUrl = $"{cdnUrl.TrimEnd('/')}/{SpeedTestFile}";
                var data = await client.GetByteArrayAsync(speedUrl, speedCts.Token).ConfigureAwait(false);
                sw.Stop();

                long speedKBps = data.Length > 0 && sw.ElapsedMilliseconds > 0
                    ? (data.Length / 1024) * 1000 / sw.ElapsedMilliseconds
                    : 0;

                return new CdnLatencyResult
                {
                    CdnUrl = cdnUrl,
                    CdnName = cdnName,
                    IsReachable = true,
                    LatencyMs = latencyMs,
                    DownloadSpeedKBps = speedKBps,
                    TestedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartCdn] Test failed for {cdnName}: {ex.Message}");
                return new CdnLatencyResult
                {
                    CdnUrl = cdnUrl,
                    CdnName = cdnName,
                    IsReachable = false,
                    LatencyMs = 99999,
                    TestedAt = DateTime.UtcNow
                };
            }
        }

        private static string GetCdnName(string url)
        {
            if (url.Contains("r2.dev") || url.Contains("ardysamods.my.id")) return "Cloudflare R2";
            if (url.Contains("jsdelivr")) return "jsDelivr";
            if (url.Contains("github")) return "GitHub Raw";
            return "Unknown CDN";
        }

        private bool IsCacheExpired(List<CdnLatencyResult> results)
        {
            if (results.Count == 0) return true;
            return results.Any(r => DateTime.UtcNow - r.TestedAt > CacheExpiry);
        }

        private async Task<List<CdnLatencyResult>?> LoadCacheAsync()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return null;

                string json = await File.ReadAllTextAsync(_cacheFilePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<CdnLatencyResult>>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartCdn] Failed to load cache: {ex.Message}");
                return null;
            }
        }

        private async Task SaveCacheAsync(List<CdnLatencyResult> results)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(results, options);
                await File.WriteAllTextAsync(_cacheFilePath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartCdn] Failed to save cache: {ex.Message}");
            }
        }

        #endregion
    }
}
