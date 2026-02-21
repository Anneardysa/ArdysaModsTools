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
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Simple in-memory cache with expiration support.
    /// Thread-safe and optimized for frequent reads.
    /// </summary>
    /// <typeparam name="TKey">Cache key type.</typeparam>
    /// <typeparam name="TValue">Cached value type.</typeparam>
    public sealed class MemoryCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _locks = new();
        private readonly TimeSpan _defaultExpiration;
        private readonly System.Threading.Timer? _cleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Creates a new memory cache with the specified default expiration.
        /// </summary>
        /// <param name="defaultExpiration">Default time before entries expire.</param>
        /// <param name="cleanupInterval">How often to run cleanup. Null to disable auto-cleanup.</param>
        public MemoryCache(TimeSpan defaultExpiration, TimeSpan? cleanupInterval = null)
        {
            _defaultExpiration = defaultExpiration;
            
            if (cleanupInterval.HasValue)
            {
                _cleanupTimer = new System.Threading.Timer(
                    _ => Cleanup(),
                    null,
                    cleanupInterval.Value,
                    cleanupInterval.Value);
            }
        }

        /// <summary>
        /// Gets or creates a cached value.
        /// If the key exists and hasn't expired, returns cached value.
        /// Otherwise, calls the factory to create a new value.
        /// </summary>
        public TValue GetOrCreate(TKey key, Func<TValue> factory, TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out var existing) && !existing.IsExpired)
            {
                return existing.Value;
            }

            var value = factory();
            var entry = new CacheEntry(value, expiration ?? _defaultExpiration);
            _cache[key] = entry;
            return value;
        }

        /// <summary>
        /// Gets or creates a cached value asynchronously.
        /// Uses per-key locking to prevent cache stampede — only one caller
        /// invokes the factory for a given expired key while others wait.
        /// </summary>
        public async Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> factory, TimeSpan? expiration = null)
        {
            // Fast path: check without lock
            if (_cache.TryGetValue(key, out var existing) && !existing.IsExpired)
            {
                return existing.Value;
            }

            // Slow path: acquire per-key lock to prevent stampede
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock — another caller may have populated it
                if (_cache.TryGetValue(key, out existing) && !existing.IsExpired)
                {
                    return existing.Value;
                }

                var value = await factory().ConfigureAwait(false);
                var entry = new CacheEntry(value, expiration ?? _defaultExpiration);
                _cache[key] = entry;
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Tries to get a cached value without creating it.
        /// </summary>
        public bool TryGet(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Sets a value in the cache.
        /// </summary>
        public void Set(TKey key, TValue value, TimeSpan? expiration = null)
        {
            _cache[key] = new CacheEntry(value, expiration ?? _defaultExpiration);
        }

        /// <summary>
        /// Removes a specific key from the cache.
        /// </summary>
        public bool Remove(TKey key)
        {
            _locks.TryRemove(key, out _);
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _locks.Clear();
        }

        /// <summary>
        /// Removes expired entries from the cache.
        /// </summary>
        public void Cleanup()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    _cache.TryRemove(kvp.Key, out _);
                    _locks.TryRemove(kvp.Key, out _);
                }
            }
        }

        /// <summary>
        /// Gets the number of entries in the cache (including expired).
        /// </summary>
        public int Count => _cache.Count;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cleanupTimer?.Dispose();
            _cache.Clear();
            foreach (var semaphore in _locks.Values)
            {
                semaphore.Dispose();
            }
            _locks.Clear();
        }

        private sealed class CacheEntry
        {
            public TValue Value { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

            public CacheEntry(TValue value, TimeSpan expiration)
            {
                Value = value;
                ExpiresAt = DateTime.UtcNow.Add(expiration);
            }
        }
    }

    /// <summary>
    /// Static helper for common caching scenarios.
    /// </summary>
    public static class CacheHelper
    {
        private static readonly MemoryCache<string, object> _globalCache = 
            new(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));

        /// <summary>
        /// Gets or creates a value in the global cache.
        /// </summary>
        public static T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null)
        {
            return (T)_globalCache.GetOrCreate(key, () => factory()!, expiration)!;
        }

        /// <summary>
        /// Gets or creates a value in the global cache asynchronously.
        /// </summary>
        public static async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            return (T)(await _globalCache.GetOrCreateAsync(key, async () => (await factory())!, expiration))!;
        }

        /// <summary>
        /// Invalidates a specific cache key.
        /// </summary>
        public static void Invalidate(string key) => _globalCache.Remove(key);

        /// <summary>
        /// Clears the entire global cache.
        /// </summary>
        public static void ClearAll() => _globalCache.Clear();
    }
}

