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
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public static class RankOverridePlan
    {
        public const int MinTier = 0;
        public const int MaxTier = 8;

        public const int MinStars = 1;
        public const int MaxStars = 7;

        public const int MaxLeaderboard = 3;

        public const int MaxMmr = 13_000;

        public const int MaxConfidence = 100;

        private static readonly int[] MaxLeaderboardRank = { 0, 5000, 100, 10 };

        public static int MaxLeaderboardRankFor(int bracket)
        {
            RequireLeaderboard(bracket);
            return MaxLeaderboardRank[bracket];
        }

        public const string IconFolder = "panorama/images/rank_tier_icons";

        private const string Ext = ".vtex_c";

        private static readonly string[] ImmortalProfile =
            { "rank8_psd", "rank8a_psd", "rank8b_psd", "rank8c_psd" };

        private static readonly string[] ImmortalMini =
            { "mini/rank8_psd", "rank8a_mini_psd", "rank8b_mini_psd", "rank8c_mini_psd" };

        private static readonly string[] ImmortalProfileAll =
            { "rank8inactive_psd", "rank8_psd", "rank8a_psd", "rank8b_psd", "rank8c_psd" };

        private static readonly string[] ImmortalMiniAll =
            { "mini/rank8_psd", "rank8_mini_psd", "rank8a_mini_psd", "rank8b_mini_psd", "rank8c_mini_psd" };

        public static bool TierHasStars(int tier) => tier > MinTier && tier < MaxTier;

        public static bool WipesPips(RankChangerRequest r)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));
            return r.TargetTier == MaxTier || r.TargetTier == MinTier;
        }

        public static IReadOnlyList<RankOverrideEntry> Build(RankChangerRequest r)
        {
            if (r is null) throw new ArgumentNullException(nameof(r));

            RequireTier(r.TargetTier, nameof(r.TargetTier));
            if (!r.ReplaceEveryRank) RequireTier(r.CurrentTier, nameof(r.CurrentTier));
            if (r.TargetTier == MaxTier) RequireLeaderboard(r.TargetLeaderboard);

            RequireRange(r.ProfileMmr, 0, MaxMmr, nameof(r.ProfileMmr));
            RequireRange(r.RankConfidence, 0, MaxConfidence, nameof(r.RankConfidence));
            if (r.TargetTier == MaxTier && r.TargetLeaderboard > 0)
                RequireRange(r.LeaderboardRank, 1, MaxLeaderboardRank[r.TargetLeaderboard], nameof(r.LeaderboardRank));

            bool targetStarred = TierHasStars(r.TargetTier);
            bool currentStarred = r.ReplaceEveryRank || TierHasStars(r.CurrentTier);
            bool starSet = r.ApplyStars && targetStarred && currentStarred;
            if (starSet)
            {
                RequireStars(r.TargetStars, nameof(r.TargetStars));
                if (!r.ReplaceEveryRank) RequireStars(r.CurrentStars, nameof(r.CurrentStars));
            }

            string profileSource = Profile(r.TargetTier, r.TargetLeaderboard);
            string scoreboardSource = Scoreboard(r.TargetTier, r.TargetLeaderboard);
            var entries = new List<RankOverrideEntry>();

            void Redraw(int tier)
            {
                if (r.ApplyProfile)
                    foreach (var dest in ProfileDestinations(tier))
                        entries.Add(new(profileSource, dest));
                if (r.ApplyScoreboard)
                    foreach (var dest in ScoreboardDestinations(tier))
                        entries.Add(new(scoreboardSource, dest));
            }

            if (r.ReplaceEveryRank)
            {
                for (int tier = MinTier; tier <= MaxTier; tier++) Redraw(tier);

                if (starSet)
                    for (int stars = MinStars; stars <= MaxStars; stars++)
                        entries.Add(new(Stars(r.TargetStars), Stars(stars)));
            }
            else
            {
                Redraw(r.CurrentTier);
                if (starSet) entries.Add(new(Stars(r.TargetStars), Stars(r.CurrentStars)));
            }

            return entries;
        }

        public static string Profile(int tier, int leaderboard = 0) =>
            tier == MaxTier ? ImmortalProfile[leaderboard] + Ext : $"rank{tier}_psd{Ext}";

        public static string Scoreboard(int tier, int leaderboard = 0) =>
            tier == MaxTier ? ImmortalMini[leaderboard] + Ext : $"mini/rank{tier}_psd{Ext}";

        public static string Stars(int stars) => $"pip{stars}_psd{Ext}";

        public static IReadOnlyList<string> ProfileDestinations(int tier) =>
            tier == MaxTier ? Suffixed(ImmortalProfileAll) : new[] { $"rank{tier}_psd{Ext}" };

        public static IReadOnlyList<string> ScoreboardDestinations(int tier) =>
            tier == MaxTier ? Suffixed(ImmortalMiniAll) : new[] { $"mini/rank{tier}_psd{Ext}" };

        private static string[] Suffixed(string[] names)
        {
            var result = new string[names.Length];
            for (int i = 0; i < names.Length; i++) result[i] = names[i] + Ext;
            return result;
        }

        private static void RequireTier(int tier, string name)
        {
            if (tier < MinTier || tier > MaxTier)
                throw new ArgumentOutOfRangeException(name, tier, $"Rank tier must be {MinTier}-{MaxTier}.");
        }

        private static void RequireStars(int stars, string name)
        {
            if (stars < MinStars || stars > MaxStars)
                throw new ArgumentOutOfRangeException(name, stars, $"Star count must be {MinStars}-{MaxStars}.");
        }

        private static void RequireLeaderboard(int leaderboard)
        {
            if (leaderboard < 0 || leaderboard > MaxLeaderboard)
                throw new ArgumentOutOfRangeException(nameof(RankChangerRequest.TargetLeaderboard),
                    leaderboard, $"Leaderboard bracket must be 0-{MaxLeaderboard}.");
        }

        private static void RequireRange(int value, int min, int max, string name)
        {
            if (value < min || value > max)
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be {min}-{max}.");
        }
    }
}
