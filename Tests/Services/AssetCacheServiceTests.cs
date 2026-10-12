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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Core.Services.Cdn;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetCacheServiceTests
    {
        private string _dir = null!;
        private AssetCacheService _cache = null!;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "amt-assetcache-test-" + Guid.NewGuid().ToString("N"));
            _cache = new AssetCacheService(_dir,
                (_, _) => Task.FromResult(new AssetFetchResult(null, null, null, false, null)));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch {  }
        }

        #region IsKnownMissing

        [Test]
        public void IsKnownMissing_EmptyUrl_ReturnsFalse()
        {
            Assert.That(_cache.IsKnownMissing("", TimeSpan.FromDays(7)), Is.False);
        }

        [Test]
        public void IsKnownMissing_UnmarkedUrl_ReturnsFalse()
        {
            var unique = $"https://cdn.ardysamods.my.id/Assets/misc/never/{Guid.NewGuid():N}.png";
            Assert.That(_cache.IsKnownMissing(unique, TimeSpan.FromDays(7)), Is.False);
        }

        #endregion

        #region InvalidateOnEpochChange


        [Test]
        public void InvalidateOnEpochChange_FirstObservation_DoesNotInvalidate()
        {
            var epochKey = $"test-epoch-{Guid.NewGuid():N}";
            var url = $"https://cdn.ardysamods.my.id/Assets/misc/{Guid.NewGuid():N}.png";

            var changed = _cache.InvalidateOnEpochChange(epochKey, "1.0.0", new[] { url });

            Assert.That(changed, Is.False, "the first time an epoch key is seen there is nothing to diff against");
        }

        [Test]
        public void InvalidateOnEpochChange_SameVersionAgain_DoesNotInvalidate()
        {
            var epochKey = $"test-epoch-{Guid.NewGuid():N}";
            var url = $"https://cdn.ardysamods.my.id/Assets/misc/{Guid.NewGuid():N}.png";
            var cache = _cache;

            cache.InvalidateOnEpochChange(epochKey, "1.0.0", new[] { url });
            var changedAgain = cache.InvalidateOnEpochChange(epochKey, "1.0.0", new[] { url });

            Assert.That(changedAgain, Is.False, "an unchanged version must not re-invalidate on every open");
        }

        [Test]
        public void InvalidateOnEpochChange_VersionMoves_InvalidatesGivenUrlsAndReturnsTrue()
        {
            var epochKey = $"test-epoch-{Guid.NewGuid():N}";
            var url = $"https://cdn.ardysamods.my.id/Assets/misc/{Guid.NewGuid():N}.png";
            var cache = _cache;

            cache.InvalidateOnEpochChange(epochKey, "1.0.0", new[] { url });
            var changed = cache.InvalidateOnEpochChange(epochKey, "1.0.1", new[] { url });

            Assert.That(changed, Is.True);
            Assert.That(cache.IsCached(url), Is.False, "the URL must be invalidated when its epoch changes");
        }

        [Test]
        public void InvalidateOnEpochChange_EmptyVersion_DoesNothing()
        {
            var epochKey = $"test-epoch-{Guid.NewGuid():N}";
            var url = $"https://cdn.ardysamods.my.id/Assets/misc/{Guid.NewGuid():N}.png";

            var changed = _cache.InvalidateOnEpochChange(epochKey, "", new[] { url });

            Assert.That(changed, Is.False);
        }

        #endregion

        #region IsNotFoundError


        [TestCase("HTTP 404")]
        [TestCase("HTTP 403")]
        public void IsNotFoundError_DefinitiveMisses_AreRecorded(string error)
        {
            Assert.That(AssetCacheService.IsNotFoundError(error), Is.True);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("HTTP 500")]
        [TestCase("The operation was canceled")]
        public void IsNotFoundError_TransientOrEmpty_IsNotRecorded(string? error)
        {
            Assert.That(AssetCacheService.IsNotFoundError(error), Is.False);
        }

        [Test]
        public void IsNotFoundError_GuardRefusal_IsNotRecorded()
        {
            Assert.That(AssetCacheService.IsNotFoundError("HTTP 401"), Is.False);
            Assert.That(
                AssetCacheService.IsNotFoundError($"{ErrorCodes.DL_CDN_UNAUTHORIZED}: {CdnAuthFailure.Reason}"),
                Is.False);
        }

        [Test]
        public void IsNotFoundError_MissOnAnAssetWhoseNameContains401_IsStillRecorded()
        {
            Assert.That(AssetCacheService.IsNotFoundError(
                "HTTP 404 for https://cdn.ardysamods.my.id/Assets/models/Sven/x401.zip"), Is.True);
            Assert.That(AssetCacheService.IsNotFoundError(
                "HTTP 404 (sha256 c401ab...)"), Is.True);
        }

        #endregion
    }

    [TestFixture]
    public class AssetCacheServiceIsolatedTests
    {
        private string _dir = null!;
        private int _fetchCalls;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "amt-assetcache-test-" + Guid.NewGuid().ToString("N"));
            _fetchCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch {  }
        }

        private static string Url(int i) => $"https://example.test/thumbs/{i}.png";

        private static List<string> Urls(int n) => Enumerable.Range(0, n).Select(Url).ToList();

        private static byte[] Bytes(string url) => Encoding.UTF8.GetBytes("img:" + url);

        private AssetCacheService Cache(Func<string, AssetFetchResult>? respond = null, int delayMs = 0,
            Action? onEnter = null, Action? onExit = null)
        {
            respond ??= u => new AssetFetchResult(Bytes(u), "\"etag\"", null, false, u);
            return new AssetCacheService(_dir, async (url, ct) =>
            {
                Interlocked.Increment(ref _fetchCalls);
                onEnter?.Invoke();
                try
                {
                    if (delayMs > 0) await Task.Delay(delayMs, ct);
                    return respond(url);
                }
                finally { onExit?.Invoke(); }
            });
        }

        private sealed class Recorder : IProgress<(int current, int total, string url)>
        {
            public readonly List<(int current, int total)> Reports = new();
            public void Report((int current, int total, string url) value)
            {
                lock (Reports) Reports.Add((value.current, value.total));
            }
        }

        [Test]
        public async Task Downloaded_Asset_SurvivesRestart_WithoutRefetch()
        {
            var url = Url(1);
            var first = await Cache().GetAssetBytesAsync(url);
            Assert.That(first, Is.EqualTo(Bytes(url)));
            Assert.That(_fetchCalls, Is.EqualTo(1));

            var restarted = Cache();
            Assert.That(restarted.IsCached(url), Is.True);
            Assert.That(await restarted.GetAssetBytesAsync(url), Is.EqualTo(Bytes(url)));
            Assert.That(_fetchCalls, Is.EqualTo(1), "a cached thumbnail must never be downloaded again");
        }

        [Test]
        public async Task Preload_SecondRun_FetchesNothing()
        {
            var urls = Urls(20);
            var (downloaded, _, failed) = await Cache().PreloadAssetsWithProgressAsync(urls);
            Assert.That((downloaded, failed), Is.EqualTo((20, 0)));

            _fetchCalls = 0;
            var (downloaded2, skipped2, failed2) = await Cache().PreloadAssetsWithProgressAsync(urls);

            Assert.That(_fetchCalls, Is.Zero);
            Assert.That((downloaded2, skipped2, failed2), Is.EqualTo((0, 20, 0)));
        }

        [Test]
        public async Task Preload_Progress_IsMonotonic_AndEndsAtTotal()
        {
            var urls = Urls(60);
            var progress = new Recorder();

            await Cache(delayMs: 2).PreloadAssetsWithProgressAsync(urls, progress);

            var currents = progress.Reports.Select(r => r.current).ToList();
            Assert.That(currents, Is.Ordered.Ascending, "the launch log must never jump backwards");
            Assert.That(currents, Is.EqualTo(Enumerable.Range(1, 60)), "one report per finished asset");
            Assert.That(progress.Reports.Last(), Is.EqualTo((60, 60)));
        }

        [Test]
        public async Task Preload_NeverExceedsConcurrencyCap()
        {
            int inFlight = 0, peak = 0;
            var cache = Cache(delayMs: 15,
                onEnter: () =>
                {
                    int now = Interlocked.Increment(ref inFlight);
                    int seen;
                    while (now > (seen = Volatile.Read(ref peak)) &&
                           Interlocked.CompareExchange(ref peak, now, seen) != seen) { }
                },
                onExit: () => Interlocked.Decrement(ref inFlight));

            await cache.PreloadAssetsWithProgressAsync(Urls(40));

            Assert.That(peak, Is.LessThanOrEqualTo(AssetCacheService.PreloadConcurrency));
            Assert.That(peak, Is.GreaterThan(1), "downloads should actually run in parallel");
        }

        [Test]
        public async Task FailedFetch_IsNotCached_AndCountsAsFailed()
        {
            var url = Url(7);
            var cache = Cache(_ => new AssetFetchResult(null, null, null, false, null));

            Assert.That(await cache.GetAssetBytesAsync(url), Is.Null);
            Assert.That(cache.IsCached(url), Is.False,
                "the in-memory negative entry of a failed download is not a cached asset");

            var (downloaded, skipped, failed) = await cache.PreloadAssetsWithProgressAsync(new[] { url });
            Assert.That((downloaded, skipped, failed), Is.EqualTo((0, 0, 1)),
                "a failure must be reported as unavailable, not skipped");
        }

        [Test]
        public async Task NotFound_PersistsAsKnownMissing_TransientDoesNot()
        {
            var missing = Url(404);
            var flaky = Url(500);
            var cache = Cache(u => new AssetFetchResult(null, null, null, NotFound: u == missing, null));

            await cache.GetAssetBytesAsync(missing);
            await cache.GetAssetBytesAsync(flaky);

            var restarted = Cache();
            Assert.That(restarted.IsKnownMissing(missing, TimeSpan.FromDays(7)), Is.True);
            Assert.That(restarted.IsKnownMissing(flaky, TimeSpan.FromDays(7)), Is.False,
                "a transient failure must be retried next launch");
        }

        [Test]
        public void Preload_Cancelled_LeavesOnlyReadableCacheFiles()
        {
            using var cts = new CancellationTokenSource();
            var cache = Cache(delayMs: 20);
            cts.CancelAfter(30);

            Assert.That(async () => await cache.PreloadAssetsWithProgressAsync(Urls(40), null, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());

            foreach (var file in Directory.GetFiles(_dir, "*" + AssetCacheFile.Extension))
                Assert.That(AssetCacheFile.IsValidCacheFile(file), Is.True, $"corrupt cache file left behind: {file}");
        }


        [Test]
        public void EpochSelector_FirstObservationAndSameValue_NeverAskForUrls()
        {
            var cache = Cache();
            int calls = 0;
            IEnumerable<string> Select(string _) { calls++; return Urls(3); }

            Assert.That(cache.InvalidateOnEpochChange("k", "1|2026-09-28", Select), Is.False);
            Assert.That(cache.InvalidateOnEpochChange("k", "1|2026-09-28", Select), Is.False);
            Assert.That(calls, Is.Zero, "no change means nothing to resolve, let alone invalidate");
        }

        [Test]
        public async Task EpochSelector_ValueMoves_GetsPreviousValue_AndInvalidatesOnlyItsUrls()
        {
            var cache = Cache();
            var urls = Urls(10);
            await cache.PreloadAssetsWithProgressAsync(urls);
            cache.InvalidateOnEpochChange("k", "1|2026-09-28", _ => urls);

            string? seen = null;
            var changed = cache.InvalidateOnEpochChange("k", "2|2026-09-29", last => { seen = last; return urls.Take(2); });

            Assert.That(changed, Is.True);
            Assert.That(seen, Is.EqualTo("1|2026-09-28"));
            Assert.That(urls.Count(cache.IsCached), Is.EqualTo(8), "untouched thumbnails must stay cached");
            Assert.That(cache.IsCached(urls[0]) || cache.IsCached(urls[1]), Is.False);

            Assert.That(Cache().InvalidateOnEpochChange("k", "2|2026-09-29", _ => urls), Is.False);
        }

        [Test]
        public async Task EpochChange_ForgetsMisses_SoANewlyUploadedAssetLoads()
        {
            var late = Url(1);
            bool uploaded = false;
            var cache = Cache(u => uploaded
                ? new AssetFetchResult(Bytes(u), null, null, false, u)
                : new AssetFetchResult(null, null, null, NotFound: true, null));

            cache.InvalidateOnEpochChange("k", "1", Array.Empty<string>());
            Assert.That(await cache.GetAssetBytesAsync(late), Is.Null);
            Assert.That(cache.IsKnownMissing(late, TimeSpan.FromDays(7)), Is.True);

            uploaded = true;
            Assert.That(await cache.GetAssetBytesAsync(late), Is.Null.Or.Empty, "still inside the miss window");

            Assert.That(cache.InvalidateOnEpochChange("k", "2", Array.Empty<string>()), Is.True);
            Assert.That(cache.IsKnownMissing(late, TimeSpan.FromDays(7)), Is.False);
            Assert.That(Cache().IsKnownMissing(late, TimeSpan.FromDays(7)), Is.False, "forgotten on disk too");
            Assert.That(await cache.GetAssetBytesAsync(late), Is.EqualTo(Bytes(late)));
        }

        [Test]
        public async Task SameEpoch_KeepsMisses()
        {
            var gone = Url(2);
            var cache = Cache(_ => new AssetFetchResult(null, null, null, NotFound: true, null));
            cache.InvalidateOnEpochChange("k", "1", Array.Empty<string>());
            await cache.GetAssetBytesAsync(gone);

            cache.InvalidateOnEpochChange("k", "1", Array.Empty<string>());

            Assert.That(cache.IsKnownMissing(gone, TimeSpan.FromDays(7)), Is.True,
                "an unchanged dataset must not re-fetch permanently absent files on every open");
        }


        [TestCase("Assets/models/axe/thumb.jpg")]
        [TestCase("not a url")]
        public void RelativeOrMalformedUrl_QueriesReturnFalse_InsteadOfThrowing(string url)
        {
            var cache = Cache();

            Assert.That(cache.IsCached(url), Is.False);
            Assert.That(cache.IsKnownMissing(url, TimeSpan.FromDays(7)), Is.False);
            Assert.DoesNotThrow(() => cache.Invalidate(url));
        }

        [Test]
        public async Task Preload_OneMalformedUrl_CountsAsFailed_OthersStillDownload()
        {
            var urls = Urls(5);
            urls.Insert(2, "Assets/models/axe/thumb.jpg");
            var progress = new Recorder();

            var (downloaded, skipped, failed) = await Cache().PreloadAssetsWithProgressAsync(urls, progress);

            Assert.That((downloaded, skipped, failed), Is.EqualTo((5, 0, 1)));
            Assert.That(progress.Reports.Last(), Is.EqualTo((6, 6)));
            Assert.That(_fetchCalls, Is.EqualTo(5), "a URL that can't be fetched must not reach the network");
        }
    }
}
