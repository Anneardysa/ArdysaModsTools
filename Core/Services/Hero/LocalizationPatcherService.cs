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
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public class LocalizationPatcherService
    {
        private readonly IAppLogger? _logger;
        private readonly HttpClient _http = HttpClientProvider.Client;

        private static string[] BaseUrls => new[]
        {
            $"{Constants.CdnConfig.R2BaseUrl}/remote/localization/",
            EnvironmentConfig.BuildRawUrl("remote/localization/")
        };

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArdysaModsTools", "cache", "localization");

        private static readonly string HashManifestPath = Path.Combine(CacheDir, "hashes.json");

        private static readonly string[] LocalizationFiles = new[]
        {
            "dota_brazilian.txt",
            "dota_english.txt",
            "dota_french.txt",
            "dota_german.txt",
            "dota_italian.txt",
            "dota_japanese.txt",
            "dota_koreana.txt",
            "dota_portuguese.txt",
            "dota_russian.txt",
            "dota_schinese.txt",
            "dota_spanish.txt",
            "dota_tchinese.txt",
            "dota_ukrainian.txt"
        };

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
                _logger?.Log($"[LOC] Failed to create cache directory: {ex.Message}");
            }

            string localizationDir = Path.Combine(extractDir, "resource", "localization");
            try
            {
                Directory.CreateDirectory(localizationDir);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[LOC] Failed to create localization directory: {ex.Message}");
                return false;
            }

            var hashManifest = LoadHashManifest();

            int successCount = 0;
            int cachedCount = 0;
            int totalFiles = LocalizationFiles.Length;

            log?.Invoke($"Downloading {totalFiles} localization files...");
            _logger?.Log($"[LOC] Starting localization download to: {localizationDir}");

            var semaphore = new SemaphoreSlim(3, 3);
            var tasks = new List<Task<(bool success, bool fromCache)>>();

            int filesDone = 0;
            async Task<(bool success, bool fromCache)> RunOneAsync(string filename)
            {
                var result = await DownloadOrCopyFileAsync(filename, localizationDir, hashManifest, semaphore, ct).ConfigureAwait(false);
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
            _logger?.Log($"[LOC] Applied {successCount}/{totalFiles} localization files ({cachedCount} from cache)");

            return successCount >= totalFiles / 2;
        }

        private async Task<(bool success, bool fromCache)> DownloadOrCopyFileAsync(
            string filename,
            string targetDir,
            Dictionary<string, string> hashManifest,
            SemaphoreSlim semaphore,
            CancellationToken ct)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                string targetPath = Path.Combine(targetDir, filename);
                string cachedPath = Path.Combine(CacheDir, filename);

                foreach (var baseUrl in BaseUrls)
                {
                    ct.ThrowIfCancellationRequested();
                    string url = baseUrl + filename;

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMinutes(2));

                    string? remoteHash = null;
                    try
                    {
                        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                        var headResponse = await _http.SendAsync(headRequest, cts.Token).ConfigureAwait(false);

                        if (headResponse.IsSuccessStatusCode)
                        {
                            remoteHash = headResponse.Headers.ETag?.Tag
                                ?? headResponse.Content.Headers.LastModified?.ToString("O");
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(remoteHash) &&
                        hashManifest.TryGetValue(filename, out var cachedHash) &&
                        cachedHash == remoteHash &&
                        File.Exists(cachedPath))
                    {
                        try
                        {
                            File.Copy(cachedPath, targetPath, overwrite: true);
                            return (true, true);
                        }
                        catch (Exception ex)
                        {
                            _logger?.Log($"[LOC] Failed to copy cached {filename}: {ex.Message}");
                        }
                    }

                    byte[] content;
                    try
                    {
                        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                            .ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger?.Log($"[LOC] Failed to download {filename} from {url}: {response.StatusCode}");
                            continue;
                        }

                        content = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[LOC] Error downloading {filename} from {url}: {ex.Message}");
                        continue;
                    }

                    try
                    {
                        await File.WriteAllBytesAsync(cachedPath, content, cts.Token).ConfigureAwait(false);

                        string newHash = remoteHash ?? ComputeSha1(content);
                        lock (hashManifest)
                        {
                            hashManifest[filename] = newHash;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[LOC] Failed to cache {filename}: {ex.Message}");
                    }

                    await File.WriteAllBytesAsync(targetPath, content, cts.Token).ConfigureAwait(false);

                    return (true, false);
                }

                return (false, false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Log($"[LOC] Download cancelled: {filename}");
                return (false, false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[LOC] Error downloading {filename}: {ex.Message}");
                return (false, false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private Dictionary<string, string> LoadHashManifest()
        {
            try
            {
                if (File.Exists(HashManifestPath))
                {
                    var json = File.ReadAllText(HashManifestPath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                        ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[LOC] Failed to load hash manifest: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private void SaveHashManifest(Dictionary<string, string> manifest)
        {
            try
            {
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HashManifestPath, json);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[LOC] Failed to save hash manifest: {ex.Message}");
            }
        }

        private static string ComputeSha1(byte[] data)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(data);
            return Convert.ToHexString(hash);
        }
    }
}


