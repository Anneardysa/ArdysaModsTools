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
using ArdysaModsTools.Models;

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

        #region "set_names" / "incompatible" parsing

        [Test]
        public void ParseHeroesJson_SetNamesAndIncompatible_AreRead()
        {
            const string json = @"
            [
              {
                ""name"": ""abaddon"",
                ""sets"": {
                  ""Set 1"": [""https://cdn/blightfall.zip""],
                  ""Set 2"": [""https://cdn/item_weapon_mace.zip""]
                },
                ""set_names"": {
                  ""blightfall"": ""Blightfall"",
                  ""item_weapon_mace"": ""Mace of the Chosen""
                },
                ""incompatible"": [
                  { ""sets"": [""blightfall""], ""with"": [""item_weapon_mace""] },
                  { ""sets"": [""a"", ""b""] }
                ]
              }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.SetNames["blightfall"], Is.EqualTo("Blightfall"));
                Assert.That(hero.SetNames["item_weapon_mace"], Is.EqualTo("Mace of the Chosen"));
                Assert.That(hero.Incompatible, Has.Count.EqualTo(2));
                Assert.That(hero.Incompatible[0].Sets, Is.EqualTo(new[] { "blightfall" }));
                Assert.That(hero.Incompatible[0].With, Is.EqualTo(new[] { "item_weapon_mace" }));
                Assert.That(hero.Incompatible[1].Sets, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(hero.Incompatible[1].With, Is.Empty);
            });
        }

        [Test]
        public void ParseHeroesJson_WithoutTheNewFields_StillParses()
        {
            const string json = @"
            [
              { ""name"": ""abaddon"", ""sets"": { ""Set 1"": [""https://cdn/a.zip""] } }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.SetNames, Is.Empty);
                Assert.That(hero.Incompatible, Is.Empty);
            });
        }

        [Test]
        public void ParseHeroesJson_MalformedNewFields_AreSkippedWithoutLosingTheHero()
        {
            const string json = @"
            [
              {
                ""name"": ""abaddon"",
                ""sets"": { ""Set 1"": [""https://cdn/a.zip""] },
                ""set_names"": { ""good"": ""Good Name"", ""bad"": 12345, ""blank"": ""   "" },
                ""incompatible"": [
                  ""not-an-object"",
                  { ""with"": [""only-a-right-hand-side""] },
                  { ""sets"": [""x"", """", null], ""with"": ""not-an-array"" }
                ]
              }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("abaddon"), "a malformed optional field must not drop the hero");
                Assert.That(hero.SetNames.Keys, Is.EquivalentTo(new[] { "good" }));
                Assert.That(hero.Incompatible, Has.Count.EqualTo(1));
                Assert.That(hero.Incompatible[0].Sets, Is.EqualTo(new[] { "x" }));
                Assert.That(hero.Incompatible[0].With, Is.Empty);
            });
        }

        #endregion

        #region "notices" parsing

        private static HeroSummary ParseWithNotices(string noticesJson) =>
            HeroService.ParseHeroesJson(@"[ { ""name"": ""abaddon"", ""sets"": { ""Set 1"": [""https://cdn/a.zip""] }, ""notices"": " + noticesJson + " } ]")[0];

        [Test]
        public void ParseHeroesJson_Notices_ShorthandAndObjectFormsAreRead()
        {
            var hero = ParseWithNotices(@"{
                ""blightfall"": [""anim_demo_only"", { ""type"": ""anim_bug"", ""anim"": ""Idle"" }],
                ""item_weapon_mace"": [{ ""type"": ""custom"", ""text"": ""  Particles differ slightly  "" }]
            }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Notices["blightfall"], Is.EqualTo(new[]
                {
                    new SetNotice(SetNotice.AnimDemoOnly),
                    new SetNotice(SetNotice.AnimBug, Anim: "Idle")
                }));
                Assert.That(hero.Notices["item_weapon_mace"], Is.EqualTo(new[]
                {
                    new SetNotice(SetNotice.Custom, Text: "Particles differ slightly")
                }));
            });
        }

        [Test]
        public void ParseHeroesJson_Notices_TypeIsCaseInsensitiveAndStemLookupIgnoresCase()
        {
            var hero = ParseWithNotices(@"{ ""Blightfall"": ["" ANIM_Demo_Only ""] }");

            Assert.That(hero.Notices["blightfall"], Is.EqualTo(new[] { new SetNotice(SetNotice.AnimDemoOnly) }));
        }

        [Test]
        public void ParseHeroesJson_Notices_UnknownTypeFallsBackToItsTextOrIsDropped()
        {
            var hero = ParseWithNotices(@"{
                ""a"": [{ ""type"": ""sound_bug"", ""text"": ""Voice lines missing"" }, ""sound_bug""]
            }");

            Assert.That(hero.Notices["a"], Is.EqualTo(new[] { new SetNotice(SetNotice.Custom, Text: "Voice lines missing") }));
        }

        [Test]
        public void ParseHeroesJson_Notices_AnimBugIgnoresTextAndCustomNeedsText()
        {
            var hero = ParseWithNotices(@"{
                ""a"": [{ ""type"": ""anim_bug"" }, { ""type"": ""custom"" }, { ""type"": ""custom"", ""text"": ""  "" }]
            }");

            Assert.That(hero.Notices["a"], Is.EqualTo(new[] { new SetNotice(SetNotice.AnimBug) }));
        }

        [Test]
        public void ParseHeroesJson_Notices_MalformedPiecesAreSkippedWithoutLosingTheHero()
        {
            var hero = ParseWithNotices(@"{
                ""good"": [42, null, [""anim_bug""], {}, { ""type"": 7 }, """", ""anim_demo_only""],
                ""not-an-array"": ""anim_bug"",
                ""   "": [""anim_bug""],
                ""all-bad"": [42]
            }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("abaddon"), "a malformed notice must not drop the hero");
                Assert.That(hero.Sets, Has.Count.EqualTo(1));
                Assert.That(hero.Notices.Keys, Is.EquivalentTo(new[] { "good" }));
                Assert.That(hero.Notices["good"], Is.EqualTo(new[] { new SetNotice(SetNotice.AnimDemoOnly) }));
            });
        }

        [Test]
        public void ParseHeroesJson_Notices_NotAnObject_IsIgnored()
        {
            var hero = ParseWithNotices(@"[""anim_bug""]");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("abaddon"));
                Assert.That(hero.Notices, Is.Empty);
            });
        }

        [Test]
        public void ParseHeroesJson_Notices_DuplicatesDroppedCountAndLengthCapped()
        {
            var longText = new string('x', HeroService.MaxNoticeTextLength + 50);
            var hero = ParseWithNotices(@"{
                ""a"": [""anim_demo_only"", ""anim_demo_only"",
                        { ""type"": ""anim_bug"", ""anim"": ""Idle"" }, { ""type"": ""anim_bug"", ""anim"": ""Idle"" },
                        { ""type"": ""custom"", ""text"": """ + longText + @""" },
                        { ""type"": ""anim_bug"", ""anim"": ""Run"" },
                        { ""type"": ""anim_bug"", ""anim"": ""Attack"" }]
            }");

            var list = hero.Notices["a"];
            Assert.Multiple(() =>
            {
                Assert.That(list, Has.Count.EqualTo(HeroService.MaxNoticesPerSet));
                Assert.That(list, Is.Unique);
                Assert.That(list[2].Text, Has.Length.EqualTo(HeroService.MaxNoticeTextLength));
                Assert.That(list.Select(n => n.Anim), Does.Not.Contain("Attack"), "past the cap");
            });
        }

        [Test]
        public void ParseHeroesJson_WithoutNotices_HasEmptyNotices()
        {
            var hero = HeroService.ParseHeroesJson(@"[ { ""name"": ""abaddon"", ""sets"": { ""Set 1"": [""https://cdn/a.zip""] } } ]")[0];

            Assert.That(hero.Notices, Is.Empty);
        }

        #endregion

        #region "base_locks" parsing

        private static HeroSummary ParseWithLocks(string locksJson) =>
            HeroService.ParseHeroesJson(@"[ { ""name"": ""zeus"", ""sets"": { ""Set 1"": [""https://cdn/base_arcana.zip""] }, ""base_locks"": " + locksJson + " } ]")[0];

        [Test]
        public void ParseHeroesJson_BaseLocks_ArrayAndShorthandAreRead_LowercasedAndDeduped()
        {
            var hero = ParseWithLocks(@"{ ""base_arcana"": ["" Head "", ""head"", ""ARMS""], ""base_persona"": ""head"" }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.BaseLocks["BASE_ARCANA"], Is.EqualTo(new[] { "head", "arms" }));
                Assert.That(hero.BaseLocks["base_persona"], Is.EqualTo(new[] { "head" }));
            });
        }

        [Test]
        public void ParseHeroesJson_BaseLocks_NonBaseStemsAndMalformedPiecesAreSkipped()
        {
            var hero = ParseWithLocks(@"{
                ""storm_set"": [""head""],
                ""item_head_crown"": [""head""],
                ""base_bad"": 7,
                ""base_empty"": ["""", 3, null],
                ""base_arcana"": [""head"", { ""x"": 1 }]
            }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("zeus"), "a malformed lock must never drop the hero");
                Assert.That(hero.BaseLocks.Keys, Is.EquivalentTo(new[] { "base_arcana" }));
                Assert.That(hero.BaseLocks["base_arcana"], Is.EqualTo(new[] { "head" }));
            });
        }

        [Test]
        public void ParseHeroesJson_BaseLocks_CappedPerBase()
        {
            var slots = string.Join(",", Enumerable.Range(0, 20).Select(i => $"\"s{i}\""));
            var hero = ParseWithLocks($"{{ \"base_arcana\": [{slots}] }}");

            Assert.That(hero.BaseLocks["base_arcana"], Has.Count.EqualTo(HeroService.MaxLockedSlotsPerBase));
        }

        [Test]
        public void ParseHeroesJson_BaseLocks_NotAnObjectOrAbsent_IsEmpty()
        {
            Assert.That(ParseWithLocks(@"[""head""]").BaseLocks, Is.Empty);
            Assert.That(HeroService.ParseHeroesJson(@"[ { ""name"": ""zeus"" } ]")[0].BaseLocks, Is.Empty);
        }

        #endregion

        #region "ability_names" parsing

        private static HeroSummary ParseWithAbilityNames(string namesJson) =>
            HeroService.ParseHeroesJson(@"[ { ""name"": ""sven"", ""sets"": { ""Set 1"": [""https://cdn/effect_ability2_vigil.zip""] }, ""ability_names"": " + namesJson + " } ]")[0];

        [Test]
        public void ParseHeroesJson_AbilityNames_ValidTagsAreRead_Trimmed()
        {
            var hero = ParseWithAbilityNames(@"{ ""ability2"": "" Great Cleave "", ""ABILITY3"": ""Warcry"" }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.AbilityNames["ability2"], Is.EqualTo("Great Cleave"));
                Assert.That(hero.AbilityNames["ability3"], Is.EqualTo("Warcry"));
            });
        }

        [Test]
        public void ParseHeroesJson_AbilityNames_BadTagsAndValuesAreSkipped_HeroKept()
        {
            var longName = new string('x', HeroService.MaxAbilityNameLength + 10);
            var hero = ParseWithAbilityNames(@"{
                ""weapon"": ""Sword"", ""ability0"": ""Zero"", ""ability1"": 5,
                ""ability2"": """", ""ability3"": null, ""ability4"": """ + longName + @"""
            }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("sven"), "a malformed label must never drop the hero");
                Assert.That(hero.AbilityNames.Keys, Is.EquivalentTo(new[] { "ability4" }));
                Assert.That(hero.AbilityNames["ability4"], Has.Length.EqualTo(HeroService.MaxAbilityNameLength));
            });
        }

        [Test]
        public void ParseHeroesJson_AbilityNames_NotAnObjectOrAbsent_IsEmpty()
        {
            Assert.That(ParseWithAbilityNames(@"[""Great Cleave""]").AbilityNames, Is.Empty);
            Assert.That(HeroService.ParseHeroesJson(@"[ { ""name"": ""sven"" } ]")[0].AbilityNames, Is.Empty);
        }

        [Test]
        public void MapFromSummaries_AbilityNames_ReachHeroModel()
        {
            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary>
            {
                ParseWithAbilityNames(@"{ ""ability2"": ""Great Cleave"" }")
            })[0];

            Assert.That(hero.AbilityNames["ABILITY2"], Is.EqualTo("Great Cleave"));
        }

        #endregion

        #region "ability_icons" / "effect_icons" parsing

        private const string Img = "https://cdn.ardysamods.my.id/Assets/image/Invoker/abilities/";

        private static HeroSummary ParseWithIcons(string abilityIcons, string effectIcons) =>
            HeroService.ParseHeroesJson(@"[ { ""name"": ""invoker"", ""sets"": { ""Set 1"": [""https://cdn/effect_ability1_head_apex.zip""] }, ""ability_icons"": "
                + abilityIcons + @", ""effect_icons"": " + effectIcons + " } ]")[0];

        [Test]
        public void ParseHeroesJson_Icons_ValidEntriesAreRead_KeysLowercased()
        {
            var hero = ParseWithIcons(
                @"{ ""ABILITY1"": """ + Img + @"invoker_quas.png"" }",
                @"{ "" effect_ability1_head_apex "": """ + Img + @"invoker_magus_apex_invoker_quas.webp"" }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.AbilityIcons["ability1"], Is.EqualTo(Img + "invoker_quas.png"));
                Assert.That(hero.EffectIcons["effect_ability1_head_apex"], Is.EqualTo(Img + "invoker_magus_apex_invoker_quas.webp"));
            });
        }

        [Test]
        public void ParseHeroesJson_Icons_BadKeysAndUrlsAreSkipped_HeroKept()
        {
            var hero = ParseWithIcons(
                @"{ ""ability0"": """ + Img + @"a.png"", ""weapon"": """ + Img + @"b.png"",
                    ""ability2"": ""Assets/image/x.png"", ""ability3"": ""javascript:alert(1)//x.png"",
                    ""ability4"": ""file:///C:/x.png"", ""ability5"": """ + Img + @"x.zip"",
                    ""ability6"": 7, ""ability7"": """ + Img + @"invoker_tornado.png"" }",
                @"{ ""item_head_apex"": """ + Img + @"a.png"", ""effect_"": """ + Img + @"b.png"",
                    ""effect_ability2_head_apex"": null }");

            Assert.Multiple(() =>
            {
                Assert.That(hero.Name, Is.EqualTo("invoker"), "a malformed icon must never drop the hero");
                Assert.That(hero.AbilityIcons.Keys, Is.EquivalentTo(new[] { "ability7" }));
                Assert.That(hero.EffectIcons, Is.Empty);
            });
        }

        [Test]
        public void ParseHeroesJson_Icons_NotAnObjectOrAbsent_IsEmpty()
        {
            var hero = ParseWithIcons(@"[""x.png""]", @"""x.png""");
            var bare = HeroService.ParseHeroesJson(@"[ { ""name"": ""invoker"" } ]")[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.AbilityIcons, Is.Empty);
                Assert.That(hero.EffectIcons, Is.Empty);
                Assert.That(bare.AbilityIcons, Is.Empty);
                Assert.That(bare.EffectIcons, Is.Empty);
            });
        }

        [Test]
        public void MapFromSummaries_Icons_ReachHeroModel_CaseInsensitive()
        {
            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary>
            {
                ParseWithIcons(@"{ ""ability1"": """ + Img + @"q.png"" }", @"{ ""effect_ability1_head_apex"": """ + Img + @"a.png"" }")
            })[0];

            Assert.Multiple(() =>
            {
                Assert.That(hero.AbilityIcons["ABILITY1"], Is.EqualTo(Img + "q.png"));
                Assert.That(hero.EffectIcons["Effect_Ability1_Head_Apex"], Is.EqualTo(Img + "a.png"));
            });
        }

        [Test]
        public void CollectThumbnailUrls_IncludesAbilityIcons_OncePerUrl()
        {
            var hero = HeroModelMapper.MapFromSummaries(new List<HeroSummary>
            {
                ParseWithIcons(@"{ ""ability1"": """ + Img + @"q.png"", ""ability2"": """ + Img + @"w.png"" }",
                    @"{ ""effect_ability1_head_apex"": """ + Img + @"apex_q.png"", ""effect_ability1_back_artistry_cape"": """ + Img + @"q.png"" }")
            });

            var urls = ArdysaModsTools.UI.Forms.HeroGalleryForm.CollectThumbnailUrls(hero);

            Assert.That(urls.Where(u => u.StartsWith(Img)), Is.EquivalentTo(new[] { Img + "q.png", Img + "w.png", Img + "apex_q.png" }));
        }

        #endregion

        #region base-priority "method" parsing

        [Test]
        public void ParseHeroesJson_LegacyScalarMethod_BecomesTheHeroDefault()
        {
            const string json = @"
            [
              { ""name"": ""abaddon"", ""method"": 2, ""sets"": { ""A"": [""https://cdn/a.zip""] } }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.That(hero.Method, Is.EqualTo(2));
            Assert.That(hero.BasePriority.Default, Is.EqualTo(2));
            Assert.That(hero.BasePriority.HasOverrides, Is.False);
        }

        [Test]
        public void ParseHeroesJson_NoMethod_LeavesPolicyEmpty()
        {
            const string json = @"[ { ""name"": ""axe"" } ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.That(hero.Method, Is.Null);
            Assert.That(hero.BasePriority.HasOverrides, Is.False);
        }

        [Test]
        public void ParseHeroesJson_ScopedMethodObject_ParsesDefaultSetsAndItems()
        {
            const string json = @"
            [
              {
                ""name"": ""juggernaut"",
                ""method"": {
                  ""default"": 1,
                  ""sets"": { ""Bladeform Legacy"": 2, ""Shadowforce Gale"": ""1"" },
                  ""items"": { ""12345"": 2, ""6789"": ""1"" }
                },
                ""sets"": { ""Bladeform Legacy"": [""https://cdn/b.zip""] }
              }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.That(hero.BasePriority.Default, Is.EqualTo(1));
            Assert.That(hero.BasePriority.Sets["Bladeform Legacy"], Is.EqualTo(2));
            Assert.That(hero.BasePriority.Sets["Shadowforce Gale"], Is.EqualTo(1), "quoted numbers are accepted");
            Assert.That(hero.BasePriority.Items[12345], Is.EqualTo(2));
            Assert.That(hero.BasePriority.Items[6789], Is.EqualTo(1));

            Assert.That(hero.Sets.Keys, Is.EquivalentTo(new[] { "Bladeform Legacy" }));
        }

        [Test]
        public void ParseHeroesJson_MalformedMethodEntries_SkippedButHeroKept()
        {
            const string json = @"
            [
              {
                ""name"": ""pudge"",
                ""method"": {
                  ""default"": ""oops"",
                  ""sets"": { ""Good Set"": 2, ""Bad Set"": [1] },
                  ""items"": { ""111"": 1, ""not-an-id"": 2, ""222"": {} }
                }
              }
            ]";

            var hero = HeroService.ParseHeroesJson(json)[0];

            Assert.That(hero.Name, Is.EqualTo("pudge"), "one bad override must never drop the hero");
            Assert.That(hero.BasePriority.Default, Is.Null);
            Assert.That(hero.BasePriority.Sets.Keys, Is.EquivalentTo(new[] { "Good Set" }));
            Assert.That(hero.BasePriority.Items.Keys, Is.EquivalentTo(new[] { 111 }));
        }

        #endregion
    }
}

