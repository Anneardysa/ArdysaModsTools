using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// A Panel that renders with rounded corners and optional border.
    /// Double-buffered for smoother drawing.
    /// </summary>
    public class RoundedPanel : Panel
    {
        private int borderRadius = 0;
        private int borderThickness = 1;
        private Color borderColor = Color.FromArgb(51, 51, 51);

        [Category("Appearance")]
        [Description("Corner radius for the rounded rectangle.")]
        public int BorderRadius
        {
            get => borderRadius;
            set
            {
                borderRadius = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Border thickness (0 = no border).")]
        public int BorderThickness
        {
            get => borderThickness;
            set
            {
                borderThickness = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Border color for the rounded rectangle.")]
        public Color BorderColor
        {
            get => borderColor;
            set
            {
                borderColor = value;
                Invalidate();
            }
        }

        public RoundedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.FromArgb(5, 5, 5);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Clear with parent's background to remove dark rectangle
            Color parentBgColor = Parent?.BackColor ?? Color.Transparent;
            using (var clearBrush = new SolidBrush(parentBgColor))
            {
                e.Graphics.FillRectangle(clearBrush, ClientRectangle);
            }
            
            PaintRoundedBackground(e);
            PaintBorder(e);
        }

        private void PaintRoundedBackground(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            // Shrink slightly if border is present to prevent clipping
            if (borderThickness > 0)
            {
                int inset = borderThickness / 2 + 1;
                rect.Inflate(-inset, -inset);
            }
            using var path = GetRoundedRectPath(rect, borderRadius);
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
        }

        private void PaintBorder(PaintEventArgs e)
        {
            if (borderThickness <= 0 || borderColor == Color.Transparent) return;

            var rect = ClientRectangle;
            int inset = borderThickness / 2 + 1;
            rect.Inflate(-inset, -inset);

            using var path = GetRoundedRectPath(rect, borderRadius);
            using var pen = new Pen(borderColor, borderThickness);
            e.Graphics.DrawPath(pen, path);
        }

        private GraphicsPath GetRoundedRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
