using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// A button with smooth anti-aliased rounded corners
    /// </summary>
    public class RoundedButton : Button
    {
        private int _borderRadius = 0; // Default to sharp corners for minimal theme
        private Color _hoverBackColor;
        private Color _hoverForeColor = Color.Empty; // Hover text color (for inversion effect)
        private bool _isHovering = false;
        
        // Hover animation fields
        private System.Windows.Forms.Timer? _hoverTimer;
        private float _hoverProgress = 0f;
        private const float HOVER_SPEED = 0.15f;
        private Color _borderColor = Color.FromArgb(51, 51, 51); // #333333
        
        // Highlight/glow effect fields
        private bool _isHighlighted = false;
        private Color _highlightColor = Color.FromArgb(255, 255, 255); // White glow for minimal theme
        private System.Windows.Forms.Timer? _pulseTimer;
        private float _pulsePhase = 0f;
        private const float PULSE_SPEED = 0.15f;

        /// <summary>
        /// Border radius in pixels. Set to -1 for maximum (pill shape) or 0 for no rounding.
        /// </summary>
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

        /// <summary>
        /// Text color when hovering. If Empty, uses white.
        /// </summary>
        public Color HoverForeColor
        {
            get => _hoverForeColor;
            set => _hoverForeColor = value;
        }

        /// <summary>
        /// Border color for the button (default: #333333)
        /// </summary>
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        /// <summary>
        /// When true, shows a pulsing glow border to attract attention
        /// </summary>
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

        /// <summary>
        /// Color of the highlight glow effect
        /// </summary>
        public Color HighlightColor
        {
            get => _highlightColor;
            set { _highlightColor = value; Invalidate(); }
        }

        private void StartPulseAnimation()
        {
            if (_pulseTimer != null) return;
            
            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 50; // ~20fps
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
            // Required for custom painting and transparency
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
            
            // Clear the background with parent's color to remove dark rectangle
            Color parentBgColor = Parent?.BackColor ?? Color.Transparent;
            using (SolidBrush clearBrush = new SolidBrush(parentBgColor))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }
            
            // Determine colors based on enabled/hover state with smooth animation
            Color bgColor;
            Color textColor;
            Color currentBorderColor;
            
            if (!Enabled)
            {
                // Disabled state - greyed out
                bgColor = Color.FromArgb(10, 10, 10);
                textColor = Color.FromArgb(68, 68, 68);
                currentBorderColor = Color.FromArgb(34, 34, 34);
            }
            else
            {
                // Interpolate colors based on hover progress
                Color normalBg = BackColor;
                Color hoverBg = _hoverBackColor != Color.Empty ? _hoverBackColor : BackColor;
                bgColor = InterpolateColor(normalBg, hoverBg, _hoverProgress);
                
                // Interpolate text color (use HoverForeColor if set, otherwise white)
                Color normalText = ForeColor;
                Color hoverText = _hoverForeColor != Color.Empty ? _hoverForeColor : Color.FromArgb(255, 255, 255);
                textColor = InterpolateColor(normalText, hoverText, _hoverProgress);
                
                // Interpolate border color (always to white on hover for good contrast)
                currentBorderColor = InterpolateColor(_borderColor, Color.FromArgb(255, 255, 255), _hoverProgress);
            }

            // Calculate actual radius (-1 = max/pill shape, 0 = no rounding)
            int actualRadius = _borderRadius < 0 ? Height / 2 : _borderRadius;

            // Create rounded rectangle path
            using (GraphicsPath path = CreateRoundedRectangle(rect, actualRadius))
            {
                // Draw highlight glow if enabled (inset glow within button bounds)
                if (_isHighlighted && Enabled)
                {
                    // Pulsing opacity using sine wave (0.7 to 1.0 range)
                    float pulse = (float)(0.7 + 0.3 * Math.Sin(_pulsePhase));
                    int glowAlpha = (int)(255 * pulse);
                    
                    // Draw multiple inset borders for glow effect with proper rounding
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

                // Fill background
                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }

                // Draw highlight border
                if (_isHighlighted && Enabled)
                {
                    float pulse = (float)(0.7 + 0.3 * Math.Sin(_pulsePhase));
                    int borderAlpha = (int)(255 * pulse);
                    using (Pen highlightPen = new Pen(Color.FromArgb(borderAlpha, _highlightColor), 2))
                    {
                        g.DrawPath(highlightPen, path);
                    }
                }
                // Always draw border (animated on hover)
                else
                {
                    using (Pen pen = new Pen(currentBorderColor, 1))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // Draw text centered
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
                _hoverTimer.Interval = 16; // ~60fps
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
            
            // Handle zero or negative radius - just use a regular rectangle
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            
            int diameter = radius * 2;
            
            // Ensure diameter doesn't exceed rectangle dimensions
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;
            
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            // Top left arc
            path.AddArc(arc, 180, 90);

            // Top right arc
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        // Ensure parent background shows through rounded corners
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        // Handle parent background change
        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            Invalidate();
        }

        // Handle enabled state change
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }
}
