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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class HeroBadgeParityTests
    {
        private static string BadgesTs()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return File.ReadAllText(Path.Combine(dir!.FullName, "Frontend", "src", "pages", "utilities", "badges.ts"));
        }

        private static int Const(string source, string name)
        {
            var m = Regex.Match(source, $@"export const {name}\s*=\s*(\d+)");
            Assert.That(m.Success, Is.True, $"{name} not found in badges.ts");
            return int.Parse(m.Groups[1].Value);
        }

        [Test]
        public void LevelRange_MatchesThePlan()
        {
            string ts = BadgesTs();
            Assert.That(Const(ts, "LEVEL_MIN"), Is.EqualTo(HeroBadgePlan.MinLevel));
            Assert.That(Const(ts, "LEVEL_MAX"), Is.EqualTo(HeroBadgePlan.MaxLevel));
        }

        [Test]
        public void Tiers_MatchThePlan()
        {
            var tiers = Regex.Matches(BadgesTs(), @"\{ tier: (\d+), key:").Select(m => int.Parse(m.Groups[1].Value)).ToList();
            Assert.That(tiers, Is.EqualTo(Enumerable.Range(HeroBadgePlan.MinTier, HeroBadgePlan.MaxTier - HeroBadgePlan.MinTier + 1)));
        }

        [Test]
        public void LevelXpTable_MatchesThePlan()
        {
            var m = Regex.Match(BadgesTs(), @"export const LEVEL_XP = \[([\d,\s]+)\]");
            Assert.That(m.Success, Is.True, "LEVEL_XP not found in badges.ts");
            var xp = m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
            Assert.That(xp, Is.EqualTo(HeroBadgePlan.LevelXp));
        }

        [Test]
        public void TierStartLevels_MatchThePlan()
        {
            var from = Regex.Matches(BadgesTs(), @"\{ tier: \d+, key: ""[^""]+"", name: ""[^""]+"", from: (\d+) \}")
                .Select(m => int.Parse(m.Groups[1].Value));
            Assert.That(from, Is.EqualTo(HeroBadgePlan.TierStartLevel));
        }

        [Test]
        public void PreviewBarColours_MatchTheTexture()
        {
            string ts = BadgesTs();
            foreach (int? tier in new int?[] { null, 0, 1, 2, 3, 4, 5 })
            {
                string key = tier is null ? @"\[-1\]" : tier.ToString()!;
                var m = Regex.Match(ts, $@"\n\s*{key}:\s*\[""(#[0-9A-Fa-f]{{6}})"",\s*""(#[0-9A-Fa-f]{{6}})""\]");
                Assert.That(m.Success, Is.True, $"BAR_COLORS entry for {tier?.ToString() ?? "no tier"} not found");
                Assert.That((m.Groups[1].Value, m.Groups[2].Value), Is.EqualTo(HeroBadgePlan.ProgressColors(tier)), $"tier {tier}");
            }
        }
    }
}
