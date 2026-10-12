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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class RankPickerParityTests
    {
        private static string RanksTs()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");

            var path = Path.Combine(dir!.FullName, "Frontend", "src", "pages", "utilities", "ranks.ts");
            Assert.That(File.Exists(path), Is.True, "ranks.ts is missing");
            return File.ReadAllText(path);
        }

        private static Match Find(string source, string pattern, string what)
        {
            var m = Regex.Match(source, pattern);
            Assert.That(m.Success, Is.True, $"{what} not found in ranks.ts — update this test with its new shape");
            return m;
        }

        [Test]
        public void MmrCeiling_MatchesThePage()
        {
            var m = Find(RanksTs(), @"export const MMR_MAX\s*=\s*(\d+)", "MMR_MAX");
            Assert.That(int.Parse(m.Groups[1].Value), Is.EqualTo(RankOverridePlan.MaxMmr));
        }

        [Test]
        public void LeaderboardRankCeilings_MatchThePage()
        {
            var m = Find(RanksTs(),
                @"LB_RANK_MAX[^=]*=\s*\{\s*1:\s*(\d+),\s*2:\s*(\d+),\s*3:\s*(\d+)\s*\}", "LB_RANK_MAX");

            for (int bracket = 1; bracket <= RankOverridePlan.MaxLeaderboard; bracket++)
                Assert.That(int.Parse(m.Groups[bracket].Value), Is.EqualTo(RankOverridePlan.MaxLeaderboardRankFor(bracket)),
                    $"bracket {bracket}");
        }

        [Test]
        public void StarredTiers_MatchThePage()
        {
            var m = Find(RanksTs(), @"tierHasStars\s*=\s*\(tier: number\)\s*=>\s*tier > (\d+) && tier < (\d+)", "tierHasStars");
            Assert.That(int.Parse(m.Groups[1].Value), Is.EqualTo(RankOverridePlan.MinTier));
            Assert.That(int.Parse(m.Groups[2].Value), Is.EqualTo(RankOverridePlan.MaxTier));
        }
    }
}
