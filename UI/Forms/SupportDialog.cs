using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Support dialog with payment options: PayPal, Ko-Fi, Sociabuzz.
    /// Styled to match GenerationPreviewForm theme.
    /// </summary>
    public sealed class SupportDialog : Form
    {
        private const string PayPalUrl = "https://paypal.me/ardysa";
        private const string KoFiUrl = "https://ko-fi.com/ardysa";
        private const string SociabuzzUrl = "https://sociabuzz.com/ardysa/support";

        public SupportDialog()
        {
            InitializeComponent();
            UI.FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent()
        {
            // Form settings - compact size
            Text = "Support";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(800, 340);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            // Main container
            var mainContainer = new Panel
            {
                Location = new Point(1, 1),
                Size = new Size(Width - 2, Height - 2),
                BackColor = Color.Black
            };
            Controls.Add(mainContainer);

            // Top badge - centered
            var badgeLabel = new Label
            {
                Text = "[ SUPPORT ]",
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 30, 30),
                AutoSize = false,
                Size = new Size(120, 28),
                Location = new Point((Width - 120) / 2, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badgeLabel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, badgeLabel.Width - 1, badgeLabel.Height - 1);
            };
            mainContainer.Controls.Add(badgeLabel);

            // Title - centered
            var titleLabel = new Label
            {
                Text = "SUPPORT THE DEVELOPMENT",
                Font = new Font("JetBrains Mono", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(Width - 80, 36),
                Location = new Point(40, 56),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(titleLabel);

            // Subtitle - centered
            var subtitleLabel = new Label
            {
                Text = "ArdysaModsTools is free and always will be. Your support helps keep this project alive!",
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = false,
                Size = new Size(Width - 80, 26),
                Location = new Point(40, 92),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(subtitleLabel);

            // Options container - centered horizontally
            int optionWidth = 230;
            int optionHeight = 65;
            int spacing = 16;
            int totalOptionsWidth = (optionWidth * 3) + (spacing * 2);
            int startX = (Width - totalOptionsWidth) / 2;
            int optionY = 130;

            string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");

            // PayPal option
            var paypalPanel = CreateOptionPanel("PAYPAL", "One-time donation", Path.Combine(assetsPath, "paypal.png"), startX, optionY, optionWidth, optionHeight);
            paypalPanel.Click += (s, e) => OpenUrl(PayPalUrl);
            MakeClickable(paypalPanel, PayPalUrl);
            mainContainer.Controls.Add(paypalPanel);

            // Ko-Fi option
            var kofiPanel = CreateOptionPanel("KO-FI", "One-time / monthly", Path.Combine(assetsPath, "ko-fi.png"), startX + optionWidth + spacing, optionY, optionWidth, optionHeight);
            kofiPanel.Click += (s, e) => OpenUrl(KoFiUrl);
            MakeClickable(kofiPanel, KoFiUrl);
            mainContainer.Controls.Add(kofiPanel);

            // Sociabuzz option
            var sociabuzzPanel = CreateOptionPanel("SOCIABUZZ", "One-time donation", Path.Combine(assetsPath, "sociabuzz.png"), startX + (optionWidth + spacing) * 2, optionY, optionWidth, optionHeight);
            sociabuzzPanel.Click += (s, e) => OpenUrl(SociabuzzUrl);
            MakeClickable(sociabuzzPanel, SociabuzzUrl);
            mainContainer.Controls.Add(sociabuzzPanel);

            // Footer message - centered
            var footerLabel = new Label
            {
                Text = "❤ Every contribution helps improve the tool for everyone ❤",
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize = false,
                Size = new Size(Width - 80, 22),
                Location = new Point(40, 210),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(footerLabel);

            // Close button - centered
            var closeButton = new Controls.RoundedButton
            {
                Text = "[ CLOSE ]",
                Size = new Size(120, 38),
                Location = new Point((Width - 120) / 2, 250),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                HoverForeColor = Color.White
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            mainContainer.Controls.Add(closeButton);

            // Border paint
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.White, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make form draggable from title
            titleLabel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
        }

        private Panel CreateOptionPanel(string title, string subtitle, string imagePath, int x, int y, int w, int h)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.FromArgb(20, 20, 20),
                Cursor = Cursors.Hand
            };

            // Icon - PictureBox with image
            var iconPictureBox = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(15, (h - 32) / 2),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };
            
            // Load image if exists
            try
            {
                if (File.Exists(imagePath))
                {
                    iconPictureBox.Image = Image.FromFile(imagePath);
                }
            }
            catch { }
            
            panel.Controls.Add(iconPictureBox);

            // Title - vertically centered
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(w - 85, 20),
                Location = new Point(65, (h / 2) - 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(titleLabel);

            // Subtitle
            var subtitleLabel = new Label
            {
                Text = subtitle,
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize = false,
                Size = new Size(w - 85, 18),
                Location = new Point(65, (h / 2) + 2),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(subtitleLabel);

            // Arrow - vertically centered on right
            var arrowLabel = new Label
            {
                Text = "→",
                Font = new Font("JetBrains Mono", 12f),
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = false,
                Size = new Size(25, h),
                Location = new Point(w - 35, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(arrowLabel);

            // Hover effects
            panel.MouseEnter += (s, e) => { panel.BackColor = Color.FromArgb(35, 35, 35); arrowLabel.ForeColor = Color.White; };
            panel.MouseLeave += (s, e) => { panel.BackColor = Color.FromArgb(20, 20, 20); arrowLabel.ForeColor = Color.FromArgb(60, 60, 60); };

            // Border
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            return panel;
        }

        private void MakeClickable(Panel panel, string url)
        {
            foreach (Control c in panel.Controls)
            {
                c.Click += (s, e) => OpenUrl(url);
                c.MouseEnter += (s, e) => { panel.BackColor = Color.FromArgb(35, 35, 35); };
                c.MouseLeave += (s, e) => { panel.BackColor = Color.FromArgb(20, 20, 20); };
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }
    }
}
