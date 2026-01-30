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
using System.Linq;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Dialog for resolving critical mod conflicts that require user intervention.
    /// Presents available resolution options and allows user selection.
    /// </summary>
    public sealed class ConflictResolutionDialog : Form
    {
        private readonly ModConflict _conflict;
        private ConflictResolutionOption? _selectedOption;
        private Panel _optionsContainer = null!;
        private Panel? _selectedCard;
        private RoundedButton _btnConfirm = null!;
        private RoundedButton _btnCancel = null!;

        /// <summary>
        /// Gets the resolution option selected by the user.
        /// </summary>
        public ConflictResolutionOption? SelectedOption => _selectedOption;

        /// <summary>
        /// Creates a new ConflictResolutionDialog for the specified conflict.
        /// </summary>
        /// <param name="conflict">The conflict requiring resolution.</param>
        public ConflictResolutionDialog(ModConflict conflict)
        {
            _conflict = conflict ?? throw new ArgumentNullException(nameof(conflict));
            InitializeComponent();
            FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent()
        {
            // Form settings - borderless modern dark theme
            Text = "Resolve Mod Conflict";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 480);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            // Main container
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(20)
            };
            Controls.Add(mainContainer);

            // Header with warning icon
            var headerPanel = CreateHeaderPanel();
            mainContainer.Controls.Add(headerPanel);

            // Conflict details panel
            var detailsPanel = CreateDetailsPanel();
            detailsPanel.Location = new Point(20, 70);
            mainContainer.Controls.Add(detailsPanel);

            // Resolution options container
            _optionsContainer = new Panel
            {
                Location = new Point(20, 175),
                Size = new Size(Width - 40, 200),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            CreateOptionCards();
            mainContainer.Controls.Add(_optionsContainer);

            // Button panel
            var buttonPanel = CreateButtonPanel();
            mainContainer.Controls.Add(buttonPanel);

            // Border paint
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 100, 100), 2);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make form draggable
            headerPanel.MouseDown += HandleDrag;
            foreach (Control c in headerPanel.Controls)
            {
                c.MouseDown += HandleDrag;
            }
        }

        private Panel CreateHeaderPanel()
        {
            var headerPanel = new Panel
            {
                Height = 60,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(40, 0, 0) // Subtle red tint for warning
            };

            var warningIcon = new Label
            {
                Text = "⚠",
                Font = new Font("Segoe UI Emoji", 24f),
                ForeColor = Color.FromArgb(255, 100, 100),
                AutoSize = true,
                Location = new Point(20, 10)
            };

            var titleLabel = new Label
            {
                Text = "MOD CONFLICT DETECTED",
                Font = new Font("JetBrains Mono", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 100, 100),
                AutoSize = true,
                Location = new Point(65, 18)
            };

            headerPanel.Controls.Add(warningIcon);
            headerPanel.Controls.Add(titleLabel);

            return headerPanel;
        }

        private Panel CreateDetailsPanel()
        {
            var panel = new Panel
            {
                Size = new Size(Width - 40, 95),
                BackColor = Color.FromArgb(15, 15, 15)
            };

            // Conflict type and severity
            var typeLabel = new Label
            {
                Text = $"[{_conflict.Severity}] {_conflict.Type} Conflict",
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = GetSeverityColor(_conflict.Severity),
                AutoSize = true,
                Location = new Point(15, 12)
            };

            // Description
            var descLabel = new Label
            {
                Text = _conflict.Description,
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.White,
                Size = new Size(Width - 80, 40),
                Location = new Point(15, 35)
            };

            // Affected files count
            var filesLabel = new Label
            {
                Text = $"Affected files: {_conflict.AffectedFiles.Count}",
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = true,
                Location = new Point(15, 72)
            };

            panel.Controls.Add(typeLabel);
            panel.Controls.Add(descLabel);
            panel.Controls.Add(filesLabel);

            // Border
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            return panel;
        }

        private void CreateOptionCards()
        {
            int yPos = 0;
            foreach (var option in _conflict.AvailableResolutions)
            {
                var card = CreateOptionCard(option, yPos);
                _optionsContainer.Controls.Add(card);
                yPos += 55;
            }
        }

        private Panel CreateOptionCard(ConflictResolutionOption option, int yPos)
        {
            var card = new Panel
            {
                Size = new Size(_optionsContainer.Width - 20, 50),
                Location = new Point(0, yPos),
                BackColor = Color.FromArgb(15, 15, 15),
                Cursor = Cursors.Hand,
                Tag = option
            };

            // Strategy badge
            var strategyLabel = new Label
            {
                Text = $"[{option.Strategy}]",
                Font = new Font("JetBrains Mono", 9f, FontStyle.Bold),
                ForeColor = GetStrategyColor(option.Strategy),
                AutoSize = true,
                Location = new Point(12, 8),
                Cursor = Cursors.Hand
            };

            // Description
            var descLabel = new Label
            {
                Text = option.Description,
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.White,
                Size = new Size(card.Width - 30, 20),
                Location = new Point(12, 28),
                Cursor = Cursors.Hand
            };

            card.Controls.Add(strategyLabel);
            card.Controls.Add(descLabel);

            // Click handlers
            card.Click += (s, e) => SelectOption(card, option);
            strategyLabel.Click += (s, e) => SelectOption(card, option);
            descLabel.Click += (s, e) => SelectOption(card, option);

            // Hover effects
            void OnEnter(object? s, EventArgs e) { if (_selectedCard != card) card.BackColor = Color.FromArgb(25, 25, 25); }
            void OnLeave(object? s, EventArgs e) { if (_selectedCard != card) card.BackColor = Color.FromArgb(15, 15, 15); }

            card.MouseEnter += OnEnter;
            card.MouseLeave += OnLeave;
            strategyLabel.MouseEnter += OnEnter;
            strategyLabel.MouseLeave += OnLeave;
            descLabel.MouseEnter += OnEnter;
            descLabel.MouseLeave += OnLeave;

            // Border paint
            card.Paint += (s, e) =>
            {
                var isSelected = _selectedCard == card;
                var color = isSelected ? Theme.Accent : Color.FromArgb(51, 51, 51);
                using var pen = new Pen(color, isSelected ? 2 : 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            return card;
        }

        private void SelectOption(Panel card, ConflictResolutionOption option)
        {
            // Deselect previous
            if (_selectedCard != null)
            {
                _selectedCard.BackColor = Color.FromArgb(15, 15, 15);
                _selectedCard.Invalidate();
            }

            // Select new
            _selectedCard = card;
            _selectedOption = option;
            card.BackColor = Color.FromArgb(20, 30, 30);
            card.Invalidate();

            // Enable confirm button
            _btnConfirm.Enabled = true;
            _btnConfirm.BackColor = Color.White;
        }

        private Panel CreateButtonPanel()
        {
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.Black
            };

            int btnY = (buttonPanel.Height - 44) / 2;

            // Confirm button - disabled until selection
            _btnConfirm = new RoundedButton
            {
                Text = "[ APPLY RESOLUTION ]",
                Size = new Size(180, 44),
                BackColor = Color.FromArgb(51, 51, 51),
                ForeColor = Color.FromArgb(100, 100, 100),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.Black,
                HoverForeColor = Color.White,
                Enabled = false
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += (s, e) =>
            {
                if (_selectedOption != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            // Cancel button
            _btnCancel = new RoundedButton
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
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            buttonPanel.Controls.Add(_btnConfirm);
            buttonPanel.Controls.Add(_btnCancel);

            // Center buttons on resize
            buttonPanel.Resize += (s, e) =>
            {
                int totalWidth = _btnCancel.Width + 15 + _btnConfirm.Width;
                int startX = (buttonPanel.Width - totalWidth) / 2;
                _btnCancel.Location = new Point(startX, btnY);
                _btnConfirm.Location = new Point(startX + _btnCancel.Width + 15, btnY);
            };

            return buttonPanel;
        }

        private static Color GetSeverityColor(ConflictSeverity severity)
        {
            return severity switch
            {
                ConflictSeverity.Low => Color.FromArgb(100, 200, 100),
                ConflictSeverity.Medium => Color.FromArgb(255, 200, 100),
                ConflictSeverity.High => Color.FromArgb(255, 150, 100),
                ConflictSeverity.Critical => Color.FromArgb(255, 100, 100),
                _ => Color.White
            };
        }

        private static Color GetStrategyColor(ResolutionStrategy strategy)
        {
            return strategy switch
            {
                ResolutionStrategy.HigherPriority => Color.FromArgb(0, 255, 255),
                ResolutionStrategy.MostRecent => Color.FromArgb(150, 200, 255),
                ResolutionStrategy.Merge => Color.FromArgb(200, 150, 255),
                ResolutionStrategy.KeepExisting => Color.FromArgb(100, 255, 150),
                ResolutionStrategy.UseNew => Color.FromArgb(255, 200, 100),
                ResolutionStrategy.Interactive => Color.FromArgb(255, 150, 150),
                _ => Color.White
            };
        }

        private void HandleDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
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
