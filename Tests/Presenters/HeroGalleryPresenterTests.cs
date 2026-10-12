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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    public class HeroGalleryPresenterTests
    {
        private Mock<IHeroGenerationService> _generation = null!;
        private Mock<IConfigService> _config = null!;
        private Mock<IHeroGalleryView> _view = null!;
        private Mock<ISkinSelectorCooldownService> _cooldown = null!;
        private Mock<ISkinManifestReader> _manifest = null!;
        private HeroGalleryPresenter _presenter = null!;
        private Func<Task<FeatureCheckResult>> _featureCheck = null!;

        [SetUp]
        public void Setup()
        {
            _generation = new Mock<IHeroGenerationService>();
            _config = new Mock<IConfigService>();
            _view = new Mock<IHeroGalleryView>();
            _cooldown = new Mock<ISkinSelectorCooldownService>();
            _manifest = new Mock<ISkinManifestReader>();

            _config.Setup(c => c.GetLastTargetPath()).Returns(@"C:\Dota2");
            _view.Setup(v => v.UpdateStatusAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.UpdateCooldownAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.SaveSelectionsAsync()).Returns(Task.CompletedTask);
            _view.Setup(v => v.ShowDetectionAsync(It.IsAny<bool>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.ApplyDetectedSelectionsAsync(It.IsAny<IReadOnlyDictionary<string, HeroSelectionState>>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.ShowGenerationAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
                 .ReturnsAsync(false);
            _view.Setup(v => v.ConfirmBaseNoSetAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _view.Setup(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()))
                 .Returns(true);

            TimeSpan zero = TimeSpan.Zero;
            SkinSelectorLockReason noReason = SkinSelectorLockReason.None;
            _cooldown.Setup(c => c.IsOnCooldown(out zero, out noReason)).Returns(false);
            _cooldown.Setup(c => c.GetStatus()).Returns(new SkinSelectorCooldownStatus
            {
                IsActive = false,
                Remaining = TimeSpan.Zero,
                TotalDuration = TimeSpan.FromMinutes(10),
                DailyGenerationsUsed = 0,
                DailyGenerationsMax = 5,
                IsDailyLimitReached = false,
                LockReason = SkinSelectorLockReason.None
            });

            _featureCheck = () => Task.FromResult(FeatureCheckResult.Allowed("Skin Selector"));

            _presenter = new HeroGalleryPresenter(
                _generation.Object, _config.Object, null, () => _featureCheck(), _cooldown.Object, _manifest.Object);
            _presenter.SetView(_view.Object);
        }

        #region DetectInstalledAsync — reads the package, not a local record

        private static SkinManifest Manifest(params (string heroId, string setName, string layer)[] entries)
        {
            var m = new SkinManifest { GeneratedAtUtc = "2026-08-28T00:00:00Z", Build = "2380" };
            foreach (var (heroId, setName, layer) in entries)
                m.Entries.Add(new SkinManifestEntry { HeroId = heroId, SetName = setName, Layer = layer });
            return m;
        }

        [Test]
        public async Task DetectInstalledAsync_NoManifest_ReportsNothingInstalled()
        {
            _manifest.Setup(m => m.ReadInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((SkinManifest?)null);

            await _presenter.DetectInstalledAsync(new[] { Hero("juggernaut", "Bladeform") });

            _view.Verify(v => v.ShowDetectionAsync(false, 0), Times.Once);
            _view.Verify(v => v.ApplyDetectedSelectionsAsync(It.IsAny<IReadOnlyDictionary<string, HeroSelectionState>>()), Times.Never);
        }

        [Test]
        public async Task DetectInstalledAsync_NoTargetPath_ReportsNothingWithoutTouchingThePackage()
        {
            _config.Setup(c => c.GetLastTargetPath()).Returns(string.Empty);

            await _presenter.DetectInstalledAsync(new[] { Hero("juggernaut", "Bladeform") });

            _view.Verify(v => v.ShowDetectionAsync(false, 0), Times.Once);
            _manifest.Verify(m => m.ReadInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task DetectInstalledAsync_ReaderThrows_StillReportsInsteadOfBubbling()
        {
            _manifest.Setup(m => m.ReadInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new IOException("package locked"));

            await _presenter.DetectInstalledAsync(new[] { Hero("juggernaut", "Bladeform") });

            _view.Verify(v => v.ShowDetectionFailedAsync(), Times.Once);
            _view.Verify(v => v.ShowDetectionAsync(It.IsAny<bool>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public async Task DetectInstalledAsync_ReselectsDetectedSkinsAndReportsTheCount()
        {
            _manifest.Setup(m => m.ReadInstalledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Manifest(("juggernaut", "Bladeform", "set"), ("pudge", "Butcher", "set")));

            IReadOnlyDictionary<string, HeroSelectionState>? applied = null;
            _view.Setup(v => v.ApplyDetectedSelectionsAsync(It.IsAny<IReadOnlyDictionary<string, HeroSelectionState>>()))
                 .Callback<IReadOnlyDictionary<string, HeroSelectionState>>(s => applied = s)
                 .Returns(Task.CompletedTask);

            await _presenter.DetectInstalledAsync(new[]
            {
                Hero("juggernaut", "Arcana", "Bladeform"),
                Hero("pudge", "Butcher")
            });

            _view.Verify(v => v.ShowDetectionAsync(true, 2), Times.Once);
            Assert.That(applied, Is.Not.Null);
            Assert.That(applied!["juggernaut"].SetIndex, Is.EqualTo(1));
            Assert.That(applied["pudge"].SetIndex, Is.EqualTo(0));
        }

        [Test]
        public void BuildSelectionsFromManifest_MapsEachLayerToItsOwnSlot()
        {
            var heroes = new[] { Hero("juggernaut", "Arcana", "item_sword", "base_jugg", "prismatic_gold") };

            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(heroes, Manifest(
                ("juggernaut", "Arcana", "set"),
                ("juggernaut", "item_sword", "item"),
                ("juggernaut", "base_jugg", "base"),
                ("juggernaut", "prismatic_gold", "prismatic")));

            var state = selections["juggernaut"];
            Assert.That(state.SetIndex, Is.EqualTo(0));
            Assert.That(state.ItemIndices, Is.EqualTo(new[] { 1 }));
            Assert.That(state.BaseIndex, Is.EqualTo(2));
            Assert.That(state.PrismaticIndex, Is.EqualTo(3));
        }

        [Test]
        public void BuildSelectionsFromManifest_DropsEntriesWhoseHeroOrSetIsGone()
        {
            var heroes = new[] { Hero("juggernaut", "Arcana") };

            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(heroes, Manifest(
                ("juggernaut", "Arcana", "set"),
                ("juggernaut", "Retired Set", "set"),
                ("nobody", "Arcana", "set")));

            Assert.That(selections, Has.Count.EqualTo(1));
            Assert.That(selections["juggernaut"].SetIndex, Is.EqualTo(0));
        }

        [Test]
        public void BuildSelectionsFromManifest_DropsPrismaticWithoutItsBase()
        {
            var heroes = new[] { Hero("juggernaut", "prismatic_gold") };

            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(heroes, Manifest(
                ("juggernaut", "prismatic_gold", "prismatic"),
                ("juggernaut", "base_gone", "base")));

            Assert.That(selections, Is.Empty);
        }

        [Test]
        public void BuildSelectionsFromManifest_NullInputs_ReturnEmpty()
        {
            Assert.That(HeroGalleryPresenter.BuildSelectionsFromManifest(null, Manifest()), Is.Empty);
            Assert.That(HeroGalleryPresenter.BuildSelectionsFromManifest(new[] { Hero("x", "y") }, null), Is.Empty);
        }

        #endregion

        #region Helpers

        private static HeroModel Hero(string id, params string[] setNames)
        {
            var sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in setNames)
                sets[s] = new List<string> { $"https://cdn/{s}.zip", $"https://cdn/{s}.png" };

            return new HeroModel { HeroId = id, LocalizedName = id, ItemIds = new List<int> { 1 }, Sets = sets };
        }

        private static Dictionary<string, HeroSelectionState> Sel(
            string heroId, int? set = null, int? @base = null, params int[] items)
        {
            return new Dictionary<string, HeroSelectionState>
            {
                [heroId] = new HeroSelectionState
                {
                    SetIndex = set,
                    BaseIndex = @base,
                    ItemIndices = items.ToList()
                }
            };
        }

        private void SetupGenerationResult(OperationResult result)
        {
            _view.Setup(v => v.RunGenerationWithProgressAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .ReturnsAsync(result);
        }

        #endregion

        #region Constructor

        [Test]
        public void Constructor_NullGenerationService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new HeroGalleryPresenter(null!, _config.Object));
        }

        [Test]
        public void Constructor_NullConfigService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new HeroGalleryPresenter(_generation.Object, null!));
        }

        #endregion

        #region BuildPlan

        [Test]
        public void BuildPlan_CollectsSetItemsAndBaseLayers()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB", "SetC") };
            var selections = Sel("h1", set: 0, @base: 2, items: new[] { 1 });

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(
                plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "SetA", "SetB", "SetC" }));
            Assert.That(plan.HasSelections, Is.True);
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        [Test]
        public void BuildPlan_NegativeIndex_IsIgnored()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: -1);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets, Is.Empty);
        }

        [Test]
        public void BuildPlan_OutOfRangeIndex_IsIgnored()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: 5);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets, Is.Empty);
        }

        [Test]
        public void BuildPlan_SameSetInMultipleSlots_IsDeduplicated()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB") };
            var selections = Sel("h1", set: 0, @base: 0);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Count, Is.EqualTo(1));
            Assert.That(plan.Sets[0].setName, Is.EqualTo("SetA"));
        }

        [Test]
        public void BuildPlan_BaseSelectedWithoutSet_ReportsHero()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB") };
            var selections = Sel("h1", @base: 1);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.BaseWithoutSetHeroNames, Does.Contain("h1"));
        }

        [Test]
        public void BuildPlan_PrismaticWithBase_IsIncluded()
        {
            var heroes = new[] { Hero("h1", "SetA", "BaseB", "PrismaticC") };
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { BaseIndex = 1, PrismaticIndex = 2 }
            };

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "BaseB", "PrismaticC" }));
        }

        [Test]
        public void BuildPlan_PrismaticWithoutBase_IsDropped()
        {
            var heroes = new[] { Hero("h1", "SetA", "BaseB", "PrismaticC") };
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, PrismaticIndex = 2 }
            };

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "SetA" }));
        }

        [Test]
        public void BuildPlan_UnknownHero_IsSkipped()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("ghost", set: 0);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.HasSelections, Is.False);
        }

        [Test]
        public void BuildPlan_NullInputs_ReturnEmptyPlan()
        {
            var plan = _presenter.BuildPlan(null!, null!);
            Assert.That(plan.HasSelections, Is.False);
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        private static HeroModel LockingHero(bool locked = true)
        {
            var hero = Hero("h1", "storm", "item_head_crown", "item_weapon_bolt", "base_arcana");
            if (locked) hero.BaseLocks["base_arcana"] = new List<string> { "head" };
            return hero;
        }

        [Test]
        public void BuildPlan_LockingBase_DropsItemsInTheLockedSlot_KeepsSetAndOtherItems()
        {
            var plan = _presenter.BuildPlan(new[] { LockingHero() }, Sel("h1", set: 0, @base: 3, items: new[] { 1, 2 }));

            Assert.That(plan.Sets.Select(s => s.setName),
                Is.EquivalentTo(new[] { "storm", "item_weapon_bolt", "base_arcana" }),
                "the head item is the arcana's slot; the legacy set stays and loses its head at generation");
        }

        [Test]
        public void BuildPlan_NoBaseOrNonLockingBase_KeepsTheHeadItem()
        {
            var noBase = _presenter.BuildPlan(new[] { LockingHero() }, Sel("h1", set: 0, items: new[] { 1 }));
            var unlocked = _presenter.BuildPlan(new[] { LockingHero(locked: false) }, Sel("h1", @base: 3, items: new[] { 1 }));

            Assert.That(noBase.Sets.Select(s => s.setName), Does.Contain("item_head_crown"));
            Assert.That(unlocked.Sets.Select(s => s.setName), Does.Contain("item_head_crown"));
        }

        [Test]
        public void BuildPlan_LockingBaseDroppedByConflict_DoesNotLockAnything()
        {
            var hero = LockingHero();
            hero.Incompatible.Add(new SetIncompatibilityRule { Sets = new[] { "storm" }, With = new[] { "base_arcana" } });

            var plan = _presenter.BuildPlan(new[] { hero }, Sel("h1", set: 0, @base: 3, items: new[] { 1 }));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EquivalentTo(new[] { "storm", "item_head_crown" }),
                "the earlier pick wins the conflict, and a base that is not generated locks nothing");
        }

        [Test]
        public void BuildPlan_CompoundLockedSlot_DropsItemsInEitherPart()
        {
            var hero = Hero("h1", "inquisitor", "item_head_hood", "item_body_cloak", "item_weapon_bf", "base_arcana_1");
            hero.BaseLocks["base_arcana_1"] = new List<string> { "body_head" };

            var plan = _presenter.BuildPlan(new[] { hero }, Sel("h1", set: 0, @base: 4, items: new[] { 1, 2, 3 }));

            Assert.That(plan.Sets.Select(s => s.setName),
                Is.EquivalentTo(new[] { "inquisitor", "item_weapon_bf", "base_arcana_1" }));
        }

        [Test]
        public void BuildPlan_Persona_DropsItemsBaseAndPrismatic()
        {
            var hero = Hero("h1", "persona_madame", "item_head_crown", "base_arcana", "prismatic_gold");
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, ItemIndices = new List<int> { 1 }, BaseIndex = 2, PrismaticIndex = 3 }
            };

            var plan = _presenter.BuildPlan(new[] { hero }, selections);

            Assert.That(plan.Sets.Select(s => s.setName), Is.EquivalentTo(new[] { "persona_madame" }));
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        private static HeroModel EffectHero() => Hero("h1", "storm", "item_weapon_vigil", "base_sven",
            "prismatic_gold", "effect_ability2_vigil", "effect_ability2_signet", "effect_ability3_signet",
            "persona_x", "effect_weapon_bad");

        private static Dictionary<string, HeroSelectionState> EffectSel(int? set, int? @base, int? prismatic, params int[] effects) =>
            new()
            {
                ["h1"] = new HeroSelectionState
                {
                    SetIndex = set, BaseIndex = @base, PrismaticIndex = prismatic, EffectIndices = effects.ToList()
                }
            };

        [Test]
        public void BuildPlan_Effect_AppendedAfterPrismatic()
        {
            var plan = _presenter.BuildPlan(new[] { EffectHero() }, EffectSel(0, 2, 3, 4, 6));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[]
            {
                "storm", "base_sven", "prismatic_gold", "effect_ability2_vigil", "effect_ability3_signet"
            }));
        }

        [Test]
        public void BuildPlan_Persona_DropsEffects()
        {
            var plan = _presenter.BuildPlan(new[] { EffectHero() }, EffectSel(7, null, null, 4, 6));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "persona_x" }));
        }

        [Test]
        public void BuildPlan_TwoEffectsSameAbility_KeepsTheFirst()
        {
            var plan = _presenter.BuildPlan(new[] { EffectHero() }, EffectSel(null, null, null, 5, 4));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "effect_ability2_signet" }));
        }

        [TestCase(1, TestName = "BuildPlan_EffectIndex_PointingAtAnItem_IsDropped")]
        [TestCase(8, TestName = "BuildPlan_EffectIndex_WithMalformedTag_IsDropped")]
        [TestCase(99, TestName = "BuildPlan_EffectIndex_OutOfRange_IsDropped")]
        [TestCase(-1, TestName = "BuildPlan_EffectIndex_Negative_IsDropped")]
        public void BuildPlan_EffectIndexNotAnEffect_IsDropped(int badIndex)
        {
            var plan = _presenter.BuildPlan(new[] { EffectHero() }, EffectSel(null, null, null, badIndex, 6));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "effect_ability3_signet" }));
        }

        [Test]
        public void BuildPlan_EffectRefusedForAConflict_DoesNotLockItsAbility()
        {
            var hero = EffectHero();
            hero.Incompatible.Add(new SetIncompatibilityRule { Sets = new[] { "effect_ability2_vigil" }, With = new[] { "storm" } });

            var plan = _presenter.BuildPlan(new[] { hero }, EffectSel(0, null, null, 4, 5));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "storm", "effect_ability2_signet" }));
        }

        [Test]
        public void BuildPlan_EffectArchiveInANonEffectSlot_IsDropped()
        {
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 4, ItemIndices = new List<int> { 5, 1 } }
            };

            var plan = _presenter.BuildPlan(new[] { EffectHero() }, selections);

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "item_weapon_vigil" }));
        }

        private static HeroModel CarrierHero() => Hero("h1", "item_head_apex", "effect_ability1_head_apex",
            "effect_ability2_head_apex", "effect_ability1_back_artistry_cape", "item_head_other", "base_arcana");

        private static Dictionary<string, HeroSelectionState> CarrierSel(int[] items, int[] effects, int? @base = null) =>
            new()
            {
                ["h1"] = new HeroSelectionState { ItemIndices = items.ToList(), EffectIndices = effects.ToList(), BaseIndex = @base }
            };

        [Test]
        public void BuildPlan_EffectWhoseItemIsPicked_IsDropped_OtherEffectsKept()
        {
            var plan = _presenter.BuildPlan(new[] { CarrierHero() }, CarrierSel(new[] { 0 }, new[] { 1, 2 }));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "item_head_apex" }));
        }

        [Test]
        public void BuildPlan_EffectDroppedForItsItem_DoesNotLockItsAbility()
        {
            var plan = _presenter.BuildPlan(new[] { CarrierHero() }, CarrierSel(new[] { 0 }, new[] { 1, 3 }));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "item_head_apex", "effect_ability1_back_artistry_cape" }));
        }

        [Test]
        public void BuildPlan_EffectWithoutItsItem_IsKept()
        {
            var plan = _presenter.BuildPlan(new[] { CarrierHero() }, CarrierSel(new[] { 4 }, new[] { 1, 2 }));

            Assert.That(plan.Sets.Select(s => s.setName),
                Is.EqualTo(new[] { "item_head_other", "effect_ability1_head_apex", "effect_ability2_head_apex" }));
        }

        [Test]
        public void BuildPlan_CarrierItemLockedByBase_ItsEffectComesBack()
        {
            var hero = CarrierHero();
            hero.BaseLocks["base_arcana"] = new List<string> { "head" };

            var plan = _presenter.BuildPlan(new[] { hero }, CarrierSel(new[] { 0 }, new[] { 1 }, @base: 5));

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "base_arcana", "effect_ability1_head_apex" }));
        }

        [Test]
        public void BuildPlan_EffectOnly_NeedsNoSetAndNoConfirm()
        {
            var plan = _presenter.BuildPlan(new[] { EffectHero() }, EffectSel(null, null, null, 4));

            Assert.That(plan.HasSelections, Is.True);
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        [Test]
        public void BuildPlan_NullEffectList_DoesNotThrow()
        {
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, EffectIndices = null! }
            };

            var plan = _presenter.BuildPlan(new[] { EffectHero() }, selections);

            Assert.That(plan.Sets.Select(s => s.setName), Is.EqualTo(new[] { "storm" }));
        }

        [Test]
        public void PresetStems_RoundTrip_SurviveASetInsertedBeforeThePicks()
        {
            var saved = EffectHero();
            var selections = EffectSel(0, 2, 3, 4, 6);
            selections["h1"].ItemIndices = new List<int> { 1 };
            HeroGalleryPresenter.AttachStems(new[] { saved }, selections);

            var json = System.Text.Json.JsonSerializer.Serialize(selections);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, HeroSelectionState>>(json)!;
            var now = Hero("h1", "new_first", "storm", "item_weapon_vigil", "base_sven",
                "prismatic_gold", "effect_ability2_vigil", "effect_ability2_signet", "effect_ability3_signet");

            var missing = HeroGalleryPresenter.ResolveStems(new[] { now }, loaded);

            var s = loaded["h1"];
            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain(@"""stems"""));
                Assert.That(missing, Is.Empty);
                Assert.That((s.SetIndex, s.BaseIndex, s.PrismaticIndex), Is.EqualTo(((int?)1, (int?)3, (int?)4)));
                Assert.That(s.ItemIndices, Is.EqualTo(new[] { 2 }));
                Assert.That(s.EffectIndices, Is.EqualTo(new[] { 5, 7 }));
                Assert.That(s.Stems, Is.Null);
            });
        }

        [Test]
        public void PresetStems_MissingStem_IsDroppedAndReported_OldPresetKeepsIndices()
        {
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState
                {
                    SetIndex = 0, EffectIndices = new List<int> { 9 },
                    Stems = new SelectionStems { Set = "storm", Effects = new List<string> { "effect_ability2_gone", "EFFECT_ABILITY3_SIGNET" } }
                },
                ["h2"] = new HeroSelectionState { SetIndex = 1 },
            };

            var missing = HeroGalleryPresenter.ResolveStems(new[] { EffectHero(), Hero("h2", "a", "b") }, selections);

            Assert.That(missing, Is.EqualTo(new[] { "h1: effect_ability2_gone" }));
            Assert.That(selections["h1"].EffectIndices, Is.EqualTo(new[] { 6 }));
            Assert.That(selections["h2"].SetIndex, Is.EqualTo(1), "a preset saved before stems keeps its index");
            Assert.That(HeroGalleryPresenter.ResolveStems(null, selections), Is.Empty);
        }

        [Test]
        public void HeroSelectionState_WithoutStems_WritesNoStemsKey()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new HeroSelectionState { SetIndex = 1 });
            Assert.That(json, Does.Not.Contain("stems"));
        }

        [Test]
        public void BuildSelectionsFromManifest_EffectLayer_RoundTrips()
        {
            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(new[] { EffectHero() }, Manifest(
                ("h1", "effect_ability2_vigil", "effect"),
                ("h1", "effect_ability3_signet", "effect"),
                ("h1", "effect_ability3_signet", "effect")));

            var state = selections["h1"];
            Assert.That(state.EffectIndices, Is.EqualTo(new[] { 4, 6 }));
            Assert.That(state.SetIndex, Is.Null, "an effect entry is never read back as the set");
        }

        [Test]
        public void HeroSelectionState_Effects_RoundTripThroughJson_OldPresetsLoadEmpty()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                new HeroSelectionState { SetIndex = 1, EffectIndices = new List<int> { 4 } });
            var back = System.Text.Json.JsonSerializer.Deserialize<HeroSelectionState>(json)!;
            var legacy = System.Text.Json.JsonSerializer.Deserialize<HeroSelectionState>(@"{""set"":1,""items"":[]}")!;

            Assert.That(json, Does.Contain(@"""effects"":[4]"));
            Assert.That(back.EffectIndices, Is.EqualTo(new[] { 4 }));
            Assert.That(legacy.EffectIndices, Is.Empty);
            Assert.That(new HeroSelectionState { EffectIndices = new List<int> { 4 } }.HasAnySelection, Is.True);
        }

        [Test]
        public void BuildPlan_NullItemList_DoesNotThrow()
        {
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, ItemIndices = null!, BaseIndex = 1 }
            };

            var plan = _presenter.BuildPlan(new[] { Hero("h1", "storm", "base_arcana") }, selections);

            Assert.That(plan.Sets.Select(s => s.setName), Is.EquivalentTo(new[] { "storm", "base_arcana" }));
        }

        [Test]
        public void BuildPlan_IncompatiblePicks_KeepTheEarlierLayer()
        {
            var hero = Hero("h1", "storm", "item_weapon_bolt", "base_arcana", "prismatic_gold");
            hero.Incompatible.Add(new SetIncompatibilityRule { Sets = new[] { "item_weapon_bolt", "base_arcana" } });
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, ItemIndices = new List<int> { 1 }, BaseIndex = 2, PrismaticIndex = 3 }
            };

            var plan = _presenter.BuildPlan(new[] { hero }, selections);

            Assert.That(plan.Sets.Select(s => s.setName), Is.EquivalentTo(new[] { "storm", "item_weapon_bolt" }),
                "the base conflicts with the earlier item, and its prismatic goes with it");
        }

        #endregion

        #region GenerateAsync

        [Test]
        public async Task GenerateAsync_NoSelections_PromptsAndDoesNotGenerate()
        {
            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") },
                new Dictionary<string, HeroSelectionState>());

            _view.Verify(v => v.UpdateStatusAsync("hero.status.selectOne"), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_BaseNoSetDeclined_Cancels()
        {
            _view.Setup(v => v.ConfirmBaseNoSetAsync(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync(false);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA", "SetB") }, Sel("h1", @base: 1));

            _view.Verify(v => v.UpdateStatusAsync("hero.status.cancelled"), Times.Once);
            _view.Verify(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_NoTargetPath_ShowsWarning()
        {
            _config.Setup(c => c.GetLastTargetPath()).Returns(string.Empty);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowWarning(It.IsAny<string>(), "common.error"), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_OnlyDefaultSet_ShowsNoSelectionsAlert()
        {
            await _presenter.GenerateAsync(new[] { Hero("h1", "Default Set") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowAlertAsync("hero.noSelections.title", It.IsAny<string>()), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_PreviewDeclined_DoesNotGenerate()
        {
            _view.Setup(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()))
                 .Returns(false);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
            _view.Verify(v => v.StoreResult(It.IsAny<ModGenerationResult>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_Success_StoresResultAndCloses()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.SaveSelectionsAsync(), Times.Once);
            _view.Verify(v => v.ShowGenerationAlertAsync("hero.complete.title", It.IsAny<string>(), false, It.IsAny<string?>()), Times.Once);
            _view.Verify(v => v.StoreResult(It.Is<ModGenerationResult>(r => r.Success && r.Type == GenerationType.SkinSelector)), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_Success_PlayRequestedFromAlert_StoresPlayRequestedTrue()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });
            _view.Setup(v => v.ShowGenerationAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
                 .ReturnsAsync(true);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.StoreResult(It.Is<ModGenerationResult>(r => r.PlayRequested)), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_Failure_StoresResultAndShowsError()
        {
            SetupGenerationResult(new OperationResult { Success = false, Message = "boom" });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.StoreResult(It.Is<ModGenerationResult>(r => !r.Success && r.ErrorMessage == "boom")), Times.Once);
            _view.Verify(v => v.ShowErrorDialog("hero.failed.title", It.IsAny<string>(), "boom"), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_SuccessWithLogLines_PassesLogTextToAlert()
        {
            SetupGenerationResult(new OperationResult
            {
                Success = true,
                Message = "Done",
                SuccessCount = 1,
                LogLines = new[] { "12:00:00 step one", "12:00:01 DEBUG detail" }
            });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowGenerationAlertAsync(
                "hero.complete.title",
                It.IsAny<string>(),
                false,
                It.Is<string?>(t => t != null && t.Contains("step one") && t.Contains("DEBUG detail"))), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_SuccessWithoutLogLines_PassesNullLogText()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowGenerationAlertAsync(
                "hero.complete.title", It.IsAny<string>(), false, null), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_FailureWithLogLines_AppendsLogToErrorDetails()
        {
            SetupGenerationResult(new OperationResult
            {
                Success = false,
                Message = "boom",
                LogLines = new[] { "12:00:00 step one" }
            });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowErrorDialog(
                "hero.failed.title",
                It.IsAny<string>(),
                It.Is<string>(d => d.Contains("boom") && d.Contains("step one"))), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_Canceled_NoErrorDialog()
        {
            SetupGenerationResult(OperationResult.Canceled());

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _view.Verify(v => v.StoreResult(It.IsAny<ModGenerationResult>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_IsReentrant_SecondCallIgnoredWhileBusy()
        {
            var gate = new TaskCompletionSource<OperationResult>();
            _view.Setup(v => v.RunGenerationWithProgressAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .Returns(gate.Task);

            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: 0);

            var first = _presenter.GenerateAsync(heroes, selections);
            var second = _presenter.GenerateAsync(heroes, selections);

            await second;
            gate.SetResult(new OperationResult { Success = true, Message = "Done" });
            await first;

            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WithoutView_Throws()
        {
            var presenter = new HeroGalleryPresenter(_generation.Object, _config.Object);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0)));
        }

        [Test]
        public async Task GenerateAsync_WhenFeatureBlocked_DoesNotGenerate()
        {
            _featureCheck = () => Task.FromResult(
                FeatureCheckResult.Blocked("Skin Selector", "Offline."));

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _generation.Verify(g => g.GenerateBatchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<IProgress<(int, int, string)>>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<IProgress<SpeedMetrics>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _view.Verify(v => v.ShowGenerationPreview(
                It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
            _view.Verify(v => v.ShowAlertAsync(It.IsAny<string>(), "Offline."), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WhenOutdated_BlocksWithUpdateNotice()
        {
            _featureCheck = () => Task.FromResult(
                FeatureCheckResult.Outdated("Skin Selector", "Update to 2.3.0.", "2.3.0"));

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowGenerationPreview(
                It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
            _view.Verify(v => v.ShowAlertAsync(It.IsAny<string>(), "Update to 2.3.0."), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WhenOnCooldown_BlocksGenerationAndShowsAlert()
        {
            TimeSpan remaining = TimeSpan.FromMinutes(5);
            SkinSelectorLockReason reason = SkinSelectorLockReason.Cooldown;
            _cooldown.Setup(c => c.IsOnCooldown(out remaining, out reason)).Returns(true);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _generation.Verify(g => g.GenerateBatchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<IProgress<(int, int, string)>>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<IProgress<SpeedMetrics>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _view.Verify(v => v.ShowGenerationPreview(
                It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
            _view.Verify(v => v.ShowAlertAsync("hero.cooldown.title", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WhenDailyLimitReached_ShowsDailyLimitAlert()
        {
            TimeSpan remaining = TimeSpan.FromHours(12);
            SkinSelectorLockReason reason = SkinSelectorLockReason.DailyLimitReached;
            _cooldown.Setup(c => c.IsOnCooldown(out remaining, out reason)).Returns(true);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _generation.Verify(g => g.GenerateBatchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<IProgress<(int, int, string)>>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<IProgress<SpeedMetrics>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _view.Verify(v => v.ShowAlertAsync("hero.cooldown.dailyLimitTitle", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WhenSuccess_RecordsCooldown()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _cooldown.Verify(c => c.RecordGeneration(), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WhenFailure_DoesNotRecordCooldown()
        {
            SetupGenerationResult(new OperationResult { Success = false, Message = "Failed" });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _cooldown.Verify(c => c.RecordGeneration(), Times.Never);
        }

        [Test]
        public async Task SyncCooldownStatusAsync_PushesCooldownStateToView()
        {
            _cooldown.Setup(c => c.GetStatus()).Returns(new SkinSelectorCooldownStatus
            {
                IsActive = true,
                Remaining = TimeSpan.FromMinutes(6),
                TotalDuration = TimeSpan.FromMinutes(10),
                DailyGenerationsUsed = 1,
                DailyGenerationsMax = 5,
                IsDailyLimitReached = false,
                LockReason = SkinSelectorLockReason.Cooldown
            });

            await _presenter.SyncCooldownStatusAsync();

            _view.Verify(v => v.UpdateCooldownAsync(true, 360, 600, 1, 5, false), Times.Once);
        }

        [Test]
        public async Task SyncCooldownStatusAsync_ConfigNotCached_PreservesCallersSynchronizationContext()
        {
            var previousCachePath = FeatureAccessService.CacheFilePath;
            var tempCacheDir = Path.Combine(Path.GetTempPath(), "amt_hgp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempCacheDir);
            FeatureAccessService.CacheFilePath = Path.Combine(tempCacheDir, "feature_access_cache.json");
            FeatureAccessService.InvalidateCache();

            var testContext = new InlineSynchronizationContext();
            var previousContext = SynchronizationContext.Current;

            SynchronizationContext? observed = null;
            _view.Setup(v => v.UpdateCooldownAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
                 .Callback(() => observed = SynchronizationContext.Current)
                 .Returns(Task.CompletedTask);

            try
            {
                SynchronizationContext.SetSynchronizationContext(testContext);
                await _presenter.SyncCooldownStatusAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                FeatureAccessService.CacheFilePath = previousCachePath;
                FeatureAccessService.InvalidateCache();
                try { Directory.Delete(tempCacheDir, recursive: true); } catch { }
            }

            Assert.That(observed, Is.SameAs(testContext),
                "the presenter must not ConfigureAwait(false) before calling into the view — doing " +
                "so drops the caller's UI-thread context and crashes CoreWebView2 access");
        }

        private sealed class InlineSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state)
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try { d(state); }
                finally { SetSynchronizationContext(previous); }
            }

            public override void Send(SendOrPostCallback d, object? state) => Post(d, state);
        }

        #endregion

        #region DebugBuildAsync — the Skin Selector's debug card (local zips, Debug builds only)

        private static (string heroesJson, string zip, string root) DebugFixture()
        {
            string root = Path.Combine(Path.GetTempPath(), "amt_debugbuild_" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "Juggernaut");
            Directory.CreateDirectory(folder);
            string heroesJson = Path.Combine(root, "heroes.json");
            File.WriteAllText(heroesJson,
                "[{\"id\":[6,7],\"used_by_heroes\":\"npc_dota_hero_juggernaut\",\"name\":\"Juggernaut\"," +
                "\"sets\":{\"Default Set\":[\"default_skin.vpk\"]}}]");
            string zip = Path.Combine(folder, "item_weapon_exile_odachi.zip");
            using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            using (var w = new StreamWriter(z.CreateEntry("index.txt").Open()))
                w.Write("\"27486\"\n{\n\t\"used_by_heroes\"\n\t{\n\t\t\"npc_dota_hero_juggernaut\"\t\t\"1\"\n\t}\n}\n");
            return (heroesJson, zip, root);
        }

        private HeroGalleryPresenter DebugPresenter(Mock<IHeroGenerationService> debugService, List<SkinTesterPlan> plans)
        {
            var p = new HeroGalleryPresenter(_generation.Object, _config.Object, null, () => _featureCheck(),
                _cooldown.Object, _manifest.Object,
                (plan, trace) =>
                {
                    plans.Add(plan);
                    trace.LogDebug("Juggernaut: body owner 'Set 11' (juggernaut_arcana): 4 variant file(s) applied");
                    return debugService.Object;
                });
            p.SetView(_view.Object);
            _view.Setup(v => v.RunGenerationWithProgressAsync(It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .Returns((string _, Func<ProgressOperationRunnerContext, Task<OperationResult>> op) => op(
                     new ProgressOperationRunnerContext(CancellationToken.None, new Progress<SpeedMetrics>(),
                         new Progress<string>(), new Progress<string>(), new Progress<int>())));
            return p;
        }

        [Test]
        public async Task DebugBuildAsync_LocalZip_RunsThePipelineAndShowsTheOwnershipTrace()
        {
            var (heroesJson, zip, root) = DebugFixture();
            try
            {
                var debugService = new Mock<IHeroGenerationService>();
                debugService.Setup(s => s.GenerateBatchAsync(It.IsAny<string>(),
                        It.IsAny<IReadOnlyList<(HeroModel, string)>>(), It.IsAny<Action<string>>(),
                        It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                        It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OperationResult { Success = true, Message = "Successfully installed 1 hero set(s)",
                        LogLines = new[] { "Building VPK..." } });
                var plans = new List<SkinTesterPlan>();
                string? shownLog = null;
                _view.Setup(v => v.ShowGenerationAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
                     .Callback((string _, string _, bool _, string? log) => shownLog = log)
                     .ReturnsAsync(false);

                await DebugPresenter(debugService, plans).DebugBuildAsync(new[] { zip }, heroesJson);

                Assert.That(plans.Single().Selections.Single().Hero.Id, Is.EqualTo("npc_dota_hero_juggernaut"));
                debugService.Verify(s => s.GenerateBatchAsync(@"C:\Dota2",
                    It.Is<IReadOnlyList<(HeroModel, string)>>(l => l.Count == 1), It.IsAny<Action<string>>(),
                    It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(), It.IsAny<CancellationToken>()), Times.Once);
                _view.Verify(v => v.ShowGenerationAlertAsync("Debug build installed", It.IsAny<string>(), false, It.IsAny<string?>()), Times.Once);
                Assert.That(shownLog, Does.Contain("Building VPK...").And.Contain("body owner 'Set 11' (juggernaut_arcana)"),
                    "the run log AND the body-ownership trace - the reason this card exists");
                _generation.Verify(g => g.GenerateBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                    It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(), It.IsAny<CancellationToken>()), Times.Never, "never the CDN pipeline");
                _cooldown.Verify(c => c.RecordGeneration(), Times.Never, "a debug build costs no quota");
                _view.Verify(v => v.CloseWithSuccess(), Times.Never, "the gallery stays open for the next try");
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public async Task DebugBuildAsync_PlanErrors_AlertAndNeverBuild()
        {
            var (heroesJson, _, root) = DebugFixture();
            try
            {
                string stray = Path.Combine(root, "stray.zip");
                using (var z = System.IO.Compression.ZipFile.Open(stray, System.IO.Compression.ZipArchiveMode.Create))
                    z.CreateEntry("readme.txt");
                var debugService = new Mock<IHeroGenerationService>();
                var plans = new List<SkinTesterPlan>();

                await DebugPresenter(debugService, plans).DebugBuildAsync(new[] { stray }, heroesJson);

                _view.Verify(v => v.ShowAlertAsync("Debug build", It.Is<string>(m => m.Contains("stray.zip"))), Times.Once);
                Assert.That(plans, Is.Empty, "no pipeline is built for a plan with errors");
                _view.Verify(v => v.RunGenerationWithProgressAsync(It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public async Task DebugBuildAsync_NoTargetOrNoZips_AlertOnly()
        {
            var plans = new List<SkinTesterPlan>();
            var presenter = DebugPresenter(new Mock<IHeroGenerationService>(), plans);

            await presenter.DebugBuildAsync(Array.Empty<string>(), "missing-heroes.json");
            _view.Verify(v => v.ShowAlertAsync("Debug build", It.Is<string>(m => m.Contains("heroes.json not found"))), Times.Once);

            _config.Setup(c => c.GetLastTargetPath()).Returns((string?)null);
            await presenter.DebugBuildAsync(new[] { "x.zip" }, "missing-heroes.json");
            _view.Verify(v => v.ShowAlertAsync("Debug build", It.Is<string>(m => m.Contains("Dota 2 folder"))), Times.Once);
            Assert.That(plans, Is.Empty);
        }

        [Test]
        public async Task PlanDebugBuildAsync_RowsNameHeroAndLayer()
        {
            var (heroesJson, zip, root) = DebugFixture();
            try
            {
                var plan = await _presenter.PlanDebugBuildAsync(new[] { zip }, heroesJson);
                var row = plan.Rows.Single();
                Assert.That((row.File, row.HeroId, row.HeroName, row.Layer),
                    Is.EqualTo(("item_weapon_exile_odachi.zip", "npc_dota_hero_juggernaut", "Juggernaut", "Item")));
                Assert.That((row.Order, row.Top), Is.EqualTo((1, true)), "a lone layer is its hero's whole stack");
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public async Task PlanDebugBuildAsync_GarbageInput_ReturnsErrors_NeverThrows()
        {
            var (heroesJson, _, root) = DebugFixture();
            try
            {
                string junk = Path.Combine(root, "Juggernaut", "item_junk.zip");
                File.WriteAllText(junk, "not a zip");
                string gone = Path.Combine(root, "Juggernaut", "item_gone.zip");
                string badJson = Path.Combine(root, "bad.json");
                File.WriteAllText(badJson, "{ nope");

                var plan = await _presenter.PlanDebugBuildAsync(new[] { junk, gone }, heroesJson);
                Assert.That(plan.ErrorsByPath.Keys, Is.EquivalentTo(new[] { junk, gone }));

                var badPlan = await _presenter.PlanDebugBuildAsync(new[] { junk }, badJson);
                Assert.That(badPlan.Errors.Single(), Does.Contain("heroes.json unreadable"));
            }
            finally { Directory.Delete(root, true); }
        }

        #endregion

        #region GenerateModsPackAsync — the debug card's ModsPack release package

        private const string OutRoot = @"F:\out\modspack";

        private void RunOperationsForReal()
            => _view.Setup(v => v.RunGenerationWithProgressAsync(It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .Returns((string _, Func<ProgressOperationRunnerContext, Task<OperationResult>> op) => op(
                     new ProgressOperationRunnerContext(CancellationToken.None, new Progress<SpeedMetrics>(),
                         new Progress<string>(), new Progress<string>(), new Progress<int>())));

        private void SetupModsPackResult(OperationResult result)
            => _generation.Setup(g => g.GenerateModsPackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                    It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, string)>?>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

        private void VerifyNoModsPackRun()
            => _generation.Verify(g => g.GenerateModsPackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, string)>?>(),
                It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<CancellationToken>()), Times.Never);

        [Test]
        public async Task GenerateModsPackAsync_RunsTheReleasePipeline_NotTheNormalOne_AndCostsNoQuota()
        {
            RunOperationsForReal();
            SetupModsPackResult(new OperationResult { Success = true, Message = "Successfully installed 1 hero set(s)" });

            await _presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0), OutRoot);

            _generation.Verify(g => g.GenerateModsPackAsync(@"C:\Dota2",
                It.Is<IReadOnlyList<(HeroModel hero, string setName)>>(l => l.Single().setName == "SetA"),
                It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, string)>?>(),
                It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<CancellationToken>()), Times.Once);
            _generation.Verify(g => g.GenerateBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(), It.IsAny<CancellationToken>()), Times.Never,
                "a release package never goes through the normal split pipeline");
            _view.Verify(v => v.ShowGenerationAlertAsync("ModsPack package installed", It.IsAny<string>(), false, It.IsAny<string?>()), Times.Once);
            _cooldown.Verify(c => c.RecordGeneration(), Times.Never, "a debug run costs no quota");
            _view.Verify(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
            _view.Verify(v => v.CloseWithSuccess(), Times.Never);
        }

        [Test]
        public async Task GenerateModsPackAsync_WritesEachReleaseToItsOwnTimestampedFolderUnderTheRoot()
        {
            RunOperationsForReal();
            var dirs = new List<string>();
            _generation.Setup(g => g.GenerateModsPackAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                    It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, string)>?>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<CancellationToken>()))
                .Callback((string _, IReadOnlyList<(HeroModel, string)> _, string dir, Action<string> _,
                    IProgress<(int, string)>? _, IProgress<SpeedMetrics>? _, CancellationToken _) => dirs.Add(dir))
                .ReturnsAsync(new OperationResult { Success = true });

            await _presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0), OutRoot);

            Assert.That(dirs, Has.Count.EqualTo(1));
            Assert.That(Path.GetDirectoryName(dirs[0]), Is.EqualTo(OutRoot), "never the root itself, never elsewhere");
            Assert.That(Path.GetFileName(dirs[0]), Does.Match(@"^\d{8}-\d{6}$"));
        }

        [Test]
        public async Task GenerateModsPackAsync_VerificationFailure_ShowsFailedWithTheReason()
        {
            RunOperationsForReal();
            SetupModsPackResult(new OperationResult { Success = false, Message = "ModsPack package failed verification: x" });

            await _presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0), OutRoot);

            _view.Verify(v => v.ShowGenerationAlertAsync("ModsPack package failed",
                It.Is<string>(m => m.Contains("failed verification")), true, It.IsAny<string?>()), Times.Once);
        }

        [Test]
        public async Task GenerateModsPackAsync_NoCustomSetOrNoTarget_AlertsAndNeverBuilds()
        {
            await _presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, new Dictionary<string, HeroSelectionState>(), OutRoot);
            _view.Verify(v => v.ShowAlertAsync("Generate ModsPack", It.Is<string>(m => m.Contains("custom set"))), Times.Once);

            _config.Setup(c => c.GetLastTargetPath()).Returns((string?)null);
            await _presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0), OutRoot);
            _view.Verify(v => v.ShowAlertAsync("Generate ModsPack", It.Is<string>(m => m.Contains("Dota 2 folder"))), Times.Once);

            VerifyNoModsPackRun();
        }

        [Test]
        public void GenerateModsPackAsync_WithoutView_Throws()
        {
            var presenter = new HeroGalleryPresenter(_generation.Object, _config.Object);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                presenter.GenerateModsPackAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0), OutRoot));
        }

        #endregion

        #region Particles — ride on a kept layer, at most two, validated in C#

        private const string Jugg = "npc_dota_hero_juggernaut";

        private static HeroModel JuggHero(params string[] setNames)
        {
            var hero = Hero(Jugg, setNames);
            hero.ItemIds = new List<int> { 6, 7, 8, 9, 62, 811 };
            return hero;
        }

        private static ParticlePick P(string id, string host) => new() { Id = id, Host = host };

        private static Dictionary<string, HeroSelectionState> WithParticles(int? set, params ParticlePick[] picks) => new()
        {
            [Jugg] = new HeroSelectionState { SetIndex = set, Particles = picks.ToList() }
        };

        [Test]
        public void BuildPlan_Particles_KeptWithASet()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("SetA") }, WithParticles(0, P("833", "7")));

            Assert.That(plan.Particles[Jugg].Select(p => (p.Id, p.Host)), Is.EqualTo(new[] { ("833", "7") }));
        }

        [Test]
        public void BuildPlan_Particles_WithoutAnyLayer_AreDropped()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("SetA") }, WithParticles(null, P("833", "7")));

            Assert.That(plan.Particles, Is.Empty);
            Assert.That(plan.HasSelections, Is.False);
        }

        [Test]
        public void BuildPlan_Particles_OnlyDefaultSet_DoesNotCount()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("Default Set") }, WithParticles(0, P("833", "7")));

            Assert.That(plan.Particles, Is.Empty);
        }

        [Test]
        public void BuildPlan_Particles_PersonaDropsThem()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("persona_one") }, WithParticles(0, P("833", "7")));

            Assert.That(plan.Sets, Has.Count.EqualTo(1));
            Assert.That(plan.Particles, Is.Empty);
        }

        [Test]
        public void BuildPlan_Particles_CappedAtTwo_UnknownAndForeignDropped()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("SetA") }, WithParticles(0,
                P("not-a-particle", "7"),
                P("833", "999"),
                P("833", "7"),
                P("833", "7"),
                P("837", "6"),
                P("826", "8")));

            Assert.That(plan.Particles[Jugg].Select(p => (p.Id, p.Host)),
                Is.EqualTo(new[] { ("833", "7"), ("837", "6") }));
        }

        [Test]
        public void BuildPlan_Particles_SurviveTheNullListCopy()
        {
            var sel = WithParticles(0, P("833", "7"));
            sel[Jugg].ItemIndices = null!;

            var plan = _presenter.BuildPlan(new[] { JuggHero("SetA") }, sel);

            Assert.That(plan.Particles[Jugg], Has.Count.EqualTo(1));
        }

        [Test]
        public void BuildPlan_Particles_DroppedWhenEveryLayerIsRefused()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("SetA") }, new Dictionary<string, HeroSelectionState>
            {
                [Jugg] = new HeroSelectionState { SetIndex = 5, Particles = { P("833", "7") } }
            });

            Assert.That(plan.Particles, Is.Empty);
        }

        [Test]
        public void BuildPlan_Particles_AnAbilityEffectAloneCarriesNothing()
        {
            var plan = _presenter.BuildPlan(new[] { JuggHero("effect_ability1_head_apex") }, new Dictionary<string, HeroSelectionState>
            {
                [Jugg] = new HeroSelectionState { EffectIndices = { 0 }, Particles = { P("833", "7") } }
            });

            Assert.That(plan.Sets, Has.Count.EqualTo(1));
            Assert.That(plan.Particles, Is.Empty);
        }

        [Test]
        public async Task LoadParticleSlotsAsync_PickedSet_AnswersWithItsIndexSlots()
        {
            var slots = new ParticleSlots(new[] { new ParticleSlot("7", "Weapon", true) }, null);
            _generation.Setup(g => g.GetParticleSlotsAsync(It.Is<HeroModel>(h => h.Id == Jugg), "SetA", It.IsAny<CancellationToken>()))
                .ReturnsAsync(slots);

            await _presenter.LoadParticleSlotsAsync(new[] { JuggHero("SetA") }, Jugg, 0);

            _view.Verify(v => v.ShowParticleSlotsAsync(Jugg, 0, slots), Times.Once);
        }

        [TestCase(5)]
        [TestCase(1)]
        [TestCase(-1)]
        public async Task LoadParticleSlotsAsync_BadOrNonCarrierIndex_AnswersNull_WithoutReadingAnIndex(int setIndex)
        {
            await _presenter.LoadParticleSlotsAsync(new[] { JuggHero("SetA", "persona_one") }, Jugg, setIndex);

            _generation.Verify(g => g.GetParticleSlotsAsync(It.IsAny<HeroModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _view.Verify(v => v.ShowParticleSlotsAsync(Jugg, setIndex, null), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_PassesTheSanitizedParticlesToGeneration()
        {
            RunOperationsForReal();
            IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? passed = null;
            _generation.Setup(g => g.GenerateBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(HeroModel, string)>>(),
                    It.IsAny<Action<string>>(), It.IsAny<IProgress<(int, int, string)>?>(), It.IsAny<IProgress<(int, string)>?>(),
                    It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((string _, IReadOnlyList<(HeroModel, string)> _, Action<string> _, IProgress<(int, int, string)>? _,
                    IProgress<(int, string)>? _, IProgress<SpeedMetrics>? _, IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>>? p,
                    CancellationToken _) => passed = p)
                .ReturnsAsync(new OperationResult { Success = true, SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { JuggHero("SetA") }, WithParticles(0, P("833", "7"), P("bogus", "7")));

            Assert.That(passed, Is.Not.Null);
            Assert.That(passed![Jugg].Select(p => p.Id), Is.EqualTo(new[] { "833" }));
        }

        [Test]
        public void BuildSelectionsFromManifest_RestoresParticles_AndDropsForeignHosts()
        {
            var manifest = Manifest((Jugg, "SetA", "set"));
            manifest.Entries.Add(new SkinManifestEntry { HeroId = Jugg, SetName = "particle:833", Layer = "particle", Host = "7" });
            manifest.Entries.Add(new SkinManifestEntry { HeroId = Jugg, SetName = "particle:837", Layer = "particle", Host = "999" });

            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(new[] { JuggHero("SetA") }, manifest);

            Assert.That(selections[Jugg].SetIndex, Is.EqualTo(0));
            Assert.That(selections[Jugg].Particles.Select(p => (p.Id, p.Host)), Is.EqualTo(new[] { ("833", "7") }));
        }

        [Test]
        public void BuildSelectionsFromManifest_ParticlesAlone_AreNotASelection()
        {
            var manifest = Manifest();
            manifest.Entries.Add(new SkinManifestEntry { HeroId = Jugg, SetName = "particle:833", Layer = "particle", Host = "7" });

            Assert.That(HeroGalleryPresenter.BuildSelectionsFromManifest(new[] { JuggHero("SetA") }, manifest), Is.Empty);
        }

        [Test]
        public void BuildSelectionsFromManifest_UnknownLayer_IsNotReadAsASet()
        {
            var selections = HeroGalleryPresenter.BuildSelectionsFromManifest(
                new[] { Hero("h1", "SetA", "SetB") }, Manifest(("h1", "SetA", "item"), ("h1", "SetB", "future")));

            Assert.That(selections["h1"].SetIndex, Is.Null);
            Assert.That(selections["h1"].ItemIndices, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void Preset_ParticlesRoundTripThroughStems()
        {
            var heroes = new[] { JuggHero("SetA", "SetB") };
            var sel = WithParticles(1, P("833", "7"));

            HeroGalleryPresenter.AttachStems(heroes, sel);
            var json = System.Text.Json.JsonSerializer.Serialize(sel);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, HeroSelectionState>>(json)!;
            HeroGalleryPresenter.ResolveStems(heroes, loaded);

            Assert.That(loaded[Jugg].SetIndex, Is.EqualTo(1));
            Assert.That(loaded[Jugg].Particles.Select(p => (p.Id, p.Host)), Is.EqualTo(new[] { ("833", "7") }));
        }

        [Test]
        public void SelectionJson_WithoutParticles_ReadsAsEmptyList()
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<HeroSelectionState>(@"{""set"":1,""items"":[]}")!;
            var withPicks = System.Text.Json.JsonSerializer.Deserialize<HeroSelectionState>(
                @"{""set"":1,""particles"":[{""id"":""833"",""host"":""7""}]}")!;

            Assert.That(state.Particles, Is.Empty);
            Assert.That(withPicks.Particles.Single().Host, Is.EqualTo("7"));
            Assert.That(withPicks.HasAnySelection, Is.True);
            Assert.That(new HeroSelectionState { Particles = { P("833", "7") } }.HasAnySelection, Is.False,
                "particles alone are not a selection");
        }

        #endregion
    }
}
