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
using ArdysaModsTools.Core.Services.Cdn;
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
                // Use safe temp path for non-ASCII username compatibility
                ? Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero")
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
            // Extract to safe temp folder for compatibility with Chinese usernames
            var selectHeroTemp = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "HeroSets");
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
            if (File.Exists(destPath)) File.Delete(destPath);

            // Build CDN URL variants for the base split URL
            var cdnUrls = GetCdnUrlsInPriorityOrder(startUrl);
            
            // Extract the base path (remove ".001" extension) for each CDN variant
            var cdnBases = new string[cdnUrls.Length];
            for (int c = 0; c < cdnUrls.Length; c++)
            {
                cdnBases[c] = cdnUrls[c].Substring(0, cdnUrls[c].Length - 3); // removes "001"
            }
            
            // CDN affinity: once a CDN succeeds for a part, prefer it for subsequent parts
            int preferredCdnIndex = 0;
            
            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            long totalRead = 0;
            long lastReportedRead = 0;
            TimeSpan lastReportTime = TimeSpan.Zero;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 1; i <= MaxSplitParts; i++)
            {
                var partExt = i.ToString("D3"); // 001, 002, 003...
                log($"Processing part {i} ({partExt})...");
                
                bool partDownloaded = false;
                
                // Build ordered CDN list: preferred first, then the rest
                var orderedIndices = new List<int> { preferredCdnIndex };
                for (int c = 0; c < cdnBases.Length; c++)
                {
                    if (c != preferredCdnIndex) orderedIndices.Add(c);
                }

                foreach (var cdnIdx in orderedIndices)
                {
                    ct.ThrowIfCancellationRequested();
                    var partUrl = cdnBases[cdnIdx] + partExt;
                    var cdnName = GetCdnDisplayName(partUrl);
                    
                    try
                    {
                        if (cdnIdx != preferredCdnIndex)
                            log($"Trying {cdnName}...");
                        
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(CdnConfig.TimeoutSeconds));
                        
                        using var response = await _httpClient.GetAsync(partUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                        
                        if (i > 1 && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            log($"Part {i} not found. Assuming end of split archive.");
                            return; // End of parts — all done
                        }
                        
                        response.EnsureSuccessStatusCode();

                        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                        
                        var buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await destStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                            totalRead += bytesRead;

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
                        
                        // Success! Set CDN affinity for future parts
                        preferredCdnIndex = cdnIdx;
                        partDownloaded = true;
                        log($"Part {i} merged via {cdnName}.");
                        break; // Move to next part
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw; // User cancelled
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && i > 1)
                    {
                        log("End of split parts detected.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Split part {i} failed from {cdnName}: {ex.Message}");
                        // Try next CDN
                    }
                }
                
                if (!partDownloaded)
                {
                    if (i == 1)
                    {
                        throw new DownloadException(ErrorCodes.DL_FILE_NOT_FOUND,
                            $"Failed to download first part of split zip from all CDNs", startUrl);
                    }
                    else
                    {
                        log($"Warning: stopped at part {i} — all CDNs failed.");
                        break;
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
            // Build all CDN URLs to try in priority order (R2 → jsDelivr → GitHub Raw)
            var cdnUrls = GetCdnUrlsInPriorityOrder(url);
            
            _logger?.Log($"Starting resumable download: {url} → {destPath}");
            
            try
            {
                await ResumableDownloadService.Instance.DownloadAsync(
                    cdnUrls,
                    destPath,
                    log,
                    progress: null,
                    speedProgress,
                    ct
                ).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var dlEx = new DownloadException(ErrorCodes.DL_NETWORK_ERROR,
                    $"Download failed from all CDNs: {ex.Message}", ex, url);
                _logger?.Log($"[{dlEx.ErrorCode}] {dlEx.Message}");
                throw dlEx;
            }
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
        /// Gets all CDN URLs for an asset ordered by benchmark speed (fastest first for this user).
        /// Uses SmartCdnSelector results; falls back to static CdnConfig order if not initialized.
        /// </summary>
        /// <param name="originalUrl">Original asset URL (can be from any CDN)</param>
        /// <returns>Array of URLs to try in speed-optimized order</returns>
        private static string[] GetCdnUrlsInPriorityOrder(string originalUrl)
        {
            // Extract the asset path from the URL
            var assetPath = CdnConfig.ExtractAssetPath(originalUrl);
            
            if (string.IsNullOrEmpty(assetPath))
            {
                // Can't extract asset path, just return original URL
                return new[] { originalUrl };
            }
            
            // Use benchmark-ordered CDNs (fastest first for this user)
            var orderedBases = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            var urls = orderedBases
                .Select(baseUrl => $"{baseUrl.TrimEnd('/')}/{assetPath}")
                .ToArray();

            if (urls.Length > 0)
                return urls;

            // SmartCdnSelector not initialized — use static CdnConfig order
            return CdnConfig.GetCdnBaseUrls()
                .Select(baseUrl => $"{baseUrl.TrimEnd('/')}/{assetPath}")
                .ToArray();
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

