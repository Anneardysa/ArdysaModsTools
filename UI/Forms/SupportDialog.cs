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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Support dialog with payment options: PayPal, Ko-Fi, Sociabuzz.
    /// Includes YouTube subscriber goal with progress bar.
    /// </summary>
    public sealed class SupportDialog : Form
    {
        private const string PayPalUrl = "https://paypal.me/ardysa";
        private const string KoFiUrl = "https://ko-fi.com/ardysa";
        private const string SociabuzzUrl = "https://sociabuzz.com/ardysa/support";

        // Subscriber goal UI elements
        private Panel? _subsGoalPanel;
        private Panel? _progressBarPanel;
        private Panel? _progressFillPanel;
        private Label? _subsCountLabel;
        private readonly SubsGoalService _subsGoalService = new();
        private string _channelUrl = "https://youtube.com/@ArdysaMods";
        
        // Default config for immediate display
        private static readonly SubsGoalConfig DefaultConfig = new()
        {
            CurrentSubs = 1792,
            GoalSubs = 2000,
            ChannelUrl = "https://youtube.com/@ArdysaMods",
            Enabled = true
        };

        public SupportDialog()
        {
            InitializeComponent();
            UI.FontHelper.ApplyToForm(this);
            
            // Show default values immediately (UI is now created)
            UpdateProgressBar(DefaultConfig);
            
            // Load remote config asynchronously after form is shown
            this.Shown += async (s, e) => await LoadSubsGoalAsync();
        }

        private void InitializeComponent()
        {
            // Form settings - increased height for subscriber goal
            Text = "Support";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(680, 480); // Increased from 380 to 480
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
            int optionWidth = 200;
            int optionHeight = 95;
            int spacing = 20;
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
                Location = new Point(40, 240),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(footerLabel);

            // ═══════════════════════════════════════════════════════════════
            // SUBSCRIBER GOAL SECTION - Modern card design
            // ═══════════════════════════════════════════════════════════════
            
            // Separator line
            var separatorLine = new Panel
            {
                Location = new Point(40, 272),
                Size = new Size(Width - 80, 1),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            mainContainer.Controls.Add(separatorLine);
            
            // Card container (matching donate button style)
            int cardWidth = 400;
            int cardHeight = 80;
            _subsGoalPanel = new Panel
            {
                Location = new Point((Width - cardWidth) / 2, 290),
                Size = new Size(cardWidth, cardHeight),
                BackColor = Color.FromArgb(20, 20, 20),
                Cursor = Cursors.Hand
            };
            _subsGoalPanel.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _subsGoalPanel!.Width - 1, _subsGoalPanel.Height - 1);
            };
            _subsGoalPanel.MouseEnter += (s, e) => { _subsGoalPanel!.BackColor = Color.FromArgb(30, 30, 30); };
            _subsGoalPanel.MouseLeave += (s, e) => { _subsGoalPanel!.BackColor = Color.FromArgb(20, 20, 20); };
            mainContainer.Controls.Add(_subsGoalPanel);
            
            // YouTube icon (left side)
            var ytIconPath = Path.Combine(assetsPath, "youtube.png");
            var ytIcon = new PictureBox
            {
                Size = new Size(20, 20),
                Location = new Point(16, 12),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };
            try
            {
                if (File.Exists(ytIconPath))
                    ytIcon.Image = Image.FromFile(ytIconPath);
            }
            catch { }
            ytIcon.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Controls.Add(ytIcon);
            
            // Title (next to icon)
            var subsGoalTitle = new Label
            {
                Text = "SUBSCRIBE GOAL",
                Font = new Font("JetBrains Mono", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(140, 20),
                Location = new Point(42, 12),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            subsGoalTitle.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Controls.Add(subsGoalTitle);
            
            // Subscriber count (right aligned in header)
            _subsCountLabel = new Label
            {
                Text = "Loading...",
                Font = new Font("JetBrains Mono", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 80, 80), // Soft red
                AutoSize = false,
                Size = new Size(180, 20),
                Location = new Point(cardWidth - 196, 12),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _subsCountLabel.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Controls.Add(_subsCountLabel);
            
            // Progress bar background (full width with padding)
            _progressBarPanel = new Panel
            {
                Location = new Point(16, 42),
                Size = new Size(cardWidth - 32, 12),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            _progressBarPanel.Paint += (s, e) =>
            {
                // Rounded corners effect
                using var brush = new SolidBrush(Color.FromArgb(35, 35, 35));
                e.Graphics.FillRectangle(brush, 0, 0, _progressBarPanel!.Width, _progressBarPanel.Height);
            };
            _progressBarPanel.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Controls.Add(_progressBarPanel);
            
            // Progress bar fill (YouTube red gradient)
            _progressFillPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 12),
                BackColor = Color.FromArgb(255, 0, 0)
            };
            _progressFillPanel.Paint += (s, e) =>
            {
                // Gradient fill effect
                if (_progressFillPanel!.Width > 0)
                {
                    using var brush = new LinearGradientBrush(
                        new Rectangle(0, 0, _progressFillPanel.Width, _progressFillPanel.Height),
                        Color.FromArgb(255, 50, 50),
                        Color.FromArgb(200, 0, 0),
                        LinearGradientMode.Horizontal);
                    e.Graphics.FillRectangle(brush, 0, 0, _progressFillPanel.Width, _progressFillPanel.Height);
                }
            };
            _progressBarPanel.Controls.Add(_progressFillPanel);
            
            // Updated time label (bottom of card)
            var updateLabel = new Label
            {
                Text = "Updated daily at 0:00 GMT",
                Font = new Font("JetBrains Mono", 7f),
                ForeColor = Color.FromArgb(70, 70, 70),
                AutoSize = false,
                Size = new Size(cardWidth - 32, 14),
                Location = new Point(16, 58),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            updateLabel.Click += (s, e) => OpenUrl(_channelUrl);
            _subsGoalPanel.Controls.Add(updateLabel);
            
            // Make all child controls propagate hover
            foreach (Control ctrl in _subsGoalPanel.Controls)
            {
                ctrl.MouseEnter += (s, e) => { _subsGoalPanel.BackColor = Color.FromArgb(30, 30, 30); };
                ctrl.MouseLeave += (s, e) => { _subsGoalPanel.BackColor = Color.FromArgb(20, 20, 20); };
            }

            // ═══════════════════════════════════════════════════════════════
            // CLOSE BUTTON (moved down)
            // ═══════════════════════════════════════════════════════════════
            
            // Close button - centered
            var closeButton = new Controls.RoundedButton
            {
                Text = "[ CLOSE ]",
                Size = new Size(120, 38),
                Location = new Point((Width - 120) / 2, 400), // Moved from 290 to 400
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
        
        /// <summary>
        /// Load subscriber goal config from remote and update UI.
        /// Falls back to default values if fetch fails.
        /// </summary>
        private async Task LoadSubsGoalAsync()
        {
            try
            {
                var config = await _subsGoalService.GetConfigAsync();
                
                // Use remote config if available and enabled
                if (config != null && config.Enabled)
                {
                    _channelUrl = config.ChannelUrl;
                    UpdateProgressBar(config);
                }
                else if (config != null && !config.Enabled)
                {
                    // Remote explicitly disabled - hide section
                    if (_subsGoalPanel != null)
                        _subsGoalPanel.Visible = false;
                }
                // else: keep showing default values
            }
            catch
            {
                // Silent fail - keep showing default values
                // Don't hide the section on error
            }
        }
        
        /// <summary>
        /// Update progress bar UI based on config.
        /// </summary>
        private void UpdateProgressBar(SubsGoalConfig config)
        {
            if (_progressBarPanel == null || _progressFillPanel == null || _subsCountLabel == null)
                return;
            
            // Calculate fill width (full bar width)
            int maxFillWidth = _progressBarPanel.Width;
            int fillWidth = (int)(maxFillWidth * config.ProgressPercent / 100.0);
            
            _progressFillPanel.Width = Math.Max(0, fillWidth);
            _progressFillPanel.Invalidate(); // Force repaint for gradient
            
            // Update label
            _subsCountLabel.Text = $"{config.CurrentSubs:N0} / {config.GoalSubs:N0}";
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

            // Icon - centered at top
            var iconPictureBox = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point((w - 32) / 2, 10),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };
            
            try
            {
                if (File.Exists(imagePath))
                {
                    iconPictureBox.Image = Image.FromFile(imagePath);
                }
            }
            catch { }
            
            panel.Controls.Add(iconPictureBox);

            // Title - centered
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(w, 20),
                Location = new Point(0, 46),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = subtitle,
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize = false,
                Size = new Size(w, 18),
                Location = new Point(0, 66),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(subtitleLabel);

            // Hover effects
            panel.MouseEnter += (s, e) => { panel.BackColor = Color.FromArgb(35, 35, 35); };
            panel.MouseLeave += (s, e) => { panel.BackColor = Color.FromArgb(20, 20, 20); };

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
