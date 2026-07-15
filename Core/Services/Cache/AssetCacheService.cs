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

        private const string RefreshTimestampFile = ".last_refresh";
        private const string MissingAssetsFile = ".missing_assets";

        private static readonly TimeSpan KnownMissingTtl = TimeSpan.FromDays(7);

        private AssetCacheService()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools",
                "AssetCache"
            );

            EnsureCacheDirectoryExists();
            _lastBatchRefreshTimeUtc = LoadPersistedRefreshTime();
            LoadMissingAssets();
        }

        #endregion

        #region Fields

        private readonly string _cacheDirectory;
        
        private readonly ConcurrentDictionary<string, Task<byte[]?>> _pendingDownloads = new();
        
        private readonly ConcurrentDictionary<string, (byte[] data, DateTime accessed)> _memoryCache = new();
        private const int MaxMemoryCacheItems = 50;

        private DateTime _lastBatchRefreshTimeUtc = DateTime.MinValue;
        private readonly object _refreshTimeLock = new();

        private readonly ConcurrentDictionary<string, DateTime> _missingAssets = new();
        private readonly object _missingLock = new();

        #endregion

        #region Properties

        public string CacheDirectory => _cacheDirectory;

        #endregion

        #region Public API

        public async Task<byte[]?> GetAssetBytesAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
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
            
            if (_memoryCache.ContainsKey(cacheKey))
                return true;
            
            string cacheFilePath = GetCacheFilePath(cacheKey);
            return File.Exists(cacheFilePath) && AssetCacheFile.IsValidCacheFile(cacheFilePath);
        }

        public async Task<bool> IsStaleAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url)) return false;

            string cacheKey = GetCacheKey(url);

            if (_memoryCache.TryGetValue(cacheKey, out var memEntry) && memEntry.data.Length == 0)
                return false;

            string cacheFilePath = GetCacheFilePath(cacheKey);
            
            var cached = await AssetCacheFile.ReadAsync(cacheFilePath, url).ConfigureAwait(false);
            if (cached == null) return true;

            if (string.IsNullOrEmpty(cached.ETag) && string.IsNullOrEmpty(cached.LastModified))
                return false;

            try
            {
                var client = HttpClientProvider.Client;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return false;

                string? serverEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrEmpty(cached.ETag) && !string.IsNullOrEmpty(serverEtag))
                {
                    if (cached.ETag != serverEtag)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Stale (ETag mismatch): {url}");
                        return true;
                    }
                }

                string? serverLastModified = response.Content.Headers.LastModified?.ToString("R");
                if (!string.IsNullOrEmpty(cached.LastModified) && !string.IsNullOrEmpty(serverLastModified))
                {
                    if (cached.LastModified != serverLastModified)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Stale (Last-Modified mismatch): {url}");
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
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
            int total = urls.Count;

            using var semaphore = new SemaphoreSlim(4);

            var tasks = urls.Select(async (url, index) =>
            {
                if (ct.IsCancellationRequested) return;
                if (string.IsNullOrEmpty(url)) return;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    progress?.Report((index + 1, total, url));

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
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return (downloaded, skipped, failed);
        }

        public async Task<(int refreshed, int skipped, int failed)> RefreshStaleAssetsAsync(
            IReadOnlyList<string> urls,
            IProgress<(int current, int total, string url)>? progress = null,
            CancellationToken ct = default)
        {
            int refreshed = 0;
            int skipped = 0;
            int failed = 0;
            int total = urls.Count;
            int current = 0;

            using var semaphore = new SemaphoreSlim(8);

            var tasks = urls.Select(async url =>
            {
                if (ct.IsCancellationRequested) return;
                if (string.IsNullOrEmpty(url)) return;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var idx = Interlocked.Increment(ref current);
                    progress?.Report((idx, total, url));

                    bool isStale = await IsStaleAsync(url, ct).ConfigureAwait(false);
                    
                    if (isStale)
                    {
                        Invalidate(url);
                        var result = await GetAssetBytesAsync(url, ct).ConfigureAwait(false);
                        if (result != null && result.Length > 0)
                        {
                            Interlocked.Increment(ref refreshed);
                        }
                        else
                        {
                            Interlocked.Increment(ref failed);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref skipped);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return (refreshed, skipped, failed);
        }

        public bool ShouldRefreshAssets(TimeSpan cooldown)
        {
            lock (_refreshTimeLock)
            {
                return (DateTime.UtcNow - _lastBatchRefreshTimeUtc) > cooldown;
            }
        }

        public void MarkRefreshed()
        {
            lock (_refreshTimeLock)
            {
                _lastBatchRefreshTimeUtc = DateTime.UtcNow;
                PersistRefreshTime(_lastBatchRefreshTimeUtc);
                System.Diagnostics.Debug.WriteLine(
                    $"[AssetCacheService] Batch refresh marked at {_lastBatchRefreshTimeUtc:HH:mm:ss}");
            }
        }

        private DateTime LoadPersistedRefreshTime()
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, RefreshTimestampFile);
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path).Trim();
                    if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch {  }
            return DateTime.MinValue;
        }

        private void PersistRefreshTime(DateTime utcTime)
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, RefreshTimestampFile);
                File.WriteAllText(path, utcTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
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

        private void RemoveMissing(string cacheKey)
        {
            if (_missingAssets.TryRemove(cacheKey, out _))
                PersistMissingAssets();
        }

        private static bool IsNotFoundError(string? error)
        {
            if (string.IsNullOrEmpty(error)) return false;
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

            lock (_refreshTimeLock)
            {
                _lastBatchRefreshTimeUtc = DateTime.MinValue;
            }
            try
            {
                var refreshPath = Path.Combine(_cacheDirectory, RefreshTimestampFile);
                if (File.Exists(refreshPath)) File.Delete(refreshPath);
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

        private string GetCacheKey(string url)
        {
            var uri = new Uri(url);
            return uri.GetLeftPart(UriPartial.Path);
        }

        private string GetCacheFilePath(string cacheKey)
        {
            string fileName = AssetCacheFile.GetCacheFileName(cacheKey);
            return Path.Combine(_cacheDirectory, fileName);
        }

        private async Task<byte[]?> DownloadAndCacheAsync(string cacheKey, string url, CancellationToken ct)
        {
            try
            {
                byte[] data;
                string? etag;
                string? lastModified;
                string actualUrl = url;

                if (CdnConfig.IsModsPackUrl(url))
                {
                    var cdnResult = await CdnFallbackService.Instance
                        .DownloadWithFallbackAsync(url, ct)
                        .ConfigureAwait(false);

                    if (!cdnResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AssetCacheService] CDN fallback failed: {url} -> {cdnResult.ErrorMessage}");
                        if (IsNotFoundError(cdnResult.ErrorMessage))
                            MarkMissing(cacheKey);
                        AddToMemoryCache(cacheKey, Array.Empty<byte>());
                        return null;
                    }

                    data = cdnResult.Data!;
                    etag = cdnResult.ETag;
                    lastModified = cdnResult.LastModified;
                    actualUrl = cdnResult.SuccessfulUrl ?? url;

                    if (cdnResult.FallbacksAttempted > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AssetCacheService] Used fallback CDN (attempts: {cdnResult.FallbacksAttempted}): {actualUrl}");
                    }
                }
                else
                {
                    var client = HttpClientProvider.Client;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(30));

                    var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AssetCacheService] Download failed: {url} -> {response.StatusCode}");
                        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                            MarkMissing(cacheKey);
                        AddToMemoryCache(cacheKey, Array.Empty<byte>());
                        return null;
                    }

                    data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    etag = response.Headers.ETag?.Tag;
                    lastModified = response.Content.Headers.LastModified?.ToString("R");
                }

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
}
