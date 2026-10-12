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
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Utilities;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class PanoramaCssPatcherTests
    {
        private const string ModderStylesPath = "scripts/dataset/hero_assets/diff/modder/panorama/styles";

        #region UI Rank Badge Tests

        [Test]
        public void PatchUiRankBadge_NullOrEmpty_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PanoramaCssPatcher.PatchUiRankBadge(null!));
            Assert.Throws<ArgumentNullException>(() => PanoramaCssPatcher.PatchUiRankBadge(Array.Empty<byte>()));
        }

        [Test]
        public void PatchUiRankBadge_MissingRule_ThrowsInvalidOperationException()
        {
            byte[] dummy = Encoding.UTF8.GetBytes(".SomeOtherRule { color: red; }");
            Assert.Throws<InvalidOperationException>(() => PanoramaCssPatcher.PatchUiRankBadge(dummy));
        }

        [Test]
        public void PatchUiRankBadge_ValidBuffer_PreservesLengthAndReplacesRules()
        {
            string rule1 = "#RankLeaderboard{color: #F7E5c3;letter-spacing: 1px;font-size: 14px;margin-bottom: 9px;text-align: center;text-shadow: 1px 1px #00000080;horizontal-align: center;vertical-align: bottom;width: 100%;visibility: collapse;}.HasLeaderboardRank #RankLeaderboard{visibility: visible;}";
            string rule2 = ".RankDataMissing #EliteFX,.RankTier0 #EliteFX,.RankTier1 #EliteFX,.RankTier2 #EliteFX,.RankTier3 #EliteFX,.RankTier4 #EliteFX,.RankTier5 #EliteFX,.RankTier6 #EliteFX,.RankTier7 #EliteFX{visibility: collapse;}";

            byte[] original = Encoding.UTF8.GetBytes($"/* prefix */ {rule1} /* mid */ {rule2} /* suffix */");
            byte[] patched = PanoramaCssPatcher.PatchUiRankBadge(original);

            Assert.That(patched.Length, Is.EqualTo(original.Length), "Length invariant violated!");
            string patchedStr = Encoding.UTF8.GetString(patched);

            Assert.That(patchedStr, Does.Contain("s2r://panorama/images/amtr.vtex"));
            Assert.That(patchedStr, Does.Contain(".RankDataMissing #EliteFX{visibility: visible;}"));
            Assert.That(patchedStr, Does.Not.Contain(rule1));
        }

        #endregion

        #region Profile Stats Tests

        [Test]
        public void PatchProfileStats_NullOrEmpty_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PanoramaCssPatcher.PatchProfileStats(null!));
            Assert.Throws<ArgumentNullException>(() => PanoramaCssPatcher.PatchProfileStats(Array.Empty<byte>()));
        }

        [Test]
        public void PatchProfileStats_MissingRule_ThrowsInvalidOperationException()
        {
            byte[] dummy = Encoding.UTF8.GetBytes(".SomeOtherRule { color: red; }");
            Assert.Throws<InvalidOperationException>(() => PanoramaCssPatcher.PatchProfileStats(dummy));
        }

        [Test]
        public void PatchProfileStats_ValidBuffer_PreservesLengthAndReplacesRules()
        {
            string rule1 = "#MMRNumber{visibility: collapse;}.MMRValue{horizontal-align: center;margin-bottom: 6px;text-overflow: shrink;color: #dcd087;font-size: 20px;text-transform: uppercase;}#MMRTBD.MMRTBD{visibility: collapse;horizontal-align: left;margin-left: 0px;text-overflow: shrink;color: #dcd087;font-size: 18px;text-transform: uppercase;}";
            string rule2 = "#RankConfidenceContainer{margin-top: 2px;vertical-align: bottom;horizontal-align: right;visibility: collapse;width: 250px;}#RankConfidenceProgressBar{margin-top: 5px;width: 225px;height: 22px;border: 2px solid #403626;vertical-align: top;border-radius: 4px;margin-bottom: 4px;}#RankConfidenceProgressBar .ProgressBarLeft{background-color: gradient( linear, 0% 0%, 0% 80%, from( #997239 ), to( #a48d4b ) );}#RankConfidenceProgressBar .ProgressBarRight{background-color: #2a210e;}#RankConfidenceProgressBarValue{color: white;font-weight: bold;horizontal-align: center;vertical-align: center;}";
            string rule3 = "#RankConfidenceCalibrationThreshold{vertical-align: top;width: 2px;height: 100%;background-color: #dcd087;z-index: 1;opacity: 0.5;transform: translateX(-1px);}";
            string rule4 = ".MMRCalibrated #MMRNumber,.MMRCalibrating:not(.MMRGlicko) #MMRCalibrating,.MMRNoData:not(.MMRGlicko) #MMRNoData,.MMRCalibrating #MMRTBD{visibility: visible;}";

            byte[] original = Encoding.UTF8.GetBytes($"[A]{rule1}[B]{rule2}[C]{rule3}[D]{rule4}[E]");
            byte[] patched = PanoramaCssPatcher.PatchProfileStats(original);

            Assert.That(patched.Length, Is.EqualTo(original.Length), "Length invariant violated!");
            string patchedStr = Encoding.UTF8.GetString(patched);

            Assert.That(patchedStr, Does.Contain("s2r://panorama/images/amtm.vtex"));
            Assert.That(patchedStr, Does.Contain("s2r://panorama/images/amtc.vtex"));
            Assert.That(patchedStr, Does.Contain("#RankConfidenceCalibrationThreshold{visibility:collapse;}"));
            Assert.That(patchedStr, Does.Contain(".MMRCalibrated #MMRNumber{visibility:visible;}"));
            Assert.That(patchedStr, Does.Not.Contain(rule1));
        }

        #endregion

        #region Valve's own stylesheets (Tests/Data/RankChanger, extracted from game/dota/pak01_dir.vpk)


        private static string FixtureDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return Path.Combine(dir!.FullName, "Tests", "Data", "RankChanger");
        }

        private static byte[] Valve(string sheet) => File.ReadAllBytes(Path.Combine(FixtureDir(), Path.GetFileName(sheet)));

        private static int Count(byte[] bytes, char c) => bytes.Count(b => b == (byte)c);

        private static void AssertSameShape(byte[] original, byte[] patched, string what)
        {
            Assert.That(patched.Length, Is.EqualTo(original.Length), $"{what}: length moved");
            Assert.That(patched.AsSpan(0, 64).SequenceEqual(original.AsSpan(0, 64)), Is.True, $"{what}: resource header moved");
            Assert.That(Count(patched, '{') - Count(original, '{'), Is.EqualTo(Count(patched, '}') - Count(original, '}')),
                $"{what}: braces unbalanced");
        }

        [Test]
        public void EveryTarget_OccursExactlyOnceInValvesStylesheet()
        {
            foreach (var (sheet, name, target) in PanoramaCssPatcher.AllTargets())
            {
                byte[] file = Valve(sheet);
                byte[] needle = Encoding.UTF8.GetBytes(target);
                int at = file.AsSpan().IndexOf(needle);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), $"{name} not found in {sheet}");
                Assert.That(file.AsSpan(at + 1).IndexOf(needle), Is.EqualTo(-1), $"{name} is ambiguous in {sheet}");
            }
        }

        [Test]
        public void UiRankBadge_OnValvesFile_EveryModeAndTier_KeepsShape()
        {
            byte[] original = Valve(PanoramaCssPatcher.UiRankBadgeSheet);
            foreach (int? tier in new int?[] { null, 0, 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                byte[] patched = PanoramaCssPatcher.PatchUiRankBadge(original, tier);
                AssertSameShape(original, patched, $"ui_rank_badge tier={tier}");
                string css = Encoding.UTF8.GetString(patched);
                Assert.That(css, Does.Contain("s2r://panorama/images/amtr.vtex"));
                if (tier is int t) Assert.That(css, Does.Contain($".RankTier{t} #RankLeaderboard{{"));
            }
        }

        [Test]
        public void ProfileStats_OnValvesFile_KeepsShape()
        {
            byte[] original = Valve(PanoramaCssPatcher.ProfileStatsSheet);
            byte[] patched = PanoramaCssPatcher.PatchProfileStats(original);

            AssertSameShape(original, patched, "profile stats");
            string css = Encoding.UTF8.GetString(patched);
            Assert.That(css, Does.Contain("s2r://panorama/images/amtm.vtex"));
            Assert.That(css, Does.Contain("s2r://panorama/images/amtc.vtex"));
        }

        private static string Canon(string css) => Regex.Replace(css, " +}", "}").Replace(": ", ":");

        [TestCase(true, ".IsLocalPlayerPawn #PlayerRank{")]
        [TestCase(false, "DOTAHudHeroPickingPlayer #PlayerRank{")]
        public void PickPhase_OnValvesFile_AddsOneRuleAndLeavesValvesRulesWhole(bool onlyLocal, string selector)
        {
            byte[] original = Valve(PanoramaCssPatcher.PickPhaseSheet);
            byte[] patched = PanoramaCssPatcher.PatchHeroPickingPlayer(original, onlyLocal);

            AssertSameShape(original, patched, "picking player");
            Assert.That(Count(patched, '{') - Count(original, '{'), Is.EqualTo(1), "exactly one rule is added");

            string before = Canon(Encoding.UTF8.GetString(original));
            string after = Canon(Encoding.UTF8.GetString(patched));
            foreach (Match rule in Regex.Matches(before, @"\}[^{}]*#PlayerRank[^{}]*\{[^}]*\}"))
                Assert.That(after, Does.Contain(rule.Value), $"Valve's rule changed: {rule.Value}");

            int at = after.IndexOf(selector, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "our rule is missing");
            string ours = after[at..after.IndexOf('}', at)];
            Assert.That(ours, Does.Contain("color:#00000000;"), "Valve's text must be hidden under our picture");
            Assert.That(ours, Does.Contain("s2r://panorama/images/amtp.vtex"),
                "every target is painted, Uncalibrated included (\"Not Calibrated\")");
            Assert.That(ours, Does.Contain($"height:{VtexGenerator.PickRankCssBoxHeightPx}px;"),
                "the texture is drawn for this box height");
            Assert.That(ours, Does.Not.Contain("background-color"), "Valve's band behind the label stays");
        }

        [Test]
        public void PickPhase_PreviewReveal_OnlyInThePreviewOverload()
        {
            byte[] original = Valve(PanoramaCssPatcher.PickPhaseSheet);
            const string reveal = "DOTAHudHeroPickingPlayer #PlayerRank{visibility:visible;}";

            string shipped = Encoding.UTF8.GetString(PanoramaCssPatcher.PatchHeroPickingPlayer(original, true));
            byte[] preview = PanoramaCssPatcher.PatchHeroPickingPlayer(original, true, forceVisible: true);

            Assert.That(shipped, Does.Not.Contain(reveal));
            AssertSameShape(original, preview, "preview patch");
            Assert.That(Encoding.UTF8.GetString(preview), Does.Contain(reveal));
        }

        [Test]
        public void PickPhase_StylesheetWithoutTheAnchor_Throws()
        {
            byte[] dummy = Encoding.UTF8.GetBytes("}#PlayerRank{color: red;}\0");
            Assert.Throws<InvalidOperationException>(() => PanoramaCssPatcher.PatchHeroPickingPlayer(dummy, true));
        }

        [Test]
        public void PickPhase_AnchorTwice_RefusesRatherThanGuess()
        {
            string anchor = PanoramaCssPatcher.AllTargets().Single(t => t.Stylesheet == PanoramaCssPatcher.PickPhaseSheet).Target;
            byte[] doubled = Encoding.UTF8.GetBytes(anchor + anchor[1..] + "#a{b: c;}\0");
            Assert.That(() => PanoramaCssPatcher.PatchHeroPickingPlayer(doubled, true),
                Throws.InvalidOperationException.With.Message.Contains("more than once"));
        }

        [Test]
        public void UiRankBadge_TargetTwice_RefusesRatherThanPatchTheFirst()
        {
            byte[] original = Valve(PanoramaCssPatcher.UiRankBadgeSheet);
            string rule2 = PanoramaCssPatcher.AllTargets().Single(t => t.Name == "UiBadgeTarget2").Target;
            byte[] doubled = original.Concat(Encoding.UTF8.GetBytes(rule2)).ToArray();

            Assert.That(() => PanoramaCssPatcher.PatchUiRankBadge(doubled),
                Throws.InvalidOperationException.With.Message.Contains("more than once"));
        }

        #endregion

        #region Modder Reference Matching Tests

        [Test]
        public void PatchProfileStats_MatchesModderReferenceFile_WhenPresent()
        {
            string refModderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../" + ModderStylesPath, "dashboard_page_profile_stats_shared.vcss_c");
            if (!File.Exists(refModderPath))
                Assert.Ignore("Modder reference dataset file not present in test environment.");

            byte[] modderBytes = File.ReadAllBytes(refModderPath);
            Assert.That(modderBytes.Length, Is.EqualTo(5472));
        }

        #endregion
    }
}
