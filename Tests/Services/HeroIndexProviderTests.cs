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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroIndexProviderTests
    {
        private const string Hash = "ABC123";
        private const string AxeZipUrl = "https://cdn.ardysamods.my.id/Assets/models/Axe/arcana.zip";

        private static AssetHashEntry Entry(string sha) => new() { Sha256 = sha };
        private static string NewTempDir() =>
            Path.Combine(Path.GetTempPath(), "amt_idx_" + Guid.NewGuid().ToString("N"));

        #region DeriveIndexPath

        [Test]
        public void DeriveIndexPath_FlatZip_MapsToDataset()
        {
            var p = HeroIndexProvider.DeriveIndexPath("Assets/models/Drow_Ranger/base_arcana_1.zip", Hash);
            Assert.That(p, Is.EqualTo($"dataset/Drow_Ranger/base_arcana_1/{Hash}.txt"));
        }

        [Test]
        public void DeriveIndexPath_NestedZip_UsesFirstLevelHeroAndStem()
        {
            var p = HeroIndexProvider.DeriveIndexPath("Assets/models/Drow_Ranger/base_arcana/base_arcana_1.zip", Hash);
            Assert.That(p, Is.EqualTo($"dataset/Drow_Ranger/base_arcana_1/{Hash}.txt"));
        }

        [Test]
        public void DeriveIndexPath_NoModelsSegment_ReturnsNull()
        {
            Assert.That(HeroIndexProvider.DeriveIndexPath("Assets/misc/foo.zip", Hash), Is.Null);
        }

        #endregion

        #region GetIndexTextAsync

        [Test]
        public async Task GetIndexTextAsync_NoManifestHash_ReturnsNull()
        {
            var provider = new HeroIndexProvider(
                hashResolver: (_, _) => Task.FromResult<AssetHashEntry?>(null),
                fetch: (_, _) => Task.FromResult<string?>("should not be called"),
                cacheRoot: NewTempDir());

            var result = await provider.GetIndexTextAsync(AxeZipUrl, _ => { });

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetIndexTextAsync_ResolvesHashBuildsUrlAndReturnsText()
        {
            string? requestedUrl = null;
            var provider = new HeroIndexProvider(
                hashResolver: (_, _) => Task.FromResult<AssetHashEntry?>(Entry(Hash)),
                fetch: (url, _) => { requestedUrl = url; return Task.FromResult<string?>("INDEX-BODY"); },
                cacheRoot: NewTempDir());

            var result = await provider.GetIndexTextAsync(AxeZipUrl, _ => { });

            Assert.That(result, Is.EqualTo("INDEX-BODY"));
            Assert.That(requestedUrl, Does.EndWith($"dataset/Axe/arcana/{Hash}.txt"));
        }

        [Test]
        public async Task GetIndexTextAsync_SecondCall_ServedFromCache()
        {
            int fetchCount = 0;
            var provider = new HeroIndexProvider(
                hashResolver: (_, _) => Task.FromResult<AssetHashEntry?>(Entry(Hash)),
                fetch: (_, _) => { fetchCount++; return Task.FromResult<string?>("BODY"); },
                cacheRoot: NewTempDir());

            await provider.GetIndexTextAsync(AxeZipUrl, _ => { });
            var second = await provider.GetIndexTextAsync(AxeZipUrl, _ => { });

            Assert.That(second, Is.EqualTo("BODY"));
            Assert.That(fetchCount, Is.EqualTo(1), "second call must come from the local cache");
        }

        [Test]
        public async Task GetIndexTextAsync_SplitArchive_ResolvesMergedZipHash()
        {
            string? resolvedAssetPath = null;
            var provider = new HeroIndexProvider(
                hashResolver: (path, _) => { resolvedAssetPath = path; return Task.FromResult<AssetHashEntry?>(Entry(Hash)); },
                fetch: (_, _) => Task.FromResult<string?>("BODY"),
                cacheRoot: NewTempDir());

            await provider.GetIndexTextAsync(
                "https://cdn.ardysamods.my.id/Assets/models/Axe/arcana.zip.001", _ => { });

            Assert.That(resolvedAssetPath, Is.EqualTo("Assets/models/Axe/arcana.zip"));
        }

        #endregion
    }
}
