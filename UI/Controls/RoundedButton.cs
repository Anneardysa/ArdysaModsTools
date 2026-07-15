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
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    public class RoundedButton : Button
    {
        private int _borderRadius = 0;
        private Color _hoverBackColor;
        private Color _hoverForeColor = Color.Empty;
        private bool _isHovering = false;
        
        private System.Windows.Forms.Timer? _hoverTimer;
        private float _hoverProgress = 0f;
        private const float HOVER_SPEED = 0.15f;
        private Color _borderColor = Color.FromArgb(51, 51, 51);
        
        private bool _isHighlighted = false;
        private Color _highlightColor = Color.FromArgb(255, 255, 255);
        private System.Windows.Forms.Timer? _pulseTimer;
        private float _pulsePhase = 0f;
        private const float PULSE_SPEED = 0.15f;

        public int BorderRadius
        {
            get => _borderRadius;
            set { _borderRadius = value; Invalidate(); }
        }

        public Color HoverBackColor
        {
            get => _hoverBackColor;
            set => _hoverBackColor = value;
        }

        public Color HoverForeColor
        {
            get => _hoverForeColor;
            set => _hoverForeColor = value;
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public bool Highlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted == value) return;
                _isHighlighted = value;
                
                if (_isHighlighted)
                {
                    StartPulseAnimation();
                }
                else
                {
                    StopPulseAnimation();
                }
                Invalidate();
            }
        }

        public Color HighlightColor
        {
            get => _highlightColor;
            set { _highlightColor = value; Invalidate(); }
        }

        private void StartPulseAnimation()
        {
            if (_pulseTimer != null) return;
            
            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 50;
            _pulseTimer.Tick += (s, e) =>
            {
                _pulsePhase += PULSE_SPEED;
                if (_pulsePhase > Math.PI * 2) _pulsePhase -= (float)(Math.PI * 2);
                Invalidate();
            };
            _pulseTimer.Start();
        }

        private void StopPulseAnimation()
        {
            _pulseTimer?.Stop();
            _pulseTimer?.Dispose();
            _pulseTimer = null;
            _pulsePhase = 0f;
        }

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | 
                     ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            
            _hoverBackColor = Color.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            
            Color parentBgColor = Parent?.BackColor ?? Color.Transparent;
            using (SolidBrush clearBrush = new SolidBrush(parentBgColor))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }
            
            Color bgColor;
            Color textColor;
            Color currentBorderColor;
            
            if (!Enabled)
            {
                bgColor = Color.FromArgb(10, 10, 10);
                textColor = Color.FromArgb(68, 68, 68);
                currentBorderColor = Color.FromArgb(34, 34, 34);
            }
            else
            {
                Color normalBg = BackColor;
                Color hoverBg = _hoverBackColor != Color.Empty ? _hoverBackColor : BackColor;
                bgColor = InterpolateColor(normalBg, hoverBg, _hoverProgress);
                
                Color normalText = ForeColor;
                Color hoverText = _hoverForeColor != Color.Empty ? _hoverForeColor : Color.FromArgb(255, 255, 255);
                textColor = InterpolateColor(normalText, hoverText, _hoverProgress);
                
                currentBorderColor = InterpolateColor(_borderColor, Color.FromArgb(255, 255, 255), _hoverProgress);
            }

            int actualRadius = _borderRadius < 0 ? Height / 2 : _borderRadius;

            using (GraphicsPath path = CreateRoundedRectangle(rect, actualRadius))
            {
                if (_isHighlighted && Enabled)
                {
                    float pulse = (float)(0.7 + 0.3 * Math.Sin(_pulsePhase));
                    int glowAlpha = (int)(255 * pulse);
                    
                    for (int i = 0; i < 4; i++)
                    {
                        int inset = i;
                        Rectangle glowRect = new Rectangle(inset, inset, Width - 1 - (inset * 2), Height - 1 - (inset * 2));
                        int glowRadius = Math.Max(1, actualRadius - inset);
                        int alpha = (int)(glowAlpha * (1.0f - (i * 0.2f)));
                        alpha = Math.Clamp(alpha, 0, 255);
                        
                        using (GraphicsPath glowPath = CreateRoundedRectangle(glowRect, glowRadius))
                        using (Pen glowPen = new Pen(Color.FromArgb(alpha, _highlightColor), 1.5f))
                        {
                            g.DrawPath(glowPen, glowPath);
                        }
                    }
                }

                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }

                if (_isHighlighted && Enabled)
                {
                    float pulse = (float)(0.7 + 0.3 * Math.Sin(_pulsePhase));
                    int borderAlpha = (int)(255 * pulse);
                    using (Pen highlightPen = new Pen(Color.FromArgb(borderAlpha, _highlightColor), 2))
                    {
                        g.DrawPath(highlightPen, path);
                    }
                }
                else
                {
                    using (Pen pen = new Pen(currentBorderColor, 1))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, Text, Font, rect, textColor, flags);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovering = true;
            StartHoverAnimation();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovering = false;
            StartHoverAnimation();
            base.OnMouseLeave(e);
        }

        private void StartHoverAnimation()
        {
            if (_hoverTimer == null)
            {
                _hoverTimer = new System.Windows.Forms.Timer();
                _hoverTimer.Interval = 16;
                _hoverTimer.Tick += (s, e) =>
                {
                    if (_isHovering)
                    {
                        _hoverProgress = Math.Min(1f, _hoverProgress + HOVER_SPEED);
                    }
                    else
                    {
                        _hoverProgress = Math.Max(0f, _hoverProgress - HOVER_SPEED);
                    }
                    
                    if (_hoverProgress == 0f || _hoverProgress == 1f)
                    {
                        _hoverTimer?.Stop();
                    }
                    Invalidate();
                };
            }
            _hoverTimer.Start();
        }

        private static Color InterpolateColor(Color from, Color to, float progress)
        {
            int r = (int)(from.R + (to.R - from.R) * progress);
            int g = (int)(from.G + (to.G - from.G) * progress);
            int b = (int)(from.B + (to.B - from.B) * progress);
            return Color.FromArgb(r, g, b);
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            
            int diameter = radius * 2;
            
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;
            
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);

            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }
}

