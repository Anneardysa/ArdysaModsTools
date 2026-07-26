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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public class LocalizationPatcherService
    {
        private readonly IAppLogger? _logger;
        private readonly HttpClient _http = HttpClientProvider.Client;

        private const string RemoteDir = "remote/localization/";

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArdysaModsTools", "cache", "localization");

        private static readonly string HashManifestPath = Path.Combine(CacheDir, "hashes.json");

        private static readonly string[] LocalizationFiles = new[]
        {
            "dota_brazilian.txt",
            "dota_bulgarian.txt",
            "dota_czech.txt",
            "dota_danish.txt",
            "dota_dutch.txt",
            "dota_english.txt",
            "dota_finnish.txt",
            "dota_french.txt",
            "dota_german.txt",
            "dota_greek.txt",
            "dota_hungarian.txt",
            "dota_italian.txt",
            "dota_japanese.txt",
            "dota_koreana.txt",
            "dota_latam.txt",
            "dota_norwegian.txt",
            "dota_polish.txt",
            "dota_portuguese.txt",
            "dota_romanian.txt",
            "dota_russian.txt",
            "dota_schinese.txt",
            "dota_spanish.txt",
            "dota_swedish.txt",
            "dota_tchinese.txt",
            "dota_thai.txt",
            "dota_turkish.txt",
            "dota_ukrainian.txt",
            "dota_vietnamese.txt"
        };

        public static int FileCount => LocalizationFiles.Length;

        public LocalizationPatcherService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> PatchLocalizationAsync(
            string extractDir,
            Action<string>? log = null,
            CancellationToken ct = default,
            Action<int, int>? onFileDone = null)
        {
            if (string.IsNullOrWhiteSpace(extractDir))
                return false;

            try
            {
                Directory.CreateDirectory(CacheDir);
            }
            catch (Exception ex)
            {
                LogDiag($"[LOC] Failed to create cache directory: {ex.Message}");
            }

            string localizationDir = Path.Combine(extractDir, "resource", "localization");
            try
            {
                Directory.CreateDirectory(localizationDir);
            }
            catch (Exception ex)
            {
                LogDiag($"[LOC] Failed to create localization directory: {ex.Message}");
                return false;
            }

            var hashManifest = LoadHashManifest();

            int successCount = 0;
            int cachedCount = 0;
            int totalFiles = LocalizationFiles.Length;
            var failed = new ConcurrentBag<string>();

            log?.Invoke($"Downloading {totalFiles} localization files...");
            LogDiag($"[LOC] Starting localization download to: {localizationDir}");

            var semaphore = new SemaphoreSlim(3, 3);
            var tasks = new List<Task<(bool success, bool fromCache)>>();

            int filesDone = 0;
            async Task<(bool success, bool fromCache)> RunOneAsync(string filename)
            {
                var result = await DownloadOrCopyFileAsync(filename, localizationDir, hashManifest, semaphore, ct).ConfigureAwait(false);
                if (!result.success)
                    failed.Add(filename);
                onFileDone?.Invoke(Interlocked.Increment(ref filesDone), totalFiles);
                return result;
            }

            foreach (var filename in LocalizationFiles)
            {
                tasks.Add(RunOneAsync(filename));
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var (success, fromCache) in results)
            {
                if (success)
                {
                    successCount++;
                    if (fromCache) cachedCount++;
                }
            }

            SaveHashManifest(hashManifest);

            log?.Invoke($"Localization: {successCount}/{totalFiles} files");
            LogDiag($"[LOC] Applied {successCount}/{totalFiles} localization files ({cachedCount} from cache)");

            if (!failed.IsEmpty)
                LogDiag($"[LOC] Missing: {string.Join(", ", failed.OrderBy(f => f, StringComparer.Ordinal))}");

            return successCount == totalFiles;
        }

        private async Task<(bool success, bool fromCache)> DownloadOrCopyFileAsync(
            string filename,
            string targetDir,
            ConcurrentDictionary<string, string> hashManifest,
            SemaphoreSlim semaphore,
            CancellationToken ct)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                string targetPath = Path.Combine(targetDir, filename);
                string cachedPath = Path.Combine(CacheDir, filename);
                string url = EnvironmentConfig.BuildRawUrl(RemoteDir + filename);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMinutes(2));

                string? remoteTag = await TryGetRemoteTagAsync(url, cts.Token).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(remoteTag) &&
                    hashManifest.TryGetValue(filename, out var cachedTag) &&
                    cachedTag == remoteTag &&
                    File.Exists(cachedPath))
                {
                    try
                    {
                        File.Copy(cachedPath, targetPath, overwrite: true);
                        return (true, true);
                    }
                    catch (Exception ex)
                    {
                        LogDiag($"[LOC] Failed to copy cached {filename}: {ex.Message}");
                    }
                }

                var result = await CdnFallbackService.Instance
                    .DownloadWithFallbackAsync(url, cts.Token).ConfigureAwait(false);

                if (!result.Success || result.Data == null)
                {
                    LogDiag($"[LOC] All CDNs failed for {filename}: {result.ErrorMessage}");
                    return (false, false);
                }

                if (!LooksLikeLocalization(result.Data))
                {
                    LogDiag($"[LOC] Rejected {filename} from {result.SuccessfulUrl}: " +
                            $"not a localization file ({result.Data.Length} bytes) — likely an interstitial page.");
                    return (false, false);
                }

                string? tag = result.ETag ?? result.LastModified;
                try
                {
                    await File.WriteAllBytesAsync(cachedPath, result.Data, cts.Token).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(tag))
                        hashManifest[filename] = tag;
                    else
                        hashManifest.TryRemove(filename, out _);
                }
                catch (Exception ex)
                {
                    LogDiag($"[LOC] Failed to cache {filename}: {ex.Message}");
                }

                await File.WriteAllBytesAsync(targetPath, result.Data, cts.Token).ConfigureAwait(false);

                return (true, false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                LogDiag($"[LOC] Timed out: {filename}");
                return (false, false);
            }
            catch (Exception ex)
            {
                LogDiag($"[LOC] Error downloading {filename}: {ex.Message}");
                return (false, false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<string?> TryGetRemoteTagAsync(string url, CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return null;

                return response.Headers.ETag?.Tag
                    ?? response.Content.Headers.LastModified?.ToString("R");
            }
            catch
            {
                return null;
            }
        }

        public static bool LooksLikeLocalization(byte[] data)
        {
            if (data == null)
                return false;

            int i = (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) ? 3 : 0;

            while (i < data.Length &&
                   (data[i] == 0x20 || data[i] == 0x09 || data[i] == 0x0D || data[i] == 0x0A))
            {
                i++;
            }

            ReadOnlySpan<byte> expected = "\"lang\""u8;
            return data.Length - i >= expected.Length
                && data.AsSpan(i, expected.Length).SequenceEqual(expected);
        }

        private ConcurrentDictionary<string, string> LoadHashManifest()
        {
            try
            {
                if (File.Exists(HashManifestPath))
                {
                    var json = File.ReadAllText(HashManifestPath);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed != null)
                        return new ConcurrentDictionary<string, string>(parsed);
                }
            }
            catch (Exception ex)
            {
                LogDiag($"[LOC] Failed to load hash manifest: {ex.Message}");
            }
            return new ConcurrentDictionary<string, string>();
        }

        private void SaveHashManifest(ConcurrentDictionary<string, string> manifest)
        {
            try
            {
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HashManifestPath, json);
            }
            catch (Exception ex)
            {
                LogDiag($"[LOC] Failed to save hash manifest: {ex.Message}");
            }
        }

        private void LogDiag(string message)
        {
            if (_logger != null)
                _logger.Log(message);
            else
                FallbackLogger.LogFileOnly(message);
        }
    }
}
