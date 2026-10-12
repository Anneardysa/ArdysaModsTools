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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using ArdysaModsTools.Core.Constants;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public static class VtexGenerator
    {
        public const int TemplateHeaderSize = 2068;
        public const int WidthOffset = 1012;
        public const int HeightOffset = 1014;

        #region Panorama geometry

        public const int TextureWidth = 512;
        public const int TextureHeight = 128;

        public const float LeaderboardCssFontPx = 14f;
        public const float LeaderboardCssLetterSpacingPx = 1f;
        public const float LeaderboardCssShadowOffsetPx = 1f;

        public const float MmrCssFontPx = 20f;

        public const int LeaderboardCssBoxHeightPx = 15;
        public const int MmrCssBoxHeightPx = 22;

        public const int MmrCssBoxWidthPx = 88;

        public const int LeaderboardCssBoxWidthPx = 60;

        public static string FormatNumber(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private const string LeaderboardFontFace = "radiance-semibold.otf";
        private const FontStyle LeaderboardFallbackStyle = FontStyle.Bold;

        private const string MmrFontFace = "radiance-regular.otf";
        private const FontStyle MmrFallbackStyle = FontStyle.Regular;

        public static float ToTexturePx(float cssPx, int cssBoxHeightPx)
            => cssPx * TextureHeight / cssBoxHeightPx;

        #endregion

        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly byte[] HeaderTemplate = Convert.FromBase64String(
            "FAgAAAwAAQAIAAAAAgAAAEVKTUscAAAAoQMAAERBVEHAAwAANAQAAAAAAAAAAAAABDNWS3wWEnTp" +
            "BphGr/LmPrWQN+cBAAAAAAAAQAMAAACTAAAAAAAAAJ0DAAATAAcA8QUAAFkDAAAAAAAAAAAAAAAA" +
            "AAAAAAAApgILBAAtAAAACgABAPMGAQAAAAYAAAACAAAAAwAAAAQAAAAFFADXENNmMQcAAAAIAAAA" +
            "CSgAGwooAAgkABMLHADQDAAAAA0AAAAOAAAAD0wAhwAAALVE5JURIAATEiAAExMgAAgcAB8UHAAI" +
            "HxUcAAgfFhwACBMXHAAuGAAcAB8ZHAAIGxocAFMAAIBJEQgABCQAHxtAAAgTAxwAHxwcAAgXDxwA" +
            "Ex0gANMeAAAAHwAAACAAAAAhIAAAYAEXIiAAGyMgAAgcAB8kHAAIHiUcAAGMABMmGAITJwgAEygI" +
            "ABMpLAL/HSoAAAArAAAALAAAAG1fSW5wdXREZXBlbmRlbmNpZXMAbV9BZGRpdGlvbmFsHgAB+hpS" +
            "ZWxhdGl2ZUZpbGVuYW1lAHBhbm9yYW1hL2ltYWdlcy9oZXJvX2JhZAwA8BJfcmFua181LnBuZwBt" +
            "X1NlYXJjaFBhdGgAZG90YQBtX25QAJJDUkMAbV9iT3B/AAAMAAAXAGBFeGlzdHMOAFFJc0dhbXsA" +
            "D3cAHDB0eHTaAGxyZ3VtZW7TAKBQYXJhbWV0ZXJO0ACyX19fT3ZlcnJpZGUAAWhhdGFfX18oAPID" +
            "VHlwZQBCaW5hcnlCbG9iQXJnwgCbbmdlcnByaW50DwDwAERlZmF1bHQAZXRjAEludDAAcGZhc3QA" +
            "ZmnmAPAQaHVlU2hpZnRGaXh1cABsaWdodG1hcE1heFJlc29sdQwBYQBGbG9hdGgA8AVheGNvbXBp" +
            "bGVkdGV4dHVyZXJlcxYAtG1pcHNpemUAbWluCwAQSRoBAp0Ae1NwZWNpYWz5AIJTdHJpbmcAVEkA" +
            "IiBDWQBgciBWZXJzdgAkbV8TALNJZGVudGlmaWVyABMAAzUAAOoAQFVzZXImAQ9JAAYRIIoAYCBE" +
            "aWxhdB4AP2xvciwAB4VNaXAgTm9uZSIA6UVuY29kZSBRdWFsaXR5oQIBjQIgZWQRAhBzrgBAaGls" +
            "ZDYBcHVyY2VMaXOIAbVXZWFrUmVmZXJlbhQAAn8CRWFibGW7AClJcz0AACUAdXViYXNzZXQ9ABZz" +
            "FQAA0gEhaW6FAPEDcwAJCBgJBgYMDg0OBgYPDQ4ODgBPDwYGDwQABCgMDBAABC4AFBAEAMAMCAgI" +
            "CRABAQDd7v8AAAAAAAAAAAAAAAAAAAABAIgA0NUBP8lLxj6O1KA+m/j0PgACgAABABABAAAAAAgA" +
            "AAABAAAAAQAAAAgAAAAABAAAAQBJkiRJkiT//+AgAPn5+QEASZIkSZIk3/cnUgBPT08BAEmSJEmS" +
            "JP3/QnMAP39//wBJkgRJkAD+/6U5AMCVtf8ASYIkSAIk/f9FKQBVVJQBAEmSJEmSJP7/BkoAdXV1" +
            "AQBJkiRJkiT//4NSAPn5+QEASZIkSZIk///v3gAtLS0uvrb92/nh/zHvgBjh50kN/wBJkiRJgiTa" +
            "xmQ5SkpKTwiRtm07xp//2P8gCPp6BgH/AAkAAAAAALn34EgHAACP/wBAAgAAAADa92J6kAAA2jlT" +
            "th3bv/0fWu8gCP/90ED/AEmSJEmSBDTXZDGxsbGRKZa2Yf/99d+19yAQQvpxGAcI/+//xm//6+1A" +
            "GD2F1T2Epz/y/////y3WJQBQgyAMAP9JkiRJkiTZ18N4Upb7VAD/SZIkSZIk3L6ioIpWlSEA/0mS" +
            "JEmSJBvHo5jglVZAAP9JkiRJkiT35gFQDZarHTTNyP//////j9YkCAXACADp9f/x34/92/r/oSh/" +
            "GxUWfQBJkCRJkiQqhCIAcfn9fQD/SZIkSZIkyf0FGSspKSkA/0mSJEmSJGLyx2ikKi2tAP9JkiRJ" +
            "kiT6/8bYDy2tVQD/SZIkSZIkXv/r4Vh4XlUA/0mSJEmSJOP7JqGVllRWAP9JkiRJkiSJ1Y0qoEBA" +
            "QD4ASJIkSZIk//9lKRUVFRUBAEmSJEmSJGUpAACBAQEBfO7////5b/xFy+QICQkNlQD/SZIkSZIk" +
            "3P+DqV1UXVUA/0mSJEmSJAPxIIkCAintAP9JkiRJkiRD8gBxgKr/3QD/SZIkSZIkW/8ioXUddVUm" +
            "rv/////zw2b85BhoelpXAQBJkiRJkiT//4YxFRUVFQEASZIkSZIkZikAAAEBAQFQUTZu27Zt24Yp" +
            "QQiaSgAA/3AAAAAAEADM/CEg5fU9vQD/SZIkSZIkPO+jeVYKVQIA/0mSJEmSJJz/AnH14NWg/wYA" +
            "AAAAACCn82IQc1hYWAoLh23btm3bhjFBAIaDAAABAEmSJEmSJP//hjEVFRUVAQBJkiRJkiSGMQAA" +
            "qQEBAQEASZIkSZIkpjFFKVWqqqpTVP7v/zZu20ezhAAFF1///wAAAAAAEACJ/OM4ijqlvf8AAAAA" +
            "AAAgRtMEIQAKUFgiI//934dt20ezhADQ1PX/AQBJkiRJkiSGMWUpqqqqqgEASZIkSZIk//+GMRUV" +
            "FRUBAEmSJEmSJP//JCFVVVUAAQBJkiRJkiT//4YxVVVVAAEASZIkSZIk//9lMVVVVQC9vvZv47Zv" +
            "27z/ADD33/8APj+/fdi3bdv+/yQ5V1dVAAEASZIkSZIk//+GMVVVVQABAEmSJEmSJP//hjFVVVUA" +
            "AQBJkiRJkiT//4YxFRUVAA==");

        public const int MaxRenderableMmr = 99_999;

        public static byte[] GenerateMmrTexture(int mmr, string? dotaRoot = null)
        {
            if (mmr < 0 || mmr > MaxRenderableMmr)
                throw new ArgumentOutOfRangeException(nameof(mmr), mmr, $"MMR must be between 0 and {MaxRenderableMmr:N0}.");

            string text = FormatNumber(mmr);

            using var bmp = new Bitmap(TextureWidth, TextureHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                PrepareCanvas(g);

                using var font = ResolveFont(ToTexturePx(MmrCssFontPx, MmrCssBoxHeightPx), dotaRoot,
                    MmrFontFace, MmrFallbackStyle);

                DrawCentredRun(g, text, font.Font, Color.FromArgb(220, 208, 135), 0f, 0f);
            }

            return WrapPng(SaveToPng(bmp), TextureWidth, TextureHeight);
        }

        public static byte[] GenerateConfidenceTexture(int confidence)
        {
            if (confidence < 0 || confidence > RankOverridePlan.MaxConfidence)
                throw new ArgumentOutOfRangeException(nameof(confidence), confidence, $"Confidence must be between 0 and {RankOverridePlan.MaxConfidence}%.");

            const int width = 450;
            const int height = 44;
            int filledWidth = (int)Math.Round(width * (confidence / 100.0));

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                using var bgBrush = new SolidBrush(Color.FromArgb(42, 33, 14));
                g.FillRectangle(bgBrush, 0, 0, width, height);

                if (filledWidth > 0)
                {
                    using var gradBrush = new LinearGradientBrush(
                        new Point(0, 0),
                        new Point(0, (int)(height * 0.8)),
                        Color.FromArgb(153, 114, 57),
                        Color.FromArgb(164, 141, 75));
                    g.FillRectangle(gradBrush, 0, 0, filledWidth, height);
                }

                using var font = FindBestFont(28, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.White);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString($"{confidence}%", font, textBrush, new RectangleF(0, 0, width, height), sf);
            }

            return WrapPng(SaveToPng(bmp), width, height);
        }

        public static byte[] GenerateLeaderboardRankTexture(int rank, bool isImmortalLeaderboard, string? dotaRoot = null)
        {
            using var bmp = new Bitmap(TextureWidth, TextureHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);

                if (isImmortalLeaderboard)
                {
                    if (rank <= 0 || rank > 999999)
                        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Leaderboard rank must be between 1 and 999,999.");

                    PrepareCanvas(g);

                    string text = FormatNumber(rank);
                    float tracking = ToTexturePx(LeaderboardCssLetterSpacingPx, LeaderboardCssBoxHeightPx);
                    float shadow = ToTexturePx(LeaderboardCssShadowOffsetPx, LeaderboardCssBoxHeightPx);

                    using var font = ResolveFont(ToTexturePx(LeaderboardCssFontPx, LeaderboardCssBoxHeightPx), dotaRoot,
                        LeaderboardFontFace, LeaderboardFallbackStyle);

                    DrawCentredRun(g, text, font.Font, Color.FromArgb(247, 229, 195), tracking, shadow);
                }
            }

            return WrapPng(SaveToPng(bmp), TextureWidth, TextureHeight);
        }

        private static void DrawCentredRun(Graphics g, string text, Font font, Color color,
            float trackingPx, float shadowPx)
        {
            var fmt = StringFormat.GenericTypographic;

            var advances = new float[text.Length];
            float runWidth = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                advances[i] = g.MeasureString(text[i].ToString(), font, PointF.Empty, fmt).Width;
                runWidth += advances[i];
            }
            runWidth += trackingPx * (text.Length - 1);

            float left = (TextureWidth - runWidth) / 2f;
            float top = BaselineTop(font);

            if (shadowPx > 0f)
            {
                using var shadowBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0));
                Run(g, text, font, shadowBrush, advances, trackingPx, left + shadowPx, top + shadowPx, fmt);
            }

            using var brush = new SolidBrush(color);
            Run(g, text, font, brush, advances, trackingPx, left, top, fmt);
        }

        private static void Run(Graphics g, string text, Font font, Brush brush, float[] advances,
            float trackingPx, float x, float y, StringFormat fmt)
        {
            for (int i = 0; i < text.Length; i++)
            {
                g.DrawString(text[i].ToString(), font, brush, x, y, fmt);
                x += advances[i] + trackingPx;
            }
        }

        private static float BaselineTop(Font font)
        {
            var family = font.FontFamily;
            float em = family.GetEmHeight(font.Style);
            float ascent = font.Size * family.GetCellAscent(font.Style) / em;
            float descent = font.Size * family.GetCellDescent(font.Style) / em;

            return TextureHeight - descent - ascent;
        }

        private static void PrepareCanvas(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
        }

        #region Picking phase label

        public const float PickRankCssFontPx = 14f;
        public const float PickRankCssPaddingTopPx = 2f;

        public const int PickRankCssBoxHeightPx = 19;

        public const int PickRankCssBoxWidthPx = 90;

        public const int PickRankScale = 4;
        public const int PickRankTextureWidth = PickRankCssBoxWidthPx * PickRankScale;
        public const int PickRankTextureHeight = PickRankCssBoxHeightPx * PickRankScale;

        public static byte[] GeneratePickRankTexture(string text, string? dotaRoot = null)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            const int w = PickRankTextureWidth, h = PickRankTextureHeight;
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (text.Length > 0)
                {
                    PrepareCanvas(g);
                    var fmt = StringFormat.GenericTypographic;
                    float size = PickRankCssFontPx * PickRankScale;

                    using var font = NeedsSystemScript(text)
                        ? ResolvedFont.System(FindBestFont(size, FontStyle.Regular, CjkFaces))
                        : ResolveFont(size, dotaRoot, MmrFontFace, MmrFallbackStyle);
                    Font face = font.Font;
                    using Font? shrunk = FitWidth(g, text, face, fmt, w - 2 * PickRankScale);
                    face = shrunk ?? face;

                    float width = g.MeasureString(text, face, PointF.Empty, fmt).Width;
                    using var brush = new LinearGradientBrush(new Point(0, 0), new Point(0, h + 1),
                        Color.FromArgb(0xC5, 0xA9, 0x7A), Color.FromArgb(0x44, 0xC5, 0xA9, 0x7A));
                    g.DrawString(text, face, brush, (w - width) / 2f, PickRankCssPaddingTopPx * PickRankScale, fmt);
                }
            }

            return WrapPng(SaveToPng(bmp), w, h);
        }

        private static Font? FitWidth(Graphics g, string text, Font font, StringFormat fmt, float maxWidth)
        {
            float width = g.MeasureString(text, font, PointF.Empty, fmt).Width;
            return width <= maxWidth ? null : new Font(font.FontFamily, font.Size * maxWidth / width, font.Style, GraphicsUnit.Pixel);
        }

        private static readonly string[] CjkFaces =
            { "Microsoft YaHei UI", "Microsoft JhengHei UI", "Yu Gothic UI", "Malgun Gothic", "Leelawadee UI", "Segoe UI" };

        private static bool NeedsSystemScript(string text)
        {
            foreach (char c in text)
                if ((c >= '฀' && c <= '๿') || c >= '⺀') return true;
            return false;
        }

        #endregion

        #region Hero badge

        public const int HeroLevelCanvas = 256;

        public const int HeroLevelLargeInkPx = 56;

        public const int HeroLevelSmallInkPx = 190;

        private const string HeroLevelFontFace = "radiance-bold.otf";

        public static byte[] GenerateHeroLevelTexture(int level, bool small, string? dotaRoot = null)
        {
            if (level < HeroBadgePlan.MinLevel || level > HeroBadgePlan.MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    $"Level must be {HeroBadgePlan.MinLevel}-{HeroBadgePlan.MaxLevel}.");

            const int size = HeroLevelCanvas;
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                PrepareCanvas(g);

                using var font = ResolveFont(100f, dotaRoot, HeroLevelFontFace, FontStyle.Bold);
                using var path = new GraphicsPath();
                path.AddString(level.ToString(CultureInfo.InvariantCulture), font.Font.FontFamily,
                    (int)font.Font.Style, 100f, PointF.Empty, StringFormat.GenericTypographic);

                var ink = path.GetBounds();
                float inkHeight = small ? HeroLevelSmallInkPx : HeroLevelLargeInkPx;
                float maxWidth = small ? size - 10 : size / 2f;
                float scale = Math.Min(inkHeight / ink.Height, maxWidth / ink.Width);
                using (var m = new Matrix())
                {
                    m.Translate(size / 2f, size / 2f);
                    m.Scale(scale, scale);
                    m.Translate(-(ink.X + ink.Width / 2f), -(ink.Y + ink.Height / 2f));
                    path.Transform(m);
                }

                var halo = small
                    ? new[] { (Width: 26f, Alpha: 90), (Width: 16f, Alpha: 170) }
                    : new[] { (Width: 34f, Alpha: 28), (Width: 24f, Alpha: 45), (Width: 14f, Alpha: 80) };
                foreach (var (width, alpha) in halo)
                {
                    using var pen = new Pen(Color.FromArgb(alpha, 0, 0, 0), width) { LineJoin = LineJoin.Round };
                    g.DrawPath(pen, path);
                }

                using var fill = new SolidBrush(Color.White);
                g.FillPath(fill, path);
            }

            return WrapPng(SaveToPng(bmp), size, size);
        }

        public const int HeroProgressWidth = 1024;
        public const int HeroProgressHeight = 16;

        public static byte[] GenerateHeroProgressTexture(int percent, string darkHex, string lightHex)
        {
            if (percent < HeroBadgePlan.MinProgress || percent > HeroBadgePlan.MaxProgress)
                throw new ArgumentOutOfRangeException(nameof(percent), percent,
                    $"Progress must be {HeroBadgePlan.MinProgress}-{HeroBadgePlan.MaxProgress}.");

            int filled = (int)Math.Round(HeroProgressWidth * (percent / 100.0));

            using var bmp = new Bitmap(HeroProgressWidth, HeroProgressHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (filled > 0)
                {
                    using var brush = new LinearGradientBrush(
                        new Point(0, 0), new Point(filled + 1, 0),
                        ColorTranslator.FromHtml(darkHex), ColorTranslator.FromHtml(lightHex));
                    g.FillRectangle(brush, 0, 0, filled, HeroProgressHeight);
                }
            }

            return WrapPng(SaveToPng(bmp), HeroProgressWidth, HeroProgressHeight);
        }

        public const int HeroXpScale = 4;
        public const int HeroXpWidth = 410 * HeroXpScale;
        public const int HeroXpHeight = 20 * HeroXpScale;
        private const float HeroXpFontPx = 16f * HeroXpScale;
        private const float HeroXpGapPx = 4f * HeroXpScale;

        public static byte[] GenerateHeroXpTexture(int have, int need, string tierColorHex, string? dotaRoot = null)
        {
            if (need <= 0 || need > MaxRenderableMmr) throw new ArgumentOutOfRangeException(nameof(need));
            if (have < 0 || have > need) throw new ArgumentOutOfRangeException(nameof(have));

            using var bmp = new Bitmap(HeroXpWidth, HeroXpHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                PrepareCanvas(g);

                using var strong = ResolveFont(HeroXpFontPx, dotaRoot, "radiance-semibold.otf", FontStyle.Bold);
                using var plain = ResolveFont(HeroXpFontPx, dotaRoot, MmrFontFace, MmrFallbackStyle);
                using var fmt = (StringFormat)StringFormat.GenericTypographic.Clone();
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                var grey = Color.FromArgb(204, 0xB0, 0xBC, 0xC2);
                var runs = new (string Text, Font Font, Color Color)[]
                {
                    ($"{FormatNumber(have)} XP", strong.Font, ColorTranslator.FromHtml(tierColorHex)),
                    ("/", plain.Font, grey),
                    ($"{FormatNumber(need)} XP", plain.Font, grey),
                };

                float x = 0f;
                for (int i = 0; i < runs.Length; i++)
                {
                    var (text, font, color) = runs[i];
                    var family = font.FontFamily;
                    float em = family.GetEmHeight(font.Style);
                    float ascent = font.Size * family.GetCellAscent(font.Style) / em;
                    float descent = font.Size * family.GetCellDescent(font.Style) / em;
                    float top = (HeroXpHeight - (ascent + descent)) / 2f;

                    using var brush = new SolidBrush(color);
                    g.DrawString(text, font, brush, x, top, fmt);
                    x += g.MeasureString(text, font, PointF.Empty, fmt).Width + HeroXpGapPx;
                }
            }

            return WrapPng(SaveToPng(bmp), HeroXpWidth, HeroXpHeight);
        }

        #endregion

        public static byte[] GenerateEmptyPipTexture()
        {
            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            bmp.SetPixel(0, 0, Color.Transparent);
            return WrapPng(SaveToPng(bmp), 1, 1);
        }

        public static byte[] WrapPng(byte[] pngBytes, int width, int height)
        {
            if (pngBytes is null || pngBytes.Length == 0)
                throw new ArgumentNullException(nameof(pngBytes));

            if (width <= 0 || width > 8192)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be 1-8192.");

            if (height <= 0 || height > 8192)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be 1-8192.");

            if (!IsValidPng(pngBytes))
                throw new ArgumentException("Payload is not a valid PNG image.", nameof(pngBytes));

            var result = new byte[TemplateHeaderSize + pngBytes.Length];
            Buffer.BlockCopy(HeaderTemplate, 0, result, 0, TemplateHeaderSize);

            result[WidthOffset] = (byte)(width & 0xFF);
            result[WidthOffset + 1] = (byte)((width >> 8) & 0xFF);
            result[HeightOffset] = (byte)(height & 0xFF);
            result[HeightOffset + 1] = (byte)((height >> 8) & 0xFF);

            Buffer.BlockCopy(pngBytes, 0, result, TemplateHeaderSize, pngBytes.Length);
            return result;
        }

        private static bool IsValidPng(byte[] data)
        {
            if (data.Length < PngMagic.Length) return false;
            for (int i = 0; i < PngMagic.Length; i++)
            {
                if (data[i] != PngMagic[i]) return false;
            }
            return true;
        }

        private static byte[] SaveToPng(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        private sealed class ResolvedFont : IDisposable
        {
            public required Font Font { get; init; }
            public required string FamilyName { get; init; }
            private PrivateFontCollection? Collection { get; init; }

            public static ResolvedFont System(Font font) => new() { Font = font, FamilyName = font.Name };

            public static ResolvedFont Private(Font font, PrivateFontCollection collection)
                => new() { Font = font, FamilyName = font.Name, Collection = collection };

            public void Dispose()
            {
                Font.Dispose();
                Collection?.Dispose();
            }
        }

        private static ResolvedFont ResolveFont(float sizePx, string? dotaRoot, string faceFile, FontStyle fallbackStyle)
        {
            if (!string.IsNullOrWhiteSpace(dotaRoot))
            {
                string face = Path.Combine(dotaRoot,
                    DotaPaths.PanoramaFonts.Replace('/', Path.DirectorySeparatorChar), faceFile);

                if (File.Exists(face))
                {
                    PrivateFontCollection? collection = null;
                    try
                    {
                        collection = new PrivateFontCollection();
                        collection.AddFontFile(face);
                        if (collection.Families.Length > 0)
                        {
                            var font = new Font(collection.Families[0], sizePx, FontStyle.Regular, GraphicsUnit.Pixel);
                            return ResolvedFont.Private(font, collection);
                        }
                    }
                    catch
                    {
                    }
                    collection?.Dispose();
                }
            }

            return ResolvedFont.System(FindBestFont(sizePx, fallbackStyle));
        }

        private static readonly string[] LatinFaces = { "Radiance", "Segoe UI", "Arial", "Tahoma", "Trebuchet MS" };

        private static Font FindBestFont(float size, FontStyle style, string[]? preferred = null)
        {
            foreach (var name in preferred ?? LatinFaces)
            {
                try
                {
                    var font = new Font(name, size, style, GraphicsUnit.Pixel);
                    if (string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase))
                        return font;
                    font.Dispose();
                }
                catch
                {
                }
            }

            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
        }
    }
}
