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
using System.IO;
using System.Linq;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroBadgeTextureTests
    {
        private static Bitmap Decode(byte[] vtex)
        {
            using var ms = new MemoryStream(vtex, VtexGenerator.TemplateHeaderSize, vtex.Length - VtexGenerator.TemplateHeaderSize);
            return new Bitmap(ms);
        }

        private static Rectangle WhiteInk(Bitmap bmp)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.A > 200 && c.R > 200 && c.G > 200 && c.B > 200)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }
            return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(30)]
        public void LargeLevel_DigitsLandAtTheCalibratedHeight_Centred(int level)
        {
            using var bmp = Decode(VtexGenerator.GenerateHeroLevelTexture(level, small: false));
            var ink = WhiteInk(bmp);

            Assert.That(bmp.Size, Is.EqualTo(new Size(256, 256)));
            Assert.That(ink.Height, Is.EqualTo(VtexGenerator.HeroLevelLargeInkPx).Within(3));
            Assert.That(ink.X + ink.Width / 2.0, Is.EqualTo(128).Within(3));
            Assert.That(ink.Y + ink.Height / 2.0, Is.EqualTo(128).Within(3));
        }

        [Test]
        public void SmallLevel_FillsTheCanvasWithoutClipping()
        {
            using var bmp = Decode(VtexGenerator.GenerateHeroLevelTexture(30, small: true));
            var ink = WhiteInk(bmp);

            Assert.That(ink.Height, Is.GreaterThan(150), "the small box needs every pixel of glyph");
            Assert.That(ink.Left, Is.GreaterThan(0));
            Assert.That(ink.Right, Is.LessThan(256));
        }

        [TestCase(0)]
        [TestCase(31)]
        public void Level_OutOfRange_Throws(int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroLevelTexture(level, small: false));
        }

        [TestCase(0)]
        [TestCase(37)]
        [TestCase(100)]
        public void Progress_FillsExactlyThePercent_LeavesTheRestClear(int percent)
        {
            using var bmp = Decode(VtexGenerator.GenerateHeroProgressTexture(percent, "#4b141d", "#fe8650"));
            int filledTo = (int)Math.Round(VtexGenerator.HeroProgressWidth * percent / 100.0);
            int mid = bmp.Height / 2;

            Assert.That(bmp.Size, Is.EqualTo(new Size(VtexGenerator.HeroProgressWidth, VtexGenerator.HeroProgressHeight)));
            if (filledTo > 0)
            {
                Assert.That(bmp.GetPixel(0, mid).A, Is.EqualTo(255), "fill starts at the left edge");
                Assert.That(bmp.GetPixel(filledTo - 1, mid).A, Is.EqualTo(255), "fill reaches the percent");
                Assert.That(bmp.GetPixel(filledTo - 1, mid).R, Is.GreaterThan(bmp.GetPixel(0, mid).R));
            }
            if (filledTo < bmp.Width)
                Assert.That(bmp.GetPixel(filledTo, mid).A, Is.EqualTo(0), "past the fill the track shows through");
        }

        [Test]
        public void Xp_IsTheLineAt4x_LeftAligned_TierColourThenGrey()
        {
            using var bmp = Decode(VtexGenerator.GenerateHeroXpTexture(3400, 6800, "#ddc165"));

            Assert.That(bmp.Size, Is.EqualTo(new Size(410 * 4, 20 * 4)));

            int firstInk = -1, lastInk = -1;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                    if (bmp.GetPixel(x, y).A > 128) { if (firstInk < 0) firstInk = x; lastInk = x; break; }

            Assert.That(firstInk, Is.InRange(0, 16), "Valve's labels flow from the left edge");
            Assert.That(lastInk, Is.LessThan(bmp.Width - 1), "the whole line fits the canvas");

            Color first = Enumerable.Range(0, bmp.Height).Select(y => bmp.GetPixel(firstInk + 4, y)).First(c => c.A > 200);
            Color last = Enumerable.Range(0, bmp.Height).Select(y => bmp.GetPixel(lastInk - 2, y)).First(c => c.A > 100);
            Assert.That(first.R, Is.GreaterThan(first.B + 60), "gold: red well above blue");
            Assert.That(Math.Abs(last.R - 0xB0), Is.LessThan(40), "grey");
        }

        [Test]
        public void Xp_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroXpTexture(10, 0, "#ffffff"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroXpTexture(9000, 8000, "#ffffff"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroXpTexture(-1, 8000, "#ffffff"));
        }

        [Test]
        public void Progress_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroProgressTexture(101, "#000000", "#ffffff"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VtexGenerator.GenerateHeroProgressTexture(-1, "#000000", "#ffffff"));
        }
    }
}
