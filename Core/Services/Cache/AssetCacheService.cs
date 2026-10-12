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
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cache
{
    public sealed class AssetCacheService
    {
        #region Singleton

        private static readonly Lazy<AssetCacheService> _instance = 
            new Lazy<AssetCacheService>(() => new AssetCacheService());

        public static AssetCacheService Instance => _instance.Value;

        private const string EpochsFile = ".epochs";
        private const string MissingAssetsFile = ".missing_assets";

        private static readonly TimeSpan KnownMissingTtl = TimeSpan.FromDays(7);

        internal const int PreloadConcurrency = 8;

        private AssetCacheService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools",
                "AssetCache"))
        {
        }

        internal AssetCacheService(string cacheDirectory,
            Func<string, CancellationToken, Task<AssetFetchResult>>? fetch = null)
        {
            _cacheDirectory = cacheDirectory;
            _fetch = fetch ?? FetchRemoteAsync;

            EnsureCacheDirectoryExists();
            LoadEpochs();
            LoadMissingAssets();
        }

        #endregion

        #region Fields

        private readonly string _cacheDirectory;
        private readonly Func<string, CancellationToken, Task<AssetFetchResult>> _fetch;

        private readonly ConcurrentDictionary<string, Task<byte[]?>> _pendingDownloads = new();
        
        private readonly ConcurrentDictionary<string, (byte[] data, DateTime accessed)> _memoryCache = new();
        private const int MaxMemoryCacheItems = 50;

        private readonly Dictionary<string, string> _epochs = new();
        private readonly object _epochLock = new();

        private readonly ConcurrentDictionary<string, DateTime> _missingAssets = new();
        private readonly object _missingLock = new();

        #endregion

        #region Properties

        public string CacheDirectory => _cacheDirectory;

        #endregion

        #region Public API

        public async Task<byte[]?> GetAssetBytesAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                return null;

            string cacheKey = GetCacheKey(url);
            string cacheFilePath = GetCacheFilePath(cacheKey);

            if (_memoryCache.TryGetValue(cacheKey, out var memEntry))
            {
                _memoryCache[cacheKey] = (memEntry.data, DateTime.UtcNow);
                return memEntry.data;
            }

            var cachedResult = await AssetCacheFile.ReadAsync(cacheFilePath, url).ConfigureAwait(false);
            if (cachedResult != null && cachedResult.Data.Length > 0)
            {
                AddToMemoryCache(cacheKey, cachedResult.Data);
                return cachedResult.Data;
            }

            if (IsKnownMissing(url, KnownMissingTtl))
                return null;

            var downloadTask = _pendingDownloads.GetOrAdd(url, key => DownloadAndCacheAsync(key, url, ct));
            
            try
            {
                var result = await downloadTask.ConfigureAwait(false);
                return result;
            }
            finally
            {
                _pendingDownloads.TryRemove(url, out _);
            }
        }

        public async Task<string?> GetAssetAsDataUriAsync(string url, CancellationToken ct = default)
        {
            var bytes = await GetAssetBytesAsync(url, ct).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return null;

            string mimeType = GetMimeType(url);
            string base64 = Convert.ToBase64String(bytes);
            return $"data:{mimeType};base64,{base64}";
        }

        public bool IsCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            string cacheKey = GetCacheKey(url);
            
            if (_memoryCache.TryGetValue(cacheKey, out var entry) && entry.data.Length > 0)
                return true;
            
            string cacheFilePath = GetCacheFilePath(cacheKey);
            return File.Exists(cacheFilePath) && AssetCacheFile.IsValidCacheFile(cacheFilePath);
        }

        public async Task PreloadAssetsAsync(IEnumerable<string> urls, CancellationToken ct = default)
        {
            var tasks = new List<Task>();
            
            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(url)) continue;
                
                if (!IsCached(url))
                {
                    tasks.Add(GetAssetBytesAsync(url, ct));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public async Task<(int downloaded, int skipped, int failed)> PreloadAssetsWithProgressAsync(
            IReadOnlyList<string> urls, 
            IProgress<(int current, int total, string url)>? progress = null,
            CancellationToken ct = default)
        {
            int downloaded = 0;
            int skipped = 0;
            int failed = 0;
            int completed = 0;
            var progressLock = new object();
            int total = urls.Count;

            using var semaphore = new SemaphoreSlim(PreloadConcurrency);

            var tasks = urls.Select(async url =>
            {
                if (ct.IsCancellationRequested) return;
                if (string.IsNullOrEmpty(url)) return;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (IsCached(url))
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    var result = await GetAssetBytesAsync(url, ct).ConfigureAwait(false);
                    if (result != null && result.Length > 0)
                    {
                        Interlocked.Increment(ref downloaded);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
                finally
                {
                    semaphore.Release();
                    lock (progressLock)
                        progress?.Report((++completed, total, url));
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return (downloaded, skipped, failed);
        }

        public bool InvalidateOnEpochChange(string epochKey, string currentVersion, IEnumerable<string> urlsIfChanged) =>
            InvalidateOnEpochChange(epochKey, currentVersion, _ => urlsIfChanged);

        public bool InvalidateOnEpochChange(string epochKey, string currentVersion, Func<string, IEnumerable<string>> urlsChangedSince)
        {
            if (string.IsNullOrEmpty(epochKey) || string.IsNullOrEmpty(currentVersion))
                return false;

            lock (_epochLock)
            {
                if (_epochs.TryGetValue(epochKey, out var lastSeen))
                {
                    if (lastSeen == currentVersion)
                        return false;
                }
                else
                {
                    _epochs[epochKey] = currentVersion;
                    PersistEpochs();
                    return false;
                }

                int invalidated = 0;
                foreach (var url in urlsChangedSince(lastSeen))
                {
                    Invalidate(url);
                    invalidated++;
                }

                ForgetMisses();

                _epochs[epochKey] = currentVersion;
                PersistEpochs();
                FallbackLogger.LogFileOnly(
                    $"[AssetCacheService] Epoch '{epochKey}' changed {lastSeen} -> {currentVersion}, invalidated {invalidated} thumbnail(s)");
                return true;
            }
        }

        private void LoadEpochs()
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, EpochsFile);
                if (!File.Exists(path)) return;

                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (data != null)
                {
                    foreach (var kvp in data)
                        _epochs[kvp.Key] = kvp.Value;
                }
            }
            catch {  }
        }

        private void PersistEpochs()
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, EpochsFile);
                File.WriteAllText(path, JsonSerializer.Serialize(_epochs));
            }
            catch {  }
        }

        public bool IsKnownMissing(string url, TimeSpan ttl)
        {
            if (string.IsNullOrEmpty(url)) return false;

            string cacheKey = GetCacheKey(url);
            if (_missingAssets.TryGetValue(cacheKey, out var markedUtc))
            {
                if (DateTime.UtcNow - markedUtc <= ttl)
                    return true;

                RemoveMissing(cacheKey);
            }
            return false;
        }

        private void MarkMissing(string cacheKey)
        {
            _missingAssets[cacheKey] = DateTime.UtcNow;
            PersistMissingAssets();
        }

        private void ForgetMisses()
        {
            foreach (var (key, entry) in _memoryCache)
                if (entry.data.Length == 0) _memoryCache.TryRemove(key, out _);
            if (_missingAssets.IsEmpty) return;
            _missingAssets.Clear();
            PersistMissingAssets();
        }

        private void RemoveMissing(string cacheKey)
        {
            if (_missingAssets.TryRemove(cacheKey, out _))
                PersistMissingAssets();
        }

        internal static bool IsNotFoundError(string? error)
        {
            if (string.IsNullOrEmpty(error)) return false;

            if (error.Contains("HTTP 401") || error.Contains(ErrorCodes.DL_CDN_UNAUTHORIZED)) return false;

            return error.Contains("404") || error.Contains("403");
        }

        private void LoadMissingAssets()
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, MissingAssetsFile);
                if (!File.Exists(path)) return;

                var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(path));
                if (data != null)
                {
                    foreach (var kvp in data)
                        _missingAssets[kvp.Key] = kvp.Value;
                }
            }
            catch {  }
        }

        private void PersistMissingAssets()
        {
            lock (_missingLock)
            {
                try
                {
                    var path = Path.Combine(_cacheDirectory, MissingAssetsFile);
                    File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, DateTime>(_missingAssets)));
                }
                catch {  }
            }
        }

        public void ClearCache()
        {
            _memoryCache.Clear();

            _missingAssets.Clear();
            try
            {
                var missingPath = Path.Combine(_cacheDirectory, MissingAssetsFile);
                if (File.Exists(missingPath)) File.Delete(missingPath);
            }
            catch {  }

            lock (_epochLock)
            {
                _epochs.Clear();
            }
            try
            {
                var epochsPath = Path.Combine(_cacheDirectory, EpochsFile);
                if (File.Exists(epochsPath)) File.Delete(epochsPath);
            }
            catch {  }

            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    var files = Directory.GetFiles(_cacheDirectory, $"*{AssetCacheFile.Extension}");
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch {  }
                    }
                }
                System.Diagnostics.Debug.WriteLine("[AssetCacheService] Cache cleared");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Clear error: {ex.Message}");
            }
        }

        public (int fileCount, long totalBytes, int memoryCount) GetCacheStats()
        {
            int fileCount = 0;
            long totalBytes = 0;

            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    var files = Directory.GetFiles(_cacheDirectory, $"*{AssetCacheFile.Extension}");
                    fileCount = files.Length;
                    foreach (var file in files)
                    {
                        try { totalBytes += new FileInfo(file).Length; } catch { }
                    }
                }
            }
            catch { }

            return (fileCount, totalBytes, _memoryCache.Count);
        }

        public void Invalidate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            
            string cacheKey = GetCacheKey(url);
            
            _memoryCache.TryRemove(cacheKey, out _);

            RemoveMissing(cacheKey);

            string cacheFilePath = GetCacheFilePath(cacheKey);
            try
            {
                if (File.Exists(cacheFilePath))
                    File.Delete(cacheFilePath);
            }
            catch { }
        }

        #endregion

        #region Private Helpers

        private void EnsureCacheDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    Directory.CreateDirectory(_cacheDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Failed to create cache dir: {ex.Message}");
            }
        }

        private static string GetCacheKey(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path) : url;

        private string GetCacheFilePath(string cacheKey)
        {
            string fileName = AssetCacheFile.GetCacheFileName(cacheKey);
            return Path.Combine(_cacheDirectory, fileName);
        }

        private async Task<byte[]?> DownloadAndCacheAsync(string cacheKey, string url, CancellationToken ct)
        {
            try
            {
                var fetched = await _fetch(url, ct).ConfigureAwait(false);
                if (fetched.Data == null)
                {
                    if (fetched.NotFound)
                        MarkMissing(cacheKey);
                    AddToMemoryCache(cacheKey, Array.Empty<byte>());
                    return null;
                }

                byte[] data = fetched.Data;
                string? etag = fetched.ETag;
                string? lastModified = fetched.LastModified;
                string actualUrl = fetched.ActualUrl ?? url;

                if (data.Length == 0)
                {
                    AddToMemoryCache(cacheKey, Array.Empty<byte>());
                    return null;
                }

                string cacheFilePath = GetCacheFilePath(cacheKey);
                string extension = Path.GetExtension(new Uri(url).AbsolutePath);

                await AssetCacheFile.WriteAsync(cacheFilePath, url, extension, data, etag, lastModified)
                    .ConfigureAwait(false);

                AddToMemoryCache(cacheKey, data);

                RemoveMissing(cacheKey);

                System.Diagnostics.Debug.WriteLine(
                    $"[AssetCacheService] Cached: {actualUrl} ({data.Length} bytes) ETag={etag ?? "none"}");
                return data;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Download cancelled: {url}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Download error: {url} -> {ex.Message}");
                AddToMemoryCache(cacheKey, Array.Empty<byte>());
                return null;
            }
        }

        private static async Task<AssetFetchResult> FetchRemoteAsync(string url, CancellationToken ct)
        {
            if (CdnConfig.IsModsPackUrl(url))
            {
                var cdnResult = await CdnFallbackService.Instance
                    .DownloadWithFallbackAsync(url, ct)
                    .ConfigureAwait(false);

                if (!cdnResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AssetCacheService] CDN fallback failed: {url} -> {cdnResult.ErrorMessage}");
                    return new AssetFetchResult(null, null, null, IsNotFoundError(cdnResult.ErrorMessage), null);
                }

                if (cdnResult.FallbacksAttempted > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AssetCacheService] Used fallback CDN (attempts: {cdnResult.FallbacksAttempted}): {cdnResult.SuccessfulUrl ?? url}");
                }

                return new AssetFetchResult(cdnResult.Data, cdnResult.ETag, cdnResult.LastModified, false, cdnResult.SuccessfulUrl);
            }

            var client = HttpClientProvider.Client;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AssetCacheService] Download failed: {url} -> {response.StatusCode}");
                return new AssetFetchResult(null, null, null,
                    response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden, null);
            }

            var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return new AssetFetchResult(data, response.Headers.ETag?.Tag,
                response.Content.Headers.LastModified?.ToString("R"), false, url);
        }

        private void AddToMemoryCache(string key, byte[] data)
        {
            if (_memoryCache.Count >= MaxMemoryCacheItems)
            {
                string? oldestKey = null;
                DateTime oldest = DateTime.MaxValue;
                
                foreach (var kvp in _memoryCache)
                {
                    if (kvp.Value.accessed < oldest)
                    {
                        oldest = kvp.Value.accessed;
                        oldestKey = kvp.Key;
                    }
                }

                if (oldestKey != null)
                    _memoryCache.TryRemove(oldestKey, out _);
            }

            _memoryCache[key] = (data, DateTime.UtcNow);
        }

        private static string GetMimeType(string url)
        {
            string ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    internal readonly record struct AssetFetchResult(
        byte[]? Data, string? ETag, string? LastModified, bool NotFound, string? ActualUrl);
}
