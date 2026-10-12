using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Utilities;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class RankOverridePlanTests
    {
        private const int Archon = 4;
        private const int Divine = 7;
        private const int Immortal = 8;
        private const int Uncalibrated = 0;

        private static RankChangerRequest Request(
            bool replaceEveryRank = false,
            int currentTier = Archon,
            int currentStars = 1,
            int targetTier = Divine,
            int targetStars = 5,
            bool applyStars = true,
            bool applyProfile = true,
            bool applyScoreboard = true,
            int targetLeaderboard = 0,
            int profileMmr = 0,
            int rankConfidence = 100,
            int leaderboardRank = 6)
            => new(replaceEveryRank, currentTier, currentStars, targetTier, targetStars,
                   applyStars, applyProfile, applyScoreboard, targetLeaderboard,
                   profileMmr, rankConfidence, leaderboardRank);

        private const int DestsPerSurface = 8 + 5;

        [Test]
        public void Build_ReplaceEveryRank_RedrawsEveryTierAndEveryStarCount()
        {
            var entries = RankOverridePlan.Build(Request(replaceEveryRank: true));

            Assert.That(entries, Has.Count.EqualTo(DestsPerSurface + DestsPerSurface + 7));

            var profileDests = AllDestinations(RankOverridePlan.ProfileDestinations);
            var scoreboardDests = AllDestinations(RankOverridePlan.ScoreboardDestinations);
            Assert.That(entries.Where(e => profileDests.Contains(e.DestRelative)),
                Is.All.Matches<RankOverrideEntry>(e => e.SourceRelative == RankOverridePlan.Profile(Divine)));
            Assert.That(entries.Where(e => scoreboardDests.Contains(e.DestRelative)),
                Is.All.Matches<RankOverrideEntry>(e => e.SourceRelative == RankOverridePlan.Scoreboard(Divine)));
            Assert.That(entries.Where(e => e.DestRelative.StartsWith("pip")),
                Is.All.Matches<RankOverrideEntry>(e => e.SourceRelative == RankOverridePlan.Stars(5)));

            Assert.That(entries.Select(e => e.DestRelative).Distinct().Count(), Is.EqualTo(entries.Count));
        }

        [Test]
        public void Build_OnlyMyRank_TouchesOneFilePerSurface()
        {
            var entries = RankOverridePlan.Build(Request(currentTier: Archon, currentStars: 2, targetTier: Divine, targetStars: 5));

            Assert.That(entries, Has.Count.EqualTo(3));
            Assert.That(entries, Does.Contain(new RankOverrideEntry(RankOverridePlan.Profile(Divine), RankOverridePlan.Profile(Archon))));
            Assert.That(entries, Does.Contain(new RankOverrideEntry(RankOverridePlan.Scoreboard(Divine), RankOverridePlan.Scoreboard(Archon))));
            Assert.That(entries, Does.Contain(new RankOverrideEntry(RankOverridePlan.Stars(5), RankOverridePlan.Stars(2))));
        }

        [Test]
        public void Build_SurfaceSwitchedOff_ContributesNothing()
        {
            var entries = RankOverridePlan.Build(Request(applyProfile: false, applyScoreboard: false));

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].DestRelative, Does.StartWith("pip"));
        }

        [Test]
        public void Build_NothingSelected_IsEmptyRatherThanThrowing()
        {
            Assert.That(RankOverridePlan.Build(Request(applyStars: false, applyProfile: false, applyScoreboard: false)),
                Is.Empty);
        }

        [TestCase(Immortal, TestName = "Build_ImmortalTarget_WritesNoStarOverlay")]
        [TestCase(Uncalibrated, TestName = "Build_UncalibratedTarget_WritesNoStarOverlay")]
        public void Build_StarlessTarget_WritesNoStarOverlay(int targetTier)
        {
            var entries = RankOverridePlan.Build(Request(replaceEveryRank: true, targetTier: targetTier));

            Assert.That(entries.Any(e => e.DestRelative.StartsWith("pip")), Is.False);
            Assert.That(entries, Has.Count.EqualTo(DestsPerSurface + DestsPerSurface));
        }

        [Test]
        public void Build_StarlessCurrentRank_WritesNoStarOverlay()
        {
            var entries = RankOverridePlan.Build(Request(currentTier: Immortal, targetTier: Divine));

            Assert.That(entries.Any(e => e.DestRelative.StartsWith("pip")), Is.False);
            Assert.That(entries, Has.Count.EqualTo(5 + 5));
        }

        [Test]
        public void Build_TargetEqualsCurrent_IsAnIdentityMapNotAnError()
        {
            var entries = RankOverridePlan.Build(Request(currentTier: Divine, currentStars: 5, targetTier: Divine, targetStars: 5));

            Assert.That(entries, Has.Count.EqualTo(3));
            Assert.That(entries, Is.All.Matches<RankOverrideEntry>(e => e.SourceRelative == e.DestRelative));
        }

        [TestCase(-1)]
        [TestCase(9)]
        public void Build_TierOutsideWhatValveShips_Throws(int targetTier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RankOverridePlan.Build(Request(targetTier: targetTier)));
        }

        [TestCase(0)]
        [TestCase(8)]
        public void Build_StarCountOutsideWhatValveShips_Throws(int targetStars)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RankOverridePlan.Build(Request(targetStars: targetStars)));
        }

        [Test]
        public void Build_ImmortalDestination_CoversEveryFileTheClientCanDrawForIt()
        {
            var entries = RankOverridePlan.Build(Request(currentTier: Immortal, targetTier: Divine, applyScoreboard: false));

            Assert.That(entries.Select(e => e.DestRelative), Is.EquivalentTo(new[]
            {
                "rank8inactive_psd.vtex_c", "rank8_psd.vtex_c",
                "rank8a_psd.vtex_c", "rank8b_psd.vtex_c", "rank8c_psd.vtex_c"
            }));
        }

        [TestCase(0, "rank8_psd.vtex_c", "mini/rank8_psd.vtex_c")]
        [TestCase(1, "rank8a_psd.vtex_c", "rank8a_mini_psd.vtex_c")]
        [TestCase(2, "rank8b_psd.vtex_c", "rank8b_mini_psd.vtex_c")]
        [TestCase(3, "rank8c_psd.vtex_c", "rank8c_mini_psd.vtex_c")]
        public void Build_ImmortalTarget_DrawsTheBracketArtwork(int bracket, string profile, string mini)
        {
            var entries = RankOverridePlan.Build(Request(currentTier: Archon, targetTier: Immortal, targetLeaderboard: bracket));

            Assert.That(entries, Does.Contain(new RankOverrideEntry(profile, RankOverridePlan.Profile(Archon))));
            Assert.That(entries, Does.Contain(new RankOverrideEntry(mini, RankOverridePlan.Scoreboard(Archon))));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void Build_LeaderboardBracketOutsideWhatValveShips_Throws(int bracket)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RankOverridePlan.Build(Request(targetTier: Immortal, targetLeaderboard: bracket)));
        }

        #region Picker value validation (ProfileMmr, RankConfidence, LeaderboardRank)

        [TestCase(0)]
        [TestCase(RankOverridePlan.MaxMmr)]
        public void Build_MmrAtItsLimits_DoesNotThrow(int mmr)
        {
            Assert.DoesNotThrow(() => RankOverridePlan.Build(Request(profileMmr: mmr)));
        }

        [TestCase(-1)]
        [TestCase(RankOverridePlan.MaxMmr + 1)]
        public void Build_MmrOutsideRange_Throws(int mmr)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RankOverridePlan.Build(Request(profileMmr: mmr)));
        }

        [TestCase(0)]
        [TestCase(100)]
        public void Build_ConfidenceAtItsLimits_DoesNotThrow(int confidence)
        {
            Assert.DoesNotThrow(() => RankOverridePlan.Build(Request(rankConfidence: confidence)));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void Build_ConfidenceOutsideRange_Throws(int confidence)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RankOverridePlan.Build(Request(rankConfidence: confidence)));
        }

        [TestCase(1, 5000)]
        [TestCase(2, 100)]
        [TestCase(3, 10)]
        public void Build_LeaderboardRankAtBracketCeiling_DoesNotThrow(int bracket, int rank)
        {
            Assert.DoesNotThrow(() => RankOverridePlan.Build(
                Request(targetTier: Immortal, targetLeaderboard: bracket, leaderboardRank: rank)));
        }

        [TestCase(1, 5001)]
        [TestCase(2, 101)]
        [TestCase(3, 11)]
        [TestCase(3, 0)]
        public void Build_LeaderboardRankAboveBracketCeiling_Throws(int bracket, int rank)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RankOverridePlan.Build(
                Request(targetTier: Immortal, targetLeaderboard: bracket, leaderboardRank: rank)));
        }

        [Test]
        public void Build_LeaderboardRankIgnored_WhenBracketIsPlainImmortal()
        {
            Assert.DoesNotThrow(() => RankOverridePlan.Build(
                Request(targetTier: Immortal, targetLeaderboard: 0, leaderboardRank: 999_999)));
        }

        #endregion

        #region WipesPips

        [TestCase(Immortal, false, TestName = "WipesPips_ImmortalReplaceEveryRank_IsTrue")]
        [TestCase(Immortal, true, TestName = "WipesPips_ImmortalOnlyMyRank_IsTrue")]
        [TestCase(Uncalibrated, false, TestName = "WipesPips_UncalibratedReplaceEveryRank_IsTrue")]
        [TestCase(Uncalibrated, true, TestName = "WipesPips_UncalibratedOnlyMyRank_IsTrue")]
        public void WipesPips_TargetIsImmortalOrUncalibrated_IsTrueEvenInOnlyMyRankMode(int targetTier, bool onlyMyRank)
        {
            var request = Request(replaceEveryRank: !onlyMyRank, targetTier: targetTier);
            Assert.That(RankOverridePlan.WipesPips(request), Is.True);
        }

        [TestCase(Archon)]
        [TestCase(Divine)]
        public void WipesPips_TargetHasItsOwnStars_IsFalse(int targetTier)
        {
            Assert.That(RankOverridePlan.WipesPips(Request(targetTier: targetTier)), Is.False);
        }

        [Test]
        public void WipesPips_NullRequest_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RankOverridePlan.WipesPips(null!));
        }

        #endregion

        private static HashSet<string> AllDestinations(Func<int, IReadOnlyList<string>> of)
        {
            var names = new HashSet<string>();
            for (int tier = RankOverridePlan.MinTier; tier <= RankOverridePlan.MaxTier; tier++)
                names.UnionWith(of(tier));
            return names;
        }
    }
}
