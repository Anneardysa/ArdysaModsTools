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
using System.Collections.Concurrent;
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
    public sealed class CdnLatencyResult
    {
        public string CdnUrl { get; init; } = "";
        public string CdnName { get; init; } = "";
        public bool IsReachable { get; init; }
        public long LatencyMs { get; init; }
        public long DownloadSpeedKBps { get; init; }
        public DateTime TestedAt { get; init; }
    }

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

        private const string LatencyTestFile = "Assets/set_update.json";

        private const string SpeedTestFile = "Assets/heroes.json";

        private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(6);

        private static readonly TimeSpan LatencyTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan SpeedTimeout = TimeSpan.FromSeconds(30);

        #endregion

        #region Fields

        private readonly string _cacheFilePath;
        private List<CdnLatencyResult>? _cachedResults;
        private string[]? _orderedCdnUrls;
        private readonly SemaphoreSlim _testLock = new(1, 1);
        private bool _isInitialized;

        private readonly ConcurrentDictionary<string, CdnPenalty> _penalties = new();

        #endregion

        #region Public API

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized) return;

            await _testLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                _cachedResults = await LoadCacheAsync().ConfigureAwait(false);

                if (_cachedResults != null && !IsCacheExpired(_cachedResults))
                {
                    Debug.WriteLine("[SmartCdn] Using cached CDN latency results");
                    _orderedCdnUrls = _cachedResults
                        .Where(r => r.IsReachable)
                        .OrderBy(r => r.LatencyMs)
                        .Select(r => r.CdnUrl)
                        .ToArray();

                    var allCdns = CdnConfig.GetCdnBaseUrls();
                    _orderedCdnUrls = _orderedCdnUrls
                        .Concat(allCdns.Where(c => !_orderedCdnUrls.Contains(c)))
                        .ToArray();
                }
                else
                {
                    await RunLatencyTestsAsync(ct).ConfigureAwait(false);
                }

                _isInitialized = true;
            }
            finally
            {
                _testLock.Release();
            }
        }

        public string[] GetOrderedCdnUrls()
        {
            string[] baseOrder = (_orderedCdnUrls != null && _orderedCdnUrls.Length > 0)
                ? _orderedCdnUrls
                : CdnConfig.GetCdnBaseUrls();

            if (CdnConfig.IsR2Enabled)
            {
                var r2 = CdnConfig.R2BaseUrl;
                bool r2Present = baseOrder.Any(u =>
                    string.Equals(u, r2, StringComparison.OrdinalIgnoreCase));

                if (r2Present && !string.Equals(baseOrder[0], r2, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = baseOrder
                        .Where(u => !string.Equals(u, r2, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    baseOrder = new[] { r2 }.Concat(rest).ToArray();
                }
            }

            if (_penalties.IsEmpty)
                return baseOrder;

            var now = DateTime.UtcNow;
            var healthy = new List<string>(baseOrder.Length);
            var tripped = new List<string>();

            foreach (var cdn in baseOrder)
            {
                if (IsCurrentlyTripped(cdn, now))
                    tripped.Add(cdn);
                else
                    healthy.Add(cdn);
            }

            if (tripped.Count == 0)
                return baseOrder;

            healthy.AddRange(tripped);
            return healthy.ToArray();
        }

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

        public IReadOnlyList<CdnLatencyResult>? GetLastResults() => _cachedResults;

        public void ReportFailure(string cdnUrl)
        {
            if (string.IsNullOrEmpty(cdnUrl)) return;

            var penalty = _penalties.GetOrAdd(cdnUrl, static _ => new CdnPenalty());
            lock (penalty)
            {
                penalty.Failures++;
                if (penalty.Failures >= CdnConfig.CdnFailureThreshold)
                {
                    penalty.TrippedUntilUtc = DateTime.UtcNow.AddSeconds(CdnConfig.CdnCooldownSeconds);
                    Debug.WriteLine($"[SmartCdn] CDN tripped for {CdnConfig.CdnCooldownSeconds}s: {GetCdnName(cdnUrl)}");
                }
                else
                {
                    Debug.WriteLine($"[SmartCdn] CDN failure {penalty.Failures}/{CdnConfig.CdnFailureThreshold}: {GetCdnName(cdnUrl)}");
                }
            }
        }

        public void ReportSuccess(string cdnUrl)
        {
            if (string.IsNullOrEmpty(cdnUrl)) return;

            if (_penalties.TryGetValue(cdnUrl, out var penalty))
            {
                lock (penalty)
                {
                    penalty.Failures = 0;
                    penalty.TrippedUntilUtc = DateTime.MinValue;
                }
            }
        }

        private bool IsCurrentlyTripped(string cdnUrl, DateTime nowUtc)
        {
            if (!_penalties.TryGetValue(cdnUrl, out var penalty))
                return false;

            lock (penalty)
            {
                return nowUtc < penalty.TrippedUntilUtc;
            }
        }

        #endregion

        #region Private Methods

        private async Task RunLatencyTestsAsync(CancellationToken ct)
        {
            Debug.WriteLine("[SmartCdn] Running CDN latency tests...");

            var cdnUrls = CdnConfig.GetCdnBaseUrls();
            var results = new List<CdnLatencyResult>();

            var tasks = cdnUrls.Select(cdnUrl => TestCdnAsync(cdnUrl, ct));
            var testResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            results.AddRange(testResults);

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

            foreach (var r in results)
            {
                Debug.WriteLine($"[SmartCdn] {r.CdnName}: {(r.IsReachable ? $"{r.LatencyMs}ms, {r.DownloadSpeedKBps}KB/s" : "UNREACHABLE")}");
            }

            await SaveCacheAsync(results).ConfigureAwait(false);
        }

        private async Task<CdnLatencyResult> TestCdnAsync(string cdnUrl, CancellationToken ct)
        {
            string cdnName = GetCdnName(cdnUrl);

            try
            {
                var client = HttpClientProvider.Client;

                long latencyMs = 0;
                bool headOk = false;
                try
                {
                    var headSw = Stopwatch.StartNew();
                    using var latencyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    latencyCts.CancelAfter(LatencyTimeout);

                    string testUrl = $"{cdnUrl.TrimEnd('/')}/{LatencyTestFile}";
                    using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
                    using var response = await client.SendAsync(request, latencyCts.Token).ConfigureAwait(false);

                    headSw.Stop();
                    latencyMs = headSw.ElapsedMilliseconds;
                    headOk = response.IsSuccessStatusCode;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
                {
                    Debug.WriteLine($"[SmartCdn] HEAD probe failed for {cdnName}: {ex.Message} — verifying via GET");
                }

                var getSw = Stopwatch.StartNew();
                using var speedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                speedCts.CancelAfter(SpeedTimeout);

                string speedUrl = $"{cdnUrl.TrimEnd('/')}/{SpeedTestFile}";
                var data = await client.GetByteArrayAsync(speedUrl, speedCts.Token).ConfigureAwait(false);
                getSw.Stop();

                long speedKBps = data.Length > 0 && getSw.ElapsedMilliseconds > 0
                    ? (data.Length / 1024) * 1000 / getSw.ElapsedMilliseconds
                    : 0;

                if (!headOk || latencyMs <= 0)
                    latencyMs = getSw.ElapsedMilliseconds;

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

        private sealed class CdnPenalty
        {
            public int Failures;
            public DateTime TrippedUntilUtc = DateTime.MinValue;
        }

        private static string GetCdnName(string url)
        {
            if (url.Contains("r2.dev") || url.Contains("ardysamods.my.id")) return "Cloudflare R2";
            if (url.Contains("jsdelivr")) return "jsDelivr";
            if (url.Contains("ghfast.top")) return "GitHub Proxy (ghfast)";
            if (url.Contains("gh-proxy.com")) return "GitHub Proxy (gh-proxy)";
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
