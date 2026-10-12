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
using System.Text;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public static class HeroBadgeCssPatcher
    {
        private sealed record Edit(string Name, string Target, Func<HeroBadgeEffects, string?> Replace);

        private const string Styles = "panorama/styles/";

        private static string S2r(string vpkPath) => "s2r://" + vpkPath[..^2];

        private static string Emptied(string target) => target[..(target.IndexOf('{') + 1)] + "}";

        private static string Under(HeroBadgeEffects fx, string selector) =>
            fx.Scope is int s ? $".{HeroBadgePlan.TierClass(s)} {selector}" : selector;

        private static Edit Reveal(string name, string target) => new(name, target, fx => fx.Reveal ? Emptied(target) : null);

        private static Edit Unlock(string name, string target) => new(name, target, fx => fx.Unlock ? Emptied(target) : null);

        private const string NoText = "color:#00000000;text-shadow:0px 0px 0px 0.0 #00000000;";

        private static string Paint(string size, string texture) =>
            $"background-size:{size};background-repeat:no-repeat;background-position:center;background-image:url(\"{S2r(texture)}\");";

        private static Edit Label(string name, string target, string paint)
        {
            string selector = target[1..target.IndexOf('{')];
            return new(name, target, fx => fx.Scope is not null ? null : fx.Level switch
            {
                HeroBadgeLevel.Hide => $"}}{selector}{{visibility:collapse;}}",
                HeroBadgeLevel.Custom => $"}}{selector}{{{paint}}}",
                _ => null
            });
        }

        private static Edit LabelShadow(int tier, string shadow)
        {
            string target = $"}}DOTAHeroBadge.LevelStyle.{HeroBadgePlan.TierClass(tier)} > Label{{text-shadow: {shadow};}}";
            return new($"{HeroBadgePlan.TierClass(tier)}LabelShadow", target,
                fx => fx.Level == HeroBadgeLevel.Custom && (fx.Scope is null || fx.Scope == tier) ? Emptied(target) : null);
        }

        private static Edit NoTierLabel(string name, string target) =>
            new(name, target, fx => fx.Reveal && fx.Level == HeroBadgeLevel.Custom ? Emptied(target) : null);

        private const string Collapse = "{visibility: collapse;}";

        private static readonly string LevelStylePaint = NoText + Paint("contain", HeroBadgePlan.LargeLevelTexture);

        private static readonly string ModelStylePaint = NoText + Paint("22%", HeroBadgePlan.LargeLevelTexture);

        private static readonly string GridPaint =
            "width:16px;height:14px;background-color:#00000000;" + NoText + Paint("16px 14px", HeroBadgePlan.SmallLevelTexture);

        private static readonly string PickerPaint =
            "width:11px;height:13px;background-color:#00000000;" + NoText + Paint("11px 10px", HeroBadgePlan.SmallLevelTexture);

        private static Edit ScopedOverrides()
        {
            const string target = "}.ProgressBarLeft{background-color: #ffffff33;}";
            return new("ScopedBadgeOverrides", target, fx =>
            {
                if (fx.Scope is not int scope) return null;
                string tier = "." + HeroBadgePlan.TierClass(scope);
                string level = $"DOTAHeroBadge.LevelStyle{tier} > Label";
                string model = $"DOTAHeroBadge.ModelStyle{tier} > Label";
                string grid = $"{tier} #HeroBadgeLevel";
                string picker = $"DOTAUIHeroPicker {tier} #HeroBadgeLevel";

                var rules = new StringBuilder();
                if (fx.Level == HeroBadgeLevel.Hide)
                    rules.Append($"{level},{model},{grid}{{visibility:collapse;}}");
                if (fx.Level == HeroBadgeLevel.Custom)
                    rules.Append($"{level}{{width:100%;height:100%;{LevelStylePaint}}}")
                         .Append($"{model}{{width:100%;height:100%;z-index:9;{ModelStylePaint}}}")
                         .Append($"{grid}{{{GridPaint}}}")
                         .Append($"{picker}{{{PickerPaint}}}");
                if (fx.HideBadge)
                    rules.Append($"DOTAHeroBadge.ModelStyle{tier} > DOTAScenePanel{{visibility:collapse;}}");

                return rules.Length == 0 ? null : target + rules;
            });
        }


        private const string LevelLabelTarget =
            "}DOTAHeroBadge.LevelStyle > Label{vertical-align: middle;horizontal-align: center;font-size: 29px;color: white;font-weight: bold;text-shadow: 0px 0px 6px 6.0 black;text-align: center;margin-top: 2px;margin-right: 0px;letter-spacing: 1px;font-family: titleFont;}";

        private const string ModelLabelTarget =
            "}DOTAHeroBadge.ModelStyle > Label{vertical-align: middle;horizontal-align: center;font-size: 29px;color: white;font-weight: bold;text-shadow: 0px 2px 8px 4 rgba(0, 0, 0, 0.6);text-align: center;margin-top: 8px;margin-right: 0px;letter-spacing: 1px;font-family: titleFont;}";

        private const string NoTierBadgeTarget =
            "}DOTAHeroBadge.LevelStyle.NoTier{background-image: url(\"s2r://panorama/images/hero_badges/hero_badge_rank_empty_psd.vtex\");}";

        private const string SceneTarget = "}DOTAHeroBadge.ModelStyle > DOTAScenePanel{width: 100%;height: 100%;margin-top: 8px;}";

        private const string XpLabelsTarget =
            "}.LevelProgressXPValuesContainer{width: 100%;flow-children: right;}.LevelProgressXPValuesContainer .CurrentLevelXPLabel{font-weight: semi-bold;}.LevelProgressXPValuesContainer .NextLevelXPLabel{margin-left: 4px;}";

        private const string ProgressBarTarget =
            "}#HeroLevelProgress{width: 100%;height: 6px;border: 0px;border-radius: 0px;background-color: #ddf2ff08;margin: 8px 0px;}";

        private const string GridLevelTarget =
            "}#HeroBadgeLevel{font-size: 12px;color: white;font-weight: bold;text-align: center;text-shadow: 0px 0px 4px 2.0 black;margin-bottom: -5px;horizontal-align: center;max-width: 16px;text-overflow: shrink;background-color: gradient( linear, 0% 100%, 0% 0%, from( #000 ), to( #00000009 ) );background-color: gradient( radial, 50% 100%, 0% 0%, 50% 100%, from( #000000ee ), to( #00000009 ) );font-family: titleFont;}";

        private const string PickerLevelTarget =
            "}#HeroBadgeLevel{font-size: 14px;color: white;font-weight: bold;text-align: center;text-shadow: 0px 0px 4px 2.0 black;margin-left: -8px;margin-bottom: -7px;width: 11px;text-overflow: shrink;background-color: gradient( linear, 0% 100%, 0% 0%, from( #000 ), to( #00000005 ) );}";

        private const string TopBarTarget =
            "}DOTATopBarPlayer #HeroBadgeOverlay{width: 100%;height: 100%;background-size: 69px 47px;background-position: 0px -4px;background-repeat: no-repeat;visibility: collapse;}" +
            "DOTATopBarPlayer.RadiantCoach .SlantedContainer{horizontal-align: right;margin-right: 8px;}" +
            "DOTATopBarPlayer.DireCoach .SlantedContainer{horizontal-align: left;margin-left: 8px;}" +
            "DOTATopBarPlayer.Coach .SlantedContainer{width: 55px;height: 30px;}" +
            "DOTATopBarPlayer.Coach{width: 80px;}" +
            "DOTATopBarPlayer.BronzeTier #HeroBadgeOverlay{visibility: visible;background-image: url(\"s2r://panorama/images/hud/reborn/top_bar_hero_radiant_badge_1_psd.vtex\");}";

        private static Edit PlusIcon(string suffix)
        {
            string cls = $".PlusHeroBadgeIcon{suffix}";
            string art = $"s2r://panorama/images/hero_badges/hero_badge_rank_0{suffix.ToLowerInvariant() switch { "small" => "_small", "tiny" => "_tiny", _ => "" }}_png.vtex";
            string bronze = $".BronzeTier {cls},.BronzeTier{cls}";
            string target = $"}}{cls}{{background-repeat: no-repeat;background-size: contain;background-position: center;}}{bronze}{{background-image: url(\"{art}\");}}";
            string revealed = $"}}{cls}{{background-repeat:no-repeat;background-size:contain;background-position:center;background-image:url(\"{art}\");}}{bronze}{{}}";
            return new($"PlusHeroBadgeIcon{suffix}", target, fx => fx.Reveal ? revealed : null);
        }

        private static readonly IReadOnlyDictionary<string, Edit[]> Sheets = new Dictionary<string, Edit[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Styles + "hero_badge.vcss_c"] = new[]
            {
                new Edit("NoTierBadge", NoTierBadgeTarget, fx => fx.Reveal
                    ? "}DOTAHeroBadge.NoTier" + NoTierBadgeTarget[NoTierBadgeTarget.IndexOf('{')..].Replace(": ", ":")
                    : null),
                Label("LevelStyleLabel", LevelLabelTarget, "width:100%;height:100%;margin-top:2px;" + LevelStylePaint),
                LabelShadow(0, "0px 0px 6px 6.0 #321307"),
                LabelShadow(1, "0px 0px 6px 6.0 #2f444e"),
                LabelShadow(2, "0px 0px 6px 6.0 #442a02"),
                NoTierLabel("LevelStyleNoTierLabel", "}DOTAHeroBadge.LevelStyle.NoTier > Label" + Collapse),
                Label("ModelStyleLabel", ModelLabelTarget, "width:100%;height:100%;margin-top:8px;z-index:9;" + ModelStylePaint),
                NoTierLabel("ModelStyleNoTierLabel", "}DOTAHeroBadge.ModelStyle.NoTier > Label" + Collapse),
                new Edit("ModelStyleScene", SceneTarget, fx => fx.HideBadge && fx.Scope is null
                    ? "}DOTAHeroBadge.ModelStyle > DOTAScenePanel{visibility:collapse;}"
                    : null),
            },
            [Styles + "dashboard_page_hero_new_v2.vcss_c"] = new[]
            {
                Unlock("HeroPageBadgeGrey", "}.MaxHeroLevelWithoutSubscription #HeroBadge{saturation: 0.0;wash-color: #999;}"),
                Reveal("HeroPageBadge", "}#HeroBadge.NoTier" + Collapse),
                Reveal("HeroPageLevelBar", "}.NoTier #HeroLevelBarContainer" + Collapse),
                Unlock("HeroPageLockIcon", "}.MaxHeroLevelWithoutSubscription .HeroLevelBarLockIcon{visibility: visible;}"),
                new Edit("HeroPageXp", XpLabelsTarget, fx => fx.ProgressPercent is null ? null : fx.ShowsXp
                    ? $"{XpLabelsTarget}{Under(fx, ".LevelProgressXPValuesContainer")}{{background-size:contain;background-repeat:no-repeat;background-image:url(\"{S2r(HeroBadgePlan.XpTexture)}\");}}" +
                      $"{Under(fx, ".LevelProgressXPValuesContainer Label")}{{opacity:0;}}"
                    : $"{XpLabelsTarget}{Under(fx, ".LevelProgressXPValuesContainer")}{{visibility:collapse;}}"),
                new Edit("HeroPageProgress", ProgressBarTarget, fx => fx.ProgressPercent is null ? null :
                    $"{ProgressBarTarget}{Under(fx, "#HeroLevelProgress")}{{background-size:100% 100%;background-image:url(\"{S2r(HeroBadgePlan.ProgressTexture)}\");}}" +
                    $"{Under(fx, "#HeroLevelProgress .ProgressBarLeft")}{{opacity:0;}}"),
            },
            [Styles + "dashboard_page_post_game.vcss_c"] = new[]
            {
                Reveal("PostGameStatBadge", "}#OverviewStatHeroBadge.NoTier" + Collapse),
                Reveal("PostGameHeroIconBadge", "}.HeroIcon #HeroBadge.NoTier" + Collapse),
            },
            [Styles + "post_game_progress.vcss_c"] = new[]
            {
                Reveal("PostGameStatBadge", "}#OverviewStatHeroBadge.NoTier" + Collapse),
                Reveal("PostGameHeroIconBadge", "}.HeroIcon #HeroBadge.NoTier" + Collapse),
            },
            [Styles + "dotastyles.vcss_c"] = new[] { PlusIcon(""), PlusIcon("Small"), PlusIcon("Tiny"), ScopedOverrides() },
            [Styles + "hero_grid_new.vcss_c"] = new[]
            {
                Label("HeroGridLevel", GridLevelTarget, "margin-bottom:-5px;horizontal-align:center;" + GridPaint),
                Reveal("HeroGridStatus", "}.NoTier #HeroBadgeStatus" + Collapse),
            },
            [Styles + "ui_hero_picker.vcss_c"] = new[]
            {
                Label("HeroPickerLevel", PickerLevelTarget, "margin-left:-8px;margin-bottom:-7px;" + PickerPaint),
                Reveal("HeroPickerStatus", "}.NoTier #HeroBadgeStatus" + Collapse),
            },
            [Styles + "hud/dota_hud_inspect.vcss_c"] = new[] { Reveal("InspectBadge", "}#HeroBadge.NoTier" + Collapse) },
            [Styles + "hud/dota_hud_top_bar.vcss_c"] = new[]
            {
                new Edit("TopBarBadge", TopBarTarget, fx => fx.Reveal
                    ? TopBarTarget
                        .Replace(
                            "{width: 100%;height: 100%;background-size: 69px 47px;background-position: 0px -4px;background-repeat: no-repeat;visibility: collapse;}",
                            "{width:100%;height:100%;background-size:69px 47px;background-position:0px -4px;background-repeat:no-repeat;background-image:url(\"s2r://panorama/images/hud/reborn/top_bar_hero_radiant_badge_1_psd.vtex\");}")
                        .Replace(
                            "{visibility: visible;background-image: url(\"s2r://panorama/images/hud/reborn/top_bar_hero_radiant_badge_1_psd.vtex\");}",
                            "{visibility: visible;}")
                    : null),
            },
            [Styles + "hud/versus/dota_hud_versus_scene_interactive.vcss_c"] = new[] { Reveal("VersusBadge", "}.NoTier #HeroBadge" + Collapse) },
            [Styles + "showcase/showcase_item.vcss_c"] = new[]
            {
                Reveal("ShowcaseModelBadge", "}.HeroModel #HeroBadge.NoTier" + Collapse),
                Reveal("ShowcaseBadge", "}#HeroBadge.NoTier" + Collapse),
            },
            [Styles + "tooltips/tooltip_hero_card.vcss_c"] = new[] { Reveal("TooltipBadge", "}#HeroBadge.NoTier" + Collapse) },
            [Styles + "ui_dota_plus_hero_page.vcss_c"] = new[]
            {
                Unlock("PlusPageBarGrey", "}.MaxHeroLevelWithoutSubscription .HeroLevelBarMain{brightness: 0.3;saturation: 0.1;opacity: 0.8;}"),
            },
            [Styles + "ui_dota_plus_hero_page_v2.vcss_c"] = new[]
            {
                Reveal("PlusPageProgression", "}.NoTier #HeroBadgeProgression" + Collapse),
                Unlock("PlusPageBarGrey", "}.MaxHeroLevelWithoutSubscription .HeroLevelBarMain{brightness: 0.3;saturation: 0.1;opacity: 0.8;}"),
            },
        };

        public static IReadOnlyList<string> Stylesheets { get; } = Sheets.Keys.ToList();

        public static IReadOnlyList<string> StylesheetsFor(HeroBadgeEffects fx) =>
            Sheets.Where(s => s.Value.Any(e => e.Replace(fx) != null)).Select(s => s.Key).ToList();

        public static byte[]? Patch(string stylesheet, byte[] original, HeroBadgeEffects fx)
        {
            if (original is null || original.Length == 0) throw new ArgumentNullException(nameof(original));
            if (fx is null) throw new ArgumentNullException(nameof(fx));
            if (!Sheets.TryGetValue(stylesheet, out var edits))
                throw new ArgumentException($"Not a hero badge stylesheet: {stylesheet}", nameof(stylesheet));

            return PanoramaCssPatcher.ApplyEdits(stylesheet, original,
                edits.Select(e => (e.Name, e.Target, e.Replace(fx))));
        }

        internal static IEnumerable<(string Stylesheet, string Name, string Target)> AllTargets() =>
            Sheets.SelectMany(s => s.Value.Select(e => (s.Key, e.Name, e.Target)));
    }
}
