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
using System.Collections.Generic;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetHashManifestServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetHashManifestService.Instance.SetManifestForTesting(null);
        }

        #region ParseManifest

        [Test]
        public void ParseManifest_ValidJson_ReturnsEntries()
        {
            const string json = """
            {
              "version": 1,
              "algorithm": "SHA-256",
              "assets": {
                "Assets/models/npc_dota_hero_abaddon/set1/model.zip": { "sha256": "ABCDEF", "size": 1234 },
                "Assets/Original.zip": { "sha256": "999", "size": 5678 }
              }
            }
            """;

            var map = AssetHashManifestService.ParseManifest(json);

            Assert.That(map, Is.Not.Null);
            Assert.That(map!.Count, Is.EqualTo(2));
            Assert.That(map["Assets/Original.zip"].Sha256, Is.EqualTo("999"));
            Assert.That(map["Assets/Original.zip"].Size, Is.EqualTo(5678));
        }

        [Test]
        public void ParseManifest_KeysAreCaseInsensitive()
        {
            const string json = """
            { "assets": { "Assets/Models/Abc/model.zip": { "sha256": "AA", "size": 1 } } }
            """;

            var map = AssetHashManifestService.ParseManifest(json);

            Assert.That(map!.ContainsKey("assets/models/abc/model.zip"), Is.True);
        }

        [Test]
        public void ParseManifest_SkipsEntriesWithoutSha()
        {
            const string json = """
            { "assets": {
                "a.zip": { "size": 1 },
                "b.zip": { "sha256": "BB", "size": 2 }
            } }
            """;

            var map = AssetHashManifestService.ParseManifest(json);

            Assert.That(map!.Count, Is.EqualTo(1));
            Assert.That(map.ContainsKey("b.zip"), Is.True);
        }

        [Test]
        public void ParseManifest_MalformedJson_ReturnsNull()
        {
            Assert.That(AssetHashManifestService.ParseManifest("not json"), Is.Null);
        }

        [Test]
        public void ParseManifest_NoAssetsObject_ReturnsNull()
        {
            Assert.That(AssetHashManifestService.ParseManifest("{ \"version\": 1 }"), Is.Null);
        }

        #endregion

        #region GetExpectedAsync

        [Test]
        public async Task GetExpectedAsync_KnownAsset_ReturnsEntry()
        {
            AssetHashManifestService.Instance.SetManifestForTesting(new Dictionary<string, AssetHashEntry>
            {
                ["Assets/Original.zip"] = new AssetHashEntry { Sha256 = "ABC", Size = 42 }
            });

            var entry = await AssetHashManifestService.Instance.GetExpectedAsync("Assets/Original.zip");

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Sha256, Is.EqualTo("ABC"));
        }

        [Test]
        public async Task GetExpectedAsync_UnknownAsset_ReturnsNull()
        {
            AssetHashManifestService.Instance.SetManifestForTesting(new Dictionary<string, AssetHashEntry>());

            var entry = await AssetHashManifestService.Instance.GetExpectedAsync("Assets/missing.zip");

            Assert.That(entry, Is.Null);
        }

        [Test]
        public async Task GetExpectedAsync_NullOrEmptyPath_ReturnsNull()
        {
            Assert.That(await AssetHashManifestService.Instance.GetExpectedAsync(null), Is.Null);
            Assert.That(await AssetHashManifestService.Instance.GetExpectedAsync(""), Is.Null);
        }

        #endregion
    }
}
