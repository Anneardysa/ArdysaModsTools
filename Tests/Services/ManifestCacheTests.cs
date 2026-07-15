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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ManifestCacheTests
    {
        private readonly List<string> _created = new();

        [TearDown]
        public void Cleanup()
        {
            foreach (var name in _created)
            {
                TryDelete(ManifestCache.GetManifestPath(name));
                TryDelete(Path.Combine(ManifestCache.DataDirectory, name + ".meta.json"));
            }
            _created.Clear();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch {  }
        }

        private string UniqueName()
        {
            var name = $"test_{Guid.NewGuid():N}.json";
            _created.Add(name);
            return name;
        }

        [Test]
        public void ComputeSha256_IsDeterministic_AndLowercaseHexOfKnownVector()
        {
            const string expected = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

            Assert.That(ManifestCache.ComputeSha256("hello"), Is.EqualTo(expected));
            Assert.That(ManifestCache.ComputeSha256("hello"), Is.Not.EqualTo(ManifestCache.ComputeSha256("world")));
        }

        [Test]
        public void NormalizeJson_StripsBomAndCarriageReturns()
        {
            string input = "\uFEFF{\r\n\"a\":1\r\n}";

            string normalized = ManifestCache.NormalizeJson(input);

            Assert.That(normalized, Is.EqualTo("{\n\"a\":1\n}"));
            Assert.That(normalized.StartsWith("\uFEFF", StringComparison.Ordinal), Is.False);
        }

        [Test]
        public async Task WriteThenRead_RoundTripsContentAndMeta()
        {
            string name = UniqueName();
            string content = "{\n  \"hero\": \"juggernaut\"\n}";
            var meta = new ManifestMeta
            {
                Sha256 = ManifestCache.ComputeSha256(content),
                ETag = "\"etag-123\"",
                LastModified = "Mon, 16 Jun 2026 00:00:00 GMT",
                FetchedAtUtc = DateTime.UtcNow,
                ItemCount = 42,
                Source = "cdn"
            };

            await ManifestCache.WriteAsync(name, content, meta);

            Assert.That(await ManifestCache.ReadAsync(name), Is.EqualTo(content));

            var readMeta = ManifestCache.ReadMeta(name);
            Assert.That(readMeta, Is.Not.Null);
            Assert.That(readMeta!.Sha256, Is.EqualTo(meta.Sha256));
            Assert.That(readMeta.ETag, Is.EqualTo(meta.ETag));
            Assert.That(readMeta.ItemCount, Is.EqualTo(42));
            Assert.That(readMeta.Source, Is.EqualTo("cdn"));
        }

        [Test]
        public async Task ReadAsync_AndReadMeta_ReturnNull_WhenAbsent()
        {
            string name = $"missing_{Guid.NewGuid():N}.json";

            Assert.That(await ManifestCache.ReadAsync(name), Is.Null);
            Assert.That(ManifestCache.ReadMeta(name), Is.Null);
        }

        [Test]
        public void CountSets_ExcludesDefaultSet_AndSumsAcrossHeroes()
        {
            var heroes = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "npc_dota_hero_a",
                    Sets = new Dictionary<string, string[]>
                    {
                        ["Default Set"] = new[] { "default_skin.vpk" },
                        ["Set 1"] = new[] { "a.webp" },
                        ["Set 2"] = new[] { "b.png" }
                    }
                },
                new HeroSummary
                {
                    Name = "npc_dota_hero_b",
                    Sets = new Dictionary<string, string[]>
                    {
                        ["Default Set"] = new[] { "default_skin.vpk" },
                        ["Set 1"] = new[] { "c.webp" }
                    }
                }
            };

            Assert.That(HeroService.CountSets(heroes), Is.EqualTo(3));
        }
    }
}
