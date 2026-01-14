using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// Modern search box with rounded corners and search icon.
    /// </summary>
    public class ModernSearchBox : UserControl
    {
        private TextBox textBox = null!;
        private PictureBox searchIcon = null!;
        private System.Windows.Forms.Timer debounceTimer = null!;
        private string lastSearchText = "";

        public event EventHandler<string>? SearchTextChanged;

        public string SearchText
        {
            get => textBox.Text;
            set => textBox.Text = value;
        }

        public string PlaceholderText
        {
            get => textBox.PlaceholderText;
            set => textBox.PlaceholderText = value;
        }

        public int DebounceMs { get; set; } = 300;

        public ModernSearchBox()
        {
            InitializeComponents();
            SetupDebounceTimer();
            
            // Enable double buffering for smooth painting
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void InitializeComponents()
        {
            // Container settings - pure black background
            this.Height = 40;
            this.BackColor = Color.Black;
            this.Padding = new Padding(12, 8, 12, 8);

            // Search icon
            searchIcon = new PictureBox
            {
                Size = new Size(20, 20),
                Location = new Point(12, 10),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = CreateSearchIcon(),
                BackColor = Color.Transparent
            };

            textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(200, 200, 210),
                Font = new Font("JetBrains Mono", 10F),
                Location = new Point(40, 10),
                Width = this.Width - 52,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            textBox.TextChanged += TextBox_TextChanged;
            textBox.KeyDown += TextBox_KeyDown;

            this.Controls.Add(searchIcon);
            this.Controls.Add(textBox);

            this.Resize += (s, e) =>
            {
                textBox.Width = this.Width - 52;
            };
        }

        private void SetupDebounceTimer()
        {
            debounceTimer = new System.Windows.Forms.Timer();
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                string currentText = textBox.Text?.Trim() ?? "";
                if (currentText != lastSearchText)
                {
                    lastSearchText = currentText;
                    SearchTextChanged?.Invoke(this, currentText);
                }
            };
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            debounceTimer.Stop();
            debounceTimer.Interval = DebounceMs;
            debounceTimer.Start();
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                textBox.Clear();
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw subtle border for visibility
            using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            // Top left arc
            path.AddArc(arc, 180, 90);

            // Top right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private Image CreateSearchIcon()
        {
            Bitmap icon = new Bitmap(20, 20);
            using (Graphics g = Graphics.FromImage(icon))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Draw circle (lens)
                using (Pen pen = new Pen(Color.FromArgb(150, 150, 160), 2))
                {
                    g.DrawEllipse(pen, 3, 3, 10, 10);
                }

                // Draw handle
                using (Pen pen = new Pen(Color.FromArgb(150, 150, 160), 2))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 11, 11, 16, 16);
                }
            }
            return icon;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                debounceTimer?.Dispose();
                searchIcon?.Image?.Dispose();
                searchIcon?.Dispose();
                textBox?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
