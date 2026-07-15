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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroGenerationServiceTests
    {
        #region GetSortWeight Tests

        [Test]
        public void GetSortWeight_WhenBaseHasHeroBaseSlotIsTrue_ReturnsCorrectWeights()
        {
            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, true);
            Assert.That(weightBase, Is.EqualTo(3));

            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, true);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, true);
            Assert.That(weightLegacy, Is.EqualTo(2));
            Assert.That(weightCustom, Is.EqualTo(2));
            Assert.That(weightPersona, Is.EqualTo(2));

            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, true);
            Assert.That(weightItem, Is.EqualTo(1));
        }

        [Test]
        public void GetSortWeight_WhenBaseHasHeroBaseSlotIsFalse_ReturnsCorrectWeights()
        {
            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, false);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, false);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, false);
            Assert.That(weightLegacy, Is.EqualTo(3));
            Assert.That(weightCustom, Is.EqualTo(3));
            Assert.That(weightPersona, Is.EqualTo(3));

            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false);
            Assert.That(weightItem, Is.EqualTo(2));

            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, false);
            Assert.That(weightBase, Is.EqualTo(1));
        }

        #endregion

        #region ResolveBaseWins Tests

        [Test]
        public void ResolveBaseWins_ExplicitMethod_OverridesDetection()
        {
            Assert.That(HeroGenerationService.ResolveBaseWins(1, false), Is.True);
            Assert.That(HeroGenerationService.ResolveBaseWins(1, true), Is.True);

            Assert.That(HeroGenerationService.ResolveBaseWins(2, true), Is.False);
            Assert.That(HeroGenerationService.ResolveBaseWins(2, false), Is.False);
        }

        [Test]
        public void ResolveBaseWins_NoOrInvalidMethod_FallsBackToDetection()
        {
            Assert.That(HeroGenerationService.ResolveBaseWins(null, true), Is.True);
            Assert.That(HeroGenerationService.ResolveBaseWins(null, false), Is.False);

            Assert.That(HeroGenerationService.ResolveBaseWins(0, true), Is.True);
            Assert.That(HeroGenerationService.ResolveBaseWins(3, false), Is.False);
        }

        #endregion

        #region Priority order Tests

        [Test]
        public void GetSortWeight_ItemsAlwaysBelowSets_BothMethods()
        {
            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, true),
                Is.GreaterThan(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true)));
            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true),
                Is.GreaterThan(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, true)));

            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, false),
                Is.GreaterThan(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false)));
            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false),
                Is.GreaterThan(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, false)));

            foreach (var setCat in new[] { HeroModelMapper.SkinCategory.LegacySet, HeroModelMapper.SkinCategory.CustomSet, HeroModelMapper.SkinCategory.Persona })
            {
                Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, true),
                    Is.LessThan(HeroGenerationService.GetSortWeight(setCat, true)));
                Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false),
                    Is.LessThan(HeroGenerationService.GetSortWeight(setCat, false)));
            }
        }

        [Test]
        public void GetSortWeight_PrismaticIsAlwaysLowest_AppliedLastOnTop()
        {
            foreach (var baseHasHeroBaseSlot in new[] { true, false })
            {
                var prismatic = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Prismatic, baseHasHeroBaseSlot);
                foreach (var other in new[]
                {
                    HeroModelMapper.SkinCategory.BaseHero,
                    HeroModelMapper.SkinCategory.LegacySet,
                    HeroModelMapper.SkinCategory.CustomSet,
                    HeroModelMapper.SkinCategory.Persona,
                    HeroModelMapper.SkinCategory.Item,
                })
                {
                    Assert.That(prismatic,
                        Is.LessThan(HeroGenerationService.GetSortWeight(other, baseHasHeroBaseSlot)),
                        $"Prismatic must sort below {other} (baseHasHeroBaseSlot={baseHasHeroBaseSlot}) so it is applied last.");
                }
            }
        }

        #endregion

        #region hero_base slot detection (KeyValuesBlockHelper.AnyBlockHasItemSlot)


        [Test]
        public void HeroBaseSlot_EmptyText_ReturnsFalse()
        {
            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot("", "hero_base"), Is.False);
        }

        [Test]
        public void HeroBaseSlot_ContainsHeroBaseSlot_ReturnsTrue()
        {
            string indexContent = @"""855""
{
	""name""		""Earthshaker's Base""
	""item_slot""		""hero_base""
}";
            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(indexContent, "hero_base"), Is.True);
        }

        [Test]
        public void HeroBaseSlot_HeroBaseOnlyInNestedBlock_ReturnsFalse()
        {
            string indexContent = @"""460""
{
	""item_slot""		""arms""
	""visuals""
	{
		""asset""		""hero_base""
	}
}";
            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(indexContent, "hero_base"), Is.False);
        }

        [Test]
        public void HeroBaseSlot_DoesNotContainHeroBaseSlot_ReturnsFalse()
        {
            string indexContent = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""item_slot""		""arms""
}";
            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(indexContent, "hero_base"), Is.False);
        }

        [Test]
        public void HeroBaseSlot_CaseInsensitiveAndWhitespaceFlexible_ReturnsTrue()
        {
            string indexContent = @"""855""
{
	""ITEM_SLOT""	   	""HERO_BASE""
}";
            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(indexContent, "hero_base"), Is.True);
        }

        #endregion

        #region Generation report / skip tracking (drives GenerateBatchAsync)

        [Test]
        public async Task GenerateBatchAsync_SetWithoutZip_RecordsSkipWarning_AndSavesReport()
        {
            var vpkStub = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            bool createdStub = false;
            var targetPath = Path.Combine(Path.GetTempPath(), "ArdysaGenTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(targetPath);

            try
            {
                if (!File.Exists(vpkStub)) { File.WriteAllText(vpkStub, "stub"); createdStub = true; }

                var hero = new HeroModel
                {
                    HeroId = "npc_dota_hero_tester",
                    Name = "Tester",
                    ItemIds = { 123 },
                    Sets =
                    {
                        ["Cool Set"] = new System.Collections.Generic.List<string> { "https://cdn.example/thumb.png" }
                    }
                };

                var service = new HeroGenerationService();
                var result = await service.GenerateBatchAsync(
                    targetPath,
                    new[] { (hero, "Cool Set") },
                    _ => { },
                    ct: CancellationToken.None);

                Assert.That(result.Success, Is.True);
                Assert.That(result.Warnings, Is.Not.Null.And.Count.EqualTo(1));
                Assert.That(result.Warnings![0], Does.Contain("Tester").And.Contains(".zip"));

                var reports = Directory.GetFiles(targetPath, "generation_report_*.txt", SearchOption.AllDirectories);
                Assert.That(reports, Is.Not.Empty, "expected a saved generation_report_*.txt");
            }
            finally
            {
                if (createdStub) { try { File.Delete(vpkStub); } catch { } }
                try { Directory.Delete(targetPath, true); } catch { }
            }
        }

        #endregion
    }
}
