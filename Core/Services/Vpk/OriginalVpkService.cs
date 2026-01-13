using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
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
    /// Service for downloading Original.zip from GitHub.
    /// Original.zip contains pak01_dir.vpk which needs HLExtract to extract.
    /// Caches both the zip and extracted VPK to speed up subsequent runs.
    /// </summary>
    public sealed class OriginalVpkService : IOriginalVpkProvider
    {
        // URL now loaded from environment configuration
        private static string OriginalZipUrl => EnvironmentConfig.BuildDownloadUrl("Assets/Original.zip");
        
        private readonly HttpClient _httpClient;
        private readonly IVpkExtractor _extractor;
        private readonly string _cacheRoot;
        private readonly ILogger? _logger;

        public OriginalVpkService(HttpClient? httpClient = null, IVpkExtractor? extractor = null, ILogger? logger = null)
        {
            _httpClient = httpClient ?? HttpClientProvider.Client;
            _extractor = extractor ?? new VpkExtractorService(logger);
            _cacheRoot = Path.Combine(Path.GetTempPath(), "ArdysaSelectHero", "cache", "original");
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
                await DownloadFileWithProgressAsync(OriginalZipUrl, zipPath, log, ct, speedProgress, progress).ConfigureAwait(false);
                
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

        private async Task DownloadFileWithProgressAsync(
            string url, 
            string destPath, 
            Action<string> log, 
            CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            IProgress<int>? progress = null)
        {
            try
            {
                // Use retry logic for transient network failures
                await RetryHelper.ExecuteAsync(async () =>
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));
                    
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                        .ConfigureAwait(false);
                    
                    // Check for transient status codes that should trigger retry
                    if (RetryHelper.IsTransientStatusCode(response.StatusCode))
                        throw new HttpRequestException($"Server returned {response.StatusCode}");
                    
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var totalMb = totalBytes / 1024.0 / 1024.0;
                    
                    log($"Downloading base files ({totalMb:F1} MB)...");

                    await using var contentStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
                    await using var progressStream = new ArdysaModsTools.Core.Helpers.ProgressStream(contentStream, speedProgress, totalBytes > 0 ? totalBytes : null);
                    await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
                    
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;
                    int lastPercentReported = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    
                    while ((bytesRead = await progressStream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), timeoutCts.Token).ConfigureAwait(false);
                        totalRead += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            int percent = (int)(totalRead * 100 / totalBytes);
                            if (percent >= lastPercentReported + 1) // Report every 1% for smoothness
                            {
                                lastPercentReported = percent;
                                progress?.Report(percent);
                            }
                        }
                    }
                    
                    sw.Stop();
                    var avgSpeed = SpeedCalculator.FormatSpeed(totalRead, sw.Elapsed.TotalSeconds);
                    log($"Download complete ({totalRead / 1024.0 / 1024.0:F1} MB at {avgSpeed})");
                    speedProgress?.Report(new SpeedMetrics { DownloadSpeed = "-- MB/S" }); // Reset to default on complete
                },
                maxAttempts: 3,
                initialDelayMs: 1000, // Longer initial delay for large files
                onRetry: (attempt, ex) => log($"Retry {attempt}/3: {ex.Message}"),
                ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"OriginalVpkService download error: {ex}");
                if (File.Exists(destPath)) File.Delete(destPath);
                throw;
            }
        }
    }
}
