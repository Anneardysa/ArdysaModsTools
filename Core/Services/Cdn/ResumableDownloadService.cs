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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// Resumable download service using HTTP Range requests.
    /// 
    /// Key features:
    /// - Downloads files as a single streamed Range request from the resume offset
    /// - Resumes from .partial file on disk if the previous attempt was interrupted
    /// - Switches CDN mid-download on stall/failure without losing progress
    /// - CDN affinity: once a CDN works, prefer it for the remainder
    /// - Falls back to non-Range download if server doesn't support Accept-Ranges
    /// 
    /// Thread-safe singleton.
    /// </summary>
    public sealed class ResumableDownloadService
    {
        #region Singleton

        private static readonly Lazy<ResumableDownloadService> _instance =
            new(() => new ResumableDownloadService());

        public static ResumableDownloadService Instance => _instance.Value;

        private ResumableDownloadService() { }

        #endregion

        #region Constants

        /// <summary>Buffer size for streaming data (80KB).</summary>
        private const int BufferSize = 81920;

        /// <summary>Seconds of zero data before switching CDN.</summary>
        private const int StallTimeoutSeconds = 30;

        /// <summary>Maximum overall timeout per CDN attempt (10 minutes).</summary>
        private static readonly TimeSpan PerCdnTimeout = TimeSpan.FromMinutes(10);

        /// <summary>Extension for partial download files.</summary>
        private const string PartialExtension = ".partial";

        #endregion

        #region Public API

        /// <summary>
        /// Download a file with resumable Range requests and automatic CDN fallback.
        /// </summary>
        /// <param name="urls">CDN URLs to try in priority order (R2, jsDelivr, GitHub, etc.).</param>
        /// <param name="destPath">Final destination file path.</param>
        /// <param name="log">Status logger for UI messages.</param>
        /// <param name="progress">Progress reporter (0-100%).</param>
        /// <param name="speedProgress">Speed metrics reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task DownloadAsync(
            string[] urls,
            string destPath,
            Action<string>? log = null,
            IProgress<int>? progress = null,
            IProgress<SpeedMetrics>? speedProgress = null,
            CancellationToken ct = default)
        {
            if (urls == null || urls.Length == 0)
                throw new ArgumentException("At least one download URL is required.", nameof(urls));

            if (string.IsNullOrWhiteSpace(destPath))
                throw new ArgumentException("Destination path is required.", nameof(destPath));

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            string partialPath = destPath + PartialExtension;
            long existingBytes = 0;

            // Check for existing partial download
            if (File.Exists(partialPath))
            {
                existingBytes = new FileInfo(partialPath).Length;
                if (existingBytes > 0)
                {
                    log?.Invoke($"Resuming download from {FormatBytes(existingBytes)}...");
                    Debug.WriteLine($"[ResumableDL] Resuming from {existingBytes} bytes: {partialPath}");
                }
            }

            int totalCdns = urls.Length;
            string? lastError = null;

            for (int cdnIdx = 0; cdnIdx < totalCdns; cdnIdx++)
            {
                ct.ThrowIfCancellationRequested();

                string url = urls[cdnIdx];
                string cdnName = GetCdnDisplayName(url);

                if (cdnIdx > 0)
                {
                    log?.Invoke($"Trying {cdnName} ({cdnIdx + 1}/{totalCdns})...");
                    Debug.WriteLine($"[ResumableDL] CDN fallback to {cdnName}: {url}");
                }

                try
                {
                    await DownloadFromUrlAsync(
                        url, partialPath, existingBytes,
                        log, progress, speedProgress, ct
                    ).ConfigureAwait(false);

                    // Download complete — rename .partial to final destination
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(partialPath, destPath);

                    Debug.WriteLine($"[ResumableDL] Complete via {cdnName}: {destPath}");
                    return; // Success!
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // User cancelled
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Debug.WriteLine($"[ResumableDL] {cdnName} failed: {ex.Message}");

                    // Report failure to SmartCdnSelector
                    SmartCdnSelector.Instance.ReportFailure(url);

                    // Update existing bytes — partial file may have grown
                    if (File.Exists(partialPath))
                    {
                        existingBytes = new FileInfo(partialPath).Length;
                    }

                    if (cdnIdx < totalCdns - 1)
                    {
                        log?.Invoke($"{cdnName} failed, switching to next server...");
                    }
                }
            }

            // All CDNs failed — clean up partial file
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }

            throw new HttpRequestException(
                $"Download failed from all servers. Please check your internet connection. ({lastError})");
        }

        #endregion

        #region Private — Core Download

        /// <summary>
        /// Download from a single URL with Range request support and stall detection.
        /// </summary>
        private async Task DownloadFromUrlAsync(
            string url,
            string partialPath,
            long existingBytes,
            Action<string>? log,
            IProgress<int>? progress,
            IProgress<SpeedMetrics>? speedProgress,
            CancellationToken ct)
        {
            var client = HttpClientProvider.Client;

            // Step 1: Probe the server
            using var cdnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cdnCts.CancelAfter(PerCdnTimeout);

            long totalBytes = -1;
            bool supportsRange = false;

            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResp = await client.SendAsync(headReq, cdnCts.Token).ConfigureAwait(false);

                if (!headResp.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)headResp.StatusCode} {headResp.StatusCode}");

                totalBytes = headResp.Content.Headers.ContentLength ?? -1;
                supportsRange = headResp.Headers.AcceptRanges?.Contains("bytes") == true;

                Debug.WriteLine($"[ResumableDL] HEAD {url}: {totalBytes} bytes, Range={supportsRange}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // HEAD failed — try GET without Range as fallback
                Debug.WriteLine($"[ResumableDL] HEAD failed ({ex.Message}), falling back to GET");
                supportsRange = false;
            }

            // Step 2: Determine resume offset
            long offset = 0;
            if (supportsRange && existingBytes > 0 && totalBytes > 0)
            {
                if (existingBytes >= totalBytes)
                {
                    // Already complete
                    log?.Invoke("Download already complete.");
                    return;
                }

                offset = existingBytes;
                log?.Invoke($"Resuming from {FormatBytes(offset)} / {FormatBytes(totalBytes)}...");
            }
            else if (existingBytes > 0 && !supportsRange)
            {
                // Server doesn't support Range — must restart
                log?.Invoke("Server doesn't support resume — restarting download...");
                try { File.Delete(partialPath); } catch { }
                offset = 0;
            }

            // Step 3: Build GET request with Range header
            using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
            if (supportsRange && offset > 0)
            {
                getReq.Headers.Range = new RangeHeaderValue(offset, null);
            }

            using var response = await client.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, cdnCts.Token)
                .ConfigureAwait(false);

            // Accept both 200 (full) and 206 (partial)
            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.PartialContent)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.StatusCode}");
            }

            // If server returned 200 instead of 206, it's sending the full file
            if (response.StatusCode == HttpStatusCode.OK && offset > 0)
            {
                // Server didn't honor Range request — restart from beginning
                offset = 0;
                try { File.Delete(partialPath); } catch { }
            }

            // Get total size from response if not known from HEAD
            if (totalBytes <= 0)
            {
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    totalBytes = offset + contentLength.Value;
                }
            }

            if (totalBytes > 0)
            {
                var totalMb = totalBytes / 1024.0 / 1024.0;
                log?.Invoke($"Downloading ({totalMb:F1} MB)...");
            }

            // Step 4: Stream data to .partial file with stall detection
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            // Open file for append (if resuming) or create (if starting fresh)
            var fileMode = offset > 0 ? FileMode.Append : FileMode.Create;
            await using var fileStream = new FileStream(partialPath, fileMode, FileAccess.Write, FileShare.None, BufferSize);

            var buffer = new byte[BufferSize];
            long totalRead = offset;
            int lastPercentReported = totalBytes > 0 ? (int)(offset * 100 / totalBytes) : 0;
            var sw = Stopwatch.StartNew();
            var lastActivityTime = DateTime.UtcNow;
            bool stallWarningShown = false;

            // Speed tracking
            long speedWindowBytes = 0;
            var speedWindowStart = sw.Elapsed;

            // Stall monitor task
            using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cdnCts.Token);
            var stallMonitorTask = RunStallMonitorAsync(stallCts, () => lastActivityTime, log);

            try
            {
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), stallCts.Token).ConfigureAwait(false);
                    totalRead += bytesRead;
                    speedWindowBytes += bytesRead;
                    lastActivityTime = DateTime.UtcNow;

                    // Report progress
                    if (totalBytes > 0)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes);
                        if (percent > lastPercentReported)
                        {
                            lastPercentReported = percent;
                            progress?.Report(percent);
                        }
                    }

                    // Report speed every 500ms
                    var elapsed = sw.Elapsed - speedWindowStart;
                    if (elapsed.TotalMilliseconds >= 500)
                    {
                        string speedStr = SpeedCalculator.FormatSpeed(speedWindowBytes, elapsed.TotalSeconds);
                        string details = totalBytes > 0
                            ? $"{FormatBytes(totalRead)} / {FormatBytes(totalBytes)}"
                            : FormatBytes(totalRead);

                        speedProgress?.Report(new SpeedMetrics
                        {
                            DownloadSpeed = speedStr,
                            ProgressDetails = details,
                            DownloadedBytes = totalRead,
                            TotalBytes = totalBytes > 0 ? totalBytes : 0
                        });

                        speedWindowBytes = 0;
                        speedWindowStart = sw.Elapsed;
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Stall timeout or per-CDN timeout — not user cancellation
                await fileStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                throw new TimeoutException(
                    $"Download stalled with {FormatBytes(totalRead)} received");
            }
            finally
            {
                stallCts.Cancel();
                try { await stallMonitorTask.ConfigureAwait(false); } catch { }
            }

            // Verify we got all the data
            if (totalRead == 0)
            {
                throw new HttpRequestException("Server returned empty response body");
            }

            if (totalBytes > 0 && totalRead < totalBytes)
            {
                // Flush what we have — next CDN attempt can resume from here
                await fileStream.FlushAsync(ct).ConfigureAwait(false);
                throw new IOException(
                    $"Incomplete download: got {FormatBytes(totalRead)} of {FormatBytes(totalBytes)}");
            }

            sw.Stop();
            var avgSpeed = SpeedCalculator.FormatSpeed(totalRead - offset, sw.Elapsed.TotalSeconds);
            log?.Invoke($"Download complete ({FormatBytes(totalRead)} at {avgSpeed})");
            progress?.Report(100);
            speedProgress?.Report(new SpeedMetrics { DownloadSpeed = "-- MB/S" });
        }

        #endregion

        #region Private — Stall Monitor

        /// <summary>
        /// Background task that monitors for download stalls.
        /// If no data is received for <see cref="StallTimeoutSeconds"/>, cancels the download
        /// so the caller can try the next CDN.
        /// </summary>
        private static async Task RunStallMonitorAsync(
            CancellationTokenSource stallCts,
            Func<DateTime> getLastActivity,
            Action<string>? log)
        {
            bool warned = false;

            try
            {
                while (!stallCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(3000, stallCts.Token).ConfigureAwait(false);

                    var stalledSeconds = (DateTime.UtcNow - getLastActivity()).TotalSeconds;

                    if (stalledSeconds >= 15 && !warned)
                    {
                        warned = true;
                        log?.Invoke("Download stalled — will try alternate server shortly...");
                    }

                    if (stalledSeconds >= StallTimeoutSeconds)
                    {
                        Debug.WriteLine($"[ResumableDL] Stall detected after {stalledSeconds:F0}s");
                        stallCts.Cancel();
                    }

                    // Reset warning if data starts flowing again
                    if (stalledSeconds < 5 && warned)
                    {
                        warned = false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected — download completed or stall triggered
            }
        }

        #endregion

        #region Private — Helpers

        private static string GetCdnDisplayName(string url)
        {
            if (url.Contains("ardysamods.my.id") || url.Contains("r2.dev"))
                return "R2 CDN";
            if (url.Contains("jsdelivr.net"))
                return "jsDelivr CDN";
            if (url.Contains("githubusercontent.com") || url.Contains("github.com"))
                return "GitHub";
            return "Server";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        #endregion
    }
}
