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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Cdn;

namespace ArdysaModsTools.Core.Services
{
    public interface IHeroIndexProvider
    {
        Task<string?> GetIndexTextAsync(string zipUrl, Action<string> log, CancellationToken ct = default);
    }

    public sealed class HeroIndexProvider : IHeroIndexProvider
    {
        private readonly Func<string, CancellationToken, Task<AssetHashEntry?>> _hashResolver;
        private readonly Func<string, CancellationToken, Task<string?>> _fetch;
        private readonly string _cacheRoot;
        private readonly IAppLogger? _logger;

        public HeroIndexProvider(
            Func<string, CancellationToken, Task<AssetHashEntry?>>? hashResolver = null,
            Func<string, CancellationToken, Task<string?>>? fetch = null,
            string? cacheRoot = null,
            IAppLogger? logger = null)
        {
            _hashResolver = hashResolver ?? AssetHashManifestService.Instance.GetExpectedAsync;
            _fetch = fetch ?? ((url, ct) => CdnFallbackService.Instance.DownloadStringWithFallbackAsync(url, ct));
            _cacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
                ? Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "cache", "index")
                : cacheRoot!;
            _logger = logger;
        }

        public async Task<string?> GetIndexTextAsync(string zipUrl, Action<string> log, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(zipUrl))
                return null;

            var assetPath = CdnConfig.ExtractAssetPath(zipUrl);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                _logger?.LogDebug($"[IndexProvider] Could not extract asset path from {zipUrl}");
                return null;
            }

            if (assetPath.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase))
                assetPath = assetPath.Substring(0, assetPath.Length - 4);

            var expected = await _hashResolver(assetPath, ct).ConfigureAwait(false);
            if (expected == null || string.IsNullOrWhiteSpace(expected.Sha256))
            {
                _logger?.LogDebug($"[IndexProvider] No manifest hash for {assetPath} — cannot locate cloud index.");
                return null;
            }

            string hash = expected.Sha256.Trim();
            string indexAssetPath = DeriveIndexPath(assetPath, hash);
            if (indexAssetPath == null)
            {
                _logger?.LogDebug($"[IndexProvider] Could not derive index path from {assetPath}");
                return null;
            }

            string cacheFile = Path.Combine(_cacheRoot, $"{hash}.txt");
            try
            {
                if (File.Exists(cacheFile))
                    return await File.ReadAllTextAsync(cacheFile, Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[IndexProvider] Cache read failed for {hash}: {ex.Message}");
            }

            string url = CdnConfig.BuildUrl(indexAssetPath);
            log($"Fetching index from cloud ({hash[..Math.Min(8, hash.Length)]}…)");
            string? text = await _fetch(url, ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(text))
            {
                _logger?.LogDebug($"[IndexProvider] Index not available at {url}");
                return null;
            }

            try
            {
                Directory.CreateDirectory(_cacheRoot);
                await File.WriteAllTextAsync(cacheFile, text, Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[IndexProvider] Cache write failed for {hash}: {ex.Message}");
            }

            return text;
        }

        public static string? DeriveIndexPath(string assetPath, string hash)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(hash))
                return null;

            var parts = assetPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            int modelsIdx = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("models", StringComparison.OrdinalIgnoreCase))
                {
                    modelsIdx = i;
                    break;
                }
            }

            if (modelsIdx < 0 || modelsIdx + 2 > parts.Length - 1)
                return null;

            string hero = parts[modelsIdx + 1];
            string stem = Path.GetFileNameWithoutExtension(parts[^1]);
            if (string.IsNullOrEmpty(hero) || string.IsNullOrEmpty(stem))
                return null;

            return $"dataset/{hero}/{stem}/{hash}.txt";
        }
    }
}
