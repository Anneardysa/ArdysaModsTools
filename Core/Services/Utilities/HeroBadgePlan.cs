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
using System.Linq;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public sealed record HeroBadgeEffects(int? Tier, bool HideBadge, HeroBadgeLevel Level, int CustomLevel,
        int? ProgressPercent, int? Scope = null)
    {
        public bool Reveal => Tier is not null && Scope is null;

        public bool Unlock => (Tier is not null || ProgressPercent is not null)
                              && (Scope is null || Scope == HeroBadgePlan.MaxTier);

        public int? BarTier => Tier ?? Scope ?? (Level == HeroBadgeLevel.Custom ? HeroBadgePlan.TierForLevel(CustomLevel) : null);

        public bool ShowsXp => ProgressPercent is not null && Level == HeroBadgeLevel.Custom;
    }

    public static class HeroBadgePlan
    {
        public const int MinTier = 0;
        public const int MaxTier = 5;
        public const int MinLevel = 1;
        public const int MaxLevel = 30;
        public const int MinProgress = 0;
        public const int MaxProgress = 100;

        public const string MarkerEntry = "panorama/images/amtbm.vtex_c";

        public const string LargeLevelTexture = "panorama/images/amtbl.vtex_c";
        public const string SmallLevelTexture = "panorama/images/amtbs.vtex_c";
        public const string ProgressTexture = "panorama/images/amtbp.vtex_c";
        public const string XpTexture = "panorama/images/amtbx.vtex_c";

        public static string TierClass(int tier) => tier switch
        {
            0 => "BronzeTier",
            1 => "SilverTier",
            2 => "GoldTier",
            3 => "PlatinumTier",
            4 => "MasterTier",
            5 => "GrandmasterTier",
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };

        public static (int Have, int Need) Xp(int level, int percent)
        {
            if (level < MinLevel || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            if (percent < MinProgress || percent > MaxProgress) throw new ArgumentOutOfRangeException(nameof(percent));
            int need = LevelXp[Math.Min(level, MaxLevel - 1)];
            return ((int)Math.Round(need * percent / 100.0, MidpointRounding.AwayFromZero), need);
        }

        private const string BadgeDir = "panorama/images/hero_badges";

        public static int Slot(int tier) => tier + 1;

        public static IReadOnlyList<int> LevelXp { get; } = new[]
        {
            50, 300, 400, 500, 600,
            900, 1000, 1100, 1200, 1300, 1400,
            1700, 1800, 1900, 2000, 2100, 2200,
            2500, 2600, 2700, 2800, 2900, 3000, 3100,
            6800, 4000, 4200, 4400, 4600,
            8000,
        };

        public static IReadOnlyList<int> TierStartLevel { get; } = new[] { 1, 6, 12, 18, 25, 30 };

        public static int TierForLevel(int level)
        {
            if (level < MinLevel || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            int tier = MinTier;
            while (tier < MaxTier && TierStartLevel[tier + 1] <= level) tier++;
            return tier;
        }

        public static int TotalXp(int level)
        {
            if (level < MinLevel || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            return LevelXp.Take(level).Sum();
        }

        public static HeroBadgeEffects Resolve(HeroBadgeRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (!Enum.IsDefined(request.Show)) throw new ArgumentOutOfRangeException(nameof(request.Show));
            if (!Enum.IsDefined(request.Level)) throw new ArgumentOutOfRangeException(nameof(request.Level));
            if (!Enum.IsDefined(request.Progress)) throw new ArgumentOutOfRangeException(nameof(request.Progress));

            if (request.Show == HeroBadgeShow.Tier && (request.Tier < MinTier || request.Tier > MaxTier))
                throw new ArgumentOutOfRangeException(nameof(request.Tier), request.Tier, $"Tier must be {MinTier}-{MaxTier}.");

            if (request.OnlyTier is int scope && (scope < MinTier || scope > MaxTier))
                throw new ArgumentOutOfRangeException(nameof(request.OnlyTier), scope, $"Tier must be {MinTier}-{MaxTier}.");

            bool hide = request.Show == HeroBadgeShow.Hide;
            var level = hide ? HeroBadgeLevel.Hide : request.Level;

            if (level == HeroBadgeLevel.Custom && (request.CustomLevel < MinLevel || request.CustomLevel > MaxLevel))
                throw new ArgumentOutOfRangeException(nameof(request.CustomLevel), request.CustomLevel, $"Level must be {MinLevel}-{MaxLevel}.");

            int? shownTier = request.Show == HeroBadgeShow.Tier ? request.Tier
                : request.Show == HeroBadgeShow.Keep ? request.OnlyTier
                : null;
            if (shownTier is int shown && level == HeroBadgeLevel.Custom && TierForLevel(request.CustomLevel) != shown)
                throw new ArgumentOutOfRangeException(nameof(request.CustomLevel), request.CustomLevel,
                    $"Level {request.CustomLevel} is not a tier {shown} level.");

            if (request.Progress == HeroBadgeProgress.Custom &&
                (request.CustomProgress < MinProgress || request.CustomProgress > MaxProgress))
                throw new ArgumentOutOfRangeException(nameof(request.CustomProgress), request.CustomProgress, $"Progress must be {MinProgress}-{MaxProgress}.");

            int? progress = request.Progress switch
            {
                HeroBadgeProgress.Max => MaxProgress,
                HeroBadgeProgress.Custom => request.CustomProgress,
                _ => null
            };

            var fx = new HeroBadgeEffects(
                request.Show == HeroBadgeShow.Tier ? request.Tier : null,
                hide,
                level,
                level == HeroBadgeLevel.Custom ? request.CustomLevel : 0,
                progress,
                request.OnlyTier);

            bool artChanges = fx.HideBadge || (fx.Tier is int t && t != fx.Scope);
            if (!artChanges && fx.Level == HeroBadgeLevel.Keep && fx.ProgressPercent is null)
                throw new InvalidOperationException("The request leaves everything as the game draws it.");

            return fx;
        }

        private static IEnumerable<string> BadgeArt(int tier) => new[]
        {
            $"{BadgeDir}/hero_badge_rank_{tier}_png.vtex_c",
            $"{BadgeDir}/hero_badge_rank_{tier}_small_png.vtex_c",
            $"{BadgeDir}/hero_badge_rank_{tier}_tiny_png.vtex_c",
        };

        private static string EmptyBadge => $"{BadgeDir}/hero_badge_rank_empty_psd.vtex_c";

        private static IEnumerable<string> HudArt(int slot) => new[]
        {
            $"panorama/images/hud/portrait_hero_badge_frame_tier_{slot}_psd.vtex_c",
            $"panorama/images/hud/reborn/top_bar_hero_radiant_badge_{slot}_psd.vtex_c",
            $"panorama/images/hud/reborn/top_bar_hero_dire_badge_{slot}_psd.vtex_c",
        };

        private static IEnumerable<string> SceneArt(int slot) => new[]
        {
            $"particles/ui/plus/ui_hero_level_{slot}_icon_ambient.vpcf_c",
            $"models/particle/mastery_icons/mastery_rank{slot}.vmdl_c",
        };

        private static IEnumerable<int> Tiers => Enumerable.Range(MinTier, MaxTier - MinTier + 1);

        public static IReadOnlyList<(string Source, string Dest)> Copies(int tier, int? scope = null)
        {
            if (tier < MinTier || tier > MaxTier) throw new ArgumentOutOfRangeException(nameof(tier));
            if (scope is < MinTier or > MaxTier) throw new ArgumentOutOfRangeException(nameof(scope));

            var copies = new List<(string, string)>();
            foreach (int other in Tiers.Where(t => t != tier && (scope is null || t == scope)))
            {
                copies.AddRange(BadgeArt(tier).Zip(BadgeArt(other)));
                copies.AddRange(HudArt(Slot(tier)).Zip(HudArt(Slot(other))));
                copies.AddRange(SceneArt(Slot(tier)).Zip(SceneArt(Slot(other))));
            }

            if (scope is null)
                copies.Add(($"{BadgeDir}/hero_badge_rank_{tier}_png.vtex_c", EmptyBadge));
            return copies;
        }

        public static IReadOnlyList<string> Blanked(int? scope = null)
        {
            if (scope is int only)
                return BadgeArt(only).Concat(HudArt(Slot(only))).ToList();

            return Tiers.SelectMany(BadgeArt)
                .Append(EmptyBadge)
                .Concat(Tiers.SelectMany(t => HudArt(Slot(t))))
                .ToList();
        }

        public static IReadOnlyList<string> OwnedPaths() =>
            Blanked()
                .Concat(Tiers.SelectMany(t => SceneArt(Slot(t))))
                .Concat(HeroBadgeCssPatcher.Stylesheets)
                .Append(LargeLevelTexture)
                .Append(SmallLevelTexture)
                .Append(ProgressTexture)
                .Append(XpTexture)
                .Append(MarkerEntry)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static string XpColor(int? tier) => tier switch
        {
            0 => "#a86a35",
            2 => "#ddc165",
            4 => "#9f5abc",
            _ => "#B0BCC2",
        };

        public static (string Dark, string Light) ProgressColors(int? tier) => tier switch
        {
            0 => ("#97472A", "#CF9F68"),
            1 => ("#59748A", "#C8E9FF"),
            3 => ("#4867C5", "#85D4EF"),
            4 => ("#48165E", "#918BD0"),
            5 => ("#4b141d", "#fe8650"),
            _ => ("#ae9249", "#e0c59a"),
        };
    }
}
