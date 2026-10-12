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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class RankTierNamesTests
    {
        private const string Russian = "\"lang\"\n{\n\t\"Tokens\"\n\t{\n" +
            "\t\t\"DOTARankTierFormat\"\t\t\"{s:rank_tier_name}{s:rank_pips_name}\"\n" +
            "\t\t\"DOTARankTierName0\"\t\t\"На калибровке\"\n" +
            "\t\t\"DOTARankTierName7\"\t\t\"Божество\"\n" +
            "\t\t\"DOTARankTierName8\"\t\t\"Титан\"\n" +
            "\t\t\"DOTARankPipsName0\"\t\t\"\"\n" +
            "\t\t\"DOTARankPipsName5\"\t\t\" V\"\n" +
            "\t\t\"DOTARankTierNameLeaderboard\" \"Ранг: {d:leaderboard_rank}\"\n" +
            "\t}\n}\n";

        private const string SimplifiedChinese =
            "\"DOTARankTierName0\"\t\"未校准\"\n\"DOTARankTierNameLeaderboard\" \"天梯第{d:leaderboard_rank}名\"\n";

        private static RankChangerRequest Immortal(int bracket, int placing) =>
            new(false, 4, 1, 8, 5, TargetLeaderboard: bracket, LeaderboardRank: placing);

        #region Picking top bar (feedback screenshot: "Immortal", "Rank 2,691", "Not Calibrated")

        [TestCase(1, 2691, "Rank 2,691")]
        [TestCase(1, 4471, "Rank 4,471")]
        [TestCase(1, 5000, "Rank 5,000")]
        [TestCase(2, 100, "Rank 100")]
        [TestCase(3, 1, "Rank 1")]
        [TestCase(3, 10, "Rank 10")]
        public void LeaderboardImmortal_ReadsRankN_NotImmortal(int bracket, int placing, string expected) =>
            Assert.That(RankTierNames.English.Label(Immortal(bracket, placing)), Is.EqualTo(expected));

        [Test]
        public void PlainImmortal_ReadsImmortal_EvenWithAPlacingLeftOver() =>
            Assert.That(RankTierNames.English.Label(Immortal(0, 2691)), Is.EqualTo("Immortal"));

        [TestCase(true)]
        [TestCase(false)]
        public void Uncalibrated_ReadsNotCalibrated_NeverEmpty(bool everyRank) =>
            Assert.That(RankTierNames.English.Label(new RankChangerRequest(everyRank, 4, 1, 0, 5)), Is.EqualTo("Not Calibrated"));

        [Test]
        public void LeaderboardLabel_FollowsTheGameLanguage()
        {
            Assert.That(RankTierNames.Parse(Russian).Label(Immortal(1, 2691)), Is.EqualTo("Ранг: 2,691"));
            Assert.That(RankTierNames.Parse(SimplifiedChinese).Label(Immortal(3, 7)), Is.EqualTo("天梯第7名"));
            Assert.That(RankTierNames.Parse(SimplifiedChinese).Label(new RankChangerRequest(false, 4, 1, 0, 5)), Is.EqualTo("未校准"));
        }

        [Test]
        public void LeaderboardToken_MissingOrWithoutItsNumber_KeepsEnglish()
        {
            Assert.That(RankTierNames.Parse("\"DOTARankTierName8\" \"Титан\"").Label(Immortal(2, 50)), Is.EqualTo("Rank 50"));
            Assert.That(RankTierNames.Parse("\"DOTARankTierNameLeaderboard\" \"Ранг\"").Label(Immortal(2, 50)), Is.EqualTo("Rank 50"),
                "a token that lost {d:leaderboard_rank} would draw a label with no number");
        }

        [TestCase(1, 0)]
        [TestCase(1, 5001)]
        [TestCase(2, 101)]
        [TestCase(3, 11)]
        [TestCase(3, -1)]
        public void LeaderboardPlacing_OutsideItsBracket_Throws(int bracket, int placing) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => RankTierNames.English.Label(Immortal(bracket, placing)));

        [Test]
        public void Label_NullRequest_Throws() =>
            Assert.Throws<ArgumentNullException>(() => RankTierNames.English.Label((RankChangerRequest)null!));

        [Test]
        public void EveryAcceptedRequest_YieldsAPrintableLabel()
        {
            int checkedCount = 0;
            foreach (var r in AllAcceptedRequests())
            foreach (var names in new[] { RankTierNames.English, RankTierNames.Parse(Russian), RankTierNames.Parse(SimplifiedChinese) })
            {
                string label = names.Label(r);
                Assert.That(label, Is.Not.Empty, $"{r}");
                Assert.That(label, Does.Not.Contain("{"), $"{r} left a placeholder: {label}");
                if (r.TargetTier == 8 && r.TargetLeaderboard > 0)
                    Assert.That(label, Does.Contain(VtexGenerator.FormatNumber(r.LeaderboardRank)), $"{r}");
                checkedCount++;
            }
            Assert.That(checkedCount, Is.GreaterThan(100));
        }

        internal static System.Collections.Generic.IEnumerable<RankChangerRequest> AllAcceptedRequests()
        {
            foreach (bool every in new[] { true, false })
            foreach (bool stars in new[] { true, false })
            for (int tier = RankOverridePlan.MinTier; tier <= RankOverridePlan.MaxTier; tier++)
            for (int star = 1; star <= 5; star++)
            for (int bracket = 0; bracket <= (tier == RankOverridePlan.MaxTier ? RankOverridePlan.MaxLeaderboard : 0); bracket++)
            foreach (int placing in bracket == 0 ? new[] { 6 } : new[] { 1, RankOverridePlan.MaxLeaderboardRankFor(bracket) })
            {
                var r = new RankChangerRequest(every, 4, 1, tier, star, ApplyStars: stars,
                    TargetLeaderboard: bracket, LeaderboardRank: placing);
                try { RankOverridePlan.Build(r); }
                catch (ArgumentOutOfRangeException) { continue; }
                yield return r;
            }
        }

        #endregion

        [TestCase(1, 1, "Herald I")]
        [TestCase(4, 3, "Archon III")]
        [TestCase(7, 5, "Divine V")]
        [TestCase(6, 7, "Ancient VII")]
        [TestCase(8, 0, "Immortal")]
        [TestCase(0, 0, "Not Calibrated")]
        public void English_MatchesValvesTokens(int tier, int stars, string expected) =>
            Assert.That(RankTierNames.English.Label(tier, stars), Is.EqualTo(expected));

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(-1)]
        public void StarsOutsideThePipRange_DrawNoPips(int stars) =>
            Assert.That(RankTierNames.English.Label(5, stars), Is.EqualTo("Legend"));

        [Test]
        public void Label_TierOutOfRange_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => RankTierNames.English.Label(9, 1));

        [Test]
        public void Parse_GameLanguage_UsesItsTokens()
        {
            var names = RankTierNames.Parse(Russian);

            Assert.That(names.Label(7, 5), Is.EqualTo("Божество V"));
            Assert.That(names.Label(8, 0), Is.EqualTo("Титан"));
        }

        [Test]
        public void Parse_TokenMissing_FallsBackToEnglishForThatTokenOnly() =>
            Assert.That(RankTierNames.Parse(Russian).Label(4, 3), Is.EqualTo("Archon III"));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a localization file {{{")]
        public void Parse_NothingUsable_IsEnglish(string? text) =>
            Assert.That(RankTierNames.Parse(text).Label(7, 5), Is.EqualTo("Divine V"));

        [Test]
        public void Parse_FormatWithoutTierPlaceholder_KeepsEnglishFormat()
        {
            var names = RankTierNames.Parse("\"DOTARankTierFormat\" \"{s:rank_pips_name}\"\n\"DOTARankTierName7\" \"Божество\"");
            Assert.That(names.Label(7, 0), Is.EqualTo("Божество"));
        }

        [Test]
        public void Parse_FormatOrderFollowsTheLanguage()
        {
            var names = RankTierNames.Parse("\"DOTARankTierFormat\" \"{s:rank_pips_name} {s:rank_tier_name}\"");
            Assert.That(names.Label(7, 5), Is.EqualTo("V Divine"));
        }

        [Test]
        public void Label_ForARequest_TargetStarsOnlyWhereTheTierHasThem()
        {
            Assert.That(RankTierNames.English.Label(new RankChangerRequest(false, 4, 1, 7, 5)), Is.EqualTo("Divine V"));
            Assert.That(RankTierNames.English.Label(new RankChangerRequest(false, 4, 1, 8, 5)), Is.EqualTo("Immortal"));
            Assert.That(RankTierNames.English.Label(new RankChangerRequest(false, 4, 1, 7, 5, ApplyStars: false)), Is.EqualTo("Divine"));
        }
    }
}
