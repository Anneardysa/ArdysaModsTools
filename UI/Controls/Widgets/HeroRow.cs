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
using System.Linq;
using System.Windows.Forms;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Controls.Widgets;

namespace ArdysaModsTools.UI.Controls.Widgets
{
    /// <summary>
    /// Hero row with expand/collapse accordion functionality.
    /// Collapsed: shows only header (title + fav + expand button)
    /// Expanded: shows header + tiles grid
    /// </summary>
    public class HeroRow : UserControl
    {
        private Label lblTitle = null!;
        private Label lblFav = null!;
        private Label lblExpand = null!; // ▼/▶ button
        private Panel headerPanel = null!;
        private FlowLayoutPanel tilesFlow = null!;
        private Panel innerCard = null!;
        private HeroModel? _boundHero;

        private bool _isFav;
        private bool _isExpanded = false;
        private bool _hasCustomSelection = false;
        
        public string HeroId { get; private set; } = string.Empty;
        public bool IsExpanded => _isExpanded;

        // Header height when collapsed
        private const int HeaderHeight = 48;

        public event Action<string, string>? TileClicked;
        public event Action<string, bool>? FavoriteToggled;
        public event Action<HeroRow>? ExpandedChanged; // passes the row that changed

        public HeroRow()
        {
            Initialize();
        }

        #region Initialization

        private void Initialize()
        {
            BackColor = Color.Transparent;
            Margin = new Padding(0);
            Padding = new Padding(0);

            CreateInnerCard();
            CreateHeader();
            CreateTilesFlow();

            Controls.Add(innerCard);

            // Start collapsed
            SetExpanded(false);
            
            // Recalculate layout when resized (for responsive tile arrangement)
            this.Resize += (s, e) => { if (_isExpanded) RecalculateLayout(); };
        }

        private void CreateInnerCard()
        {
            innerCard = new Panel
            {
                BackColor = Theme.RowBackground,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Dock = DockStyle.Top,
                AutoSize = false
            };
        }

        private void CreateHeader()
        {
            headerPanel = new Panel
            {
                Height = HeaderHeight,
                BackColor = Theme.RowBackground,
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand
            };

            lblTitle = new Label
            {
                AutoSize = true,
                Font = Theme.TitleFont,
                ForeColor = Theme.TitleColor,
                Location = new Point(16, 14),
                Cursor = Cursors.Hand
            };

            lblFav = new Label
            {
                AutoSize = true,
                Font = new Font("JetBrains Mono", 12F),
                Text = "♥",
                ForeColor = Theme.FavOffColor,
                Cursor = Cursors.Hand
            };

            lblExpand = new Label
            {
                AutoSize = false,
                Size = new Size(32, 24),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Theme.TextLight,
                BackColor = Theme.Accent,
                Text = "▼",
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            // Make expand button rounded via Paint
            lblExpand.Paint += PaintExpandButton;

            // Wire click events
            headerPanel.Click += (s, e) => ToggleExpanded();
            lblTitle.Click += (s, e) => ToggleExpanded();
            lblExpand.Click += (s, e) => ToggleExpanded();

            lblFav.Click += (s, e) =>
            {
                var nowFav = !_isFav;
                SetFavorite(nowFav);
                FavoriteToggled?.Invoke(HeroId, nowFav);
            };
            lblFav.MouseEnter += (s, e) => lblFav.ForeColor = Theme.FavColor;
            lblFav.MouseLeave += (s, e) => lblFav.ForeColor = _isFav ? Theme.FavColor : Theme.FavOffColor;

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblFav);
            headerPanel.Controls.Add(lblExpand);
            innerCard.Controls.Add(headerPanel);

            // Layout header on resize
            headerPanel.Resize += (s, e) => LayoutHeader();
        }

        private void PaintExpandButton(object? sender, PaintEventArgs e)
        {
            var lbl = sender as Label;
            if (lbl == null) return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int radius = 6;
            var rect = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();

            using var brush = new SolidBrush(lbl.BackColor);
            e.Graphics.FillPath(brush, path);

            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var textBrush = new SolidBrush(lbl.ForeColor);
            e.Graphics.DrawString(lbl.Text, lbl.Font, textBrush, new RectangleF(0, 0, lbl.Width, lbl.Height), sf);
        }

        private void LayoutHeader()
        {
            if (headerPanel == null || lblFav == null || lblExpand == null) return;

            int padding = 16;
            int headerWidth = headerPanel.ClientSize.Width;

            // Expand button at far right
            lblExpand.Left = headerWidth - lblExpand.Width - padding;
            lblExpand.Top = (HeaderHeight - lblExpand.Height) / 2;

            // Fav button left of expand
            lblFav.Left = lblExpand.Left - lblFav.PreferredWidth - 12;
            lblFav.Top = (HeaderHeight - lblFav.PreferredHeight) / 2;

            // Title at left
            lblTitle.Left = padding;
            lblTitle.Top = (HeaderHeight - lblTitle.PreferredHeight) / 2;
        }

        private void CreateTilesFlow()
        {
            tilesFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                AutoSize = false,
                Dock = DockStyle.Top,
                Padding = new Padding(12, 8, 12, 12),
                BackColor = Theme.RowBackground
            };

            innerCard.Controls.Add(tilesFlow);
            tilesFlow.BringToFront();
        }

        #endregion

        #region Public Methods

        public void Bind(HeroModel hero, bool isFavorite)
        {
            _boundHero = hero;
            HeroId = hero.Id;
            lblTitle.Text = hero.DisplayName;
            SetFavorite(isFavorite);

            tilesFlow.SuspendLayout();
            tilesFlow.Controls.Clear();

            // Partition skins into Legacy and Custom categories based on zip filename
            var allSkins = hero.Skins.ToList();
            var legacySkins = allSkins.Where(s => !IsCustomSet(hero.Sets, s)).ToList();
            var customSkins = allSkins.Where(s => IsCustomSet(hero.Sets, s)).ToList();
            bool hasBoth = legacySkins.Count > 0 && customSkins.Count > 0;

            // Legacy section (top)
            if (legacySkins.Count > 0)
            {
                if (hasBoth)
                    tilesFlow.Controls.Add(CreateCategoryHeader("LEGACY SET", legacySkins.Count, Color.FromArgb(212, 160, 87)));

                foreach (var skin in legacySkins)
                    tilesFlow.Controls.Add(CreateTileForSkin(skin));
            }

            // Custom section (bottom)
            if (customSkins.Count > 0)
            {
                if (hasBoth)
                    tilesFlow.Controls.Add(CreateCategoryHeader("CUSTOM SET", customSkins.Count, Color.FromArgb(167, 139, 250)));

                foreach (var skin in customSkins)
                    tilesFlow.Controls.Add(CreateTileForSkin(skin));
            }

            tilesFlow.ResumeLayout();
        }

        /// <summary>
        /// Returns true if the set's archive filename starts with "mix_" (case-insensitive), indicating a custom/mixed set.
        /// Checks the zip/rar URLs in the set's asset list.
        /// </summary>
        private static bool IsCustomSet(Dictionary<string, List<string>>? sets, string skinName)
        {
            if (sets == null || string.IsNullOrWhiteSpace(skinName)) return false;
            if (!sets.TryGetValue(skinName, out var urls) || urls == null || urls.Count == 0) return false;

            // Find the first archive URL and check its filename
            var archiveUrl = urls.FirstOrDefault(u =>
                u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                u.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                u.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(archiveUrl)) return false;

            try
            {
                var fileName = Path.GetFileName(new Uri(archiveUrl).LocalPath);
                return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                var lastSlash = archiveUrl.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < archiveUrl.Length - 1)
                {
                    var fileName = archiveUrl.Substring(lastSlash + 1);
                    return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
        }

        /// <summary>
        /// Creates a styled category header label for the tiles flow panel.
        /// </summary>
        private Label CreateCategoryHeader(string text, int count, Color accentColor)
        {
            var label = new Label
            {
                Text = $"{text}  ({count})",
                AutoSize = false,
                Height = 28,
                Width = tilesFlow.ClientSize.Width - 24,
                Font = new Font("JetBrains Mono", 9F, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Margin = new Padding(8, 8, 8, 2)
            };
            return label;
        }

        public void SetFavorite(bool isFav)
        {
            _isFav = isFav;
            lblFav.ForeColor = isFav ? Theme.FavColor : Theme.FavOffColor;
        }

        public void ApplySelection(string selectedSkin)
        {
            foreach (var tile in tilesFlow.Controls.OfType<TileCard>())
            {
                var skin = tile.Tag as string ?? tile.Id;
                tile.SetSelected(string.Equals(skin, selectedSkin, StringComparison.OrdinalIgnoreCase));
            }

            // Change title color to cyan when a non-default set is selected
            // A selection is considered "default" if it's null, empty, "default", or contains "Default" (like "Default Set")
            bool isDefaultSelection = string.IsNullOrEmpty(selectedSkin) ||
                                      selectedSkin.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                      selectedSkin.Contains("Default", StringComparison.OrdinalIgnoreCase);
            
            _hasCustomSelection = !isDefaultSelection;
            
            if (_hasCustomSelection)
            {
                lblTitle.ForeColor = Color.FromArgb(0, 200, 255); // Cyan color
            }
            else
            {
                // Restore normal color based on expanded state
                lblTitle.ForeColor = _isExpanded ? Theme.TitleColor : Theme.TextMuted;
            }
        }

        public void ToggleExpanded() => SetExpanded(!_isExpanded);

        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;

            // Update expand button visual
            lblExpand.Text = expanded ? "▼" : "▶";
            lblExpand.BackColor = expanded ? Theme.Accent : Theme.RowBorderColor;
            lblExpand.ForeColor = expanded ? Theme.TitleColor : Theme.TextLight;
            lblExpand.Invalidate();

            // Update title opacity (but preserve cyan if custom selection is active)
            if (!_hasCustomSelection)
            {
                lblTitle.ForeColor = expanded ? Theme.TitleColor : Theme.TextMuted;
            }

            if (expanded)
            {
                tilesFlow.Visible = true;
                RecalculateLayout();
            }
            else
            {
                tilesFlow.Visible = false;
                innerCard.Height = HeaderHeight;
                this.Height = HeaderHeight;
            }

            ExpandedChanged?.Invoke(this);
        }

        public void RecalculateLayout()
        {
            if (!_isExpanded) return;

            int tileCount = tilesFlow.Controls.OfType<TileCard>().Count();
            // Account for category header labels height
            int headerHeight = tilesFlow.Controls.OfType<Label>()
                .Sum(l => l.Height + l.Margin.Vertical);

            if (tileCount == 0)
            {
                tilesFlow.Height = 50;
            }
            else
            {
                // Calculate tiles layout with dynamic sizing
                int availableWidth = Math.Max(200, this.Width - 24);
                int marginH = Theme.TileMarginHorizontal;
                int marginV = Theme.TileMarginVertical;

                // Dynamic tile sizing: calculate optimal size to fill width
                // Target: 160px min, 200px max tile size
                int minTileSize = 160;
                int maxTileSize = 200;
                int baseTileSize = Theme.TileSize;
                
                // Calculate how many columns fit at different sizes
                int optimalTileSize = baseTileSize;
                int columns = 1;
                
                for (int testSize = maxTileSize; testSize >= minTileSize; testSize -= 10)
                {
                    int tileWithMargin = testSize + marginH * 2;
                    int testColumns = Math.Max(1, availableWidth / tileWithMargin);
                    
                    // Use this size if it fits nicely
                    if (testColumns > 0)
                    {
                        int totalWidth = testColumns * tileWithMargin;
                        int waste = availableWidth - totalWidth;
                        
                        // Accept if waste is reasonable (less than one tile width)
                        if (waste < tileWithMargin && waste >= 0)
                        {
                            optimalTileSize = testSize;
                            columns = testColumns;
                            break;
                        }
                    }
                }
                
                // Fallback: use base size if no optimal found
                if (optimalTileSize == baseTileSize)
                {
                    int tileWithMargin = baseTileSize + marginH * 2;
                    columns = Math.Max(1, availableWidth / tileWithMargin);
                }
                
                int rows = (int)Math.Ceiling(tileCount / (double)columns);
                int tileRowHeight = optimalTileSize + Theme.TileCaptionHeight + marginV * 2;
                tilesFlow.Height = rows * tileRowHeight + headerHeight + 20;

                // Update tile sizes to calculated optimal size
                foreach (var tile in tilesFlow.Controls.OfType<TileCard>())
                {
                    tile.SetTileSize(optimalTileSize);
                    tile.Margin = new Padding(marginH, marginV, marginH, marginV);
                }
            }

            innerCard.Height = HeaderHeight + tilesFlow.Height;
            this.Height = innerCard.Height;
        }

        #endregion

        #region Private Helpers

        private TileCard CreateTileForSkin(string? skin)
        {
            var tile = new TileCard();
            var skinId = skin ?? "default";
            var caption = string.IsNullOrWhiteSpace(skin) ? "Default Set" : skin;
            tile.SetData(skinId, caption);
            tile.Tag = skin;
            tile.Clicked += (s, e) => TileClicked?.Invoke(HeroId, skinId);

            // Load thumbnail if available
            if (_boundHero?.Sets != null &&
                !string.IsNullOrWhiteSpace(skin) &&
                _boundHero.Sets.TryGetValue(skin, out var assets) &&
                assets != null && assets.Count > 1)
            {
                tile.SetThumbnail(assets[1]);
            }

            return tile;
        }

        #endregion
    }
}

