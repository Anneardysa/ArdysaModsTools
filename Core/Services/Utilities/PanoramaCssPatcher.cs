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
using System.Text;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public static class PanoramaCssPatcher
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public const string UiRankBadgeSheet = "panorama/styles/ui_rank_badge.vcss_c";
        public const string ProfileStatsSheet = "panorama/styles/dashboard_page_profile_stats_shared.vcss_c";
        public const string PickPhaseSheet = "panorama/styles/hud/dota_hud_hero_picking_player.vcss_c";

        public const string PickLabelTexture = "panorama/images/amtp.vtex_c";

        #region UI Rank Badge Rules

        private static readonly byte[] UiBadgeTarget1 = Utf8.GetBytes(
            "#RankLeaderboard{color: #F7E5c3;letter-spacing: 1px;font-size: 14px;margin-bottom: 9px;text-align: center;text-shadow: 1px 1px #00000080;horizontal-align: center;vertical-align: bottom;width: 100%;visibility: collapse;}.HasLeaderboardRank #RankLeaderboard{visibility: visible;}");

        private const string BadgeLayout =
            "margin-bottom:9px;horizontal-align:center;vertical-align:bottom;width:60px;height:15px;";

        private const string BadgePaint =
            "visibility:visible;color:#00000000;background-size:100% 100%;background-image:url(\"s2r://panorama/images/amtr.vtex\");";

        private static readonly byte[] UiBadgeTarget2 = Utf8.GetBytes(
            ".RankDataMissing #EliteFX,.RankTier0 #EliteFX,.RankTier1 #EliteFX,.RankTier2 #EliteFX,.RankTier3 #EliteFX,.RankTier4 #EliteFX,.RankTier5 #EliteFX,.RankTier6 #EliteFX,.RankTier7 #EliteFX{visibility: collapse;}");

        private static readonly byte[] UiBadgeRepl2 = PadRight(
            Utf8.GetBytes(".RankDataMissing #EliteFX{visibility: visible;}"), UiBadgeTarget2.Length);

        #endregion

        #region Profile Stats Rules

        private static readonly byte[] ProfileStatsTarget1 = Utf8.GetBytes(
            "#MMRNumber{visibility: collapse;}.MMRValue{horizontal-align: center;margin-bottom: 6px;text-overflow: shrink;color: #dcd087;font-size: 20px;text-transform: uppercase;}#MMRTBD.MMRTBD{visibility: collapse;horizontal-align: left;margin-left: 0px;text-overflow: shrink;color: #dcd087;font-size: 18px;text-transform: uppercase;}");

        private static readonly byte[] ProfileStatsRepl1 = PadRight(
            Utf8.GetBytes("#MMRNumber{visibility:visible;color:#00000000;width:88px;height:22px;background-size:contain;background-repeat:no-repeat;background-position:center;background-image:url(\"s2r://panorama/images/amtm.vtex\");}.MMRValue{horizontal-align:center;margin-bottom:6px;color:#dcd087;font-size:20px;}#MMRTBD.MMRTBD{visibility:collapse;}"),
            ProfileStatsTarget1.Length);

        private static readonly byte[] ProfileStatsTarget2 = Utf8.GetBytes(
            "#RankConfidenceContainer{margin-top: 2px;vertical-align: bottom;horizontal-align: right;visibility: collapse;width: 250px;}#RankConfidenceProgressBar{margin-top: 5px;width: 225px;height: 22px;border: 2px solid #403626;vertical-align: top;border-radius: 4px;margin-bottom: 4px;}#RankConfidenceProgressBar .ProgressBarLeft{background-color: gradient( linear, 0% 0%, 0% 80%, from( #997239 ), to( #a48d4b ) );}#RankConfidenceProgressBar .ProgressBarRight{background-color: #2a210e;}#RankConfidenceProgressBarValue{color: white;font-weight: bold;horizontal-align: center;vertical-align: center;}");

        private static readonly byte[] ProfileStatsRepl2 = PadRight(
            Utf8.GetBytes("#RankConfidenceContainer{margin-top:2px;vertical-align:bottom;horizontal-align:right;visibility:visible;width:250px;}#RankConfidenceProgressBar{margin-top:5px;width:225px;height:22px;border:2px solid #403626;vertical-align:top;border-radius:4px;margin-bottom:4px;background-size:100% 100%;background-image:url(\"s2r://panorama/images/amtc.vtex\");}#RankConfidenceProgressBar .ProgressBarLeft{background-color:#00000000;}#RankConfidenceProgressBar .ProgressBarRight{background-color:#00000000;}#RankConfidenceProgressBarValue{color:#00000000;horizontal-align:center;vertical-align:center;}"),
            ProfileStatsTarget2.Length);

        private static readonly byte[] ProfileStatsTarget3 = Utf8.GetBytes(
            "#RankConfidenceCalibrationThreshold{vertical-align: top;width: 2px;height: 100%;background-color: #dcd087;z-index: 1;opacity: 0.5;transform: translateX(-1px);}");

        private static readonly byte[] ProfileStatsRepl3 = PadRight(
            Utf8.GetBytes("#RankConfidenceCalibrationThreshold{visibility:collapse;}"), ProfileStatsTarget3.Length);

        private static readonly byte[] ProfileStatsTarget4 = Utf8.GetBytes(
            ".MMRCalibrated #MMRNumber,.MMRCalibrating:not(.MMRGlicko) #MMRCalibrating,.MMRNoData:not(.MMRGlicko) #MMRNoData,.MMRCalibrating #MMRTBD{visibility: visible;}");

        private static readonly byte[] ProfileStatsRepl4 = PadRight(
            Utf8.GetBytes(".MMRCalibrated #MMRNumber{visibility:visible;}"), ProfileStatsTarget4.Length);

        #endregion

        private static byte[] BuildBadgeRule(int? onlyTier)
        {
            string css = onlyTier is null
                ? $"#RankLeaderboard{{{BadgeLayout}{BadgePaint}}}"
                : $"#RankLeaderboard{{{BadgeLayout}visibility:collapse;}}"
                  + $".RankTier{onlyTier} #RankLeaderboard{{{BadgePaint}}}";

            return PadRight(Utf8.GetBytes(css), UiBadgeTarget1.Length);
        }

        public static byte[] PatchUiRankBadge(byte[] originalBytes, int? onlyTier = null)
        {
            if (originalBytes is null || originalBytes.Length == 0)
                throw new ArgumentNullException(nameof(originalBytes));

            byte[] patched = (byte[])originalBytes.Clone();
            ReplaceExact(patched, UiBadgeTarget1, BuildBadgeRule(onlyTier), nameof(UiBadgeTarget1));
            ReplaceExact(patched, UiBadgeTarget2, UiBadgeRepl2, nameof(UiBadgeTarget2));

            if (patched.Length != originalBytes.Length)
                throw new InvalidOperationException("Byte length invariant violated during ui_rank_badge patching.");

            return patched;
        }

        public static byte[] PatchProfileStats(byte[] originalBytes)
        {
            if (originalBytes is null || originalBytes.Length == 0)
                throw new ArgumentNullException(nameof(originalBytes));

            byte[] patched = (byte[])originalBytes.Clone();
            ReplaceExact(patched, ProfileStatsTarget1, ProfileStatsRepl1, nameof(ProfileStatsTarget1));
            ReplaceExact(patched, ProfileStatsTarget2, ProfileStatsRepl2, nameof(ProfileStatsTarget2));
            ReplaceExact(patched, ProfileStatsTarget3, ProfileStatsRepl3, nameof(ProfileStatsTarget3));
            ReplaceExact(patched, ProfileStatsTarget4, ProfileStatsRepl4, nameof(ProfileStatsTarget4));

            if (patched.Length != originalBytes.Length)
                throw new InvalidOperationException("Byte length invariant violated during profile_stats patching.");

            return patched;
        }

        #region Picking phase

        private const string PickAnchor =
            "}.AspectRatio4x3 DOTAHudHeroPickingPlayer{margin-left: -4px;margin-right: -4px;}";

        public static byte[] PatchHeroPickingPlayer(byte[] originalBytes, bool onlyLocalPlayer)
            => PatchHeroPickingPlayer(originalBytes, onlyLocalPlayer, forceVisible: false);

        internal static byte[] PatchHeroPickingPlayer(byte[] originalBytes, bool onlyLocalPlayer, bool forceVisible)
        {
            if (originalBytes is null || originalBytes.Length == 0)
                throw new ArgumentNullException(nameof(originalBytes));

            string selector = onlyLocalPlayer ? ".IsLocalPlayerPawn #PlayerRank" : "DOTAHudHeroPickingPlayer #PlayerRank";
            string body = $"color:#00000000;height:{VtexGenerator.PickRankCssBoxHeightPx}px;background-size:contain;" +
                "background-repeat:no-repeat;background-position:center;" +
                $"background-image:url(\"s2r://{PickLabelTexture[..^2]}\");";
            string reveal = forceVisible ? "DOTAHudHeroPickingPlayer #PlayerRank{visibility:visible;}" : "";

            return ApplyEdits(PickPhaseSheet, originalBytes,
                new[] { ("PickRankLabel", PickAnchor, (string?)$"{PickAnchor}{reveal}{selector}{{{body}}}") })!;
        }

        #endregion

        internal static IEnumerable<(string Stylesheet, string Name, string Target)> AllTargets()
        {
            yield return (UiRankBadgeSheet, nameof(UiBadgeTarget1), Utf8.GetString(UiBadgeTarget1));
            yield return (UiRankBadgeSheet, nameof(UiBadgeTarget2), Utf8.GetString(UiBadgeTarget2));
            yield return (ProfileStatsSheet, nameof(ProfileStatsTarget1), Utf8.GetString(ProfileStatsTarget1));
            yield return (ProfileStatsSheet, nameof(ProfileStatsTarget2), Utf8.GetString(ProfileStatsTarget2));
            yield return (ProfileStatsSheet, nameof(ProfileStatsTarget3), Utf8.GetString(ProfileStatsTarget3));
            yield return (ProfileStatsSheet, nameof(ProfileStatsTarget4), Utf8.GetString(ProfileStatsTarget4));
            yield return (PickPhaseSheet, "PickRankLabel", PickAnchor);
        }

        private static void ReplaceExact(byte[] buffer, byte[] target, byte[] replacement, string ruleName)
        {
            if (target.Length != replacement.Length)
                throw new ArgumentException($"Target and replacement lengths must match for rule {ruleName}. (Target: {target.Length}, Repl: {replacement.Length})");

            int index = IndexOf(buffer, target);
            if (index == -1)
                throw new InvalidOperationException($"Target CSS rule '{ruleName}' was not found in the compiled stylesheet.");
            if (buffer.AsSpan(index + 1).IndexOf(target) >= 0)
                throw new InvalidOperationException($"Target CSS rule '{ruleName}' occurs more than once in the compiled stylesheet.");

            Buffer.BlockCopy(replacement, 0, buffer, index, replacement.Length);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length > haystack.Length) return -1;
            int limit = haystack.Length - needle.Length;

            for (int i = 0; i <= limit; i++)
            {
                if (haystack[i] == needle[0])
                {
                    bool match = true;
                    for (int j = 1; j < needle.Length; j++)
                    {
                        if (haystack[i + j] != needle[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return i;
                }
            }

            return -1;
        }

        private static byte[] PadRight(byte[] content, int totalLength)
        {
            if (content.Length > totalLength)
                throw new ArgumentException($"Content length ({content.Length}) exceeds total length ({totalLength}).");

            var result = new byte[totalLength];
            Buffer.BlockCopy(content, 0, result, 0, content.Length);
            for (int i = content.Length; i < totalLength; i++)
            {
                result[i] = (byte)' ';
            }
            return result;
        }

        internal static byte[]? ApplyEdits(string stylesheet, byte[] original,
            IEnumerable<(string Name, string Target, string? Replacement)> edits)
        {
            var spans = new List<(int Start, int End, byte[] Text, string Name)>();
            foreach (var (name, targetText, replacement) in edits)
            {
                if (replacement is null) continue;

                if (replacement[0] != '}' || replacement[^1] != '}' || targetText[^1] != '}')
                    throw new InvalidOperationException($"'{name}' must be whole rules: '}}' first and last.");

                byte[] target = Encoding.UTF8.GetBytes(targetText);
                int at = original.AsSpan().IndexOf(target);
                if (at < 0)
                    throw new InvalidOperationException($"Target CSS rule '{name}' was not found in {stylesheet}.");
                if (original.AsSpan(at + 1).IndexOf(target) >= 0)
                    throw new InvalidOperationException($"Target CSS rule '{name}' occurs more than once in {stylesheet}.");

                spans.Add((at + 1, at + target.Length, Encoding.UTF8.GetBytes(replacement[1..]), name));
            }

            if (spans.Count == 0) return null;
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (int i = 1; i < spans.Count; i++)
                if (spans[i].Start < spans[i - 1].End)
                    throw new InvalidOperationException($"'{spans[i - 1].Name}' and '{spans[i].Name}' overlap in {stylesheet}.");

            int regionStart = spans[0].Start;
            int textEnd = spans[^1].End;
            while (textEnd < original.Length && !IsControl(original[textEnd])) textEnd++;
            if (textEnd == original.Length || (original[textEnd - 1] != (byte)'}' && original[textEnd - 1] != (byte)';'))
                throw new InvalidOperationException($"{stylesheet}: no clean end found for the CSS text block.");

            byte[] region = Rebuild(original, regionStart, textEnd, spans, stylesheet);

            byte[] patched = (byte[])original.Clone();
            region.CopyTo(patched, regionStart);
            return patched;
        }

        private static byte[] Rebuild(byte[] original, int start, int end,
            List<(int Start, int End, byte[] Text, string Name)> spans, string stylesheet)
        {
            var output = new List<byte>(end - start + 512);
            var removable = new List<int>();
            int depth = 0;
            int next = 0;

            for (int i = start; i < end;)
            {
                if (next < spans.Count && i == spans[next].Start)
                {
                    output.AddRange(spans[next].Text);
                    i = spans[next].End;
                    next++;
                    continue;
                }

                byte b = original[i];
                if (IsControl(b))
                    throw new InvalidOperationException($"{stylesheet}: binary inside the CSS text at {i}.");
                if (b == (byte)'{') depth++;
                else if (b == (byte)'}') depth--;
                else if (b == (byte)' ' && depth == 1 && original[i - 1] == (byte)':')
                    removable.Add(output.Count);

                output.Add(b);
                i++;
            }

            int excess = output.Count - (end - start);
            if (excess > removable.Count)
                throw new InvalidOperationException($"{stylesheet}: {excess} bytes over and only {removable.Count} spaces to give.");

            for (int k = removable.Count - 1; k >= removable.Count - Math.Max(excess, 0); k--)
                output.RemoveAt(removable[k]);

            while (output.Count < end - start) output.Add((byte)' ');
            return output.ToArray();
        }

        private static bool IsControl(byte b) => b < 0x09 || (b > 0x0D && b < 0x20);
    }
}
