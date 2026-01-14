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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// Custom retro terminal control with CRT-style effects: scanlines and text glow.
    /// </summary>
    public class RetroTerminal : UserControl
    {
        private readonly List<TerminalLine> _lines = new();
        private readonly object _lock = new();
        private int _scrollOffset = 0;
        private float _lineHeight = 18f;
        private bool _autoScroll = true;
        
        // Visual settings
        public bool EnableScanlines { get; set; } = true;
        public bool EnableGlow { get; set; } = true;
        public int ScanlineSpacing { get; set; } = 3;
        public int ScanlineAlpha { get; set; } = 15;
        public int GlowRadius { get; set; } = 1;
        public int MaxLines { get; set; } = 500;
        
        private static readonly Color BgColor = Color.Black;
        private static readonly Color ScanlineColor = Color.FromArgb(30, 0, 0, 0);
        
        // Cyberpunk colors
        public static readonly Color CyberCyan = Color.FromArgb(0, 255, 255);
        public static readonly Color CyberGreen = Color.FromArgb(0, 255, 65);
        public static readonly Color CyberRed = Color.FromArgb(255, 50, 50);
        public static readonly Color CyberOrange = Color.FromArgb(255, 150, 0);
        public static readonly Color CyberWhite = Color.FromArgb(220, 220, 220);
        public static readonly Color CyberGrey = Color.FromArgb(150, 150, 150);

        private Font? _terminalFont;
        private VScrollBar _scrollBar;
        
        public RetroTerminal()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.UserPaint | 
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            
            BackColor = BgColor;
            
            // Create scrollbar
            _scrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _scrollBar.Scroll += (s, e) =>
            {
                _scrollOffset = _scrollBar.Value;
                _autoScroll = (_scrollOffset >= _scrollBar.Maximum - _scrollBar.LargeChange);
                Invalidate();
            };
            Controls.Add(_scrollBar);
            
            // Mouse wheel handling
            MouseWheel += OnMouseWheel;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _terminalFont = Font;
            using var g = CreateGraphics();
            _lineHeight = g.MeasureString("M", Font).Height + 4;
            UpdateScrollbar();
            Invalidate();
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            int delta = e.Delta > 0 ? -3 : 3;
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset + delta, Math.Max(0, _lines.Count - VisibleLineCount)));
            _autoScroll = (_scrollOffset >= _lines.Count - VisibleLineCount);
            _scrollBar.Value = Math.Min(_scrollOffset, _scrollBar.Maximum);
            Invalidate();
        }

        private int VisibleLineCount => Math.Max(1, (int)((Height - 8) / _lineHeight));

        public void AppendLine(string text, Color color)
        {
            lock (_lock)
            {
                _lines.Add(new TerminalLine(text, color));
                
                // Trim old lines
                while (_lines.Count > MaxLines)
                    _lines.RemoveAt(0);
            }
            
            if (_autoScroll)
            {
                _scrollOffset = Math.Max(0, _lines.Count - VisibleLineCount);
            }
            
            BeginInvoke((Action)(() =>
            {
                UpdateScrollbar();
                Invalidate();
            }));
        }

        public void Clear()
        {
            lock (_lock)
            {
                _lines.Clear();
            }
            _scrollOffset = 0;
            UpdateScrollbar();
            Invalidate();
        }

        /// <summary>
        /// Gets all text from terminal for clipboard operations.
        /// </summary>
        public string GetAllText()
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _lines.Select(l => l.Text));
            }
        }

        private void UpdateScrollbar()
        {
            int totalLines = _lines.Count;
            int visible = VisibleLineCount;
            
            if (totalLines > visible)
            {
                _scrollBar.Visible = true;
                _scrollBar.Minimum = 0;
                _scrollBar.Maximum = totalLines;
                _scrollBar.LargeChange = visible;
                _scrollBar.SmallChange = 1;
                _scrollBar.Value = Math.Min(_scrollOffset, Math.Max(0, totalLines - visible));
            }
            else
            {
                _scrollBar.Visible = false;
                _scrollOffset = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            
            using (var bgBrush = new SolidBrush(BgColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }
            
            // Draw text lines
            var font = _terminalFont ?? Font;
            float y = 4;
            int startLine = _scrollOffset;
            int endLine = Math.Min(_lines.Count, startLine + VisibleLineCount + 1);
            
            lock (_lock)
            {
                for (int i = startLine; i < endLine; i++)
                {
                    if (i >= 0 && i < _lines.Count)
                    {
                        var line = _lines[i];
                        DrawGlowText(g, line.Text, font, line.Color, 8, y);
                    }
                    y += _lineHeight;
                }
            }
            
            // Draw scanlines overlay
            if (EnableScanlines)
            {
                DrawScanlines(g);
            }
        }

        private void DrawGlowText(Graphics g, string text, Font font, Color color, float x, float y)
        {
            if (EnableGlow && GlowRadius > 0)
            {
                // Draw glow (multiple offset layers with transparency)
                using var glowBrush = new SolidBrush(Color.FromArgb(40, color));
                for (int ox = -GlowRadius; ox <= GlowRadius; ox++)
                {
                    for (int oy = -GlowRadius; oy <= GlowRadius; oy++)
                    {
                        if (ox != 0 || oy != 0)
                        {
                            g.DrawString(text, font, glowBrush, x + ox, y + oy);
                        }
                    }
                }
            }
            
            // Draw main text
            using var textBrush = new SolidBrush(color);
            g.DrawString(text, font, textBrush, x, y);
        }

        private void DrawScanlines(Graphics g)
        {
            using var scanlineBrush = new SolidBrush(Color.FromArgb(ScanlineAlpha, 0, 0, 0));
            for (int y = 0; y < Height; y += ScanlineSpacing * 2)
            {
                g.FillRectangle(scanlineBrush, 0, y, Width, ScanlineSpacing);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _terminalFont?.Dispose();
                _scrollBar?.Dispose();
            }
            base.Dispose(disposing);
        }

        private class TerminalLine
        {
            public string Text { get; }
            public Color Color { get; }
            
            public TerminalLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }
    }
}

