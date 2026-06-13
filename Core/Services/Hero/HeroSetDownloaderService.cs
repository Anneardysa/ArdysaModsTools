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
        private readonly Func<string, CancellationToken, Task<AssetHashEntry?>> _hashResolver;

        // Max parts to prevent infinite loops if something is wrong
        private const int MaxSplitParts = 50;

        public HeroSetDownloaderService(
            string? baseFolder = null,
            HttpClient? httpClient = null,
            IAppLogger? logger = null,
            Func<string, CancellationToken, Task<AssetHashEntry?>>? hashResolver = null)
        {
            var bf = string.IsNullOrWhiteSpace(baseFolder)
                // Use safe temp path for non-ASCII username compatibility
                ? Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero")
                : baseFolder!;
            _cacheRoot = Path.Combine(bf, "cache", "sets");
            Directory.CreateDirectory(_cacheRoot);

            _httpClient = httpClient ?? HttpClientProvider.Client;
            _logger = logger;
            // Default to the live manifest; tests inject a resolver to avoid network access.
            _hashResolver = hashResolver ?? AssetHashManifestService.Instance.GetExpectedAsync;
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

            // Resolve the expected hash for the final (merged) asset path. For split archives the
            // manifest key is the merged ".zip" (the ".001" suffix is dropped), matching the file
            // the client assembles and verifies.
            string? assetPath = CdnConfig.ExtractAssetPath(zipUrl);
            if (isSplit && assetPath != null)
                assetPath = assetPath.Replace(".001", "", StringComparison.OrdinalIgnoreCase);
            AssetHashEntry? expected = assetPath != null
                ? await _hashResolver(assetPath, ct).ConfigureAwait(false)
                : null;

            // Download if not cached
            bool verifiedDuringDownload = false;
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
                    // ResumableDownloadService verifies per-CDN so a bad copy falls through to the
                    // next CDN; no need to re-hash the fresh single-file download afterwards.
                    await DownloadFileAsync(zipUrl, localZipPath, log, ct, speedProgress, expected).ConfigureAwait(false);
                    verifiedDuringDownload = expected != null;
                }
            }
            else
            {
                log($"Using cached set: {localZipPath}");
            }

            ct.ThrowIfCancellationRequested();

            // Integrity gate for merged split archives and cached files (ADR-0010). A mismatch
            // here also invalidates a stale cache entry (e.g., after the manifest is updated).
            if (expected != null && !verifiedDuringDownload)
            {
                bool ok = await AssetHashVerifier.VerifyFileAsync(localZipPath, expected, ct).ConfigureAwait(false);
                if (!ok)
                {
                    try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }
                    var dlEx = new DownloadException(ErrorCodes.DL_HASH_MISMATCH,
                        $"Integrity check failed for set '{setName}' ({heroId}). The file was removed; please retry.", zipUrl);
                    _logger?.Log($"[{dlEx.ErrorCode}] {dlEx.Message}");
                    throw dlEx;
                }
                log("Integrity verified.");
            }

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

            long lastReportedRead = 0;
            TimeSpan lastReportTime = TimeSpan.Zero;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 1; i <= MaxSplitParts; i++)
            {
                var partExt = i.ToString("D3"); // 001, 002, 003...
                log($"Processing part {i} ({partExt})...");

                bool partDownloaded = false;
                bool endOfParts = false;

                // Byte offset in destStream where this part begins. Each attempt truncates back
                // to here so a failed/partial part is never double-appended into the merge.
                long partStartOffset = destStream.Position;

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
                    int partNumber = i;

                    try
                    {
                        if (cdnIdx != preferredCdnIndex)
                            log($"Trying {cdnName}...");

                        // Retry transient failures (5xx/429, mid-stream I/O) on this CDN before
                        // falling through to the next one.
                        await DownloadRetryPolicy.ExecuteWithRetryAsync<bool>(async token =>
                        {
                            // Reset to the part boundary so retries don't append duplicate bytes.
                            destStream.SetLength(partStartOffset);
                            destStream.Position = partStartOffset;

                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                            cts.CancelAfter(TimeSpan.FromSeconds(CdnConfig.TimeoutSeconds));

                            using var response = await _httpClient.GetAsync(partUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                            if (!response.IsSuccessStatusCode)
                            {
                                if (Core.Helpers.RetryHelper.IsTransientStatusCode(response.StatusCode))
                                    throw new TransientDownloadException($"HTTP {(int)response.StatusCode}", DownloadRetryPolicy.GetRetryAfter(response));
                                throw new HttpRequestException($"HTTP {(int)response.StatusCode}", null, response.StatusCode);
                            }

                            long? declared = response.Content.Headers.ContentLength;

                            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);

                            var buffer = new byte[81920];
                            int bytesRead;
                            long partBytes = 0;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                            {
                                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
                                partBytes += bytesRead;

                                long totalRead = partStartOffset + partBytes;
                                var elapsed = sw.Elapsed - lastReportTime;
                                if (elapsed.TotalMilliseconds >= 500)
                                {
                                    string speedStr = SpeedCalculator.FormatSpeed(totalRead - lastReportedRead, elapsed.TotalSeconds);
                                    string details = $"{totalRead / 1024 / 1024} MB (part {partNumber})";
                                    speedProgress?.Report(new SpeedMetrics { DownloadSpeed = speedStr, ProgressDetails = details });

                                    lastReportedRead = totalRead;
                                    lastReportTime = sw.Elapsed;
                                }
                            }

                            // Size-only integrity check: reject a truncated part.
                            if (declared.HasValue && declared.Value > 0 && partBytes != declared.Value)
                                throw new TransientDownloadException($"Truncated part {partNumber}: got {partBytes} of {declared.Value} bytes");

                            await destStream.FlushAsync(cts.Token).ConfigureAwait(false);
                            return true;
                        }, log: null, ct).ConfigureAwait(false);

                        // Success! Set CDN affinity for future parts
                        preferredCdnIndex = cdnIdx;
                        partDownloaded = true;
                        log($"Part {partNumber} merged via {cdnName}.");
                        break; // Move to next part
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw; // User cancelled
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && partNumber > 1)
                    {
                        // A missing part beyond the first means we've reached the end of the archive.
                        destStream.SetLength(partStartOffset);
                        destStream.Position = partStartOffset;
                        log("End of split parts detected.");
                        endOfParts = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Reset the merge stream to the part boundary before trying the next CDN.
                        destStream.SetLength(partStartOffset);
                        destStream.Position = partStartOffset;
                        var cdnBase = CdnConfig.ExtractBaseUrl(partUrl) ?? partUrl;
                        SmartCdnSelector.Instance.ReportFailure(cdnBase);
                        _logger?.Log($"Split part {partNumber} failed from {cdnName}: {ex.Message}");
                        // Try next CDN
                    }
                }

                if (endOfParts)
                    return; // All parts merged.

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
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            AssetHashEntry? expected = null)
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
                    ct,
                    expected
                ).ConfigureAwait(false);
            }
            catch (DownloadException)
            {
                // Preserve specific error codes (e.g., DL_HASH_MISMATCH) from the download layer.
                throw;
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

