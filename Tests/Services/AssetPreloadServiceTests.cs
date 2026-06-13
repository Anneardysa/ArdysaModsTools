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
using System.Linq;
using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetPreloadServiceTests
    {
        private static MiscOption Weather() => new()
        {
            Id = "Weather",
            ThumbnailUrlPattern = "https://cdn.ardysamods.my.id/Assets/misc/weather/{choice}.webp",
            Choices = new List<string> { "Default Weather", "Rainy", "Ash" }
        };

        #region CollectThumbnailUrls

        [Test]
        public void CollectThumbnailUrls_SkipsDefaultChoices_AndBuildsMiscUrls()
        {
            var urls = AssetPreloadService.CollectThumbnailUrls(
                new[] { Weather() }, System.Array.Empty<HeroSummary>());

            Assert.That(urls, Does.Not.Contain("https://cdn.ardysamods.my.id/Assets/misc/weather/default_weather.webp"));
            Assert.That(urls, Does.Contain("https://cdn.ardysamods.my.id/Assets/misc/weather/rainy.webp"));
            Assert.That(urls, Does.Contain("https://cdn.ardysamods.my.id/Assets/misc/weather/ash.webp"));
            Assert.That(urls, Has.Count.EqualTo(2)); // "Default Weather" excluded
        }

        [Test]
        public void CollectThumbnailUrls_PicksFirstImagePerHeroSet_IgnoringNonImages()
        {
            var hero = new HeroSummary
            {
                Name = "npc_dota_hero_abaddon",
                Sets = new Dictionary<string, string[]>
                {
                    ["Set A"] = new[] { "https://cdn/models/abaddon/a.vpk", "https://cdn/models/abaddon/a.png" },
                    ["Set B"] = new[] { "https://cdn/models/abaddon/b.zip" } // no image -> skipped
                }
            };

            var urls = AssetPreloadService.CollectThumbnailUrls(System.Array.Empty<MiscOption>(), new[] { hero });

            Assert.That(urls, Is.EqualTo(new[] { "https://cdn/models/abaddon/a.png" }));
        }

        [Test]
        public void CollectThumbnailUrls_DeduplicatesAcrossSources()
        {
            var shared = "https://cdn/models/shared/thumb.png";
            var h1 = new HeroSummary { Sets = new() { ["S"] = new[] { shared } } };
            var h2 = new HeroSummary { Sets = new() { ["S"] = new[] { shared } } };

            var urls = AssetPreloadService.CollectThumbnailUrls(
                System.Array.Empty<MiscOption>(), new[] { h1, h2 });

            Assert.That(urls.Count(u => u == shared), Is.EqualTo(1));
        }

        [Test]
        public void CollectThumbnailUrls_MergesMiscAndHero()
        {
            var hero = new HeroSummary { Sets = new() { ["S"] = new[] { "https://cdn/models/x/s.webp" } } };

            var urls = AssetPreloadService.CollectThumbnailUrls(new[] { Weather() }, new[] { hero });

            Assert.That(urls, Has.Count.EqualTo(3)); // rainy + ash + hero set
            Assert.That(urls, Does.Contain("https://cdn/models/x/s.webp"));
        }

        [Test]
        public void CollectThumbnailUrls_NullInputs_ReturnEmpty()
        {
            var urls = AssetPreloadService.CollectThumbnailUrls(null!, null!);
            Assert.That(urls, Is.Empty);
        }

        #endregion
    }
}
