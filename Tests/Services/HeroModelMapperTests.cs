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
        public void MapFromSummaries_CarriesSetNamesAndIncompatibleThrough()
        {
            var summary = new HeroSummary
            {
                Name = "Abaddon",
                UsedByHeroes = "npc_dota_hero_abaddon",
                Sets = new Dictionary<string, string[]>
                {
                    ["Set 1"] = new[] { "https://cdn/blightfall.zip" },
                    ["Set 2"] = new[] { "https://cdn/item_weapon_mace.zip" }
                },
                SetNames = new Dictionary<string, string> { ["item_weapon_mace"] = "Mace of the Chosen" },
                Incompatible = new List<SetIncompatibilityRule>
                {
                    new() { Sets = new[] { "blightfall" }, With = new[] { "item_weapon_mace" } }
                }
            };

            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary> { summary })[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.SetNames["item_weapon_mace"], Is.EqualTo("Mace of the Chosen"));
                Assert.That(hero.Incompatible, Has.Count.EqualTo(1));
            });

            var map = SetCompatibilityResolver.BuildConflictMap(hero.Sets.ToList(), hero.Incompatible);
            Assert.Multiple(() =>
            {
                Assert.That(map[0], Is.EqualTo(new[] { 1 }));
                Assert.That(map[1], Is.EqualTo(new[] { 0 }));
            });
        }

        [Test]
        public void MapFromSummaries_CarriesNoticesThroughCaseInsensitively()
        {
            var summary = new HeroSummary
            {
                Name = "Abaddon",
                UsedByHeroes = "npc_dota_hero_abaddon",
                Sets = new Dictionary<string, string[]> { ["Set 1"] = new[] { "https://cdn/blightfall.zip" } },
                Notices = new Dictionary<string, List<SetNotice>>
                {
                    ["blightfall"] = new() { new SetNotice(SetNotice.AnimDemoOnly) }
                }
            };

            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary> { summary })[0];

            Assert.That(hero.Notices["BLIGHTFALL"], Is.EqualTo(new[] { new SetNotice(SetNotice.AnimDemoOnly) }));
        }

        [Test]
        public void MapFromSummaries_CarriesBaseLocksThroughAsACopy()
        {
            var locks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { ["base_arcana"] = new() { "head" } };
            var summary = new HeroSummary
            {
                Name = "Zeus",
                UsedByHeroes = "npc_dota_hero_zuus",
                Sets = new Dictionary<string, string[]> { ["Set 1"] = new[] { "https://cdn/base_arcana.zip" } },
                BaseLocks = locks
            };

            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary> { summary })[0];
            hero.BaseLocks["BASE_ARCANA"].Add("arms");

            Assert.That(locks["base_arcana"], Is.EqualTo(new[] { "head" }), "the model must not alias the summary");
        }

        [Test]
        public void ExtractArchiveStem_HandlesPlainSplitAndArchivelessSets()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HeroModelMapper.ExtractArchiveStem(
                    new List<string> { "https://cdn/Assets/models/Abaddon/item_weapon_mace.zip", "https://cdn/x.png" }),
                    Is.EqualTo("item_weapon_mace"));
                Assert.That(HeroModelMapper.ExtractArchiveStem(
                    new List<string> { "https://cdn/Assets/models/Pudge/huge_set.zip.001" }),
                    Is.EqualTo("huge_set"), "a split archive must not resolve to '<stem>.zip'");
                Assert.That(HeroModelMapper.ExtractArchiveStem(new List<string> { "default_skin.vpk" }), Is.Null);
                Assert.That(HeroModelMapper.ExtractArchiveStem(null), Is.Null);
            });
        }

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
            Assert.That(Classify("effect_ability2_vigil.zip"), Is.EqualTo(HeroModelMapper.SkinCategory.AbilityEffect));
        }

        [Test]
        public void SetDisplayName_UsesSetNamesLabel_ElseTheKey()
        {
            var hero = new HeroModel
            {
                Sets = new Dictionary<string, List<string>>
                {
                    ["Set 11"] = new() { "https://cdn/Assets/models/Sven/item_weapon_shattered.zip" },
                    ["Set 4"] = new() { "https://cdn/Assets/models/Sven/legacy.zip" },
                },
                SetNames = new Dictionary<string, string> { ["item_weapon_shattered"] = "Shattered Greatsword" },
            };

            Assert.Multiple(() =>
            {
                Assert.That(HeroModelMapper.SetDisplayName(hero, "Set 11"), Is.EqualTo("Shattered Greatsword"));
                Assert.That(HeroModelMapper.SetDisplayName(hero, "Set 4"), Is.EqualTo("Set 4"), "no label authored");
                Assert.That(HeroModelMapper.SetDisplayName(hero, "Set 99"), Is.EqualTo("Set 99"), "unknown key");
                Assert.That(HeroModelMapper.SetDisplayName(null, "Set 1"), Is.EqualTo("Set 1"));
            });
        }

        [TestCase("effect_ability2_vigil.zip", "ability2")]
        [TestCase("EFFECT_Ability4_vigil_odyssey.zip", "ability4")]
        [TestCase("effect_weapon_vigil.zip", null)]
        [TestCase("effect_ability10_vigil.zip", null)]
        [TestCase("effect_.zip", null)]
        [TestCase("item_weapon_vigil.zip", "weapon")]
        [TestCase("base_sven.zip", null)]
        public void ExtractItemTag_EffectArchive_OnlyYieldsAbilityTags(string archive, string? expected)
        {
            var urls = new List<string> { $"https://cdn.example.com/{archive}" };

            Assert.That(HeroModelMapper.ExtractItemTag(urls), Is.EqualTo(expected));
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
