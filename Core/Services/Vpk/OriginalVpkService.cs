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
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for downloading and providing the Original.zip base for hero generation.
    /// </summary>
    public interface IOriginalVpkProvider
    {
        /// <summary>
        /// Downloads Original.zip (cached), extracts pak01_dir.vpk, then uses HLExtract.
        /// </summary>
        /// <returns>Path to extracted VPK folder containing items_game.txt and assets</returns>
        Task<string> GetExtractedOriginalAsync(Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null);
    }

    /// <summary>
    /// Service for downloading Original.zip with multi-CDN fallback.
    /// Original.zip contains pak01_dir.vpk which needs HLExtract to extract.
    /// Caches both the zip and extracted VPK to speed up subsequent runs.
    /// 
    /// CDN Priority: R2 (primary) → jsDelivr → GitHub Raw.
    /// Each CDN gets a 30-second stall timeout before falling back to the next.
    /// </summary>
    public sealed class OriginalVpkService : IOriginalVpkProvider
    {
        /// <summary>
        /// Asset path within the ModsPack repo for Original.zip.
        /// </summary>
        private const string OriginalZipAssetPath = "Assets/Original.zip";
        
        /// <summary>
        /// Seconds of zero data transfer before abandoning a CDN and trying the next.
        /// Aggressive enough to fail fast, generous enough to tolerate slow starts.
        /// </summary>
        private const int StallTimeoutSeconds = 30;
        
        private readonly HttpClient _httpClient;
        private readonly IVpkExtractor _extractor;
        private readonly string _cacheRoot;
        private readonly IAppLogger? _logger;

        public OriginalVpkService(HttpClient? httpClient = null, IVpkExtractor? extractor = null, IAppLogger? logger = null)
        {
            _httpClient = httpClient ?? HttpClientProvider.Client;
            _extractor = extractor ?? new VpkExtractorService(logger);
            // Use safe temp path for non-ASCII username compatibility
            _cacheRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "original");
            Directory.CreateDirectory(_cacheRoot);
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> GetExtractedOriginalAsync(
            Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            ct.ThrowIfCancellationRequested();
            
            // Define paths
            var zipPath = Path.Combine(_cacheRoot, "Original.zip");
            var zipExtractDir = Path.Combine(_cacheRoot, "zip_contents");
            var vpkExtractDir = Path.Combine(_cacheRoot, "vpk_extracted");

            // FAST PATH: Check if VPK is already fully extracted
            if (Directory.Exists(vpkExtractDir) && 
                File.Exists(Path.Combine(vpkExtractDir, "scripts", "items", "items_game.txt")))
            {
                log("Using cached base files...");
                return vpkExtractDir;
            }

            // Download if not cached
            if (!File.Exists(zipPath))
            {
                await DownloadWithCdnFallbackAsync(zipPath, log, ct, speedProgress, progress).ConfigureAwait(false);
                
                try
                {
                    var fileInfo = new FileInfo(zipPath);
                    if (fileInfo.Length < 1024)
                    {
                        log("Downloaded file too small, retrying...");
                        File.Delete(zipPath);
                        throw new Exception("Download incomplete - file too small");
                    }
                    
                    using var testZip = ZipFile.OpenRead(zipPath);
                    if (testZip.Entries.Count == 0)
                    {
                        log("Downloaded zip is empty, retrying...");
                        File.Delete(zipPath);
                        throw new Exception("Downloaded zip file is empty");
                    }
                }
                catch (InvalidDataException)
                {
                    log("Downloaded file is corrupted, please try again...");
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    throw new Exception("Downloaded file is corrupted.");
                }
            }
            else
            {
                log("Using cached Original.zip...");
            }

            ct.ThrowIfCancellationRequested();

            // Extract Original.zip
            var vpkPath = FindVpkFile(zipExtractDir);
            if (string.IsNullOrEmpty(vpkPath) || !File.Exists(vpkPath))
            {
                if (Directory.Exists(zipExtractDir))
                {
                    Directory.Delete(zipExtractDir, true);
                }
                Directory.CreateDirectory(zipExtractDir);

                log("Extracting Original.zip...");
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, zipExtractDir, overwriteFiles: true);
                }
                catch (Exception ex)
                {
                    log($"Zip extraction failed: {ex.Message}");
                    _logger?.Log($"OriginalVpkService zip extract error: {ex}");
                    throw;
                }
                
                vpkPath = FindVpkFile(zipExtractDir);
            }

            if (string.IsNullOrEmpty(vpkPath) || !File.Exists(vpkPath))
            {
                log("Base file appears corrupted, clearing cache...");
                try
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    if (Directory.Exists(zipExtractDir)) Directory.Delete(zipExtractDir, true);
                }
                catch { }
                throw new FileNotFoundException("pak01_dir.vpk not found in Original.zip.");
            }

            ct.ThrowIfCancellationRequested();

            // Extract VPK
            Directory.CreateDirectory(vpkExtractDir);
            
            if (Directory.Exists(vpkExtractDir) && 
                !File.Exists(Path.Combine(vpkExtractDir, "scripts", "items", "items_game.txt")))
            {
                Directory.Delete(vpkExtractDir, true);
                Directory.CreateDirectory(vpkExtractDir);
            }

            log("Extracting pak01_dir.vpk (this may take a while)...");
            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            
            if (!File.Exists(hlExtractPath))
            {
                throw new FileNotFoundException("HLExtract.exe not found");
            }

            var extractSuccess = await _extractor.ExtractAsync(
                hlExtractPath, vpkPath, vpkExtractDir, log, ct).ConfigureAwait(false);

            if (!extractSuccess)
            {
                try { Directory.Delete(vpkExtractDir, true); } catch { }
                throw new Exception("Failed to extract pak01_dir.vpk using HLExtract");
            }

            log("Base files ready!");
            return vpkExtractDir;
        }

        private string? FindVpkFile(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            var directPath = Path.Combine(folder, "pak01_dir.vpk");
            if (File.Exists(directPath)) return directPath;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "pak01_dir.vpk", SearchOption.AllDirectories))
                {
                    return file;
                }
                foreach (var file in Directory.EnumerateFiles(folder, "*.vpk", SearchOption.AllDirectories))
                {
                    return file;
                }
            }
            catch { }

            return null;
        }

        // ================================================================
        // CDN Fallback Download
        // ================================================================

        /// <summary>
        /// Download Original.zip with automatic CDN fallback.
        /// Tries each CDN in speed-ranked order (via SmartCdnSelector).
        /// Each CDN gets <see cref="StallTimeoutSeconds"/> to start delivering data;
        /// if it stalls, we cancel and move to the next CDN.
        /// </summary>
        private async Task DownloadWithCdnFallbackAsync(
            string destPath,
            Action<string> log,
            CancellationToken ct,
            IProgress<SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            // Get CDN URLs ordered by measured speed (fastest first)
            var cdnBaseUrls = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            int totalCdns = cdnBaseUrls.Length;
            string? lastError = null;

            for (int i = 0; i < totalCdns; i++)
            {
                ct.ThrowIfCancellationRequested();

                string cdnBase = cdnBaseUrls[i];
                string cdnUrl = $"{cdnBase.TrimEnd('/')}/{OriginalZipAssetPath}";
                string cdnName = GetCdnDisplayName(cdnBase);

                if (i > 0)
                {
                    log($"Trying alternate server ({i + 1}/{totalCdns}): {cdnName}...");
                    _logger?.Log($"OriginalVpkService: CDN fallback to {cdnName} ({cdnUrl})");
                    
                    // Reset progress for new CDN attempt
                    progress?.Report(0);
                    speedProgress?.Report(new SpeedMetrics { DownloadSpeed = "-- MB/S" });
                }

                try
                {
                    await DownloadFromSingleCdnAsync(cdnUrl, destPath, log, ct, speedProgress, progress)
                        .ConfigureAwait(false);

                    // Success — log which CDN worked
                    _logger?.Log($"OriginalVpkService: Download succeeded from {cdnName}");
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // User cancelled — propagate immediately, don't try next CDN
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    
                    // Clean up partial download before trying next CDN
                    try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }

                    // Report failure to SmartCdnSelector for future reordering
                    SmartCdnSelector.Instance.ReportFailure(cdnBase);

                    _logger?.Log($"OriginalVpkService: {cdnName} failed — {ex.Message}");

                    // If this is the last CDN, don't silently continue
                    if (i == totalCdns - 1)
                    {
                        log($"All servers failed. Last error: {ex.Message}");
                    }
                    else
                    {
                        log($"Server {cdnName} failed, switching to next...");
                    }
                }
            }

            // All CDNs exhausted
            _logger?.Log($"OriginalVpkService: All {totalCdns} CDNs failed. Last error: {lastError}");
            throw new HttpRequestException(
                $"Failed to download base files from all servers. " +
                $"Please check your internet connection and try again. ({lastError})");
        }

        /// <summary>
        /// Download from a single CDN URL with stall detection.
        /// Throws <see cref="TimeoutException"/> if no data is received for
        /// <see cref="StallTimeoutSeconds"/>, causing the caller to try the next CDN.
        /// </summary>
        private async Task DownloadFromSingleCdnAsync(
            string url,
            string destPath,
            Action<string> log,
            CancellationToken ct,
            IProgress<SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            // Overall timeout: 10 minutes per CDN (generous for slow connections)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            // Transient HTTP errors → throw to trigger next CDN
            if (RetryHelper.IsTransientStatusCode(response.StatusCode))
                throw new HttpRequestException($"Server returned {(int)response.StatusCode} {response.StatusCode}");

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var totalMb = totalBytes / 1024.0 / 1024.0;

            log($"Downloading base files ({totalMb:F1} MB)...");

            await using var contentStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            await using var progressStream = new ProgressStream(contentStream, speedProgress, totalBytes > 0 ? totalBytes : null);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            int lastPercentReported = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Stall detection: track time of last received data
            var lastActivityTime = DateTime.UtcNow;
            var stallWarningShown = false;

            // Stall monitor: cancels the download if no data for StallTimeoutSeconds
            // This triggers fallback to the next CDN instead of waiting minutes
            using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            var stallMonitorTask = Task.Run(async () =>
            {
                try
                {
                    while (!stallCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(3000, stallCts.Token).ConfigureAwait(false);
                        
                        var stalledSeconds = (DateTime.UtcNow - lastActivityTime).TotalSeconds;

                        // At 15s: show warning to user
                        if (stalledSeconds >= 15 && !stallWarningShown)
                        {
                            stallWarningShown = true;
                            log("Download stalled — will try alternate server shortly...");
                        }

                        // At StallTimeoutSeconds: cancel this CDN attempt
                        if (stalledSeconds >= StallTimeoutSeconds)
                        {
                            _logger?.Log($"OriginalVpkService: Stall detected after {stalledSeconds:F0}s on {url}");
                            stallCts.Cancel();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected — either stall triggered or download completed
                }
            }, stallCts.Token);

            try
            {
                while ((bytesRead = await progressStream.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), stallCts.Token).ConfigureAwait(false);
                    totalRead += bytesRead;
                    lastActivityTime = DateTime.UtcNow;

                    // Reset stall warning on resumed activity
                    if (stallWarningShown)
                    {
                        stallWarningShown = false;
                        log($"Download resumed — {totalRead / 1024.0 / 1024.0:F1} MB received so far");
                    }

                    if (totalBytes > 0)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes);
                        if (percent >= lastPercentReported + 1)
                        {
                            lastPercentReported = percent;
                            progress?.Report(percent);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Stall timeout or per-CDN timeout — not user cancellation
                // Throw a descriptive exception so the fallback loop can catch it
                throw new TimeoutException(
                    $"Download stalled for {StallTimeoutSeconds}s with {totalRead / 1024.0 / 1024.0:F1} MB received");
            }
            finally
            {
                // Stop stall monitor cleanly
                stallCts.Cancel();
                try { await stallMonitorTask.ConfigureAwait(false); } catch { }
            }

            // Verify we actually got data
            if (totalRead == 0)
            {
                throw new HttpRequestException("Server returned empty response body");
            }

            sw.Stop();
            var avgSpeed = SpeedCalculator.FormatSpeed(totalRead, sw.Elapsed.TotalSeconds);
            log($"Download complete ({totalRead / 1024.0 / 1024.0:F1} MB at {avgSpeed})");
            speedProgress?.Report(new SpeedMetrics { DownloadSpeed = "-- MB/S" });
        }

        /// <summary>
        /// Get a user-friendly display name for a CDN base URL.
        /// </summary>
        private static string GetCdnDisplayName(string cdnBaseUrl)
        {
            if (cdnBaseUrl.Contains("ardysamods.my.id")) return "R2 CDN";
            if (cdnBaseUrl.Contains("jsdelivr.net")) return "jsDelivr CDN";
            if (cdnBaseUrl.Contains("raw.githubusercontent.com")) return "GitHub";
            return "Server";
        }
    }
}

