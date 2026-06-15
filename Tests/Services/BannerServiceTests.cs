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
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="BannerService.Parse"/> — the pure parsing seam of the banner manifest
    /// fetcher (the network/CDN path uses a process-wide singleton and is not unit-tested here).
    /// </summary>
    [TestFixture]
    public class BannerServiceTests
    {
        [Test]
        public void Parse_ValidManifest_ReturnsSlidesInOrder()
        {
            const string json = """
            {
              "slides": [
                { "image": "Assets/image/banner/slide1.jpg", "link": "https://ardysamods.my.id", "title": "New Set" },
                { "image": "Assets/image/banner/slide2.jpg" }
              ]
            }
            """;

            var config = BannerService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Slides, Has.Count.EqualTo(2));
            Assert.That(config.Slides[0].Image, Is.EqualTo("Assets/image/banner/slide1.jpg"));
            Assert.That(config.Slides[0].Link, Is.EqualTo("https://ardysamods.my.id"));
            Assert.That(config.Slides[0].Title, Is.EqualTo("New Set"));
            Assert.That(config.Slides[1].Image, Is.EqualTo("Assets/image/banner/slide2.jpg"));
            Assert.That(config.Slides[1].Link, Is.Null);
        }

        [Test]
        public void Parse_ReadsModspackVersion()
        {
            const string json = """
            {
              "modspackVersion": "2.6",
              "slides": [ { "image": "Assets/image/banner/slide1.jpg" } ]
            }
            """;

            var config = BannerService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.ModspackVersion, Is.EqualTo("2.6"));
        }

        [Test]
        public void Parse_ModspackVersionAbsent_IsNull()
        {
            const string json = """
            { "slides": [ { "image": "Assets/image/banner/slide1.jpg" } ] }
            """;

            var config = BannerService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.ModspackVersion, Is.Null);
        }

        [Test]
        public void Parse_DropsSlidesWithoutImage()
        {
            const string json = """
            {
              "slides": [
                { "image": "Assets/image/banner/ok.jpg" },
                { "image": "", "title": "no image" },
                { "title": "missing image field" }
              ]
            }
            """;

            var config = BannerService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Slides, Has.Count.EqualTo(1));
            Assert.That(config.Slides[0].Image, Is.EqualTo("Assets/image/banner/ok.jpg"));
        }

        [Test]
        public void Parse_EmptyOrNoUsableSlides_ReturnsNull()
        {
            Assert.That(BannerService.Parse(null), Is.Null);
            Assert.That(BannerService.Parse(""), Is.Null);
            Assert.That(BannerService.Parse("   "), Is.Null);
            Assert.That(BannerService.Parse("{ \"slides\": [] }"), Is.Null);
            Assert.That(BannerService.Parse("{ \"slides\": [ { \"title\": \"x\" } ] }"), Is.Null);
        }

        [Test]
        public void Parse_InvalidJson_ReturnsNull()
        {
            Assert.That(BannerService.Parse("not json"), Is.Null);
            Assert.That(BannerService.Parse("{ \"slides\": "), Is.Null);
        }
    }
}
