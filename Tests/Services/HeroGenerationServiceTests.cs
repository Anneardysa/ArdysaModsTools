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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroGenerationServiceTests
    {
        #region GetSortWeight Tests

        [Test]
        public void GetSortWeight_WhenBaseWins_BaseIsFoundationAboveSetsAndItems()
        {
            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, true);
            Assert.That(weightBase, Is.EqualTo(HeroGenerationService.BaseAnchorWeight));

            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, true);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, true);
            Assert.That(weightLegacy, Is.LessThan(weightBase));
            Assert.That(weightCustom, Is.EqualTo(weightLegacy));
            Assert.That(weightPersona, Is.EqualTo(weightLegacy));

            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, true);
            Assert.That(weightItem, Is.LessThan(weightLegacy));
            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Prismatic, true),
                Is.LessThan(weightItem));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetSortWeight_AbilityEffect_IsAppliedAfterEveryOtherLayer(bool baseWins)
        {
            int effect = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.AbilityEffect, baseWins);

            foreach (var other in Enum.GetValues<HeroModelMapper.SkinCategory>()
                         .Where(c => c != HeroModelMapper.SkinCategory.AbilityEffect))
                Assert.That(effect, Is.LessThan(HeroGenerationService.GetSortWeight(other, baseWins)), other.ToString());
        }

        [Test]
        public void LayersAppliedOverTheLock_ArePrismaticAndAbilityEffects()
        {
            var yieldTo = HeroGenerationService.LayersAppliedOverTheLock(new[]
            {
                (HeroModelMapper.SkinCategory.LegacySet, "Set 1"),
                (HeroModelMapper.SkinCategory.BaseHero, "Set 2"),
                (HeroModelMapper.SkinCategory.Item, "Set 3"),
                (HeroModelMapper.SkinCategory.Prismatic, "Set 4"),
                (HeroModelMapper.SkinCategory.AbilityEffect, "Set 5"),
            });

            Assert.That(yieldTo, Is.EquivalentTo(new[] { "Set 4", "Set 5" }));
        }

        private static void Check(string setName, string root) =>
            HeroGenerationService.EnsureEffectLayersAreSafe(new[] { (setName, root) });

        private static string Touch(string root, params string[] parts)
        {
            var file = Path.Combine(new[] { root }.Concat(parts).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "x");
            return file;
        }

        [Test]
        public void EnsureEffectLayersAreSafe_HexModelEffect_Passes_SharedModelOrWearable_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "amt-effect-" + Guid.NewGuid().ToString("N"));
            var fish = Path.Combine(root, "fish");
            var spike = Path.Combine(root, "spike");
            var poacher = Path.Combine(root, "poacher");
            try
            {
                Touch(fish, "particles", "units", "heroes", "hero_lion", "lion_spell_voodoo.vpcf_c");
                Touch(fish, "models", "props_gameplay", "frog.vmdl_c");
                Touch(fish, "panorama", "images", "spellicons", "lion_voodoo_png.vtex_c");
                Touch(spike, "particles", "units", "heroes", "hero_lion", "lion_spell_impale.vpcf_c");
                Assert.DoesNotThrow(() => HeroGenerationService.EnsureEffectLayersAreSafe(
                    new[] { ("Set 20", fish), ("Set 21", spike) }), "disjoint effects of two abilities");

                Touch(poacher, "models", "props_gameplay", "frog.vmdl_c");
                var ex = Assert.Throws<InvalidOperationException>(() => HeroGenerationService.EnsureEffectLayersAreSafe(
                    new[] { ("Set 20", fish), ("Set 22", poacher) }));
                Assert.That(ex!.Message, Does.Contain("Set 20").And.Contain("Set 22").And.Contain("frog.vmdl_c"));

                Touch(spike, "models", "heroes", "lion", "lion_weapon.vmdl_c");
                ex = Assert.Throws<InvalidOperationException>(() => Check("Set 21", spike));
                Assert.That(ex!.Message, Does.Contain("lion_weapon.vmdl_c"), "a wearable is never an effect file");

                Assert.DoesNotThrow(() => HeroGenerationService.EnsureEffectLayersAreSafe(
                    Array.Empty<(string, string)>()));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void EnsureEffectLayersAreSafe_ParticlesOnly_Passes_ModelOrIndex_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "amt-effect-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cleave = Path.Combine(root, "particles", "units", "heroes", "hero_sven", "sven_spell_great_cleave.vpcf_c");
                Directory.CreateDirectory(Path.GetDirectoryName(cleave)!);
                File.WriteAllText(cleave, "x");
                Assert.DoesNotThrow(() => Check("Set 5", root));

                var icon = Path.Combine(root, "panorama", "images", "spellicons", "sven_great_cleave_png.vtex_c");
                Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
                File.WriteAllText(icon, "x");
                Assert.DoesNotThrow(() => Check("Set 5", root),
                    "the ability's HUD icon at its vanilla path rides with the particles");

                var itemIcon = Path.Combine(root, "panorama", "images", "items", "blink_png.vtex_c");
                Directory.CreateDirectory(Path.GetDirectoryName(itemIcon)!);
                File.WriteAllText(itemIcon, "x");
                Assert.Throws<InvalidOperationException>(() => Check("Set 5", root));
                File.Delete(itemIcon);

                File.WriteAllText(Path.Combine(root, "index.txt"), "\"17\" { }");
                var ex = Assert.Throws<InvalidOperationException>(() => Check("Set 5", root));
                Assert.That(ex!.Message, Does.Contain("Set 5").And.Contain("index.txt"));

                Assert.Throws<InvalidOperationException>(() =>
                    Check("Set 5", Path.Combine(root, "missing")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void GetSortWeight_WhenBaseLast_SetsAndItemsBecomeTheFoundation()
        {
            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, false);
            Assert.That(weightBase, Is.EqualTo(HeroGenerationService.BaseAnchorWeight),
                "The Base anchor must not move — per-set overrides position the other layers around it.");

            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, false);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, false);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, false);
            Assert.That(weightLegacy, Is.GreaterThan(weightBase));
            Assert.That(weightCustom, Is.EqualTo(weightLegacy));
            Assert.That(weightPersona, Is.EqualTo(weightLegacy));

            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false);
            Assert.That(weightItem, Is.LessThan(weightLegacy));
            Assert.That(weightItem, Is.GreaterThan(weightBase));
        }

        [Test]
        public void GetSortWeight_MixedPerSetPriorities_StayStrictlyOrderedAroundTheBase()
        {
            int baseWeight = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, true);
            int setOnTop = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true);
            int setUnder = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, false);

            Assert.That(setUnder, Is.GreaterThan(baseWeight), "base-last set must be the foundation");
            Assert.That(setOnTop, Is.LessThan(baseWeight), "base-wins set must layer on top of the Base");
            Assert.That(HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, false),
                Is.EqualTo(baseWeight), "the Base anchor is the same in both resolutions");
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

        #region GenerateModsPackAsync — release guards that fire before anything is built

        private static HeroModel ReleaseHero(string url = "https://cdn.example/Assets/models/tester/cool.zip") => new()
        {
            HeroId = "npc_dota_hero_tester",
            Name = "Tester",
            ItemIds = { 123 },
            Sets = { ["Cool Set"] = new System.Collections.Generic.List<string> { url } }
        };

        private static async Task WithReleaseRunAsync(
            Func<string, string, Mock<IOriginalVpkProvider>, Mock<IHeroSetDownloader>, Mock<IVpkReplacer>, Task> body)
        {
            var vpkStub = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            bool createdStub = false;
            var root = Path.Combine(Path.GetTempPath(), "ArdysaReleaseTest_" + Guid.NewGuid().ToString("N"));
            var targetPath = Path.Combine(root, "dota");
            var baseDir = Path.Combine(root, "pristine");
            Directory.CreateDirectory(targetPath);
            Directory.CreateDirectory(baseDir);
            File.WriteAllText(Path.Combine(baseDir, "readme.txt"), "base");

            var original = new Mock<IOriginalVpkProvider>();
            original.Setup(o => o.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()))
                .ReturnsAsync(baseDir);
            var downloader = new Mock<IHeroSetDownloader>();
            var replacer = new Mock<IVpkReplacer>();

            try
            {
                if (!File.Exists(vpkStub)) { File.WriteAllText(vpkStub, "stub"); createdStub = true; }
                await body(targetPath, Path.Combine(root, "out", "20261010-120000"), original, downloader, replacer);
            }
            finally
            {
                if (createdStub) { try { File.Delete(vpkStub); } catch { } }
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static HeroGenerationService ReleaseService(
            Mock<IOriginalVpkProvider> original, Mock<IHeroSetDownloader> downloader, Mock<IVpkReplacer> replacer)
        {
            var extractor = new Mock<IGameItemsGameExtractor>();
            extractor.Setup(e => e.RefreshFromGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var index = new Mock<IHeroIndexProvider>();
            index.Setup(i => i.GetIndexTextAsync(It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
            return new HeroGenerationService(originalProvider: original.Object, downloader: downloader.Object,
                replacer: replacer.Object, itemsGameExtractor: extractor.Object, indexProvider: index.Object);
        }

        [Test]
        public Task GenerateModsPackAsync_EncryptedSet_DoesNotBlockTheRelease_OnlyNotedInTheLog()
            => WithReleaseRunAsync(async (target, releaseDir, original, downloader, replacer) =>
            {
                var setFolder = Path.Combine(Path.GetDirectoryName(releaseDir)!, "..", "set");
                Directory.CreateDirectory(setFolder);
                downloader.Setup(d => d.DownloadAndExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<Action?>()))
                    .Callback((string _, string _, string _, Action<string> _, CancellationToken _, IProgress<SpeedMetrics>? _, Action? onEncrypted)
                        => onEncrypted?.Invoke())
                    .ReturnsAsync(setFolder);

                var result = await ReleaseService(original, downloader, replacer)
                    .GenerateModsPackAsync(target, new[] { (ReleaseHero(), "Cool Set") }, releaseDir, _ => { });

                Assert.That(result.Message, Does.Not.Contain("encrypted"), "an encrypted set never blocks a ModsPack release");
                Assert.That(result.Message, Does.Contain("No index found"));
                Assert.That(result.LogLines, Has.Some.Match("Note: 'Cool Set' .*encrypted set"),
                    "the run log still says which sets ship decrypted");
                Assert.That(result.Warnings ?? new System.Collections.Generic.List<string>(),
                    Has.None.Contains("encrypted"), "a note, not a warning: the result dialog stays clean");
            });

        [Test]
        public Task GenerateModsPackAsync_ReleaseFolderTaken_FailsBeforeAnyDownload()
            => WithReleaseRunAsync(async (target, releaseDir, original, downloader, replacer) =>
            {
                Directory.CreateDirectory(releaseDir);

                var result = await ReleaseService(original, downloader, replacer)
                    .GenerateModsPackAsync(target, new[] { (ReleaseHero(), "Cool Set") }, releaseDir, _ => { });

                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("already exists"));
                original.Verify(o => o.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()), Times.Never);
            });

        [Test]
        public Task GenerateModsPackAsync_SelectedSetTheBuildWouldSkip_FailsInsteadOfShippingWithoutIt()
            => WithReleaseRunAsync(async (target, releaseDir, original, downloader, replacer) =>
            {
                var withoutZip = ReleaseHero("https://cdn.example/thumb.png");
                withoutZip.HeroId = "npc_dota_hero_other";

                var result = await ReleaseService(original, downloader, replacer)
                    .GenerateModsPackAsync(target, new[] { (ReleaseHero(), "Cool Set"), (withoutZip, "Cool Set") }, releaseDir, _ => { });

                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("missing from the build").And.Contain("npc_dota_hero_other: Cool Set"));
                original.Verify(o => o.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()), Times.Never);
            });

        [Test]
        public async Task GenerateModsPackAsync_NoReleaseFolder_Refused()
        {
            var result = await new HeroGenerationService()
                .GenerateModsPackAsync(@"C:\Dota2", new[] { (ReleaseHero(), "Cool Set") }, " ", _ => { });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("release folder"));
        }

        #endregion
    }
}
