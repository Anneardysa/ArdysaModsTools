using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ArdysaModsTools.Helpers
{
    public static class UIHelpers
    {
        // PInvoke: rounded window region
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public static void ApplyRoundedCorner(Form f, int radius = 16)
        {
            if (f == null) return;
            var r = CreateRoundRectRgn(0, 0, f.Width + 1, f.Height + 1, radius, radius);
            f.Region = Region.FromHrgn(r);
        }

        // Quick drop-shadow: simulate by painting a shadow panel behind floating panel (we build it in designer)
        // Slide-in animation from right: animates Left from startX to targetX over duration ms
        public static void SlideInFromRight(Form form, Rectangle targetBounds, int durationMs = 300)
        {
            var fps = 60;
            var interval = 1000 / fps;
            var steps = Math.Max(1, durationMs / interval);
            var start = new Point(Screen.FromControl(form).WorkingArea.Right, targetBounds.Y);
            var end = new Point(targetBounds.X, targetBounds.Y);
            var deltaX = (end.X - start.X) / (double)steps;
            var timer = new System.Windows.Forms.Timer { Interval = interval };
            int current = 0;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(start, targetBounds.Size);
            form.Show();
            timer.Tick += (s, e) =>
            {
                current++;
                var newX = (int)Math.Round(start.X + deltaX * current);
                form.Left = newX;
                if (current >= steps)
                {
                    timer.Stop();
                    form.Left = end.X;
                }
            };
            timer.Start();
        }
    }
}
