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
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class HeroModelMapperTests
    {
        #region MapFromSummaries Tests

        [Test]
        public void MapFromSummaries_NullList_ReturnsEmpty()
        {
            var result = HeroModelMapper.MapFromSummaries(null!);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_EmptyList_ReturnsEmpty()
        {
            var result = HeroModelMapper.MapFromSummaries(new List<HeroSummary>());

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_MapsAllFields_Correctly()
        {
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

            var result = HeroModelMapper.MapFromSummaries(summaries);

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

            var hero = HeroModelMapper.MapFromSummaries(summaries)[0];

            Assert.That(hero.SetStyles, Has.Count.EqualTo(2));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Group, Is.EqualTo("Manifold Paradox"));
            Assert.That(hero.SetStyles["Manifold Paradox (Corrupted)"].Label, Is.EqualTo("Corrupted"));
            Assert.That(hero.SetStyles.ContainsKey("manifold paradox (default)"), Is.True);
        }

        [Test]
        public void MapFromSummaries_NoSetStyles_UsesEmptyDictionary()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary { Name = "Axe", UsedByHeroes = "npc_dota_hero_axe" }
            };

            var hero = HeroModelMapper.MapFromSummaries(summaries)[0];

            Assert.That(hero.SetStyles, Is.Not.Null);
            Assert.That(hero.SetStyles, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_MissingSets_UsesEmptyDictionary()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Axe",
                    UsedByHeroes = "npc_dota_hero_axe",
                    PrimaryAttr = "str"
                }
            };

            var result = HeroModelMapper.MapFromSummaries(summaries);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Sets, Is.Not.Null);
        }

        [Test]
        public void MapFromSummaries_FallsBackToUniversal_WhenNoAttribute()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Test Hero",
                    UsedByHeroes = "npc_dota_hero_test",
                    PrimaryAttr = ""
                }
            };

            var result = HeroModelMapper.MapFromSummaries(summaries);

            Assert.That(result[0].PrimaryAttribute, Is.EqualTo("universal"));
        }

        [Test]
        public void MapFromSummaries_ParsesItemIds_FromStringArray()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "Crystal Maiden",
                    UsedByHeroes = "npc_dota_hero_crystal_maiden",
                    Ids = new[] { "10", "invalid", "20", "10" }
                }
            };

            var result = HeroModelMapper.MapFromSummaries(summaries);

            Assert.That(result[0].ItemIds, Is.EquivalentTo(new[] { 10, 20 }));
        }

        [Test]
        public void MapFromSummaries_UsesName_WhenUsedByHeroesEmpty()
        {
            var summaries = new List<HeroSummary>
            {
                new HeroSummary
                {
                    Name = "FallbackHero",
                    UsedByHeroes = "",
                    PrimaryAttr = "int"
                }
            };

            var result = HeroModelMapper.MapFromSummaries(summaries);

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
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/mix_arcana_antimage.zip"
            };

            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.True);
        }

        [Test]
        public void IsCustomSet_NormalSet_ReturnsFalse()
        {
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/arcana_antimage.zip"
            };

            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.False);
        }

        [Test]
        public void IsCustomSet_NoArchiveUrl_ReturnsFalse()
        {
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/preview.png"
            };

            Assert.That(HeroModelMapper.IsCustomSet(urls), Is.False);
        }

        [Test]
        public void IsCustomSet_DictionaryOverload_FindsCorrectSet()
        {
            var sets = new Dictionary<string, List<string>>
            {
                { "Normal Set", new List<string> { "https://cdn.example.com/normal.zip" } },
                { "Mix Set", new List<string> { "https://cdn.example.com/mix_special.zip" } }
            };

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

        #region ClassifySet Tests

        [Test]
        public void ClassifySet_PrismaticPrefix_ReturnsPrismatic()
        {
            var urls = new List<string>
            {
                "https://cdn.example.com/thumb.jpg",
                "https://cdn.example.com/prismatic_glow_antimage.zip"
            };

            Assert.That(HeroModelMapper.ClassifySet(urls), Is.EqualTo(HeroModelMapper.SkinCategory.Prismatic));
            Assert.That(HeroModelMapper.IsPrismaticSet(urls), Is.True);
        }

        [Test]
        public void ClassifySet_KnownPrefixes_MapToCategories()
        {
            HeroModelMapper.SkinCategory Classify(string archive) =>
                HeroModelMapper.ClassifySet(new List<string> { $"https://cdn.example.com/{archive}" });

            Assert.That(Classify("prismatic_glow.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.Prismatic));
            Assert.That(Classify("persona_invoker.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.Persona));
            Assert.That(Classify("item_head_helm.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.Item));
            Assert.That(Classify("base_antimage.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.BaseHero));
            Assert.That(Classify("mix_arcana.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.CustomSet));
            Assert.That(Classify("arcana_antimage.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.LegacySet));
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
