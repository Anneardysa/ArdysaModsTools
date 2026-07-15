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
using System.Linq;
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
    public interface IOriginalVpkProvider
    {
        Task<string> GetExtractedOriginalAsync(Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null);
    }

    public sealed class OriginalVpkService : IOriginalVpkProvider
    {
        private const string OriginalZipAssetPath = "Assets/Original.zip";

        private const string BaseReadyMarkerSuffix = ".ready2";



        private readonly HttpClient _httpClient;
        private readonly IVpkExtractor _extractor;
        private readonly string _cacheRoot;
        private readonly IAppLogger? _logger;

        public OriginalVpkService(HttpClient? httpClient = null, IVpkExtractor? extractor = null, IAppLogger? logger = null)
        {
            _httpClient = httpClient ?? HttpClientProvider.Client;
            _extractor = extractor ?? new VpkExtractorService(logger);
            _cacheRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "original");
            Directory.CreateDirectory(_cacheRoot);
            _logger = logger;
        }

        public async Task<string> GetExtractedOriginalAsync(
            Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            ct.ThrowIfCancellationRequested();
            
            var zipPath = Path.Combine(_cacheRoot, "Original.zip");
            var zipExtractDir = Path.Combine(_cacheRoot, "zip_contents");
            var vpkExtractDir = Path.Combine(_cacheRoot, "vpk_extracted");
            var baseReadyMarker = vpkExtractDir + BaseReadyMarkerSuffix;

            if (Directory.Exists(vpkExtractDir) && File.Exists(baseReadyMarker))
            {
                log("Using cached base files...");
                return vpkExtractDir;
            }

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

            Directory.CreateDirectory(vpkExtractDir);
            
            if (Directory.Exists(vpkExtractDir) && !File.Exists(baseReadyMarker))
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
                hlExtractPath, vpkPath, vpkExtractDir, log, ct, null, requireItemsGame: false).ConfigureAwait(false);

            if (!extractSuccess)
            {
                try { Directory.Delete(vpkExtractDir, true); } catch { }
                try { if (Directory.Exists(zipExtractDir)) Directory.Delete(zipExtractDir, true); } catch { }
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                throw new Exception("Failed to extract pak01_dir.vpk using HLExtract");
            }

            try { File.WriteAllText(baseReadyMarker, string.Empty); }
            catch (Exception ex) { _logger?.Log($"OriginalVpkService: failed to write base-ready marker: {ex.Message}"); }

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


        private async Task DownloadWithCdnFallbackAsync(
            string destPath,
            Action<string> log,
            CancellationToken ct,
            IProgress<SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            var cdnBaseUrls = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            var urls = cdnBaseUrls
                .Select(b => $"{b.TrimEnd('/')}/{OriginalZipAssetPath}")
                .ToArray();
            
            _logger?.Log($"Starting resumable download of Original.zip ({urls.Length} CDN sources)");

            AssetHashEntry? expected = await AssetHashManifestService.Instance
                .GetExpectedAsync(OriginalZipAssetPath, ct).ConfigureAwait(false);

            await ResumableDownloadService.Instance.DownloadAsync(
                urls,
                destPath,
                log,
                progress,
                speedProgress,
                ct,
                expected
            ).ConfigureAwait(false);
        }

        private static string GetCdnDisplayName(string cdnBaseUrl)
        {
            if (cdnBaseUrl.Contains("ardysamods.my.id")) return "R2 CDN";
            if (cdnBaseUrl.Contains("jsdelivr.net")) return "jsDelivr CDN";
            if (cdnBaseUrl.Contains("raw.githubusercontent.com")) return "GitHub";
            return "Server";
        }
    }
}

