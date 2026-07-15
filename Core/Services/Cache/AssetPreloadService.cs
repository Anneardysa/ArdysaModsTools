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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Core.Services.Cache
{
    public sealed class AssetPreloadService : IAssetPreloadService
    {
        private static readonly TimeSpan MissingTtl = TimeSpan.FromDays(7);

        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

        private static readonly string[] ThumbnailExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _heroBaseFolder;

        private volatile bool _isRunning;
        private volatile bool _isComplete;

        public AssetPreloadService() : this(AppContext.BaseDirectory) { }

        public AssetPreloadService(string heroBaseFolder)
        {
            _heroBaseFolder = heroBaseFolder ?? AppContext.BaseDirectory;
        }

        public bool IsRunning => _isRunning;

        public bool IsComplete => _isComplete;

        public async Task PreloadAllAsync(IProgress<AssetPreloadProgress>? progress = null, CancellationToken ct = default)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
                return;

            _isRunning = true;
            try
            {
                await Task.Delay(InitialDelay, ct).ConfigureAwait(false);

                progress?.Report(new AssetPreloadProgress(AssetPreloadPhase.Enumerating, 0, 0));

                var urls = await CollectAllUrlsAsync(ct).ConfigureAwait(false);

                var cache = AssetCacheService.Instance;
                var pending = urls
                    .Where(u => !cache.IsCached(u) && !cache.IsKnownMissing(u, MissingTtl))
                    .ToList();

                if (pending.Count == 0)
                {
                    _isComplete = true;
                    progress?.Report(new AssetPreloadProgress(AssetPreloadPhase.Complete, urls.Count, urls.Count));
                    return;
                }

                var inner = new Progress<(int current, int total, string url)>(p =>
                    progress?.Report(new AssetPreloadProgress(AssetPreloadPhase.Downloading, p.current, p.total)));

                var (downloaded, skipped, failed) = await cache
                    .PreloadAssetsWithProgressAsync(pending, inner, ct)
                    .ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine(
                    $"[AssetPreloadService] Preload done: {downloaded} downloaded, {skipped} skipped, {failed} failed of {urls.Count} total");

                _isComplete = true;
                progress?.Report(new AssetPreloadProgress(AssetPreloadPhase.Complete, urls.Count, urls.Count, failed));
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[AssetPreloadService] Preload cancelled.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetPreloadService] Preload error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _gate.Release();
            }
        }

        private async Task<List<string>> CollectAllUrlsAsync(CancellationToken ct)
        {
            List<MiscOption> miscOptions = new();
            try
            {
                await MiscCategoryService.PreloadConfigAsync().ConfigureAwait(false);
                miscOptions = MiscCategoryService.GetAllOptions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetPreloadService] Misc enumeration failed: {ex.Message}");
            }

            ct.ThrowIfCancellationRequested();

            List<HeroSummary> heroes = new();
            try
            {
                heroes = await new HeroService(_heroBaseFolder).LoadHeroesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetPreloadService] Hero enumeration failed: {ex.Message}");
            }

            return CollectThumbnailUrls(miscOptions, heroes);
        }

        public static List<string> CollectThumbnailUrls(
            IEnumerable<MiscOption> miscOptions,
            IEnumerable<HeroSummary> heroes)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            void Add(string? url)
            {
                if (!string.IsNullOrEmpty(url) && seen.Add(url))
                    result.Add(url);
            }

            if (miscOptions != null)
            {
                foreach (var option in miscOptions)
                {
                    if (option?.Choices == null) continue;
                    foreach (var choice in option.Choices)
                        Add(option.GetThumbnailUrl(choice));
                }
            }

            if (heroes != null)
            {
                foreach (var hero in heroes)
                {
                    if (hero?.Sets == null) continue;
                    foreach (var set in hero.Sets.Values)
                    {
                        var thumb = set?.FirstOrDefault(u =>
                            !string.IsNullOrEmpty(u) &&
                            ThumbnailExtensions.Any(ext => u.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                        Add(thumb);
                    }
                }
            }

            return result;
        }
    }
}
