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
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;

namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// Fetches and caches the server-published per-asset hash manifest
    /// (<c>Assets/asset_hashes.json</c>) and resolves the expected SHA-256/size for a given asset
    /// path. Returns <c>null</c> when the manifest is unavailable or the asset is absent, so
    /// verification degrades gracefully to the size-only check during staged rollout.
    ///
    /// Thread-safe singleton, matching the established Cdn-subsystem pattern
    /// (<see cref="CdnFallbackService"/>, <see cref="SmartCdnSelector"/>). An interface is
    /// intentionally omitted; callers that need to mock the lookup inject a resolver delegate.
    /// </summary>
    // [AMT:OPUS] Supplies the expected hashes that gate download integrity (ADR-0010).
    // Manifest absence must remain a graceful skip (return null), never a silent "pass".
    public sealed class AssetHashManifestService
    {
        #region Singleton

        private static readonly Lazy<AssetHashManifestService> _instance =
            new(() => new AssetHashManifestService());

        public static AssetHashManifestService Instance => _instance.Value;

        private AssetHashManifestService() { }

        #endregion

        /// <summary>Manifest asset path on the CDN.</summary>
        private const string ManifestAssetPath = "Assets/asset_hashes.json";

        /// <summary>How long a fetched manifest is trusted before re-fetching.</summary>
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private Dictionary<string, AssetHashEntry>? _manifest;
        private DateTime _loadedAtUtc = DateTime.MinValue;

        /// <summary>
        /// Resolve the expected hash entry for an asset path (as produced by
        /// <see cref="CdnConfig.ExtractAssetPath"/>). Returns null if the manifest can't be loaded
        /// or the asset isn't listed.
        /// </summary>
        public async Task<AssetHashEntry?> GetExpectedAsync(string? assetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var manifest = await GetManifestAsync(ct).ConfigureAwait(false);
            if (manifest == null)
                return null;

            return manifest.TryGetValue(assetPath, out var entry) ? entry : null;
        }

        private async Task<Dictionary<string, AssetHashEntry>?> GetManifestAsync(CancellationToken ct)
        {
            if (_manifest != null && DateTime.UtcNow - _loadedAtUtc < CacheDuration)
                return _manifest;

            await _loadLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_manifest != null && DateTime.UtcNow - _loadedAtUtc < CacheDuration)
                    return _manifest;

                string url = $"{CdnConfig.R2BaseUrl}/{ManifestAssetPath}";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(CdnConfig.TimeoutSeconds));

                string? json = await CdnFallbackService.Instance
                    .DownloadStringWithFallbackAsync(url, cts.Token)
                    .ConfigureAwait(false);

                var parsed = string.IsNullOrEmpty(json) ? null : ParseManifest(json!);
                if (parsed != null)
                {
                    _manifest = parsed;
                    _loadedAtUtc = DateTime.UtcNow;
                    Debug.WriteLine($"[AssetHashManifest] Loaded {parsed.Count} asset hashes");
                }
                else
                {
                    Debug.WriteLine("[AssetHashManifest] Manifest unavailable or unparsable — verification will skip");
                }

                // Return whatever we have (fresh, stale, or null) — null degrades to size-only.
                return _manifest;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return _manifest; // Timeout — fall back to any cached manifest.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AssetHashManifest] Load failed: {ex.Message}");
                return _manifest;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Parse the manifest JSON into a case-insensitive asset-path → entry map. Returns null on
        /// malformed JSON; silently skips individual malformed entries. Pure and side-effect free.
        /// </summary>
        public static Dictionary<string, AssetHashEntry>? ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("assets", out var assets) ||
                    assets.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var map = new Dictionary<string, AssetHashEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var asset in assets.EnumerateObject())
                {
                    if (asset.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    string? sha = asset.Value.TryGetProperty("sha256", out var shaEl) && shaEl.ValueKind == JsonValueKind.String
                        ? shaEl.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(sha))
                        continue;

                    long size = asset.Value.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var s)
                        ? s
                        : 0;

                    map[asset.Name] = new AssetHashEntry { Sha256 = sha!.Trim(), Size = size };
                }

                return map;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Replace the cached manifest directly. Test-only seam to avoid network access.
        /// </summary>
        internal void SetManifestForTesting(Dictionary<string, AssetHashEntry>? manifest)
        {
            _manifest = manifest;
            _loadedAtUtc = manifest != null ? DateTime.UtcNow : DateTime.MinValue;
        }
    }
}
