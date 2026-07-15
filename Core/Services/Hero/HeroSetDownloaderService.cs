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
using ArdysaModsTools.Core.Services.Security;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace ArdysaModsTools.Core.Services
{
    public interface IHeroSetDownloader
    {
        Task<string> DownloadAndExtractAsync(
            string heroId,
            string setName,
            string zipUrl,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action? onEncryptedDetected = null);
    }

    public sealed class HeroSetDownloaderService : IHeroSetDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheRoot;
        private readonly IAppLogger? _logger;
        private readonly Func<string, CancellationToken, Task<AssetHashEntry?>> _hashResolver;

        private const int MaxSplitParts = 50;

        public HeroSetDownloaderService(
            string? baseFolder = null,
            HttpClient? httpClient = null,
            IAppLogger? logger = null,
            Func<string, CancellationToken, Task<AssetHashEntry?>>? hashResolver = null)
        {
            var bf = string.IsNullOrWhiteSpace(baseFolder)
                ? Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero")
                : baseFolder!;
            _cacheRoot = Path.Combine(bf, "cache", "sets");
            Directory.CreateDirectory(_cacheRoot);
            Core.Helpers.SafeTempPathHelper.HideDirectory(bf);

            _httpClient = httpClient ?? HttpClientProvider.Client;
            _logger = logger;
            _hashResolver = hashResolver ?? AssetHashManifestService.Instance.GetExpectedAsync;
        }

        public async Task<string> DownloadAndExtractAsync(
            string heroId,
            string setName,
            string zipUrl,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action? onEncryptedDetected = null)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                throw new ArgumentNullException(nameof(heroId));
            if (string.IsNullOrWhiteSpace(zipUrl))
                throw new DownloadException(ErrorCodes.DL_INVALID_URL, "Download URL is required", zipUrl);

            ct.ThrowIfCancellationRequested();

            var safeHeroId = MakeSafeFileName(heroId);
            var safeSetName = MakeSafeFileName(setName);
            var cacheFolder = Path.Combine(_cacheRoot, safeHeroId, safeSetName);
            Directory.CreateDirectory(cacheFolder);

            bool isSplit = zipUrl.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase);

            string finalZipName;
            if (isSplit)
            {
                var originalName = Path.GetFileName(new Uri(zipUrl).LocalPath);
                finalZipName = originalName.Replace(".001", "", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                finalZipName = Path.GetFileName(new Uri(zipUrl).LocalPath);
            }

            var localZipPath = Path.Combine(cacheFolder, finalZipName);

            string? assetPath = CdnConfig.ExtractAssetPath(zipUrl);
            if (isSplit && assetPath != null)
                assetPath = assetPath.Replace(".001", "", StringComparison.OrdinalIgnoreCase);
            AssetHashEntry? expected = assetPath != null
                ? await _hashResolver(assetPath, ct).ConfigureAwait(false)
                : null;

            bool verifiedDuringDownload = false;
            if (!File.Exists(localZipPath))
            {
                if (isSplit)
                {
                    await DownloadSplitZipAsync(zipUrl, localZipPath, log, ct, speedProgress).ConfigureAwait(false);
                }
                else
                {
                    await DownloadFileAsync(zipUrl, localZipPath, log, ct, speedProgress, expected).ConfigureAwait(false);
                    verifiedDuringDownload = expected != null;
                }
            }
            else
            {
                log("Using cached set.");
            }

            ct.ThrowIfCancellationRequested();

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

            var selectHeroTemp = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "HeroSets");
            Directory.CreateDirectory(selectHeroTemp);
            SafeTempPathHelper.HideDirectory(selectHeroTemp);
            
            var zipNameWithoutExt = Path.GetFileNameWithoutExtension(finalZipName);
            var workFolder = Path.Combine(selectHeroTemp, $"{safeHeroId}_{zipNameWithoutExt}");
            
            if (Directory.Exists(workFolder))
            {
                Directory.Delete(workFolder, true);
            }
            Directory.CreateDirectory(workFolder);

            string extractSource = localZipPath;
            string? decryptedTemp = null;
            if (AssetCipher.IsEncrypted(localZipPath))
            {
                onEncryptedDetected?.Invoke();

                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    var dlEx = new DownloadException(ErrorCodes.DL_EXTRACT_FAILED,
                        $"Encrypted set '{setName}' could not be prepared (unresolved asset path).", zipUrl);
                    _logger?.Log($"[{dlEx.ErrorCode}] {dlEx.Message}");
                    throw dlEx;
                }

                try
                {
                    decryptedTemp = await AssetCipher.DecryptToTempAsync(localZipPath, assetPath!, ct).ConfigureAwait(false);
                    extractSource = decryptedTemp;
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }
                    var dlEx = new DownloadException(ErrorCodes.DL_EXTRACT_FAILED,
                        $"Set '{setName}' could not be prepared — it will be re-downloaded on retry.", ex, zipUrl);
                    log("Set file invalid — it will be re-downloaded on retry.");
                    _logger?.Log($"[{dlEx.ErrorCode}] Decrypt failed for set '{setName}': {ex.Message}");
                    throw dlEx;
                }
            }

            try
            {
                ZipFile.ExtractToDirectory(extractSource, workFolder, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                var dlEx = new DownloadException(ErrorCodes.DL_EXTRACT_FAILED,
                    $"Failed to extract set archive: {ex.Message}", ex, zipUrl);
                log($"Extraction failed: {ex.Message}");
                _logger?.Log($"[{dlEx.ErrorCode}] {dlEx.Message}");

                try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }

                throw dlEx;
            }
            finally
            {
                if (decryptedTemp != null)
                {
                    try { if (File.Exists(decryptedTemp)) File.Delete(decryptedTemp); } catch { }
                }
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
            if (File.Exists(destPath)) File.Delete(destPath);

            var cdnUrls = GetCdnUrlsInPriorityOrder(startUrl);
            
            var cdnBases = new string[cdnUrls.Length];
            for (int c = 0; c < cdnUrls.Length; c++)
            {
                cdnBases[c] = cdnUrls[c].Substring(0, cdnUrls[c].Length - 3);
            }
            
            int preferredCdnIndex = 0;

            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            long lastReportedRead = 0;
            TimeSpan lastReportTime = TimeSpan.Zero;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 1; i <= MaxSplitParts; i++)
            {
                var partExt = i.ToString("D3");
                log($"Processing part {i} ({partExt})...");

                bool partDownloaded = false;
                bool endOfParts = false;

                long partStartOffset = destStream.Position;

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

                        await DownloadRetryPolicy.ExecuteWithRetryAsync<bool>(async token =>
                        {
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

                            if (declared.HasValue && declared.Value > 0 && partBytes != declared.Value)
                                throw new TransientDownloadException($"Truncated part {partNumber}: got {partBytes} of {declared.Value} bytes");

                            await destStream.FlushAsync(cts.Token).ConfigureAwait(false);
                            return true;
                        }, log: null, ct).ConfigureAwait(false);

                        preferredCdnIndex = cdnIdx;
                        partDownloaded = true;
                        log($"Part {partNumber} merged via {cdnName}.");
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && partNumber > 1)
                    {
                        destStream.SetLength(partStartOffset);
                        destStream.Position = partStartOffset;
                        log("End of split parts detected.");
                        endOfParts = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        destStream.SetLength(partStartOffset);
                        destStream.Position = partStartOffset;
                        var cdnBase = CdnConfig.ExtractBaseUrl(partUrl) ?? partUrl;
                        SmartCdnSelector.Instance.ReportFailure(cdnBase);
                        _logger?.Log($"Split part {partNumber} failed from {cdnName}: {ex.Message}");
                    }
                }

                if (endOfParts)
                    return;

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
        
        private static string[] GetCdnUrlsInPriorityOrder(string originalUrl)
        {
            var assetPath = CdnConfig.ExtractAssetPath(originalUrl);
            
            if (string.IsNullOrEmpty(assetPath))
            {
                return new[] { originalUrl };
            }
            
            var orderedBases = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            var urls = orderedBases
                .Select(baseUrl => $"{baseUrl.TrimEnd('/')}/{assetPath}")
                .ToArray();

            if (urls.Length > 0)
                return urls;

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

