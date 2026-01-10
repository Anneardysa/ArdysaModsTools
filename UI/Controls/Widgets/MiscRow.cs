using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Controls.Widgets
{
    /// <summary>
    /// Expandable row for a single misc option (like HeroRow).
    /// Header shows option name + current selection; expanded shows tile grid.
    /// </summary>
    public class MiscRow : UserControl
    {
        private readonly Panel _innerCard;
        private readonly Panel _headerPanel;
        private readonly Label _lblTitle;
        private readonly Label _lblSelection;
        private readonly Label _lblExpand;
        private readonly FlowLayoutPanel _tilesFlow;

        private MiscOption? _boundOption;
        private bool _isExpanded = false;
        private string _selectedChoice = string.Empty;

        private const int HeaderHeight = 52;

        public string OptionId => _boundOption?.Id ?? string.Empty;
        public string SelectedChoice => _selectedChoice;
        public bool IsExpanded => _isExpanded;

        public event Action<string, string>? SelectionChanged; // optionId, choice
        public event Action<MiscRow>? ExpandedChanged; // passes the row that changed

        public MiscRow()
        {
            BackColor = Color.Transparent;
            Margin = new Padding(0, 4, 0, 4);
            Padding = new Padding(0);

            // Inner card container
            _innerCard = new Panel
            {
                BackColor = Theme.RowBackground,
                Padding = new Padding(0),
                Dock = DockStyle.Top,
                AutoSize = false
            };

            // Header
            _headerPanel = new Panel
            {
                Height = HeaderHeight,
                BackColor = Theme.RowBackground,
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand
            };

            _lblTitle = new Label
            {
                AutoSize = false,
                Font = Theme.TitleFont,
                ForeColor = Theme.TitleColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(50, 6),
                Size = new Size(400, 22),
                Cursor = Cursors.Hand
            };

            _lblSelection = new Label
            {
                AutoSize = false,
                Font = Theme.SmallFont,
                ForeColor = Theme.Accent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(50, 28),
                Size = new Size(400, 18),
                Cursor = Cursors.Hand
            };

            _lblExpand = new Label
            {
                AutoSize = false,
                Size = new Size(32, 24),
                Font = new Font("JetBrains Mono", 10F),
                ForeColor = Theme.TextLight,
                BackColor = Theme.RowBorderColor,
                Text = "▶",
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _lblExpand.Paint += PaintExpandButton;

            // Wire header click events
            _headerPanel.Click += (s, e) => ToggleExpanded();
            _lblTitle.Click += (s, e) => ToggleExpanded();
            _lblSelection.Click += (s, e) => ToggleExpanded();
            _lblExpand.Click += (s, e) => ToggleExpanded();

            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblSelection);
            _headerPanel.Controls.Add(_lblExpand);

            // Tiles flow panel
            _tilesFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                AutoSize = false,
                Dock = DockStyle.Top,
                Padding = new Padding(12, 8, 12, 12),
                BackColor = Theme.RowBackground,
                Visible = false
            };

            _innerCard.Controls.Add(_tilesFlow);
            _innerCard.Controls.Add(_headerPanel);
            _tilesFlow.BringToFront();
            Controls.Add(_innerCard);

            // Layout header on resize
            _headerPanel.Resize += (s, e) => LayoutHeader();
            this.Resize += (s, e) => { if (_isExpanded) RecalculateLayout(); };

            // Start collapsed
            SetExpanded(false);
        }

        #region Public Methods

        public void Bind(MiscOption option, string? selectedChoice = null)
        {
            _boundOption = option;
            _lblTitle.Text = option.DisplayName;

            // Build tiles
            _tilesFlow.SuspendLayout();
            _tilesFlow.Controls.Clear();

            foreach (var choice in option.Choices)
            {
                var tile = new MiscTile();
                tile.SetData(choice, choice);
                
                // Set thumbnail if URL pattern available
                var thumbnailUrl = option.GetThumbnailUrl(choice);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    tile.SetThumbnail(thumbnailUrl);
                }
                
                tile.Clicked += (s, e) => OnTileClicked(tile);
                _tilesFlow.Controls.Add(tile);
            }

            _tilesFlow.ResumeLayout();

            // Apply initial selection
            var initial = selectedChoice ?? option.Choices.FirstOrDefault() ?? "";
            ApplySelection(initial);
        }

        public void ApplySelection(string choice)
        {
            _selectedChoice = choice;
            _lblSelection.Text = choice;

            foreach (var tile in _tilesFlow.Controls.OfType<MiscTile>())
            {
                tile.SetSelected(tile.ChoiceId == choice);
            }
        }

        public void ToggleExpanded() => SetExpanded(!_isExpanded);

        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;

            _lblExpand.Text = expanded ? "▼" : "▶";
            _lblExpand.BackColor = expanded ? Theme.Accent : Theme.RowBorderColor;
            _lblExpand.ForeColor = expanded ? Theme.TitleColor : Theme.TextLight;
            _lblExpand.Invalidate();

            _lblTitle.ForeColor = expanded ? Theme.TitleColor : Theme.TextMuted;

            if (expanded)
            {
                _tilesFlow.Visible = true;
                RecalculateLayout();
            }
            else
            {
                _tilesFlow.Visible = false;
                _innerCard.Height = HeaderHeight;
                this.Height = HeaderHeight;
            }

            ExpandedChanged?.Invoke(this);
        }

        public void RecalculateLayout()
        {
            if (!_isExpanded) return;

            int tileCount = _tilesFlow.Controls.Count;
            if (tileCount == 0)
            {
                _tilesFlow.Height = 60;
            }
            else
            {
                int availableWidth = Math.Max(200, this.Width - 24);
                int tileWidth = 150 + 16;  // MiscTile 150px + margins
                int tileHeight = 150 + 28 + 12;  // MiscTile 150px + caption + margins

                int columns = Math.Max(1, availableWidth / tileWidth);
                int rows = (int)Math.Ceiling(tileCount / (double)columns);

                // Calculate padding to center tiles
                int usedWidth = Math.Min(tileCount, columns) * tileWidth;
                int leftPadding = Math.Max(12, (availableWidth - usedWidth) / 2);
                _tilesFlow.Padding = new Padding(leftPadding, 8, 12, 12);

                _tilesFlow.Height = rows * tileHeight + 24;
            }

            _innerCard.Height = HeaderHeight + _tilesFlow.Height;
            this.Height = _innerCard.Height;
        }

        #endregion

        #region Private Methods

        private void OnTileClicked(MiscTile tile)
        {
            ApplySelection(tile.ChoiceId);
            SelectionChanged?.Invoke(OptionId, tile.ChoiceId);
        }

        private void LayoutHeader()
        {
            if (_headerPanel == null || _lblExpand == null) return;

            int padding = 16;
            int headerWidth = _headerPanel.ClientSize.Width;

            // Expand button at far right
            _lblExpand.Left = headerWidth - _lblExpand.Width - padding;
            _lblExpand.Top = (HeaderHeight - _lblExpand.Height) / 2;

            // Center labels (leave space for expand button)
            int labelWidth = headerWidth - 100; // Leave margins on both sides
            _lblTitle.Left = (headerWidth - labelWidth) / 2;
            _lblTitle.Width = labelWidth;
            _lblSelection.Left = (headerWidth - labelWidth) / 2;
            _lblSelection.Width = labelWidth;
        }

        private void PaintExpandButton(object? sender, PaintEventArgs e)
        {
            var lbl = sender as Label;
            if (lbl == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = new GraphicsPath();
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

        #endregion
    }
}
