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
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for the shared <see cref="HeroModelMapper"/> utility class.
    /// Validates mapping, IsCustomSet, and FormatHeroIdAsName.
    /// </summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class HeroModelMapperTests
    {
        #region MapFromSummaries Tests

        [Test]
        public void MapFromSummaries_NullList_ReturnsEmpty()
        {
            // Act
            var result = HeroModelMapper.MapFromSummaries(null!);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_EmptyList_ReturnsEmpty()
        {
            // Act
            var result = HeroModelMapper.MapFromSummaries(new List<HeroSummary>());

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_MapsAllFields_Correctly()
        {
            // Arrange
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Anti-Mage",
                    UsedByHeroes = "npc_dota_hero_antimage",
                    PrimaryAttr = "agi",
                    Ids = new[] { "1", "42", "99" },
                    Sets = new Dictionary<string, string[]>
                    {
                        { "Default Set", new[] { "https://cdn.example.com/set1.zip", "https://cdn.example.com/thumb.jpg" } },
                        { "Arcana", new[] { "https://cdn.example.com/arcana.zip" } }
                    }
                }
            };

            // Act
            var result = HeroModelMapper.MapFromSummaries(summaries);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));

            var hero = result[0];
            Assert.That(hero.Name, Is.EqualTo("npc_dota_hero_antimage"));
            Assert.That(hero.HeroId, Is.EqualTo("npc_dota_hero_antimage"));
            Assert.That(hero.LocalizedName, Is.EqualTo("Anti-Mage"));
            Assert.That(hero.PrimaryAttribute, Is.EqualTo("agi"));
            Assert.That(hero.ItemIds, Is.EquivalentTo(new[] { 1, 42, 99 }));
            Assert.That(hero.Sets, Has.Count.EqualTo(2));
            Assert.That(hero.Sets.ContainsKey("Default Set"), Is.True);
            Assert.That(hero.Sets.ContainsKey("Arcana"), Is.True);
        }

        [Test]
        public void MapFromSummaries_CopiesMethod()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary { Name = "Abaddon", UsedByHeroes = "npc_dota_hero_abaddon", Method = 1 },
                new HeroSummary { Name = "Axe", UsedByHeroes = "npc_dota_hero_axe", Method = null }
            };

            var result = HeroModelMapper.MapFromSummaries(summaries);

            Assert.That(result[0].Method, Is.EqualTo(1));
            Assert.That(result[1].Method, Is.Null);
        }

        [Test]
        public void MapFromSummaries_CopiesSetStyles()
        {
            // Arrange — a styled set already flattened by the parser (key = "Group (Label)").
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Anti-Mage",
                    UsedByHeroes = "npc_dota_hero_antimage",
                    Sets = new Dictionary<string, string[]>
                    {
                        { "Manifold Paradox (Default)", new[] { "https://cdn/mp_default.zip" } },
                        { "Manifold Paradox (Corrupted)", new[] { "https://cdn/mp_corrupted.zip" } }
                    },
                    SetStyles = new Dictionary<string, SetStyleInfo>
                    {
                        { "Manifold Paradox (Default)", new SetStyleInfo { Group = "Manifold Paradox", Label = "Default" } },
                        { "Manifold Paradox (Corrupted)", new SetStyleInfo { Group = "Manifold Paradox", Label = "Corrupted" } }
                    }
                }
            };

            // Act
            var hero = HeroModelMapper.MapFromSummaries(summaries)[0];

            // Assert
            Assert.That(hero.SetStyles, Has.Count.EqualTo(2));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Group, Is.EqualTo("Manifold Paradox"));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Label, Is.EqualTo("Corrupted"));
            // Case-insensitive lookup (matches Sets dictionary behavior).
            Assert.That(hero.SetStyles.ContainsKey("manifold paradox (default)"), Is.True);
        }

        [Test]
        public void MapFromSummaries_NoSetStyles_UsesEmptyDictionary()
        {
            // Arrange — legacy summary with no styles.
            var summaries = new List<HeroSummary>
            {
                new HeroSummary { Name = "Axe", UsedByHeroes = "npc_dota_hero_axe" }
            };

            // Act
            var hero = HeroModelMapper.MapFromSummaries(summaries)[0];

            // Assert
            Assert.That(hero.SetStyles, Is.Not.Null);
            Assert.That(hero.SetStyles, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_MissingSets_UsesEmptyDictionary()
        {
            // Arrange
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Axe",
                    UsedByHeroes = "npc_dota_hero_axe",
                    PrimaryAttr = "str"
                    // No Sets defined — default is empty Dictionary
                }
            };

            // Act
            var result = HeroModelMapper.MapFromSummaries(summaries);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Sets, Is.Not.Null);
            // The mapper only creates a new Sets dict if source has entries,
            // otherwise falls through to the default from HeroModel constructor
        }

        [Test]
        public void MapFromSummaries_FallsBackToUniversal_WhenNoAttribute()
        {
            // Arrange
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Test Hero",
                    UsedByHeroes = "npc_dota_hero_test",
                    PrimaryAttr = ""  // empty = should default to "universal"
                }
            };

            // Act
            var result = HeroModelMapper.MapFromSummaries(summaries);

            // Assert
            Assert.That(result[0].PrimaryAttribute, Is.EqualTo("universal"));
        }

        [Test]
        public void MapFromSummaries_ParsesItemIds_FromStringArray()
        {
            // Arrange
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Crystal Maiden",
                    UsedByHeroes = "npc_dota_hero_crystal_maiden",
                    Ids = new[] { "10", "invalid", "20", "10" } // includes invalid and duplicate
                }
            };

            // Act
            var result = HeroModelMapper.MapFromSummaries(summaries);

            // Assert — should parse valid ints, skip invalid, deduplicate, and sort
            Assert.That(result[0].ItemIds, Is.EquivalentTo(new[] { 10, 20 }));
        }

        [Test]
        public void MapFromSummaries_UsesName_WhenUsedByHeroesEmpty()
        {
            // Arrange
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "FallbackHero",
                    UsedByHeroes = "",  // empty
                    PrimaryAttr = "int"
                }
            };

            // Act
            var result = HeroModelMapper.MapFromSummaries(summaries);

            // Assert — should use Name as fallback for HeroId/Name
            Assert.That(result[0].HeroId, Is.EqualTo("FallbackHero"));
            Assert.That(result[0].Name, Is.EqualTo("FallbackHero"));
        }

        #endregion

        #region IsCustomSet Tests

        [Test]
        public void IsCustomSet_NullOrEmpty_ReturnsFalse()
        {
            Assert.That(HeroModelMapper.IsCustomSet((List<string>?)null), Is.False);
            Assert.That(HeroModelMapper.IsCustomSet(new List<string>()), Is.False);
        }

        [Test]
        public void IsCustomSet_MixPrefix_ReturnsTrue()
        {
            // Arrange
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/mix_arcana_antimage.zip"
            };

            // Act & Assert
            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.True);
        }

        [Test]
        public void IsCustomSet_NormalSet_ReturnsFalse()
        {
            // Arrange
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/arcana_antimage.zip"
            };

            // Act & Assert
            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.False);
        }

        [Test]
        public void IsCustomSet_NoArchiveUrl_ReturnsFalse()
        {
            // Arrange — only images, no archive
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/preview.png"
            };

            // Act & Assert
            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.False);
        }

        [Test]
        public void IsCustomSet_DictionaryOverload_FindsCorrectSet()
        {
            // Arrange
            var sets = new Dictionary<string, List<string>>
            {
                { "Normal Set", new List<string> { "https://cdn.example.com/normal.zip" } },
                { "Mix Set", new List<string> { "https://cdn.example.com/mix_special.zip" } }
            };

            // Act & Assert
            Assert.That(HeroModelMapper.IsCustomSet(sets, "Normal Set"), Is.False);
            Assert.That(HeroModelMapper.IsCustomSet(sets, "Mix Set"), Is.True);
            Assert.That(HeroModelMapper.IsCustomSet(sets, "Non Existent"), Is.False);
        }

        [Test]
        public void IsCustomSet_DictionaryOverload_NullSets_ReturnsFalse()
        {
            Assert.That(HeroModelMapper.IsCustomSet(null, "Set1"), Is.False);
            Assert.That(HeroModelMapper.IsCustomSet(new Dictionary<string, List<string>>(), ""), Is.False);
        }

        #endregion

        #region FormatHeroIdAsName Tests

        [Test]
        public void FormatHeroIdAsName_FormatsCorrectly()
        {
            Assert.That(HeroModelMapper.FormatHeroIdAsName("npc_dota_hero_crystal_maiden"),
                Is.EqualTo("Crystal Maiden"));
        }

        [Test]
        public void FormatHeroIdAsName_SingleWord_Works()
        {
            Assert.That(HeroModelMapper.FormatHeroIdAsName("npc_dota_hero_axe"),
                Is.EqualTo("Axe"));
        }

        [Test]
        public void FormatHeroIdAsName_AlreadyClean_ReturnsAsIs()
        {
            // If no "npc_dota_hero_" prefix, just replaces underscores
            Assert.That(HeroModelMapper.FormatHeroIdAsName("anti_mage"),
                Is.EqualTo("Anti Mage"));
        }

        [Test]
        public void FormatHeroIdAsName_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(HeroModelMapper.FormatHeroIdAsName(null!), Is.EqualTo(string.Empty));
            Assert.That(HeroModelMapper.FormatHeroIdAsName(""), Is.EqualTo(string.Empty));
            Assert.That(HeroModelMapper.FormatHeroIdAsName("   "), Is.EqualTo(string.Empty));
        }

        #endregion
    }
}
