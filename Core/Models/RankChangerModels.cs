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
namespace ArdysaModsTools.Core.Models
{
    public sealed record RankChangerRequest(
        bool ReplaceEveryRank,
        int CurrentTier,
        int CurrentStars,
        int TargetTier,
        int TargetStars,
        bool ApplyStars = true,
        bool ApplyProfile = true,
        bool ApplyScoreboard = true,
        int TargetLeaderboard = 0,
        int ProfileMmr = 0,
        int RankConfidence = 100,
        int LeaderboardRank = 6);

    public sealed record RankChangerState(bool Applied, RankChangerRequest? Request);

    public sealed record RankOverrideEntry(string SourceRelative, string DestRelative);

    public enum RankChangerStage { Start, Processing, Compiling, Finish }

    public sealed record RankChangerProgress(RankChangerStage Stage, int Percent);
}
