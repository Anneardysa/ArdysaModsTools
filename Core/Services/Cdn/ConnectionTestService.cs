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
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cdn
{
    public sealed class ConnectionTestService : IConnectionTestService
    {
        private const string PingTestPath = "Assets/set_update.json";
        private const string DefaultStreamAssetPath = "modspack-releases/mods-v4.0/mods-v4.0.zip";
        private const string FallbackStreamAssetPath = "Assets/heroes.json";

        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan BenchmarkDuration = TimeSpan.FromSeconds(6);
        private const long MaxBenchmarkSampleBytes = 35 * 1024 * 1024;

        private readonly HttpClient _httpClient;

        public ConnectionTestService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? HttpClientProvider.Client;
        }

        private sealed record ServerTarget(string Key, string Name, string BaseUrl);

        public async Task<ConnectionTestReport> RunBenchmarkAsync(
            IProgress<ConnectionTestProgress>? progress = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var targets = new List<ServerTarget>
            {
                new("asia", "Cloudflare R2", CdnConfig.R2BaseUrl),
                new("eu_us", "Backblaze B2", CdnConfig.Cdn2BaseUrl)
            };

            string latestModsPackStreamPath = await ResolveLatestModsPackStreamPathAsync(ct).ConfigureAwait(false);

            var results = new List<ServerConnectionResult>();
            int totalSteps = (targets.Count * 2) + 1;
            int currentStep = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var target = targets[i];

                currentStep++;
                progress?.Report(new ConnectionTestProgress
                {
                    Stage = "ping",
                    CurrentServerName = target.Name,
                    Percent = (int)((double)currentStep / totalSteps * 100),
                    Message = $"Measuring latency & jitter: {target.Name}..."
                });

                var pingResult = await ProbeServerLatencyAndJitterAsync(target.BaseUrl, ct).ConfigureAwait(false);

                currentStep++;
                progress?.Report(new ConnectionTestProgress
                {
                    Stage = "speed",
                    CurrentServerName = target.Name,
                    Percent = (int)((double)currentStep / totalSteps * 100),
                    Message = $"Benchmarking sustained download speed: {target.Name}..."
                });

                long sustainedSpeedKBps = 0;
                double peakSpeedMBps = 0;
                int stabilityPercent = 0;
                double dataSampledMB = 0;
                string? errorDetail = pingResult.ErrorDetail;

                if (pingResult.IsReachable)
                {
                    var streamResult = await MeasureSustainedThroughputAsync(
                        target.BaseUrl,
                        target.Name,
                        latestModsPackStreamPath,
                        currentStep,
                        totalSteps,
                        progress,
                        ct).ConfigureAwait(false);

                    sustainedSpeedKBps = streamResult.SustainedSpeedKBps;
                    peakSpeedMBps = streamResult.PeakSpeedMBps;
                    stabilityPercent = streamResult.StabilityPercent;
                    dataSampledMB = streamResult.DataSampledMB;

                    if (!streamResult.Success && errorDetail == null)
                    {
                        errorDetail = streamResult.ErrorDetail;
                    }
                }

                int score = CalculateQualityScore(pingResult.IsReachable, pingResult.AverageLatencyMs, pingResult.JitterMs, sustainedSpeedKBps, stabilityPercent);
                string status = DetermineStatus(pingResult.IsReachable, pingResult.AverageLatencyMs, sustainedSpeedKBps, stabilityPercent);

                results.Add(new ServerConnectionResult
                {
                    ServerKey = target.Key,
                    ServerName = target.Name,
                    BaseUrl = target.BaseUrl,
                    IsReachable = pingResult.IsReachable,
                    LatencyMs = pingResult.AverageLatencyMs,
                    JitterMs = pingResult.JitterMs,
                    DownloadSpeedKBps = sustainedSpeedKBps,
                    PeakSpeedMBps = peakSpeedMBps,
                    StabilityPercent = stabilityPercent,
                    DataSampledMB = dataSampledMB,
                    Status = status,
                    ErrorDetail = errorDetail,
                    QualityScore = score,
                    IsRecommended = false
                });
            }

            progress?.Report(new ConnectionTestProgress
            {
                Stage = "analysis",
                CurrentServerName = "",
                Percent = 100,
                Message = "Analyzing optimal server and network stability..."
            });

            var (recommendedKey, recommendedName, diagMessage, diagSeverity) = AnalyzeResults(results);

            for (int i = 0; i < results.Count; i++)
            {
                if (string.Equals(results[i].ServerKey, recommendedKey, StringComparison.OrdinalIgnoreCase))
                {
                    results[i] = new ServerConnectionResult
                    {
                        ServerKey = results[i].ServerKey,
                        ServerName = results[i].ServerName,
                        BaseUrl = results[i].BaseUrl,
                        IsReachable = results[i].IsReachable,
                        LatencyMs = results[i].LatencyMs,
                        JitterMs = results[i].JitterMs,
                        DownloadSpeedKBps = results[i].DownloadSpeedKBps,
                        PeakSpeedMBps = results[i].PeakSpeedMBps,
                        StabilityPercent = results[i].StabilityPercent,
                        DataSampledMB = results[i].DataSampledMB,
                        Status = results[i].Status,
                        ErrorDetail = results[i].ErrorDetail,
                        QualityScore = results[i].QualityScore,
                        IsRecommended = true
                    };
                }
            }

            return new ConnectionTestReport
            {
                Servers = results,
                RecommendedServerKey = recommendedKey,
                RecommendedServerName = recommendedName,
                DiagnosticMessage = diagMessage,
                DiagnosticSeverity = diagSeverity,
                TestedAt = DateTime.UtcNow
            };
        }

        public async Task<string> ResolveLatestModsPackStreamPathAsync(CancellationToken ct)
        {
            var candidateBases = CdnConfig.GetCdnBaseUrls();

            foreach (var baseUrl in candidateBases)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                    string manifestUrl = $"{baseUrl.TrimEnd('/')}/modspack-releases/modspack-releases.json";
                    string json = await _httpClient.GetStringAsync(manifestUrl, cts.Token).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("latest", out var latestProp))
                        {
                            string? latestVer = latestProp.GetString();
                            if (!string.IsNullOrWhiteSpace(latestVer)
                                && root.TryGetProperty("releases", out var releases)
                                && releases.TryGetProperty(latestVer, out var rel)
                                && rel.TryGetProperty("assets", out var assets)
                                && assets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var asset in assets.EnumerateArray())
                                {
                                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                                    string url = asset.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string? assetPath = CdnConfig.ExtractAssetPath(url);
                                        if (!string.IsNullOrEmpty(assetPath))
                                        {
                                            return assetPath;
                                        }
                                        return $"modspack-releases/{latestVer}/{name}";
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
                {
                    Debug.WriteLine($"[ConnectionTest] Failed to resolve latest manifest from {baseUrl}: {ex.Message}");
                }
            }

            return DefaultStreamAssetPath;
        }

        private async Task<(bool IsReachable, long AverageLatencyMs, long JitterMs, string? ErrorDetail)> ProbeServerLatencyAndJitterAsync(
            string baseUrl,
            CancellationToken ct)
        {
            string url = $"{baseUrl.TrimEnd('/')}/{PingTestPath}";
            var latencies = new List<long>();
            string? lastError = null;

            for (int i = 0; i < 3; i++)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(ProbeTimeout);

                    var sw = Stopwatch.StartNew();
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        latencies.Add(sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        lastError = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    lastError = "Connection timed out (possible ISP restriction or firewall block)";
                }
                catch (HttpRequestException ex)
                {
                    lastError = ClassifyHttpError(ex);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }

                if (i == 0 && latencies.Count == 0)
                {
                    break;
                }

                await Task.Delay(40, ct).ConfigureAwait(false);
            }

            if (latencies.Count > 0)
            {
                long avg = (long)latencies.Average();
                long jitter = 0;
                if (latencies.Count > 1)
                {
                    long diffSum = 0;
                    for (int j = 1; j < latencies.Count; j++)
                    {
                        diffSum += Math.Abs(latencies[j] - latencies[j - 1]);
                    }
                    jitter = diffSum / (latencies.Count - 1);
                }

                return (true, avg, jitter, null);
            }

            return (false, 9999, 0, lastError ?? "Server unreachable");
        }

        public async Task<(bool Success, long SustainedSpeedKBps, double PeakSpeedMBps, int StabilityPercent, double DataSampledMB, string? ErrorDetail)> MeasureSustainedThroughputAsync(
            string baseUrl,
            string serverName,
            string latestModsPackStreamPath,
            int currentStep,
            int totalSteps,
            IProgress<ConnectionTestProgress>? progress,
            CancellationToken ct)
        {
            var candidates = new List<(string RelPath, bool UseRange)>
            {
                (latestModsPackStreamPath, true),
                (DefaultStreamAssetPath, true),
                (FallbackStreamAssetPath, false)
            };

            foreach (var (relPath, useRange) in candidates)
            {
                if (ct.IsCancellationRequested) break;

                string url = $"{baseUrl.TrimEnd('/')}/{relPath.TrimStart('/')}";

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(BenchmarkDuration);

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (useRange)
                    {
                        request.Headers.Range = new RangeHeaderValue(0, MaxBenchmarkSampleBytes - 1);
                    }

                    var sw = Stopwatch.StartNew();
                    using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cts.Token).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                    byte[] buffer = new byte[65536];
                    long totalBytes = 0;
                    int bytesRead;

                    var intervalSpeeds = new List<double>();
                    long lastIntervalBytes = 0;
                    long lastIntervalMs = 0;

                    try
                    {
                        while (totalBytes < MaxBenchmarkSampleBytes &&
                               (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                        {
                            totalBytes += bytesRead;
                            long elapsed = sw.ElapsedMilliseconds;

                            if (elapsed - lastIntervalMs >= 400)
                            {
                                long deltaBytes = totalBytes - lastIntervalBytes;
                                long deltaMs = elapsed - lastIntervalMs;

                                if (deltaMs > 0 && deltaBytes > 0)
                                {
                                    double curMBps = Math.Round(((double)deltaBytes / 1024.0 / 1024.0) / (deltaMs / 1000.0), 2);
                                    intervalSpeeds.Add(curMBps);

                                    double curTotalMBps = Math.Round(((double)totalBytes / 1024.0 / 1024.0) / (elapsed / 1000.0), 1);
                                    progress?.Report(new ConnectionTestProgress
                                    {
                                        Stage = "speed",
                                        CurrentServerName = serverName,
                                        Percent = (int)((double)currentStep / totalSteps * 100),
                                        Message = $"Benchmarking {serverName}: {curTotalMBps} MB/s ({(elapsed / 1000.0):0.0}s)..."
                                    });
                                }

                                lastIntervalBytes = totalBytes;
                                lastIntervalMs = elapsed;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                    }

                    sw.Stop();

                    if (totalBytes > 0)
                    {
                        long totalElapsedMs = Math.Max(1, sw.ElapsedMilliseconds);
                        long sustainedSpeedKBps = Math.Max(1, (long)((double)totalBytes / 1024.0 * 1000.0 / totalElapsedMs));
                        double sustainedMBps = Math.Round(sustainedSpeedKBps / 1024.0, 2);

                        double peakMBps = intervalSpeeds.Count > 0 ? intervalSpeeds.Max() : sustainedMBps;
                        if (peakMBps < sustainedMBps) peakMBps = sustainedMBps;

                        int stabilityPercent = CalculateStabilityPercent(intervalSpeeds, sustainedMBps);

                        double dataSampledMB = Math.Round((double)totalBytes / (1024.0 * 1024.0), 2);

                        return (true, sustainedSpeedKBps, peakMBps, stabilityPercent, dataSampledMB, null);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConnectionTest] Candidate failed: {ex.Message}");
                }
            }

            return (false, 0, 0, 0, 0, "Failed to complete sustained streaming test");
        }

        private static int CalculateStabilityPercent(List<double> intervalSpeeds, double sustainedMBps)
        {
            if (intervalSpeeds.Count < 2 || sustainedMBps <= 0)
            {
                return 95;
            }

            double mean = intervalSpeeds.Average();
            if (mean <= 0) return 90;

            double sumOfSquares = intervalSpeeds.Sum(s => Math.Pow(s - mean, 2));
            double stdDev = Math.Sqrt(sumOfSquares / intervalSpeeds.Count);

            double coeffVariation = (stdDev / mean) * 100.0;
            int stability = (int)Math.Round(100.0 - (coeffVariation * 0.8));

            return Math.Clamp(stability, 45, 99);
        }

        public static int CalculateQualityScore(bool isReachable, long latencyMs, long jitterMs, long speedKBps, int stabilityPercent)
        {
            if (!isReachable) return 0;

            int score = 20;

            if (latencyMs <= 35) score += 35;
            else if (latencyMs <= 70) score += 30;
            else if (latencyMs <= 140) score += 22;
            else if (latencyMs <= 220) score += 15;
            else if (latencyMs <= 350) score += 8;
            else score += 4;

            if (jitterMs <= 5) score += 5;
            else if (jitterMs <= 15) score += 3;

            if (speedKBps >= 15360) score += 30;
            else if (speedKBps >= 8192) score += 25;
            else if (speedKBps >= 4096) score += 20;
            else if (speedKBps >= 2048) score += 15;
            else if (speedKBps >= 1024) score += 10;
            else if (speedKBps >= 400) score += 6;
            else score += 2;

            if (stabilityPercent >= 90) score += 10;
            else if (stabilityPercent >= 80) score += 8;
            else if (stabilityPercent >= 70) score += 5;
            else score += 2;

            return Math.Clamp(score, 0, 100);
        }

        public static string DetermineStatus(bool isReachable, long latencyMs, long speedKBps, int stabilityPercent = 90)
        {
            if (!isReachable) return "unreachable";
            if (latencyMs < 75 && speedKBps > 3000 && stabilityPercent >= 80) return "optimal";
            if (latencyMs < 160 && speedKBps > 1000) return "good";
            if (latencyMs < 300 && speedKBps > 300) return "fair";
            return "slow";
        }

        private static (string RecommendedKey, string RecommendedName, string Message, string Severity) AnalyzeResults(
            List<ServerConnectionResult> results)
        {
            var reachable = results.Where(r => r.IsReachable).ToList();

            if (reachable.Count == 0)
            {
                return ("auto", "Auto (Smart Selection)",
                    "All servers are currently unreachable. Please check your internet connection, DNS, or firewall.",
                    "error");
            }

            if (reachable.Count == 1)
            {
                var only = reachable[0];
                var failed = results.FirstOrDefault(r => !r.IsReachable);
                string failMsg = failed != null ? $"{failed.ServerName} is unreachable (possible ISP block/timeout). " : "";

                return (only.ServerKey, only.ServerName,
                    $"{failMsg}{only.ServerName} is fully operational with {only.LatencyMs}ms ping, {only.StabilityPercent}% stability, and {only.DownloadSpeedMBps} MB/s sustained download speed.",
                    "warning");
            }

            var best = reachable
                .OrderByDescending(r => r.QualityScore)
                .ThenBy(r => r.LatencyMs)
                .ThenByDescending(r => r.DownloadSpeedKBps)
                .First();

            var other = reachable.First(r => r != best);

            string comparison = best.LatencyMs < other.LatencyMs
                ? $"{best.LatencyMs}ms ping (vs {other.LatencyMs}ms), {best.StabilityPercent}% stability, and {best.DownloadSpeedMBps} MB/s sustained speed"
                : $"{best.DownloadSpeedMBps} MB/s speed, {best.StabilityPercent}% stability, and {best.LatencyMs}ms ping";

            return (best.ServerKey, best.ServerName,
                $"{best.ServerName} provides the most stable and optimal connection ({comparison}).",
                "success");
        }

        private static string ClassifyHttpError(HttpRequestException ex)
        {
            string msg = ex.Message.ToLowerInvariant();

            if (msg.Contains("name resolution") || msg.Contains("nodename nor servname") || msg.Contains("no such host"))
            {
                return "DNS resolution failed (check your DNS settings or ISP)";
            }

            if (msg.Contains("ssl") || msg.Contains("tls") || msg.Contains("certificate") || msg.Contains("handshake"))
            {
                return "SSL/TLS handshake error (possible firewall or inspection proxy)";
            }

            if (msg.Contains("refused") || msg.Contains("actively refused"))
            {
                return "Connection refused by server/firewall";
            }

            if (msg.Contains("timed out") || msg.Contains("timeout"))
            {
                return "Connection timed out (possible ISP restriction or network throttling)";
            }

            return ex.Message;
        }
    }
}
