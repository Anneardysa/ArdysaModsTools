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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Cache
{
    /// <summary>
    /// Asynchronous local asset caching service.
    /// Checks local cache first, then fetches from remote if not found.
    /// Thread-safe singleton with concurrent request coalescing.
    /// </summary>
    public sealed class AssetCacheService
    {
        #region Singleton

        private static readonly Lazy<AssetCacheService> _instance = 
            new Lazy<AssetCacheService>(() => new AssetCacheService());

        /// <summary>
        /// Gets the singleton instance of the cache service.
        /// </summary>
        public static AssetCacheService Instance => _instance.Value;

        private AssetCacheService()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools",
                "AssetCache"
            );
            
            EnsureCacheDirectoryExists();
        }

        #endregion

        #region Fields

        private readonly string _cacheDirectory;
        
        // Tracks in-flight downloads to prevent duplicate requests
        private readonly ConcurrentDictionary<string, Task<byte[]?>> _pendingDownloads = new();
        
        // Simple in-memory LRU cache for hot assets (max 50 items)
        private readonly ConcurrentDictionary<string, (byte[] data, DateTime accessed)> _memoryCache = new();
        private const int MaxMemoryCacheItems = 50;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the cache directory path.
        /// </summary>
        public string CacheDirectory => _cacheDirectory;

        #endregion

        #region Public API

        /// <summary>
        /// Get asset bytes from cache or remote URL.
        /// Local cache is checked first; if not found, fetches from remote and caches.
        /// </summary>
        /// <param name="url">Remote URL of the asset</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Asset bytes, or null if failed to fetch</returns>
        public async Task<byte[]?> GetAssetBytesAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Normalize URL for caching (remove query strings for cache key)
            string cacheKey = GetCacheKey(url);
            string cacheFilePath = GetCacheFilePath(cacheKey);

            // 1. Check memory cache first (fastest)
            if (_memoryCache.TryGetValue(cacheKey, out var memEntry))
            {
                // Update access time
                _memoryCache[cacheKey] = (memEntry.data, DateTime.UtcNow);
                return memEntry.data;
            }

            // 2. Check disk cache
            var cachedResult = await AssetCacheFile.ReadAsync(cacheFilePath, url).ConfigureAwait(false);
            if (cachedResult != null && cachedResult.Data.Length > 0)
            {
                // Add to memory cache
                AddToMemoryCache(cacheKey, cachedResult.Data);
                return cachedResult.Data;
            }

            // 3. Fetch from remote (with request coalescing)
            // This prevents multiple parallel requests for the same URL
            var downloadTask = _pendingDownloads.GetOrAdd(url, key => DownloadAndCacheAsync(key, url, ct));
            
            try
            {
                var result = await downloadTask.ConfigureAwait(false);
                return result;
            }
            finally
            {
                // Remove from pending after completion
                _pendingDownloads.TryRemove(url, out _);
            }
        }

        /// <summary>
        /// Get asset as a base64 data URI for use in HTML/WebView2.
        /// </summary>
        /// <param name="url">Remote URL of the asset</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Data URI string (e.g., "data:image/png;base64,..."), or null if failed</returns>
        public async Task<string?> GetAssetAsDataUriAsync(string url, CancellationToken ct = default)
        {
            var bytes = await GetAssetBytesAsync(url, ct).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return null;

            string mimeType = GetMimeType(url);
            string base64 = Convert.ToBase64String(bytes);
            return $"data:{mimeType};base64,{base64}";
        }

        /// <summary>
        /// Check if an asset is cached locally.
        /// </summary>
        public bool IsCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            string cacheKey = GetCacheKey(url);
            
            // Check memory cache
            if (_memoryCache.ContainsKey(cacheKey))
                return true;
            
            // Check disk cache
            string cacheFilePath = GetCacheFilePath(cacheKey);
            return File.Exists(cacheFilePath) && AssetCacheFile.IsValidCacheFile(cacheFilePath);
        }

        /// <summary>
        /// Check if a cached asset is stale (server has newer version).
        /// Uses HTTP HEAD request to compare ETag/Last-Modified headers.
        /// </summary>
        /// <param name="url">Asset URL to check</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if stale (needs re-download), false if fresh or unable to check</returns>
        public async Task<bool> IsStaleAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url)) return false;

            string cacheKey = GetCacheKey(url);
            string cacheFilePath = GetCacheFilePath(cacheKey);
            
            // Read cached metadata
            var cached = await AssetCacheFile.ReadAsync(cacheFilePath, url).ConfigureAwait(false);
            if (cached == null) return true; // Not cached = treat as stale

            // If no ETag/LastModified stored, can't validate - assume fresh
            if (string.IsNullOrEmpty(cached.ETag) && string.IsNullOrEmpty(cached.LastModified))
                return false;

            try
            {
                var client = HttpClientProvider.Client;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5)); // Quick timeout for HEAD

                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return false; // Can't check, assume fresh

                // Compare ETag
                string? serverEtag = response.Headers.ETag?.Tag;
                if (!string.IsNullOrEmpty(cached.ETag) && !string.IsNullOrEmpty(serverEtag))
                {
                    if (cached.ETag != serverEtag)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Stale (ETag mismatch): {url}");
                        return true;
                    }
                }

                // Compare Last-Modified
                string? serverLastModified = response.Content.Headers.LastModified?.ToString("R");
                if (!string.IsNullOrEmpty(cached.LastModified) && !string.IsNullOrEmpty(serverLastModified))
                {
                    if (cached.LastModified != serverLastModified)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Stale (Last-Modified mismatch): {url}");
                        return true;
                    }
                }

                return false; // Fresh
            }
            catch
            {
                // Network error - assume fresh to avoid blocking
                return false;
            }
        }

        /// <summary>
        /// Preload multiple assets into cache in parallel.
        /// Useful for warming up the cache during app initialization.
        /// </summary>
        public async Task PreloadAssetsAsync(IEnumerable<string> urls, CancellationToken ct = default)
        {
            var tasks = new List<Task>();
            
            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(url)) continue;
                
                // Only preload if not already cached
                if (!IsCached(url))
                {
                    tasks.Add(GetAssetBytesAsync(url, ct));
                }
            }

            // Wait for all downloads with limited concurrency
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Preload assets with progress reporting.
        /// Returns (downloaded, skipped, failed) counts.
        /// </summary>
        /// <param name="urls">URLs to preload</param>
        /// <param name="progress">Reports (currentIndex, totalCount, currentUrl)</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<(int downloaded, int skipped, int failed)> PreloadAssetsWithProgressAsync(
            IReadOnlyList<string> urls, 
            IProgress<(int current, int total, string url)>? progress = null,
            CancellationToken ct = default)
        {
            int downloaded = 0;
            int skipped = 0;
            int failed = 0;
            int total = urls.Count;

            // Use semaphore to limit concurrent downloads (prevent overwhelming the network)
            using var semaphore = new SemaphoreSlim(4); // 4 concurrent downloads max

            var tasks = urls.Select(async (url, index) =>
            {
                if (ct.IsCancellationRequested) return;
                if (string.IsNullOrEmpty(url)) return;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    progress?.Report((index + 1, total, url));

                    // Check if already cached
                    if (IsCached(url))
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    // Download and cache
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

        /// <summary>
        /// Refresh stale assets - check freshness and re-download if server has newer version.
        /// This ensures cached assets stay up-to-date when you update images on the server.
        /// </summary>
        /// <param name="urls">URLs to check and potentially refresh</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Count of (refreshed, skipped, failed)</returns>
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

            // Check and refresh sequentially to avoid overwhelming server with HEAD requests
            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(url)) continue;

                current++;
                progress?.Report((current, total, url));

                try
                {
                    // Check if stale
                    bool isStale = await IsStaleAsync(url, ct).ConfigureAwait(false);
                    
                    if (isStale)
                    {
                        // Invalidate and re-download
                        Invalidate(url);
                        var result = await GetAssetBytesAsync(url, ct).ConfigureAwait(false);
                        if (result != null && result.Length > 0)
                        {
                            refreshed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            return (refreshed, skipped, failed);
        }

        /// <summary>
        /// Clear the entire cache (both memory and disk).
        /// </summary>
        public void ClearCache()
        {
            // Clear memory cache
            _memoryCache.Clear();

            // Clear disk cache
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    var files = Directory.GetFiles(_cacheDirectory, $"*{AssetCacheFile.Extension}");
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch { /* Ignore individual file errors */ }
                    }
                }
                System.Diagnostics.Debug.WriteLine("[AssetCacheService] Cache cleared");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheService] Clear error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
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

        /// <summary>
        /// Remove a specific URL from cache.
        /// </summary>
        public void Invalidate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            
            string cacheKey = GetCacheKey(url);
            
            // Remove from memory
            _memoryCache.TryRemove(cacheKey, out _);
            
            // Remove from disk
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
            // Remove query string for cache key consistency
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

                // Use CDN fallback for ModsPack assets
                if (CdnConfig.IsModsPackUrl(url))
                {
                    var cdnResult = await CdnFallbackService.Instance
                        .DownloadWithFallbackAsync(url, ct)
                        .ConfigureAwait(false);

                    if (!cdnResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AssetCacheService] CDN fallback failed: {url} -> {cdnResult.ErrorMessage}");
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
                    // Non-ModsPack URL: direct download
                    var client = HttpClientProvider.Client;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(30));

                    var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AssetCacheService] Download failed: {url} -> {response.StatusCode}");
                        return null;
                    }

                    data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    etag = response.Headers.ETag?.Tag;
                    lastModified = response.Content.Headers.LastModified?.ToString("R");
                }

                if (data.Length == 0)
                    return null;

                // Cache to disk with freshness metadata
                string cacheFilePath = GetCacheFilePath(cacheKey);
                string extension = Path.GetExtension(new Uri(url).AbsolutePath);

                await AssetCacheFile.WriteAsync(cacheFilePath, url, extension, data, etag, lastModified)
                    .ConfigureAwait(false);

                // Add to memory cache
                AddToMemoryCache(cacheKey, data);

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
                return null;
            }
        }

        private void AddToMemoryCache(string key, byte[] data)
        {
            // Simple eviction: remove oldest accessed items if we're over limit
            if (_memoryCache.Count >= MaxMemoryCacheItems)
            {
                // Find and remove the oldest item
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
