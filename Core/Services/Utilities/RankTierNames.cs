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
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public sealed class RankTierNames
    {
        private const string TierToken = "{s:rank_tier_name}";
        private const string PipsToken = "{s:rank_pips_name}";
        private const string PlacingToken = "{d:leaderboard_rank}";

        private readonly string _format;
        private readonly string[] _tiers;
        private readonly string[] _pips;
        private readonly string _leaderboard;

        private RankTierNames(string format, string[] tiers, string[] pips, string leaderboard)
        {
            _format = format;
            _tiers = tiers;
            _pips = pips;
            _leaderboard = leaderboard;
        }

        public static RankTierNames English { get; } = new(
            TierToken + PipsToken,
            new[] { "Not Calibrated", "Herald", "Guardian", "Crusader", "Archon", "Legend", "Ancient", "Divine", "Immortal" },
            new[] { "", " I", " II", " III", " IV", " V", " VI", " VII" },
            "Rank " + PlacingToken);

        public static RankTierNames Parse(string? localization)
        {
            if (string.IsNullOrEmpty(localization)) return English;

            var tiers = (string[])English._tiers.Clone();
            var pips = (string[])English._pips.Clone();
            for (int i = 0; i < tiers.Length; i++) tiers[i] = Token(localization, $"DOTARankTierName{i}") ?? tiers[i];
            for (int i = 0; i < pips.Length; i++) pips[i] = Token(localization, $"DOTARankPipsName{i}") ?? pips[i];

            string? format = Token(localization, "DOTARankTierFormat");
            if (format is null || !format.Contains(TierToken, StringComparison.Ordinal)) format = English._format;

            string? leaderboard = Token(localization, "DOTARankTierNameLeaderboard");
            if (leaderboard is null || !leaderboard.Contains(PlacingToken, StringComparison.Ordinal)) leaderboard = English._leaderboard;

            return new RankTierNames(format, tiers, pips, leaderboard);
        }

        public string Label(int tier, int stars)
        {
            if (tier < RankOverridePlan.MinTier || tier > RankOverridePlan.MaxTier)
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Rank tier must be 0-8.");

            string pips = stars >= RankOverridePlan.MinStars && stars <= RankOverridePlan.MaxStars ? _pips[stars] : _pips[0];
            return _format.Replace(TierToken, _tiers[tier]).Replace(PipsToken, pips).Trim();
        }

        public string LeaderboardLabel(int placing)
        {
            if (placing < 1 || placing > RankOverridePlan.MaxLeaderboardRankFor(1))
                throw new ArgumentOutOfRangeException(nameof(placing), placing, "Leaderboard placing must be 1-5000.");
            return _leaderboard.Replace(PlacingToken, VtexGenerator.FormatNumber(placing)).Trim();
        }

        public string Label(Models.RankChangerRequest r)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));

            if (r.TargetTier == RankOverridePlan.MaxTier && r.TargetLeaderboard > 0)
            {
                int max = RankOverridePlan.MaxLeaderboardRankFor(r.TargetLeaderboard);
                if (r.LeaderboardRank < 1 || r.LeaderboardRank > max)
                    throw new ArgumentOutOfRangeException(nameof(r.LeaderboardRank), r.LeaderboardRank,
                        $"Leaderboard placing must be 1-{max} for bracket {r.TargetLeaderboard}.");
                return LeaderboardLabel(r.LeaderboardRank);
            }

            return Label(r.TargetTier, r.ApplyStars && RankOverridePlan.TierHasStars(r.TargetTier) ? r.TargetStars : 0);
        }

        private static string? Token(string text, string key)
        {
            var m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"[ \\t]+\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Replace("\\\"", "\"") : null;
        }
    }
}
