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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls.Widgets
{
    /// <summary>
    /// Visual separator between misc option categories.
    /// Displays: ───── [ CATEGORY NAME ] ───── with black and white theme
    /// </summary>
    public class MiscSectionHeader : UserControl
    {
        private readonly string _text;
        
        public MiscSectionHeader(string categoryName)
        {
            _text = $"[ {categoryName.ToUpperInvariant()} ]";

            Height = 40;
            Margin = new Padding(0, 16, 0, 8);
            BackColor = Color.Transparent;
            DoubleBuffered = true;

            Resize += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var font = new Font("JetBrains Mono", 11F, FontStyle.Bold);
            
            // Measure text size
            var textSize = e.Graphics.MeasureString(_text, font);
            int textWidth = (int)textSize.Width;
            int textHeight = (int)textSize.Height;

            // Center the text
            int textX = (Width - textWidth) / 2;
            int textY = (Height - textHeight) / 2;

            int lineY = Height / 2;

            // Draw separator lines (grey)
            using var linePen = new Pen(Color.FromArgb(51, 51, 51), 1);
            
            // Left line
            if (textX - 16 > 20)
            {
                e.Graphics.DrawLine(linePen, 20, lineY, textX - 16, lineY);
            }

            // Right line
            int textRight = textX + textWidth;
            if (textRight + 16 < Width - 20)
            {
                e.Graphics.DrawLine(linePen, textRight + 16, lineY, Width - 20, lineY);
            }

            // Draw text (white)
            using var textBrush = new SolidBrush(Color.White);
            e.Graphics.DrawString(_text, font, textBrush, textX, textY);
        }
    }
}

