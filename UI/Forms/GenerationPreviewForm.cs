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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Preview dialog showing selected hero sets with thumbnails before generation.
    /// </summary>
    public sealed class GenerationPreviewForm : Form
    {
        private FlowLayoutPanel _itemsPanel = null!;
        private RoundedButton _generateBtn = null!;
        private RoundedButton _cancelBtn = null!;
        private Label _headerLabel = null!;
        private Label _subHeaderLabel = null!;
        private readonly List<(HeroModel hero, string setName, string? thumbnailUrl)> _selections;
        private readonly HttpClient _http = HttpClientProvider.Client;
        private bool _isClosing;

        public bool Confirmed { get; private set; }

        public GenerationPreviewForm(List<(HeroModel hero, string setName, string? thumbnailUrl)> selections)
        {
            _selections = selections ?? new List<(HeroModel, string, string?)>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Form settings - borderless modern look
            Text = "Confirm Generation";
            Size = new Size(480, 500);
            MinimumSize = new Size(400, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            Font = new Font("JetBrains Mono", 10F);
            Padding = new Padding(1);
            KeyPreview = true;
            
            // Handle Escape key to cancel
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !_isClosing)
                {
                    _isClosing = true;
                    Confirmed = false;
                    DialogResult = DialogResult.Cancel;
                    e.Handled = true;
                }
            };

            // Main container with border
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(0)
            };
            Controls.Add(mainContainer);

            // Header panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.Black,
                Padding = new Padding(20, 15, 20, 10)
            };
            mainContainer.Controls.Add(headerPanel);

            _headerLabel = new Label
            {
                Text = "⚠ CONFIRM GENERATION ⚠",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("JetBrains Mono", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(_headerLabel);

            _subHeaderLabel = new Label
            {
                Text = $"{_selections.Count} hero set(s) will be generated",
                Dock = DockStyle.Bottom,
                Height = 25,
                ForeColor = Color.FromArgb(136, 136, 136),
                Font = new Font("JetBrains Mono", 9F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(_subHeaderLabel);

            // Scrollable items panel
            _itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Black,
                Padding = new Padding(15, 10, 15, 10)
            };
            mainContainer.Controls.Add(_itemsPanel);

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.Black,
                Padding = new Padding(15, 12, 15, 12)
            };
            mainContainer.Controls.Add(buttonPanel);

            // Generate button - white bg, black text (primary action)
            _generateBtn = new RoundedButton
            {
                Text = "[ GENERATE ]",
                Size = new Size(150, 44),
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.Black,
                HoverForeColor = Color.White
            };
            _generateBtn.FlatAppearance.BorderSize = 0;
            _generateBtn.Click += (s, e) =>
            {
                if (_isClosing) return;
                _isClosing = true;
                Confirmed = true;
                DialogResult = DialogResult.OK;
            };

            // Cancel button - black bg, grey text (secondary action)
            _cancelBtn = new RoundedButton
            {
                Text = "[ CANCEL ]",
                Size = new Size(120, 44),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                HoverForeColor = Color.White
            };
            _cancelBtn.FlatAppearance.BorderSize = 0;
            _cancelBtn.Click += (s, e) =>
            {
                if (_isClosing) return;
                _isClosing = true;
                Confirmed = false;
                DialogResult = DialogResult.Cancel;
            };

            buttonPanel.Controls.Add(_generateBtn);
            buttonPanel.Controls.Add(_cancelBtn);

            // Center buttons
            buttonPanel.Resize += (s, e) =>
            {
                int totalWidth = _cancelBtn.Width + 10 + _generateBtn.Width;
                int startX = (buttonPanel.Width - totalWidth) / 2;
                _cancelBtn.Location = new Point(startX, 15);
                _generateBtn.Location = new Point(startX + _cancelBtn.Width + 10, 15);
            };

            // Bring panels to front in correct order
            _itemsPanel.BringToFront();

            // Make form draggable from header
            headerPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            foreach (var (hero, setName, thumbUrl) in _selections)
            {
                var item = CreateItemPanel(hero, setName, thumbUrl, false);
                _itemsPanel.Controls.Add(item);
                
                // Load thumbnail async
                if (!string.IsNullOrEmpty(thumbUrl))
                {
                    _ = LoadThumbnailAsync(item, thumbUrl);
                }
                
                await Task.Yield();
            }
        }

        private Panel CreateItemPanel(HeroModel hero, string setName, string? thumbUrl, bool isDefaultSet)
        {
            // All items now have thumbnails (default sets are filtered out before showing preview)
            int panelHeight = 75;
            
            var panel = new Panel
            {
                Width = _itemsPanel.ClientSize.Width - 35,
                Height = panelHeight,
                BackColor = Color.FromArgb(15, 15, 15),
                Margin = new Padding(0, 0, 0, 6)
            };

            // Add border effect via paint
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            // Thumbnail
            var thumb = new PictureBox
            {
                Size = new Size(65, 65),
                Location = new Point(5, 5),
                BackColor = Color.FromArgb(30, 30, 30),
                SizeMode = PictureBoxSizeMode.Zoom,
                Tag = "thumb"
            };
            panel.Controls.Add(thumb);

            // Hero name
            var heroLabel = new Label
            {
                Text = hero.DisplayName,
                Location = new Point(80, 12),
                Size = new Size(panel.Width - 95, 24),
                ForeColor = Color.White,
                Font = new Font("JetBrains Mono", 11F, FontStyle.Bold)
            };
            panel.Controls.Add(heroLabel);

            // Set name
            var setLabel = new Label
            {
                Text = setName,
                Location = new Point(80, 38),
                Size = new Size(panel.Width - 95, 22),
                ForeColor = Color.FromArgb(0, 255, 255), // Cyan for set name
                Font = new Font("JetBrains Mono", 9F)
            };
            panel.Controls.Add(setLabel);

            // Resize handler
            _itemsPanel.Resize += (s, e) =>
            {
                panel.Width = _itemsPanel.ClientSize.Width - 35;
                heroLabel.Width = panel.Width - 95;
                setLabel.Width = panel.Width - 95;
            };

            return panel;
        }

        private async Task LoadThumbnailAsync(Panel panel, string url)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var data = await _http.GetByteArrayAsync(url, cts.Token);
                
                if (data != null && data.Length > 0)
                {
                    using var ms = new System.IO.MemoryStream(data);
                    var img = Image.FromStream(ms);
                    
                    if (!IsDisposed)
                    {
                        foreach (Control ctrl in panel.Controls)
                        {
                            if (ctrl is PictureBox thumb && ctrl.Tag?.ToString() == "thumb")
                            {
                                if (thumb.InvokeRequired)
                                {
                                    thumb.Invoke((Action)(() => thumb.Image = img));
                                }
                                else
                                {
                                    thumb.Image = img;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently fail, placeholder will show
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw border around form
            using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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

