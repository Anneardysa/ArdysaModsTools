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
using System.Linq;
using NUnit.Framework;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroServiceTests
    {
        private HeroService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroService(AppDomain.CurrentDomain.BaseDirectory);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithBaseFolder_CreatesInstance()
        {
            var service = new HeroService(Path.GetTempPath());

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullFolder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new HeroService(null!);
            });
        }

        #endregion

        #region LoadHeroesAsync Tests

        [Test]
        public async Task LoadHeroesAsync_ReturnsListOrThrows()
        {
            try
            {
                var result = await _service.LoadHeroesAsync();
                
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<List<HeroSummary>>());
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or FileNotFoundException)
            {
                Assert.Pass("Network or file not available in test environment");
            }
        }

        [Test]
        public async Task LoadHeroesAsync_ReturnsHeroesWithRequiredProperties()
        {
            try
            {
                var result = await _service.LoadHeroesAsync();
                
                if (result.Count > 0)
                {
                    var firstHero = result[0];
                    Assert.That(firstHero.Name, Is.Not.Null.And.Not.Empty);
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or FileNotFoundException)
            {
                Assert.Pass("Network or file not available in test environment");
            }
        }

        #endregion

        #region Styled Set Parsing Tests

        private const string StyledHeroJson = @"
        [
          {
            ""name"": ""antimage"",
            ""used_by_heroes"": ""npc_dota_hero_antimage"",
            ""primary_attr"": ""agility"",
            ""sets"": {
              ""Legacy Set"": [""https://cdn/set.zip"", ""https://cdn/thumb.jpg""],
              ""Manifold Paradox"": {
                ""thumbnail"": ""https://cdn/mp_cover.png"",
                ""styles"": {
                  ""Default"":   [""https://cdn/mp_default.zip"",   ""https://cdn/mp_default.jpg""],
                  ""Corrupted"": [""https://cdn/mp_corrupted.zip"", ""https://cdn/mp_corrupted.jpg""]
                }
              }
            }
          }
        ]";

        [Test]
        public void ParseHeroesJson_StyledSet_FlattensIntoSetsAndPopulatesSetStyles()
        {
            var heroes = HeroService.ParseHeroesJson(StyledHeroJson);

            Assert.That(heroes, Has.Count.EqualTo(1));
            var hero = heroes[0];

            Assert.That(hero.Sets.Keys, Is.EquivalentTo(new[]
            {
                "Legacy Set",
                "Manifold Paradox (Default)",
                "Manifold Paradox (Corrupted)"
            }));
            Assert.That(hero.Sets["Manifold Paradox (Default)"],
                Is.EqualTo(new[] { "https://cdn/mp_default.zip", "https://cdn/mp_default.jpg" }));

            Assert.That(hero.SetStyles.Keys, Is.EquivalentTo(new[]
            {
                "Manifold Paradox (Default)",
                "Manifold Paradox (Corrupted)"
            }));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Group, Is.EqualTo("Manifold Paradox"));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Label, Is.EqualTo("Corrupted"));
            Assert.That(hero.SetStyles.ContainsKey("Legacy Set"), Is.False);

            Assert.That(hero.SetStyles["Manifold Paradox (Default)"].GroupThumbnail, Is.EqualTo("https://cdn/mp_cover.png"));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].GroupThumbnail, Is.EqualTo("https://cdn/mp_cover.png"));
        }

        [Test]
        public void ParseHeroesJson_MalformedStyles_SkippedButHeroAndValidStylesKept()
        {
            const string json = @"
            [
              {
                ""name"": ""juggernaut"",
                ""sets"": {
                  ""Mixed"": {
                    ""styles"": {
                      ""Good"":     [""https://cdn/good.zip""],
                      ""BadValue"": ""not-an-array"",
                      """":         [""https://cdn/blank.zip""],
                      ""Empty"":    []
                    }
                  }
                }
              }
            ]";

            var heroes = HeroService.ParseHeroesJson(json);

            Assert.That(heroes, Has.Count.EqualTo(1));
            var hero = heroes[0];
            Assert.That(hero.Sets.Keys, Is.EquivalentTo(new[] { "Mixed (Good)" }));
            Assert.That(hero.SetStyles.Keys, Is.EquivalentTo(new[] { "Mixed (Good)" }));
            Assert.That(hero.SetStyles["Mixed (Good)"].Label, Is.EqualTo("Good"));
            Assert.That(hero.SetStyles["Mixed (Good)"].GroupThumbnail, Is.Null);
        }

        [Test]
        public void ParseHeroesJson_LegacyArraySet_ParsedUnchangedWithNoStyles()
        {
            const string json = @"
            [
              {
                ""name"": ""crystal_maiden"",
                ""sets"": {
                  ""Frost Avalanche"": [""https://cdn/fa.zip"", ""https://cdn/fa.jpg""]
                }
              }
            ]";

            var heroes = HeroService.ParseHeroesJson(json);

            Assert.That(heroes, Has.Count.EqualTo(1));
            var hero = heroes[0];
            Assert.That(hero.Sets.Keys, Is.EquivalentTo(new[] { "Frost Avalanche" }));
            Assert.That(hero.Sets["Frost Avalanche"],
                Is.EqualTo(new[] { "https://cdn/fa.zip", "https://cdn/fa.jpg" }));
            Assert.That(hero.SetStyles, Is.Empty);
        }

        #endregion
    }
}

