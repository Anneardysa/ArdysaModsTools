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
using System.Text;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroBadgeCssPatcherTests
    {
        private const string HeroBadgeSheet = "panorama/styles/hero_badge.vcss_c";
        private const string HeroPageSheet = "panorama/styles/dashboard_page_hero_new_v2.vcss_c";
        private const string DotaStyles = "panorama/styles/dotastyles.vcss_c";

        private static string FixtureDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return Path.Combine(dir!.FullName, "Tests", "Data", "HeroBadge");
        }

        private static byte[] Valve(string stylesheet) =>
            File.ReadAllBytes(Path.Combine(FixtureDir(), Path.GetFileName(stylesheet)));

        private static IEnumerable<HeroBadgeEffects> AllEffects()
        {
            var scopes = new int?[] { null, 0, 1, 2, 3, 4, 5 };
            foreach (var scope in scopes)
            foreach (var show in Enum.GetValues<HeroBadgeShow>())
            foreach (int tier in show == HeroBadgeShow.Tier ? Enumerable.Range(0, 6) : new[] { 5 })
            foreach (var level in Enum.GetValues<HeroBadgeLevel>())
            foreach (var progress in Enum.GetValues<HeroBadgeProgress>())
            {
                int? shown = show == HeroBadgeShow.Tier ? tier : show == HeroBadgeShow.Keep ? scope : null;
                int lvl = shown is int s ? HeroBadgePlan.TierStartLevel[s] : 30;
                HeroBadgeEffects fx;
                try { fx = HeroBadgePlan.Resolve(new HeroBadgeRequest(show, tier, level, lvl, progress, 37, scope)); }
                catch (InvalidOperationException) { continue; }
                yield return fx;
            }
        }

        private static string Css(byte[] bytes) =>
            Regex.Replace(Encoding.UTF8.GetString(bytes), " +}", "}").Replace(": ", ":");

        private static string Canon(string css) => css.Replace(": ", ":");

        private static string Patched(string sheet, HeroBadgeEffects fx) =>
            Css(HeroBadgeCssPatcher.Patch(sheet, Valve(sheet), fx)!);

        private static HeroBadgeEffects Fx(HeroBadgeRequest request) => HeroBadgePlan.Resolve(request);

        private static int Count(byte[] bytes, char c) => bytes.Count(b => b == (byte)c);

        #region Targets

        [Test]
        public void EveryTarget_OccursExactlyOnceInValvesStylesheet()
        {
            foreach (var (sheet, name, target) in HeroBadgeCssPatcher.AllTargets())
            {
                byte[] file = Valve(sheet);
                byte[] needle = Encoding.UTF8.GetBytes(target);
                int at = file.AsSpan().IndexOf(needle);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), $"{name} not found in {sheet}");
                Assert.That(file.AsSpan(at + 1).IndexOf(needle), Is.EqualTo(-1), $"{name} is ambiguous in {sheet}");
            }
        }

        [Test]
        public void TargetsInOneStylesheet_ShareAtMostTheAnchorBrace()
        {
            foreach (var group in HeroBadgeCssPatcher.AllTargets().GroupBy(t => t.Stylesheet))
            {
                byte[] file = Valve(group.Key);
                var spans = group
                    .Select(t => (t.Name, Start: file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(t.Target)), Length: Encoding.UTF8.GetByteCount(t.Target)))
                    .OrderBy(s => s.Start)
                    .ToList();
                for (int i = 1; i < spans.Count; i++)
                    Assert.That(spans[i].Start, Is.GreaterThanOrEqualTo(spans[i - 1].Start + spans[i - 1].Length - 1),
                        $"{spans[i - 1].Name} and {spans[i].Name} overlap in {group.Key}");
            }
        }

        #endregion

        #region Engine

        [Test]
        public void EveryCombination_PatchesEverySheetItTouches_KeepingLengthAndBalancedBraces()
        {
            int patchedSheets = 0;
            foreach (var fx in AllEffects())
            foreach (var sheet in HeroBadgeCssPatcher.StylesheetsFor(fx))
            {
                byte[] original = Valve(sheet);
                byte[]? patched = HeroBadgeCssPatcher.Patch(sheet, original, fx);

                Assert.That(patched, Is.Not.Null, $"{sheet} was listed for {fx} but nothing changed");
                Assert.That(patched!.Length, Is.EqualTo(original.Length), $"{sheet} length moved for {fx}");
                Assert.That(Count(patched, '{') - Count(original, '{'), Is.EqualTo(Count(patched, '}') - Count(original, '}')),
                    $"{sheet} braces unbalanced for {fx}");
                Assert.That(patched.AsSpan(0, 64).SequenceEqual(original.AsSpan(0, 64)), Is.True, $"{sheet} header moved for {fx}");
                patchedSheets++;
            }
            Assert.That(patchedSheets, Is.GreaterThan(500), "the sweep should have exercised every scope");
        }

        [Test]
        public void SheetsNotListedForAnEffect_AreLeftAlone()
        {
            foreach (var fx in AllEffects())
            foreach (var sheet in HeroBadgeCssPatcher.Stylesheets.Except(HeroBadgeCssPatcher.StylesheetsFor(fx)))
                Assert.That(HeroBadgeCssPatcher.Patch(sheet, Valve(sheet), fx), Is.Null, $"{sheet} changed for {fx}");
        }

        [Test]
        public void Growth_IsPaidOnlyWithTheOptionalSpaceAfterAColon()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 3, HeroBadgeLevel.Custom, 20, HeroBadgeProgress.Custom, 40));
            string before = Canon(Encoding.UTF8.GetString(Valve(HeroPageSheet)));
            string after = Patched(HeroPageSheet, fx);

            foreach (Match rule in Regex.Matches(before[before.IndexOf("#HeroLevelProgress{", StringComparison.Ordinal)..],
                         @"[^{}]+\{[^{}]*\}"))
            {
                if (rule.Value.Contains('\0')) break;
                Assert.That(after, Does.Contain(rule.Value), $"Valve's rule was altered: {rule.Value}");
            }
        }

        private static byte[] Synthetic(int fillerRules)
        {
            const string xp = "}.LevelProgressXPValuesContainer{width: 100%;flow-children: right;}.LevelProgressXPValuesContainer .CurrentLevelXPLabel{font-weight: semi-bold;}.LevelProgressXPValuesContainer .NextLevelXPLabel{margin-left: 4px;}";
            const string bar = "}#HeroLevelProgress{width: 100%;height: 6px;border: 0px;border-radius: 0px;background-color: #ddf2ff08;margin: 8px 0px;}";
            var text = new StringBuilder("\u0001").Append(xp).Append(bar[1..]);
            for (int i = 0; i < fillerRules; i++) text.Append($".f{i}{{a: 1;b: 2;c: 3;d: 4;}}");
            return Encoding.UTF8.GetBytes(text.Append('\0').Append("TAIL").ToString());
        }

        [Test]
        public void Growth_WithoutRoom_RefusesInsteadOfCorrupting()
        {
            var fx = Fx(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max, OnlyTier: 3));
            var ex = Assert.Throws<InvalidOperationException>(() => HeroBadgeCssPatcher.Patch(HeroPageSheet, Synthetic(2), fx));
            Assert.That(ex!.Message, Does.Contain("spaces to give"));
        }

        [Test]
        public void Growth_WithRoom_KeepsTheTailPastTheTextUntouched()
        {
            var fx = Fx(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max, OnlyTier: 3));
            byte[] original = Synthetic(80);
            byte[] patched = HeroBadgeCssPatcher.Patch(HeroPageSheet, original, fx)!;

            Assert.That(patched.Length, Is.EqualTo(original.Length));
            Assert.That(Encoding.UTF8.GetString(patched), Does.EndWith("\0TAIL"), "bytes after the NUL must not move");
            Assert.That(Css(patched), Does.Contain(".PlatinumTier #HeroLevelProgress .ProgressBarLeft{opacity:0;}"));
        }

        [Test]
        public void Patch_RuleMissing_Throws()
        {
            var fx = Fx(new HeroBadgeRequest(Level: HeroBadgeLevel.Hide));
            byte[] unrelated = Encoding.UTF8.GetBytes("}.SomethingElse{color: red;}\0");
            Assert.Throws<InvalidOperationException>(() => HeroBadgeCssPatcher.Patch(HeroBadgeSheet, unrelated, fx));
        }

        [Test]
        public void Patch_RuleTwice_Throws()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 0));
            string rule = "}#HeroBadge.NoTier{visibility: collapse;}";
            byte[] doubled = Encoding.UTF8.GetBytes(rule + rule + "\0");
            Assert.Throws<InvalidOperationException>(() =>
                HeroBadgeCssPatcher.Patch("panorama/styles/tooltips/tooltip_hero_card.vcss_c", doubled, fx));
        }

        [Test]
        public void Patch_UnknownSheetOrEmptyInput_Throws()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 0));
            Assert.Throws<ArgumentException>(() => HeroBadgeCssPatcher.Patch("panorama/styles/ui_rank_badge.vcss_c", new byte[] { 1 }, fx));
            Assert.Throws<ArgumentNullException>(() => HeroBadgeCssPatcher.Patch(HeroBadgeSheet, Array.Empty<byte>(), fx));
        }

        #endregion

        #region Every hero

        [Test]
        public void ProgressWithoutALevel_PaintsTheBar_AndHidesXpItCannotKnow()
        {
            var fx = Fx(new HeroBadgeRequest(Progress: HeroBadgeProgress.Custom, CustomProgress: 40));

            Assert.That(HeroBadgeCssPatcher.StylesheetsFor(fx), Is.EquivalentTo(new[]
            {
                HeroPageSheet,
                "panorama/styles/ui_dota_plus_hero_page.vcss_c",
                "panorama/styles/ui_dota_plus_hero_page_v2.vcss_c",
            }));

            string css = Patched(HeroPageSheet, fx);
            Assert.That(css, Does.Contain("#HeroLevelProgress{background-size:100% 100%;background-image:url(\"s2r://panorama/images/amtbp.vtex\");}"));
            Assert.That(css, Does.Contain("#HeroLevelProgress .ProgressBarLeft{opacity:0;}"));
            Assert.That(css, Does.Contain(".LevelProgressXPValuesContainer{visibility:collapse;}"));
            Assert.That(css, Does.Not.Contain("amtbx"));
            Assert.That(css, Does.Contain(Canon("#HeroLevelProgress{width: 100%;height: 6px;border: 0px;border-radius: 0px;background-color: #ddf2ff08;margin: 8px 0px;}")));
        }

        [Test]
        public void ProgressWithAKnownLevel_DrawsTheXpLine_OverInvisibleLabels()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 3, HeroBadgeLevel.Custom, 20, HeroBadgeProgress.Custom, 40));
            string css = Patched(HeroPageSheet, fx);

            Assert.That(css, Does.Contain(".LevelProgressXPValuesContainer{background-size:contain;background-repeat:no-repeat;background-image:url(\"s2r://panorama/images/amtbx.vtex\");}"));
            Assert.That(css, Does.Contain(".LevelProgressXPValuesContainer Label{opacity:0;}"));
            Assert.That(css, Does.Not.Contain(".LevelProgressXPValuesContainer{visibility:collapse;}"));
            Assert.That(css, Does.Contain(".LevelProgressXPValuesContainer{width:100%;flow-children:right;}"));
        }

        [Test]
        public void Reveal_NeverTouchesTheSharedTierProgressBars()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 0, Progress: HeroBadgeProgress.Max));
            string after = Patched(DotaStyles, fx);

            foreach (var tier in new[] { "Bronze", "Silver", "Gold", "Platinum", "Master", "Grandmaster" })
                Assert.That(after, Does.Contain(Canon(
                    $".{tier}Tier .ProgressBarLeft{{background-color: gradient( linear, 0% 0%, 100% 0%, from( {tier}TierColorDark ), to( {tier}TierColorLight ) );}}")),
                    $"{tier} bar rule was touched");
        }

        [Test]
        public void CustomLevel_HidesValvesTextAndItsShadows_AndPaintsOurNumber()
        {
            var fx = Fx(new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 7));
            string css = Patched(HeroBadgeSheet, fx);

            Assert.That(css, Does.Contain("DOTAHeroBadge.LevelStyle > Label{width:100%;height:100%;margin-top:2px;color:#00000000;text-shadow:0px 0px 0px 0.0 #00000000;"));
            Assert.That(css, Does.Contain("url(\"s2r://panorama/images/amtbl.vtex\")"));
            Assert.That(css, Does.Not.Contain("> Label{text-shadow"));
            Assert.That(css, Does.Contain("DOTAHeroBadge.LevelStyle.NoTier > Label{visibility:collapse;}"));
        }

        [Test]
        public void HiddenBadge_CollapsesThe3DScene_AndRevealsNothing()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Hide, Level: HeroBadgeLevel.Custom));
            Assert.That(fx.Level, Is.EqualTo(HeroBadgeLevel.Hide), "a hidden badge never carries a number");

            string css = Patched(HeroBadgeSheet, fx);
            Assert.That(css, Does.Contain("DOTAHeroBadge.ModelStyle > DOTAScenePanel{visibility:collapse;}"));
            Assert.That(css, Does.Contain("DOTAHeroBadge.LevelStyle.NoTier{background-image:url("), "NoTier must stay unrevealed");
            Assert.That(HeroBadgeCssPatcher.StylesheetsFor(fx), Has.None.EqualTo("panorama/styles/hud/dota_hud_top_bar.vcss_c"));
        }

        [Test]
        public void Reveal_TopBar_MovesTheImageIntoTheBaseRule_AndKeepsTheRulesBetween()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 2));
            string css = Patched("panorama/styles/hud/dota_hud_top_bar.vcss_c", fx);

            Assert.That(css, Does.Contain("DOTATopBarPlayer #HeroBadgeOverlay{width:100%;height:100%;background-size:69px 47px;background-position:0px -4px;background-repeat:no-repeat;background-image:url(\"s2r://panorama/images/hud/reborn/top_bar_hero_radiant_badge_1_psd.vtex\");}"));
            Assert.That(css, Does.Contain("DOTATopBarPlayer.Coach{width:80px;}"), "rules inside the window are carried over");
            Assert.That(css, Does.Contain("DOTATopBarPlayer.BronzeTier #HeroBadgeOverlay{visibility:visible;}"));
        }

        #endregion

        #region One tier only

        [Test]
        public void Scope_NeverRevealsNoTier_OrTouchesSheetsOnlyARevealNeeds()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 5, OnlyTier: 3));
            var sheets = HeroBadgeCssPatcher.StylesheetsFor(fx);

            Assert.That(fx.Reveal, Is.False);
            Assert.That(sheets, Has.None.Contains("tooltip_hero_card"));
            Assert.That(sheets, Has.None.Contains("dota_hud_top_bar"));
            Assert.That(sheets, Has.None.Contains("post_game"));
        }

        [Test]
        public void ScopedLevel_KeepsValvesRulesForOtherTiers_AndOverridesOnlyTheScopedTier()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Tier, 5, HeroBadgeLevel.Custom, 30, OnlyTier: 1));

            string badge = Patched(HeroBadgeSheet, fx);
            Assert.That(badge, Does.Contain(Canon("DOTAHeroBadge.LevelStyle > Label{vertical-align: middle;")), "Valve's base label rule must stay");
            Assert.That(badge, Does.Contain("DOTAHeroBadge.LevelStyle.SilverTier > Label{}"), "the scoped tier's shadow ties with the override");
            Assert.That(badge, Does.Contain(Canon("DOTAHeroBadge.LevelStyle.BronzeTier > Label{text-shadow: 0px 0px 6px 6.0 #321307;}")));

            string overrides = Patched(DotaStyles, fx);
            Assert.That(overrides, Does.Contain("DOTAHeroBadge.LevelStyle.SilverTier > Label{width:100%;height:100%;color:#00000000;"));
            Assert.That(overrides, Does.Contain("DOTAHeroBadge.ModelStyle.SilverTier > Label{width:100%;height:100%;z-index:9;"));
            Assert.That(overrides, Does.Contain(".SilverTier #HeroBadgeLevel{width:16px;height:14px;"));
            Assert.That(overrides, Does.Contain("DOTAUIHeroPicker .SilverTier #HeroBadgeLevel{width:11px;height:13px;"));

            Assert.That(HeroBadgeCssPatcher.Patch("panorama/styles/hero_grid_new.vcss_c", Valve("hero_grid_new.vcss_c"), fx), Is.Null,
                "Valve's grid label rule stays as it is; the scoped override lives in dotastyles");
        }

        [Test]
        public void ScopedHide_CollapsesOnlyTheScopedTiers3DScene()
        {
            var fx = Fx(new HeroBadgeRequest(HeroBadgeShow.Hide, OnlyTier: 4));
            string overrides = Patched(DotaStyles, fx);

            Assert.That(overrides, Does.Contain("DOTAHeroBadge.ModelStyle.MasterTier > DOTAScenePanel{visibility:collapse;}"));
            Assert.That(overrides, Does.Contain("DOTAHeroBadge.LevelStyle.MasterTier > Label,DOTAHeroBadge.ModelStyle.MasterTier > Label,.MasterTier #HeroBadgeLevel{visibility:collapse;}"));
            Assert.That(HeroBadgeCssPatcher.StylesheetsFor(fx), Has.None.EqualTo(HeroBadgeSheet),
                "the base scene rule stays; only the scoped override hides it");
        }

        [Test]
        public void ScopedProgress_IsPrefixedWithTheTierClass()
        {
            var fx = Fx(new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 20, Progress: HeroBadgeProgress.Max, OnlyTier: 3));
            string css = Patched(HeroPageSheet, fx);

            Assert.That(css, Does.Contain(".PlatinumTier #HeroLevelProgress{background-size:100% 100%;"));
            Assert.That(css, Does.Contain(".PlatinumTier #HeroLevelProgress .ProgressBarLeft{opacity:0;}"));
            Assert.That(css, Does.Contain(".PlatinumTier .LevelProgressXPValuesContainer Label{opacity:0;}"));
            Assert.That(fx.Unlock, Is.False, "the Plus greying only exists at max level, which is not Platinum");
        }

        #endregion
    }
}
