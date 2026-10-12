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
using System.Linq;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroBadgePlanTests
    {
        [TestCase(HeroBadgeShow.Tier, -1, HeroBadgeLevel.Keep, 30, HeroBadgeProgress.Keep, 0)]
        [TestCase(HeroBadgeShow.Tier, 6, HeroBadgeLevel.Keep, 30, HeroBadgeProgress.Keep, 0)]
        [TestCase(HeroBadgeShow.Keep, 0, HeroBadgeLevel.Custom, 0, HeroBadgeProgress.Keep, 0)]
        [TestCase(HeroBadgeShow.Keep, 0, HeroBadgeLevel.Custom, 31, HeroBadgeProgress.Keep, 0)]
        [TestCase(HeroBadgeShow.Keep, 0, HeroBadgeLevel.Keep, 30, HeroBadgeProgress.Custom, -1)]
        [TestCase(HeroBadgeShow.Keep, 0, HeroBadgeLevel.Keep, 30, HeroBadgeProgress.Custom, 101)]
        [TestCase((HeroBadgeShow)9, 0, HeroBadgeLevel.Keep, 30, HeroBadgeProgress.Keep, 0)]
        public void Resolve_OutOfRange_Throws(HeroBadgeShow show, int tier, HeroBadgeLevel level, int customLevel,
            HeroBadgeProgress progress, int customProgress)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HeroBadgePlan.Resolve(new HeroBadgeRequest(show, tier, level, customLevel, progress, customProgress)));
        }

        [Test]
        public void Resolve_UnreadFieldsAreNotValidated()
        {
            Assert.DoesNotThrow(() => HeroBadgePlan.Resolve(
                new HeroBadgeRequest(HeroBadgeShow.Keep, Tier: 99, Level: HeroBadgeLevel.Hide, CustomLevel: -5, CustomProgress: 500)));
        }

        [Test]
        public void Resolve_NothingChanged_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => HeroBadgePlan.Resolve(new HeroBadgeRequest()));
        }

        [Test]
        public void Resolve_HiddenBadge_DropsTheNumber()
        {
            var fx = HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Hide, Level: HeroBadgeLevel.Custom, CustomLevel: 12));
            Assert.That(fx.HideBadge, Is.True);
            Assert.That(fx.Level, Is.EqualTo(HeroBadgeLevel.Hide));
            Assert.That(fx.Reveal, Is.False);
        }

        [Test]
        public void Resolve_MaxProgress_IsAFullBar()
        {
            var fx = HeroBadgePlan.Resolve(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max, CustomProgress: 12));
            Assert.That(fx.ProgressPercent, Is.EqualTo(100));
            Assert.That(fx.Unlock, Is.True);
            Assert.That(fx.Reveal, Is.False);
        }

        [Test]
        public void Copies_PutTheChosenTierOverEveryOtherTier_NeverOverItself()
        {
            for (int tier = HeroBadgePlan.MinTier; tier <= HeroBadgePlan.MaxTier; tier++)
            {
                var copies = HeroBadgePlan.Copies(tier);
                int slot = HeroBadgePlan.Slot(tier);

                Assert.That(copies.Select(c => c.Dest), Is.Unique, $"tier {tier}");
                Assert.That(copies.Where(c => c.Source == c.Dest), Is.Empty, $"tier {tier}");
                Assert.That(copies.All(c => c.Source.Contains($"rank_{tier}_") || c.Source.Contains($"_{slot}_psd") ||
                                            c.Source.Contains($"level_{slot}_") || c.Source.EndsWith($"mastery_rank{slot}.vmdl_c")),
                    Is.True, $"tier {tier}: every source must be the chosen tier's own file");
                Assert.That(copies, Has.Count.EqualTo(5 * 8 + 1), $"tier {tier}");
            }
        }

        [Test]
        public void Copies_Grandmaster_UsesSlot6_WhichIsWhatValveShips()
        {
            var copies = HeroBadgePlan.Copies(5);
            Assert.That(copies, Does.Contain(("models/particle/mastery_icons/mastery_rank6.vmdl_c", "models/particle/mastery_icons/mastery_rank1.vmdl_c")));
            Assert.That(copies.Select(c => c.Dest), Has.None.Contains("mastery_rank0"), "Valve ships no slot 0");
            Assert.That(copies.Select(c => c.Dest), Has.None.Contains("level_0_"), "Valve ships no slot 0");
        }

        [Test]
        public void EverythingWritten_IsOwned_AndOwnedPathsAvoidOtherFeatures()
        {
            var owned = HeroBadgePlan.OwnedPaths();
            for (int tier = 0; tier <= 5; tier++)
                Assert.That(HeroBadgePlan.Copies(tier).Select(c => c.Dest), Is.SubsetOf(owned));
            Assert.That(HeroBadgePlan.Blanked(), Is.SubsetOf(owned));

            Assert.That(owned, Has.None.Contains("rank_tier_icons"));
            Assert.That(owned, Has.None.EndsWith("ui_rank_badge.vcss_c"));
            Assert.That(owned, Has.None.EndsWith("dashboard_page_profile_stats_shared.vcss_c"));
            Assert.That(owned, Has.None.EndsWith("ui_econ_item.vcss_c"));
            Assert.That(owned, Has.None.EndsWith("hero_slot_item_picker_loadout.vcss_c"));
            Assert.That(owned, Has.None.EndsWith("hud_reborn.vcss_c"), "the unit portrait rule would frame creeps too");
            foreach (var rank in new[] { "amtm", "amtc", "amtr", "hb_m", "hb_c", "hb_r" })
                Assert.That(owned, Has.None.EndsWith($"/{rank}.vtex_c"));
        }

        [Test]
        public void LevelTable_TotalsMatchValvesTotalXpColumn()
        {
            Assert.That(HeroBadgePlan.LevelXp, Has.Count.EqualTo(HeroBadgePlan.MaxLevel));
            Assert.That(HeroBadgePlan.TotalXp(1), Is.EqualTo(50));
            Assert.That(HeroBadgePlan.TotalXp(5), Is.EqualTo(1850));
            Assert.That(HeroBadgePlan.TotalXp(11), Is.EqualTo(8750));
            Assert.That(HeroBadgePlan.TotalXp(17), Is.EqualTo(20450));
            Assert.That(HeroBadgePlan.TotalXp(24), Is.EqualTo(40050));
            Assert.That(HeroBadgePlan.TotalXp(25), Is.EqualTo(46850));
            Assert.That(HeroBadgePlan.TotalXp(29), Is.EqualTo(64050));
            Assert.That(HeroBadgePlan.TotalXp(30), Is.EqualTo(72050));
        }

        [TestCase(1, 0)] [TestCase(5, 0)]
        [TestCase(6, 1)] [TestCase(11, 1)]
        [TestCase(12, 2)] [TestCase(17, 2)]
        [TestCase(18, 3)] [TestCase(24, 3)]
        [TestCase(25, 4)] [TestCase(29, 4)]
        [TestCase(30, 5)]
        public void TierForLevel_FollowsValvesTable(int level, int tier)
        {
            Assert.That(HeroBadgePlan.TierForLevel(level), Is.EqualTo(tier));
        }

        [TestCase(0)]
        [TestCase(31)]
        public void TierForLevel_OutOfRange_Throws(int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroBadgePlan.TierForLevel(level));
        }

        [TestCase(0, 30)]
        [TestCase(5, 29)]
        [TestCase(2, 11)]
        public void Resolve_LevelOutsideThePickedTier_IsRejected(int tier, int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroBadgePlan.Resolve(
                new HeroBadgeRequest(HeroBadgeShow.Tier, tier, HeroBadgeLevel.Custom, level)));
        }

        [Test]
        public void Resolve_AnyLevelIsFine_WhenTheBadgeIsKept()
        {
            Assert.DoesNotThrow(() => HeroBadgePlan.Resolve(new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 30)));
        }

        [Test]
        public void BarTier_FollowsThePick_ElseTheLevelsTier_ElseNone()
        {
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 3, HeroBadgeLevel.Custom, 20)).BarTier, Is.EqualTo(3));
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 26)).BarTier, Is.EqualTo(4));
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max)).BarTier, Is.Null);
        }

        #region One tier only

        [TestCase(-1)]
        [TestCase(6)]
        public void Resolve_ScopeOutOfRange_Throws(int scope)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 5, OnlyTier: scope)));
        }

        [Test]
        public void Resolve_TierSwappedForItselfInItsOwnScope_ChangesNothing()
        {
            Assert.Throws<InvalidOperationException>(() =>
                HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 3, OnlyTier: 3)));
            Assert.DoesNotThrow(() => HeroBadgePlan.Resolve(
                new HeroBadgeRequest(HeroBadgeShow.Tier, 3, HeroBadgeLevel.Custom, 20, OnlyTier: 3)));
        }

        [Test]
        public void Resolve_KeptBadgeInAScope_TakesTheScopedTiersLevels()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroBadgePlan.Resolve(
                new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 30, OnlyTier: 3)));
            Assert.DoesNotThrow(() => HeroBadgePlan.Resolve(
                new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 24, OnlyTier: 3)));
        }

        [Test]
        public void Scope_NeverReveals_AndUnlocksOnlyForGrandmaster()
        {
            var platinum = HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 5, OnlyTier: 3));
            var grandmaster = HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 4, HeroBadgeLevel.Custom, 25, OnlyTier: 5));

            Assert.That(platinum.Reveal, Is.False);
            Assert.That(platinum.Unlock, Is.False, "only max-level heroes are greyed, and max level is Grandmaster");
            Assert.That(grandmaster.Unlock, Is.True);
            Assert.That(platinum.BarTier, Is.EqualTo(5), "the bar takes the pick's colours");
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max, OnlyTier: 1)).BarTier,
                Is.EqualTo(1), "kept badge: the scoped tier's colours");
        }

        [Test]
        public void ScopedCopies_WriteOnlyTheScopedSlot_AndNeverTheEmptyFrame()
        {
            var copies = HeroBadgePlan.Copies(5, scope: 3);

            Assert.That(copies, Has.Count.EqualTo(8), "3 badge sizes + 3 HUD images + particle + model");
            Assert.That(copies.Select(c => c.Dest), Has.All.Matches<string>(d =>
                d.Contains("rank_3_") || d.Contains("_4_psd") || d.Contains("level_4_") || d.EndsWith("mastery_rank4.vmdl_c")));
            Assert.That(copies.Select(c => c.Dest), Has.None.Contains("empty"));
            Assert.That(HeroBadgePlan.Copies(3, scope: 3), Is.Empty, "a tier over itself copies nothing");
        }

        [Test]
        public void ScopedBlank_BlanksOnlyTheScopedTiersArt()
        {
            var blanked = HeroBadgePlan.Blanked(scope: 0);
            Assert.That(blanked, Has.Count.EqualTo(6));
            Assert.That(blanked, Has.All.Matches<string>(p => p.Contains("rank_0_") || p.Contains("_1_psd")));
        }

        #endregion

        #region XP line

        [TestCase(1, 50, 150, 300)]
        [TestCase(24, 50, 3400, 6800)]
        [TestCase(25, 100, 4000, 4000)]
        [TestCase(30, 100, 8000, 8000)]
        [TestCase(30, 0, 0, 8000)]
        public void Xp_FollowsValvesTable(int level, int percent, int have, int need)
        {
            Assert.That(HeroBadgePlan.Xp(level, percent), Is.EqualTo((have, need)));
        }

        [Test]
        public void Xp_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroBadgePlan.Xp(0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeroBadgePlan.Xp(10, 101));
        }

        [Test]
        public void ShowsXp_OnlyWithAKnownLevel()
        {
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 3, Progress: HeroBadgeProgress.Max)).ShowsXp, Is.True);
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max)).ShowsXp, Is.False);
            Assert.That(HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Hide, Progress: HeroBadgeProgress.Max)).ShowsXp, Is.False);
        }

        [Test]
        public void XpColor_IsValvesTierNameColour_ElseLighterGrey()
        {
            Assert.That(HeroBadgePlan.XpColor(0), Is.EqualTo("#a86a35"));
            Assert.That(HeroBadgePlan.XpColor(2), Is.EqualTo("#ddc165"));
            Assert.That(HeroBadgePlan.XpColor(4), Is.EqualTo("#9f5abc"));
            Assert.That(HeroBadgePlan.XpColor(5), Is.EqualTo("#B0BCC2"));
            Assert.That(HeroBadgePlan.XpColor(null), Is.EqualTo("#B0BCC2"));
        }

        #endregion

        [Test]
        public void ProgressColors_NoTier_IsValvesPlusPageGold()
        {
            Assert.That(HeroBadgePlan.ProgressColors(null), Is.EqualTo(HeroBadgePlan.ProgressColors(2)));
            Assert.That(HeroBadgePlan.ProgressColors(5), Is.EqualTo(("#4b141d", "#fe8650")));
        }
    }
}
