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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Utilities;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class VtexGeneratorTests
    {
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        #region MMR Texture Tests

        [TestCase(0)]
        [TestCase(12620)]
        [TestCase(99999)]
        public void GenerateMmrTexture_ValidRange_ProducesValidVtexAndPng(int mmr)
        {
            byte[] vtex = VtexGenerator.GenerateMmrTexture(mmr);

            Assert.That(vtex.Length, Is.GreaterThan(VtexGenerator.TemplateHeaderSize + PngMagic.Length));
            AssertDimensions(vtex, 512, 128);
            AssertPngPayload(vtex);
        }

        [TestCase(-1)]
        [TestCase(100000)]
        public void GenerateMmrTexture_OutOfRange_ThrowsArgumentOutOfRangeException(int invalidMmr)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateMmrTexture(invalidMmr));
        }

        #endregion

        #region Confidence Texture Tests

        [TestCase(0)]
        [TestCase(50)]
        [TestCase(100)]
        public void GenerateConfidenceTexture_ValidRange_ProducesValidVtexAndPng(int confidence)
        {
            byte[] vtex = VtexGenerator.GenerateConfidenceTexture(confidence);

            Assert.That(vtex.Length, Is.GreaterThan(VtexGenerator.TemplateHeaderSize + PngMagic.Length));
            AssertDimensions(vtex, 450, 44);
            AssertPngPayload(vtex);
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void GenerateConfidenceTexture_OutOfRange_ThrowsArgumentOutOfRangeException(int invalidConfidence)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateConfidenceTexture(invalidConfidence));
        }

        #endregion

        #region Leaderboard Rank Texture Tests

        [TestCase(1)]
        [TestCase(6)]
        [TestCase(1000)]
        public void GenerateLeaderboardRankTexture_ImmortalLeaderboard_ProducesValidVtex(int rank)
        {
            byte[] vtex = VtexGenerator.GenerateLeaderboardRankTexture(rank, isImmortalLeaderboard: true);

            Assert.That(vtex.Length, Is.GreaterThan(VtexGenerator.TemplateHeaderSize + PngMagic.Length));
            AssertDimensions(vtex, 512, 128);
            AssertPngPayload(vtex);
        }

        [Test]
        public void GenerateLeaderboardRankTexture_NonImmortal_ProducesValidTransparentTexture()
        {
            byte[] vtex = VtexGenerator.GenerateLeaderboardRankTexture(6, isImmortalLeaderboard: false);

            Assert.That(vtex.Length, Is.GreaterThan(VtexGenerator.TemplateHeaderSize + PngMagic.Length));
            AssertDimensions(vtex, 512, 128);
            AssertPngPayload(vtex);
        }

        [TestCase(0)]
        [TestCase(1000000)]
        public void GenerateLeaderboardRankTexture_InvalidRank_ThrowsArgumentOutOfRangeException(int invalidRank)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VtexGenerator.GenerateLeaderboardRankTexture(invalidRank, isImmortalLeaderboard: true));
        }

        #endregion

        #region Empty Pip Tests

        [Test]
        public void GenerateEmptyPipTexture_ProducesValid1x1Texture()
        {
            byte[] vtex = VtexGenerator.GenerateEmptyPipTexture();

            Assert.That(vtex.Length, Is.GreaterThan(VtexGenerator.TemplateHeaderSize));
            AssertDimensions(vtex, 1, 1);
            AssertPngPayload(vtex);
        }

        #endregion

        #region WrapPng Guard Tests

        [Test]
        public void WrapPng_NullPayload_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => VtexGenerator.WrapPng(null!, 512, 128));
        }

        [Test]
        public void WrapPng_NotPngPayload_ThrowsArgumentException()
        {
            byte[] garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            Assert.Throws<ArgumentException>(() => VtexGenerator.WrapPng(garbage, 512, 128));
        }

        [TestCase(0, 100)]
        [TestCase(100, 0)]
        [TestCase(-1, 100)]
        [TestCase(100, 9000)]
        public void WrapPng_InvalidDimensions_ThrowsArgumentOutOfRangeException(int w, int h)
        {
            using var bmp = new Bitmap(1, 1);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            byte[] png = ms.ToArray();

            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.WrapPng(png, w, h));
        }

        #endregion

        #region Panorama size calibration

        [TestCase("6")]
        [TestCase("42")]
        [TestCase("5000")]
        public void GenerateLeaderboardRankTexture_ProjectedDigitHeight_MatchesValves14pxText(string rank)
        {
            float projected = ProjectedGlyphHeight(
                VtexGenerator.GenerateLeaderboardRankTexture(int.Parse(rank), true),
                VtexGenerator.LeaderboardCssBoxHeightPx);

            float expected = ReferenceInkHeight(VtexGenerator.FormatNumber(int.Parse(rank)), VtexGenerator.LeaderboardCssFontPx);

            Assert.That(projected, Is.EqualTo(expected).Within(1.0f),
                $"'{rank}' draws {projected:F2}px tall in game; Valve's own 14px text is {expected:F2}px.");
        }

        [Test]
        public void GenerateLeaderboardRankTexture_ProjectedShadowOffset_IsValvesOnePixel()
        {
            byte[] vtex = VtexGenerator.GenerateLeaderboardRankTexture(6, true);
            float scale = (float)VtexGenerator.LeaderboardCssBoxHeightPx / VtexGenerator.TextureHeight;

            float overhang = (InkBounds(vtex).Bottom - InkBounds(vtex, glyphOnly: true).Bottom) * scale;

            Assert.That(overhang, Is.EqualTo(VtexGenerator.LeaderboardCssShadowOffsetPx).Within(0.35f),
                $"shadow lands {overhang:F2}px off the glyph; Valve draws it at {VtexGenerator.LeaderboardCssShadowOffsetPx}px.");
        }

        [TestCase(0)]
        [TestCase(5420)]
        [TestCase(12620)]
        public void GenerateMmrTexture_ProjectedDigitHeight_MatchesValves20pxText(int mmr)
        {
            float projected = ProjectedInkHeight(
                VtexGenerator.GenerateMmrTexture(mmr), VtexGenerator.MmrCssBoxHeightPx);

            float expected = ReferenceInkHeight(VtexGenerator.FormatNumber(mmr), VtexGenerator.MmrCssFontPx);

            Assert.That(projected, Is.EqualTo(expected).Within(1.0f),
                $"{mmr} draws {projected:F2}px tall in game; Valve's own 20px text is {expected:F2}px.");
        }

        [Test]
        public void GenerateLeaderboardRankTexture_IsDrawnHeavierThanTheMmrNumber()
        {
            var rank = InkBounds(VtexGenerator.GenerateLeaderboardRankTexture(888, true), glyphOnly: true);
            var mmr = InkBounds(VtexGenerator.GenerateMmrTexture(888), glyphOnly: true);

            float rankPerEm = rank.Width / VtexGenerator.ToTexturePx(VtexGenerator.LeaderboardCssFontPx, VtexGenerator.LeaderboardCssBoxHeightPx);
            float mmrPerEm = mmr.Width / VtexGenerator.ToTexturePx(VtexGenerator.MmrCssFontPx, VtexGenerator.MmrCssBoxHeightPx);

            Assert.That(rankPerEm, Is.GreaterThan(mmrPerEm),
                "the rank number lost its heavier face — it will read thin against Dota's own crisp text.");
        }

        [TestCase(6, ExpectedResult = "6")]
        [TestCase(53, ExpectedResult = "53")]
        [TestCase(999, ExpectedResult = "999")]
        [TestCase(4735, ExpectedResult = "4,735")]
        [TestCase(10620, ExpectedResult = "10,620")]
        [TestCase(25000, ExpectedResult = "25,000")]
        public string FormatNumber_GroupsThousands_TheWayPanoramaDoes(int value)
            => VtexGenerator.FormatNumber(value);

        [Test]
        public void GenerateMmrTexture_GroupedNumber_DrawsTheSeparator()
        {
            int grouped = InkBounds(VtexGenerator.GenerateMmrTexture(1000)).Bottom;
            int ungrouped = InkBounds(VtexGenerator.GenerateMmrTexture(999)).Bottom;

            Assert.That(grouped, Is.GreaterThan(ungrouped),
                "1,000 carries a comma below the baseline; 999 has nothing under its digits.");
        }

        [Test]
        public void GenerateMmrTexture_Separator_DoesNotShiftTheDigits()
        {
            int grouped = InkBounds(VtexGenerator.GenerateMmrTexture(1000)).Top;
            int ungrouped = InkBounds(VtexGenerator.GenerateMmrTexture(999)).Top;

            Assert.That(grouped, Is.EqualTo(ungrouped).Within(1),
                "the comma's descender pushed the digits off the baseline.");
        }

        [Test]
        public void GenerateLeaderboardRankTexture_DigitCount_DoesNotChangeGlyphHeight()
        {
            var oneDigit = InkBounds(VtexGenerator.GenerateLeaderboardRankTexture(6, true), glyphOnly: true);
            var fourDigits = InkBounds(VtexGenerator.GenerateLeaderboardRankTexture(5000, true), glyphOnly: true);

            Assert.That(fourDigits.Top, Is.EqualTo(oneDigit.Top).Within(1),
                "a longer rank must be wider, never smaller.");
            Assert.That(fourDigits.Width, Is.GreaterThan(oneDigit.Width), "four digits must be wider than one.");
        }

        [Test]
        public void GenerateLeaderboardRankTexture_WidestAllowedRank_FitsInsideTheCanvas()
        {
            var ink = InkBounds(VtexGenerator.GenerateLeaderboardRankTexture(5000, true));

            Assert.That(ink.Left, Is.GreaterThan(0), "clipped on the left");
            Assert.That(ink.Right, Is.LessThan(VtexGenerator.TextureWidth), "clipped on the right");
        }

        [TestCase(6)]
        [TestCase(5000)]
        public void GenerateLeaderboardRankTexture_GlyphsSitOnTheBaselineAndUnclipped(int rank)
        {
            var glyphs = InkBounds(VtexGenerator.GenerateLeaderboardRankTexture(rank, true), glyphOnly: true);

            Assert.That(glyphs.Top, Is.GreaterThan(0), "clipped at the top");
            Assert.That(glyphs.Bottom, Is.LessThanOrEqualTo(VtexGenerator.TextureHeight),
                "clipped at the bottom — a grouped rank's comma needs room under the baseline");

            float centre = glyphs.Top + glyphs.Height / 2f;
            Assert.That(centre, Is.EqualTo(VtexGenerator.TextureHeight / 2f).Within(14f),
                $"digits sit at y={centre:F1} of a {VtexGenerator.TextureHeight}px canvas.");
        }

        [Test]
        public void PatchedStylesheet_BoxHeight_MatchesTheGeneratorsConstant()
        {
            Assert.That(CssPixels(PatchedUiRankBadgeCss(), "RankLeaderboard", "height"),
                Is.EqualTo(VtexGenerator.LeaderboardCssBoxHeightPx),
                "the CSS box height changed — VtexGenerator's constant must follow, or the glyphs go back to being mis-sized.");
        }

        [Test]
        public void PatchedStylesheet_MmrBox_MatchesTheGeneratorsConstants()
        {
            string patched = PatchedProfileStatsCss();

            Assert.That(CssPixels(patched, "MMRNumber", "height"), Is.EqualTo(VtexGenerator.MmrCssBoxHeightPx));
            Assert.That(CssPixels(patched, "MMRNumber", "width"), Is.EqualTo(VtexGenerator.MmrCssBoxWidthPx));

            Assert.That(VtexGenerator.MmrCssBoxWidthPx,
                Is.EqualTo(VtexGenerator.TextureWidth * VtexGenerator.MmrCssBoxHeightPx / VtexGenerator.TextureHeight),
                "width:88px must stay 512 x (22/128), otherwise `contain` scales by the width instead.");
        }

        private static int CssPixels(string css, string elementId, string property)
        {
            var rule = System.Text.RegularExpressions.Regex.Match(css, $@"#{elementId}\{{(.*?)\}}");
            Assert.That(rule.Success, Is.True, $"no #{elementId} rule in the patched stylesheet");

            var value = System.Text.RegularExpressions.Regex.Match(rule.Groups[1].Value, $@"{property}:(\d+)px");
            Assert.That(value.Success, Is.True, $"#{elementId} no longer sets an explicit {property} in px");
            return int.Parse(value.Groups[1].Value);
        }

        #region Who shows the number

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(8)]
        public void PatchUiRankBadge_ScopedToATier_RevealsTheNumberOnlyThere(int tier)
        {
            string css = PatchedUiRankBadgeCss(onlyTier: tier);

            Assert.That(css, Does.Contain($".RankTier{tier} #RankLeaderboard{{"),
                "the number is not gated on the wearer's real tier");
            Assert.That(CssRule(css, "RankLeaderboard"), Does.Contain("visibility:collapse"),
                "the base rule must hide the number, or every badge still shows it");
            Assert.That(CssRule(css, "RankLeaderboard"), Does.Not.Contain("background-image"),
                "the artwork must hang off the tier rule, not off every badge");
        }

        [Test]
        public void PatchUiRankBadge_Unscoped_ShowsTheNumberOnEveryBadge()
        {
            string css = PatchedUiRankBadgeCss(onlyTier: null);

            Assert.That(css, Does.Not.Contain(".RankTier"), "no tier gate expected in every-rank mode");
            Assert.That(CssRule(css, "RankLeaderboard"), Does.Contain("visibility:visible"));
            Assert.That(CssRule(css, "RankLeaderboard"), Does.Contain("amtr.vtex"));
        }

        [TestCase(null)]
        [TestCase(8)]
        public void PatchUiRankBadge_BothShapes_FitValvesByteBudget(int? onlyTier)
        {
            Assert.DoesNotThrow(() => PatchedUiRankBadgeCss(onlyTier),
                "the replacement outgrew the rule it has to overwrite");
        }

        [Test]
        public void PatchedStylesheet_LeaderboardBox_MatchesTheGeneratorsConstants()
        {
            string css = PatchedUiRankBadgeCss();

            Assert.That(CssPixels(css, "RankLeaderboard", "height"), Is.EqualTo(VtexGenerator.LeaderboardCssBoxHeightPx));
            Assert.That(CssPixels(css, "RankLeaderboard", "width"), Is.EqualTo(VtexGenerator.LeaderboardCssBoxWidthPx));

            Assert.That(VtexGenerator.LeaderboardCssBoxWidthPx,
                Is.EqualTo(VtexGenerator.TextureWidth * VtexGenerator.LeaderboardCssBoxHeightPx / VtexGenerator.TextureHeight),
                "width:60px must stay 512 x (15/128), or the artwork no longer fills the box exactly "
                + "and dropping background-repeat/position stops being safe.");
        }

        private static string CssRule(string css, string elementId)
        {
            var rule = System.Text.RegularExpressions.Regex.Match(css, $@"#{elementId}\{{(.*?)\}}");
            Assert.That(rule.Success, Is.True, $"no #{elementId} rule in the patched stylesheet");
            return rule.Groups[1].Value;
        }

        #endregion

        private static string PatchedUiRankBadgeCss(int? onlyTier = null)
        {
            const string valveRule =
                "#RankLeaderboard{color: #F7E5c3;letter-spacing: 1px;font-size: 14px;margin-bottom: 9px;text-align: center;text-shadow: 1px 1px #00000080;horizontal-align: center;vertical-align: bottom;width: 100%;visibility: collapse;}.HasLeaderboardRank #RankLeaderboard{visibility: visible;}";
            const string eliteRule =
                ".RankDataMissing #EliteFX,.RankTier0 #EliteFX,.RankTier1 #EliteFX,.RankTier2 #EliteFX,.RankTier3 #EliteFX,.RankTier4 #EliteFX,.RankTier5 #EliteFX,.RankTier6 #EliteFX,.RankTier7 #EliteFX{visibility: collapse;}";

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes("HEADER" + valveRule + eliteRule + "TRAILER");
            return System.Text.Encoding.UTF8.GetString(PanoramaCssPatcher.PatchUiRankBadge(buffer, onlyTier));
        }

        private static string PatchedProfileStatsCss()
        {
            const string mmr =
                "#MMRNumber{visibility: collapse;}.MMRValue{horizontal-align: center;margin-bottom: 6px;text-overflow: shrink;color: #dcd087;font-size: 20px;text-transform: uppercase;}#MMRTBD.MMRTBD{visibility: collapse;horizontal-align: left;margin-left: 0px;text-overflow: shrink;color: #dcd087;font-size: 18px;text-transform: uppercase;}";
            const string confidence =
                "#RankConfidenceContainer{margin-top: 2px;vertical-align: bottom;horizontal-align: right;visibility: collapse;width: 250px;}#RankConfidenceProgressBar{margin-top: 5px;width: 225px;height: 22px;border: 2px solid #403626;vertical-align: top;border-radius: 4px;margin-bottom: 4px;}#RankConfidenceProgressBar .ProgressBarLeft{background-color: gradient( linear, 0% 0%, 0% 80%, from( #997239 ), to( #a48d4b ) );}#RankConfidenceProgressBar .ProgressBarRight{background-color: #2a210e;}#RankConfidenceProgressBarValue{color: white;font-weight: bold;horizontal-align: center;vertical-align: center;}";
            const string threshold =
                "#RankConfidenceCalibrationThreshold{vertical-align: top;width: 2px;height: 100%;background-color: #dcd087;z-index: 1;opacity: 0.5;transform: translateX(-1px);}";
            const string visibility =
                ".MMRCalibrated #MMRNumber,.MMRCalibrating:not(.MMRGlicko) #MMRCalibrating,.MMRNoData:not(.MMRGlicko) #MMRNoData,.MMRCalibrating #MMRTBD{visibility: visible;}";

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes("HEADER" + mmr + confidence + threshold + visibility + "TRAILER");
            return System.Text.Encoding.UTF8.GetString(PanoramaCssPatcher.PatchProfileStats(buffer));
        }

        private static float ProjectedInkHeight(byte[] vtex, int cssBoxHeightPx)
            => InkBounds(vtex).Height * (float)cssBoxHeightPx / VtexGenerator.TextureHeight;

        private static float ProjectedGlyphHeight(byte[] vtex, int cssBoxHeightPx)
            => InkBounds(vtex, glyphOnly: true).Height * (float)cssBoxHeightPx / VtexGenerator.TextureHeight;

        private static float ReferenceInkHeight(string text, float cssFontPx)
        {
            using var bmp = new Bitmap(256, 64, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using var font = new Font("Segoe UI", cssFontPx, FontStyle.Regular, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.White);
                g.DrawString(text, font, brush, 20f, 10f, StringFormat.GenericTypographic);
            }

            return GetNonTransparentBounds(bmp).Height;
        }

        private static Rectangle InkBounds(byte[] vtex, bool glyphOnly = false)
        {
            using var ms = new MemoryStream(vtex, VtexGenerator.TemplateHeaderSize,
                vtex.Length - VtexGenerator.TemplateHeaderSize);
            using var bmp = new Bitmap(ms);
            return glyphOnly ? GetBrightPixelBounds(bmp) : GetNonTransparentBounds(bmp);
        }

        private static Rectangle GetBrightPixelBounds(Bitmap bmp)
        {
            int minX = bmp.Width, maxX = -1, minY = bmp.Height, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.A <= 20 || p.R < 128) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return maxX < 0 ? Rectangle.Empty : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        #endregion

        #region Modder Inspection

        [Test]
        [Explicit("Requires the local-only modder reference textures under scripts/dataset/hero_assets/diff/modder.")]
        public void InspectModderReferenceTextures()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../"));
            string modderImages = Path.Combine(repoRoot, "scripts/dataset/hero_assets/diff/modder/panorama/images");

            string hbMPath = Path.Combine(modderImages, "hb_m.vtex_c");
            string hbCPath = Path.Combine(modderImages, "hb_c.vtex_c");
            string hbRPath = Path.Combine(modderImages, "hb_r.vtex_c");

            if (!File.Exists(hbMPath))
                Assert.Ignore("Modder reference files not found.");

            byte[] mData = File.ReadAllBytes(hbMPath);
            using var mMs = new MemoryStream(mData, VtexGenerator.TemplateHeaderSize, mData.Length - VtexGenerator.TemplateHeaderSize);
            using var mBmp = new Bitmap(mMs);
            var mBox = GetNonTransparentBounds(mBmp);
            Color sampleMColor = mBmp.GetPixel(mBox.X + mBox.Width / 2, mBox.Y + mBox.Height / 2);
            TestContext.Progress.WriteLine($"hb_m (MMR): size={mBmp.Width}x{mBmp.Height}, bbox=({mBox.X}, {mBox.Y}, {mBox.Width}, {mBox.Height}), centerColor=#{sampleMColor.R:X2}{sampleMColor.G:X2}{sampleMColor.B:X2}");

            byte[] rData = File.ReadAllBytes(hbRPath);
            using var rMs = new MemoryStream(rData, VtexGenerator.TemplateHeaderSize, rData.Length - VtexGenerator.TemplateHeaderSize);
            using var rBmp = new Bitmap(rMs);
            var rBox = GetNonTransparentBounds(rBmp);
            TestContext.Progress.WriteLine($"hb_r (Rank): size={rBmp.Width}x{rBmp.Height}, bbox=({rBox.X}, {rBox.Y}, {rBox.Width}, {rBox.Height})");

            byte[] cData = File.ReadAllBytes(hbCPath);
            using var cMs = new MemoryStream(cData, VtexGenerator.TemplateHeaderSize, cData.Length - VtexGenerator.TemplateHeaderSize);
            using var cBmp = new Bitmap(cMs);
            var cBox = GetWhiteTextBounds(cBmp);
            TestContext.Progress.WriteLine($"hb_c (Confidence): size={cBmp.Width}x{cBmp.Height}, text_bbox=({cBox.X}, {cBox.Y}, {cBox.Width}, {cBox.Height})");

            byte[] ourMData = VtexGenerator.GenerateMmrTexture(12620);
            using var ourMMs = new MemoryStream(ourMData, VtexGenerator.TemplateHeaderSize, ourMData.Length - VtexGenerator.TemplateHeaderSize);
            using var ourMBmp = new Bitmap(ourMMs);
            var ourMBox = GetNonTransparentBounds(ourMBmp);
            TestContext.Progress.WriteLine($"OUR hb_m (MMR): size={ourMBmp.Width}x{ourMBmp.Height}, bbox=({ourMBox.X}, {ourMBox.Y}, {ourMBox.Width}, {ourMBox.Height})");


            byte[] ourRData = VtexGenerator.GenerateLeaderboardRankTexture(6, true);
            using var ourRMs = new MemoryStream(ourRData, VtexGenerator.TemplateHeaderSize, ourRData.Length - VtexGenerator.TemplateHeaderSize);
            using var ourRBmp = new Bitmap(ourRMs);
            var ourRBox = GetNonTransparentBounds(ourRBmp);
            TestContext.Progress.WriteLine($"OUR hb_r (Rank): size={ourRBmp.Width}x{ourRBmp.Height}, bbox=({ourRBox.X}, {ourRBox.Y}, {ourRBox.Width}, {ourRBox.Height})");


            byte[] ourCData = VtexGenerator.GenerateConfidenceTexture(100);
            using var ourCMs = new MemoryStream(ourCData, VtexGenerator.TemplateHeaderSize, ourCData.Length - VtexGenerator.TemplateHeaderSize);
            using var ourCBmp = new Bitmap(ourCMs);
            var ourCBox = GetWhiteTextBounds(ourCBmp);
            TestContext.Progress.WriteLine($"OUR hb_c (Confidence): size={ourCBmp.Width}x{ourCBmp.Height}, text_bbox=({ourCBox.X}, {ourCBox.Y}, {ourCBox.Width}, {ourCBox.Height})");

            Assert.That(ourCBox.Height, Is.InRange(18, 23), "Confidence text height must match Valve/modder reference (~20px)");
        }

        private static Rectangle GetNonTransparentBounds(Bitmap bmp)
        {
            int minX = bmp.Width, maxX = -1, minY = bmp.Height, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (bmp.GetPixel(x, y).A > 20)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            return minX <= maxX ? new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1) : Rectangle.Empty;
        }

        private static Rectangle GetWhiteTextBounds(Bitmap bmp)
        {
            int minX = bmp.Width, maxX = -1, minY = bmp.Height, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R > 180 && c.G > 180 && c.B > 180)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            return minX <= maxX ? new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1) : Rectangle.Empty;
        }

        #endregion

        #region Helpers

        private static void AssertDimensions(byte[] vtex, int expectedWidth, int expectedHeight)
        {
            int w = vtex[VtexGenerator.WidthOffset] | (vtex[VtexGenerator.WidthOffset + 1] << 8);
            int h = vtex[VtexGenerator.HeightOffset] | (vtex[VtexGenerator.HeightOffset + 1] << 8);

            Assert.That(w, Is.EqualTo(expectedWidth), "Width mismatch in VTEX header");
            Assert.That(h, Is.EqualTo(expectedHeight), "Height mismatch in VTEX header");
        }

        private static void AssertPngPayload(byte[] vtex)
        {
            for (int i = 0; i < PngMagic.Length; i++)
            {
                Assert.That(vtex[VtexGenerator.TemplateHeaderSize + i], Is.EqualTo(PngMagic[i]),
                    $"Byte {i} of PNG magic mismatch in VTEX payload");
            }
        }

        #endregion

        #region Picking phase label

        [Test]
        public void GeneratePickRankTexture_HeaderCarriesTheCanvasSize()
        {
            byte[] vtex = VtexGenerator.GeneratePickRankTexture("Divine V");

            Assert.That(BitConverter.ToUInt16(vtex, VtexGenerator.WidthOffset), Is.EqualTo(VtexGenerator.PickRankTextureWidth));
            Assert.That(BitConverter.ToUInt16(vtex, VtexGenerator.HeightOffset), Is.EqualTo(VtexGenerator.PickRankTextureHeight));
            Assert.That(InkBounds(vtex), Is.Not.EqualTo(Rectangle.Empty));
        }

        [Test]
        public void GeneratePickRankTexture_EmptyText_IsFullyTransparent() =>
            Assert.That(InkBounds(VtexGenerator.GeneratePickRankTexture("")), Is.EqualTo(Rectangle.Empty));

        [Test]
        public void GeneratePickRankTexture_Null_Throws() =>
            Assert.Throws<ArgumentNullException>(() => VtexGenerator.GeneratePickRankTexture(null!));

        [TestCase("Guardian VII")]
        [TestCase("Crusader VII")]
        [TestCase("На калибровке")]
        [TestCase("Властелин VII")]
        [TestCase("Not Calibrated")]
        [TestCase("Rank 4,471")]
        [TestCase("Rank 5,000")]
        [TestCase("Ранг: 5,000")]
        [TestCase("Immortal Immortal Immortal Immortal")]
        public void GeneratePickRankTexture_WideLabel_StaysInsideTheCanvas(string text)
        {
            var ink = InkBounds(VtexGenerator.GeneratePickRankTexture(text));

            Assert.That(ink, Is.Not.EqualTo(Rectangle.Empty));
            Assert.That(ink.Left, Is.GreaterThan(0), "cut on the left");
            Assert.That(ink.Right, Is.LessThan(VtexGenerator.PickRankTextureWidth), "cut on the right");
        }

        [Test]
        public void GeneratePickRankTexture_EveryLabelARequestCanProduce_IsDrawnWhole()
        {
            var labels = new System.Collections.Generic.HashSet<string>();
            foreach (var r in RankTierNamesTests.AllAcceptedRequests()) labels.Add(RankTierNames.English.Label(r));

            Assert.That(labels, Does.Contain("Not Calibrated").And.Contain("Rank 5,000").And.Contain("Immortal"));
            foreach (string label in labels)
            {
                var ink = InkBounds(VtexGenerator.GeneratePickRankTexture(label));
                Assert.That(ink, Is.Not.EqualTo(Rectangle.Empty), label);
                Assert.That(ink.Left, Is.GreaterThan(0), $"{label}: cut on the left");
                Assert.That(ink.Right, Is.LessThan(VtexGenerator.PickRankTextureWidth), $"{label}: cut on the right");
            }
        }

        [Test]
        public void GeneratePickRankTexture_IsCentredLikeValvesTextAlignCenter()
        {
            var ink = InkBounds(VtexGenerator.GeneratePickRankTexture("Divine V"));
            float centre = ink.Left + ink.Width / 2f;

            Assert.That(centre, Is.EqualTo(VtexGenerator.PickRankTextureWidth / 2f).Within(VtexGenerator.PickRankScale * 2));
        }

        [TestCase("神话")]
        [TestCase("초월자")]
        [TestCase("天梯第2,691名")]
        public void GeneratePickRankTexture_CjkText_StillDrawsGlyphs(string text) =>
            Assert.That(InkBounds(VtexGenerator.GeneratePickRankTexture(text)), Is.Not.EqualTo(Rectangle.Empty));

        #endregion
    }
}
