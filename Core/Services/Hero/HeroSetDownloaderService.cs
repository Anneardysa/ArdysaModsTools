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
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Constants;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for hero set download and extraction.
    /// </summary>
    public interface IHeroSetDownloader
    {
        /// <summary>
        /// Downloads a hero set zip (with caching) and extracts to work folder.
        /// </summary>
        /// <param name="heroId">Hero internal ID (e.g., npc_dota_hero_abaddon)</param>
        /// <param name="setName">Set name from heroes.json</param>
        /// <param name="zipUrl">URL to download the set zip from</param>
        /// <param name="log">Progress logger</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Path to extracted work folder</returns>
        Task<string> DownloadAndExtractAsync(
            string heroId,
            string setName,
            string zipUrl,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);
    }

    /// <summary>
    /// Focused service for downloading and extracting hero set zips.
    /// Single responsibility: Download, cache, and extract set archives.
    /// </summary>
    public sealed class HeroSetDownloaderService : IHeroSetDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheRoot;
        private readonly IAppLogger? _logger;

        // Max parts to prevent infinite loops if something is wrong
        private const int MaxSplitParts = 50;

        public HeroSetDownloaderService(string? baseFolder = null, HttpClient? httpClient = null, IAppLogger? logger = null)
        {
            var bf = string.IsNullOrWhiteSpace(baseFolder) 
                ? Path.Combine(Path.GetTempPath(), "ArdysaSelectHero")
                : baseFolder!;
            _cacheRoot = Path.Combine(bf, "cache", "sets");
            Directory.CreateDirectory(_cacheRoot);
            
            _httpClient = httpClient ?? HttpClientProvider.Client;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> DownloadAndExtractAsync(
            string heroId,
            string setName,
            string zipUrl,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                throw new ArgumentNullException(nameof(heroId));
            if (string.IsNullOrWhiteSpace(zipUrl))
                throw new DownloadException(ErrorCodes.DL_INVALID_URL, "Download URL is required", zipUrl);

            ct.ThrowIfCancellationRequested();
            log($"Preparing set '{setName}' for {heroId}...");

            // Build cache paths
            var safeHeroId = MakeSafeFileName(heroId);
            var safeSetName = MakeSafeFileName(setName);
            var cacheFolder = Path.Combine(_cacheRoot, safeHeroId, safeSetName);
            Directory.CreateDirectory(cacheFolder);

            // Handle split zips (.zip.001) or standard zips
            bool isSplit = zipUrl.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase);

            string finalZipName;
            if (isSplit)
            {
                // If url is ".../model.zip.001", final name is "model.zip"
                var originalName = Path.GetFileName(new Uri(zipUrl).LocalPath);
                finalZipName = originalName.Replace(".001", "", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                finalZipName = Path.GetFileName(new Uri(zipUrl).LocalPath);
            }

            var localZipPath = Path.Combine(cacheFolder, finalZipName);

            // Download if not cached
            if (!File.Exists(localZipPath))
            {
                if (isSplit)
                {
                    log($"Downloading split archive...");
                    await DownloadSplitZipAsync(zipUrl, localZipPath, log, ct, speedProgress).ConfigureAwait(false);
                }
                else
                {
                    log($"Downloading set...");
                    await DownloadFileAsync(zipUrl, localZipPath, log, ct, speedProgress).ConfigureAwait(false);
                }
            }
            else
            {
                log($"Using cached set: {localZipPath}");
            }

            ct.ThrowIfCancellationRequested();

            // Extract to Windows temp folder under ArdysaSelectHero
            var selectHeroTemp = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero", "HeroSets");
            Directory.CreateDirectory(selectHeroTemp);
            
            var zipNameWithoutExt = Path.GetFileNameWithoutExtension(finalZipName);
            var workFolder = Path.Combine(selectHeroTemp, $"{safeHeroId}_{zipNameWithoutExt}");
            
            // Clean and recreate work folder
            if (Directory.Exists(workFolder))
            {
                Directory.Delete(workFolder, true);
            }
            Directory.CreateDirectory(workFolder);

            log($"Extracting...");
            try
            {
                ZipFile.ExtractToDirectory(localZipPath, workFolder, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                var dlEx = new DownloadException(ErrorCodes.DL_EXTRACT_FAILED,
                    $"Failed to extract set archive: {ex.Message}", ex, zipUrl);
                log($"Extraction failed: {ex.Message}");
                _logger?.Log($"[{dlEx.ErrorCode}] {dlEx.Message}");
                
                // If extraction local zip is corrupt, delete it so it redownloads next time
                try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }
                
                throw dlEx;
            }

            log("Extraction completed.");
            return workFolder;
        }

        private async Task DownloadSplitZipAsync(
            string startUrl, 
            string destPath, 
            Action<string> log, 
            CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            // We need to download .001, .002, etc. and merge them into destPath
            // We will download each part to a temp file, append to destPath, then delete temp file.
            
            // Ensure destination is empty
            if (File.Exists(destPath)) File.Delete(destPath);

            // Base URL without the extension part to easily verify pattern
            // startUrl ends with .001
            var baseUrl = startUrl.Substring(0, startUrl.Length - 3); // removes "001"
            
            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            long totalRead = 0; // Total bytes read across all parts
            long lastReportedRead = 0;
            TimeSpan lastReportTime = TimeSpan.Zero;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 1; i <= MaxSplitParts; i++)
            {
                var partExt = i.ToString("D3"); // 001, 002, 003...
                var partUrl = baseUrl + partExt;
                
                log($"Processing part {i} ({partExt})...");

                // We'll just try to download. If 404, we assume we're done (valid for standard multi-part hosted on sequential URLs)
                // BUT for the first part (i=1), it MUST exist.
                
                try
                {
                    using var response = await _httpClient.GetAsync(partUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    
                    if (i > 1 && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // End of parts
                        log($"Part {i} not found. Assuming end of split archive.");
                        break;
                    }
                    
                    response.EnsureSuccessStatusCode();

                    // Read content and write directly to the merge stream
                    await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    
                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await destStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;

                        // Report speed every 500ms
                        var elapsed = sw.Elapsed - lastReportTime;
                        if (elapsed.TotalMilliseconds >= 500)
                        {
                            string speedStr = SpeedCalculator.FormatSpeed(totalRead - lastReportedRead, elapsed.TotalSeconds);
                            string details = $"{totalRead / 1024 / 1024} MB (part {i})";
                            speedProgress?.Report(new SpeedMetrics { DownloadSpeed = speedStr, ProgressDetails = details });
                            
                            lastReportedRead = totalRead;
                            lastReportTime = sw.Elapsed;
                        }
                    }
                    
                    log($"Part {i} merged.");
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && i > 1)
                {
                    // Double check catch for fail-safe
                    log("End of split parts detected.");
                    break;
                }
                catch (Exception ex)
                {
                    if (i == 1)
                    {
                        throw new DownloadException(ErrorCodes.DL_FILE_NOT_FOUND,
                            $"Failed to download first part of split zip", ex, startUrl);
                    }
                    else
                    {
                        log($"Warning: stopped at part {i} due to error: {ex.Message}");
                        break; // Stop and assume we have what we have? Or fail? Usually fail if specific part missing.
                               // For now let's hope it's just end of file if it's a 404-like error not caught above.
                    }
                }
            }
        }

        private async Task DownloadFileAsync(
            string url, 
            string destPath, 
            Action<string> log, 
            CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            bool downloadComplete = false;
            string successfulUrl = url;
            
            // Get all CDN URLs to try in priority order (R2 → jsDelivr → GitHub Raw)
            var cdnUrls = GetCdnUrlsInPriorityOrder(url);
            Exception? lastException = null;
            
            foreach (var cdnUrl in cdnUrls)
            {
                ct.ThrowIfCancellationRequested();
                
                try
                {
                    var cdnName = GetCdnDisplayName(cdnUrl);
                    log($"Trying {cdnName}...");
                    _logger?.Log($"Attempting download from: {cdnUrl}");
                    
                    await DownloadFromUrlAsync(cdnUrl, destPath, log, ct, speedProgress).ConfigureAwait(false);
                    
                    // Success!
                    downloadComplete = true;
                    successfulUrl = cdnUrl;
                    log($"Download completed from {cdnName}.");
                    _logger?.Log($"Successfully downloaded from {cdnName}: {cdnUrl}");
                    return;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    // 403 Forbidden - try next CDN (jsDelivr blocks files > 20MB)
                    log($"CDN blocked (403). Trying next source...");
                    _logger?.Log($"HTTP 403 from {cdnUrl}, trying next CDN");
                    lastException = ex;
                    continue;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 Not Found - file doesn't exist on this CDN
                    _logger?.Log($"HTTP 404 from {cdnUrl}, trying next CDN");
                    lastException = ex;
                    continue;
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    // Timeout - try next CDN
                    log($"Request timed out. Trying next source...");
                    _logger?.Log($"Timeout from {cdnUrl}, trying next CDN");
                    lastException = ex;
                    continue;
                }
                catch (OperationCanceledException)
                {
                    // User cancelled - don't retry
                    throw;
                }
                catch (Exception ex)
                {
                    // Other error - try next CDN
                    _logger?.Log($"Error from {cdnUrl}: {ex.Message}");
                    lastException = ex;
                    continue;
                }
                finally
                {
                    // Clean up partial download if not complete
                    if (!downloadComplete && File.Exists(destPath))
                    {
                        try { File.Delete(destPath); } catch { }
                    }
                }
            }
            
            // All CDNs failed
            var dlEx = new DownloadException(ErrorCodes.DL_NETWORK_ERROR,
                $"Download failed from all CDNs: {lastException?.Message ?? "Unknown error"}", 
                lastException, 
                url);
            log($"Download failed: {dlEx.Message}");
            _logger?.Log($"[{dlEx.ErrorCode}] All CDNs failed for: {url}");
            throw dlEx;
        }
        
        /// <summary>
        /// Downloads from a specific URL with retry logic.
        /// </summary>
        private async Task DownloadFromUrlAsync(
            string url,
            string destPath,
            Action<string> log,
            CancellationToken ct,
            IProgress<SpeedMetrics>? speedProgress = null)
        {
            await RetryHelper.ExecuteAsync(async () =>
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var progressStream = new ProgressStream(contentStream, speedProgress, totalBytes > 0 ? totalBytes : null);
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                
                var buffer = new byte[81920];
                int bytesRead;
                
                while ((bytesRead = await progressStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                }
            },
            maxAttempts: 2,
            initialDelayMs: 500,
            onRetry: (attempt, ex) => log($"Retry {attempt}/2: {ex.Message}"),
            ct: ct).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Gets a display-friendly name for a CDN based on its URL.
        /// </summary>
        private static string GetCdnDisplayName(string url)
        {
            if (url.Contains("ardysamods.my.id") || url.Contains("r2.dev"))
                return "R2 CDN";
            if (url.Contains("jsdelivr.net"))
                return "jsDelivr CDN";
            if (url.Contains("githubusercontent.com"))
                return "GitHub";
            return "CDN";
        }
        
        /// <summary>
        /// Gets all CDN URLs for an asset in priority order: R2 → jsDelivr → GitHub Raw.
        /// This ensures large files (>20MB) can be downloaded from R2, which has no size limit.
        /// </summary>
        /// <param name="originalUrl">Original asset URL (can be from any CDN)</param>
        /// <returns>Array of URLs to try in priority order</returns>
        private static string[] GetCdnUrlsInPriorityOrder(string originalUrl)
        {
            // Extract the asset path from the URL
            var assetPath = CdnConfig.ExtractAssetPath(originalUrl);
            
            if (string.IsNullOrEmpty(assetPath))
            {
                // Can't extract asset path, just return original URL
                return new[] { originalUrl };
            }
            
            var urls = new List<string>();
            
            // Priority 1: R2 CDN (no file size limit)
            if (CdnConfig.IsR2Enabled)
            {
                urls.Add($"{CdnConfig.R2BaseUrl}/{assetPath}");
            }
            
            // Priority 2: jsDelivr (fast but has 20MB limit)
            urls.Add($"{CdnConfig.JsDelivrBaseUrl}/{assetPath}");
            
            // Priority 3: Raw GitHub (slowest but always works)
            urls.Add($"{CdnConfig.GitHubRawBaseUrl}/{assetPath}");
            
            return urls.ToArray();
        }
        
        private static string MakeSafeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var result = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = Array.IndexOf(invalidChars, input[i]) >= 0 ? '_' : input[i];
            }
            return new string(result);
        }
    }
}

