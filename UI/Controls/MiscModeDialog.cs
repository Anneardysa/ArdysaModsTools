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
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// Dialog for selecting generation mode in MiscForm.
    /// Styled to match DisableOptionsDialog theme.
    /// </summary>
    public sealed class MiscModeDialog : Form
    {
        private MiscGenerationMode _selectedMode = MiscGenerationMode.AddToCurrent;
        private Panel _optionAddToCurrent = null!;
        private Panel _optionGenerateOnly = null!;
        private RoundedButton _btnConfirm = null!;
        private RoundedButton _btnCancel = null!;

        public MiscGenerationMode SelectedMode => _selectedMode;

        public MiscModeDialog()
        {
            InitializeComponent();
            UI.FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent()
        {
            // Form settings - borderless modern look
            Text = "Select Generation Mode";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(480, 320);
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
                Padding = new Padding(0)
            };
            Controls.Add(mainContainer);

            // Header panel
            var headerPanel = new Panel
            {
                Height = 60,
                Dock = DockStyle.Top,
                BackColor = Color.Black
            };

            var titleLabel = new Label
            {
                Text = "⚠ SELECT GENERATION MODE ⚠",
                Font = new Font("JetBrains Mono", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(titleLabel);
            mainContainer.Controls.Add(headerPanel);

            // Options container - centered
            var optionsPanel = new Panel
            {
                Size = new Size(440, 160),
                BackColor = Color.Transparent
            };
            optionsPanel.Location = new Point((Width - optionsPanel.Width) / 2, 70);

            // Option 1: Add to Current Mods - Cyan accent
            _optionAddToCurrent = CreateOptionCard(
                "[ Add to Current Mods ]",
                "Apply modifications on top of existing game mods.",
                Color.FromArgb(0, 255, 255), // Cyan
                0
            );
            _optionAddToCurrent.Click += (s, e) => SelectOption(MiscGenerationMode.AddToCurrent);
            MakeClickable(_optionAddToCurrent);
            optionsPanel.Controls.Add(_optionAddToCurrent);

            // Option 2: Generate Only - Amber accent (warning)
            _optionGenerateOnly = CreateOptionCard(
                "[ Generate Only Misc Mods ]",
                "Create a clean VPK with only Misc mods. Removes existing mods!",
                Color.FromArgb(255, 180, 100), // Amber
                85
            );
            _optionGenerateOnly.Click += (s, e) => SelectOption(MiscGenerationMode.GenerateOnly);
            MakeClickable(_optionGenerateOnly);
            optionsPanel.Controls.Add(_optionGenerateOnly);

            mainContainer.Controls.Add(optionsPanel);

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.Black
            };

            // Center buttons
            int btnY = (buttonPanel.Height - 44) / 2;

            // Confirm button - white bg, black text (primary)
            _btnConfirm = new RoundedButton
            {
                Text = "[ CONFIRM ]",
                Size = new Size(140, 44),
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
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            // Cancel button - black bg, grey text (secondary)
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

            mainContainer.Controls.Add(buttonPanel);

            // Border paint
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make form draggable
            titleLabel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            // Initial selection
            SelectOption(MiscGenerationMode.AddToCurrent);
        }

        private Panel CreateOptionCard(string title, string desc, Color accentColor, int yPos)
        {
            var card = new Panel
            {
                Size = new Size(440, 75),
                Location = new Point(0, yPos),
                BackColor = Color.FromArgb(15, 15, 15),
                Cursor = Cursors.Hand,
                Tag = accentColor
            };

            // Title - centered
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("JetBrains Mono", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                Size = new Size(420, 28),
                Location = new Point(10, 14),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            // Description - centered
            var lblDesc = new Label
            {
                Text = desc,
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(136, 136, 136),
                Size = new Size(420, 20),
                Location = new Point(10, 44),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblDesc);

            // Hover effects
            card.MouseEnter += (s, e) => { if (!IsSelected(card)) card.BackColor = Color.FromArgb(25, 25, 25); };
            card.MouseLeave += (s, e) => { if (!IsSelected(card)) card.BackColor = Color.FromArgb(15, 15, 15); };

            // Paint border
            card.Paint += (s, e) =>
            {
                var accent = (Color)card.Tag;
                using var pen = new Pen(IsSelected(card) ? accent : Color.FromArgb(51, 51, 51), IsSelected(card) ? 2 : 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            return card;
        }

        private bool IsSelected(Panel card)
        {
            if (card == _optionAddToCurrent && _selectedMode == MiscGenerationMode.AddToCurrent) return true;
            if (card == _optionGenerateOnly && _selectedMode == MiscGenerationMode.GenerateOnly) return true;
            return false;
        }

        private void MakeClickable(Panel panel)
        {
            foreach (Control c in panel.Controls)
            {
                c.Click += (s, e) => panel.PerformClick();
                c.MouseEnter += (s, e) => { if (!IsSelected(panel)) panel.BackColor = Color.FromArgb(25, 25, 25); };
                c.MouseLeave += (s, e) => { if (!IsSelected(panel)) panel.BackColor = Color.FromArgb(15, 15, 15); };
            }
        }

        private void SelectOption(MiscGenerationMode mode)
        {
            _selectedMode = mode;
            
            // Update visuals
            _optionAddToCurrent.BackColor = mode == MiscGenerationMode.AddToCurrent 
                ? Color.FromArgb(20, 20, 20) 
                : Color.FromArgb(15, 15, 15);
            _optionGenerateOnly.BackColor = mode == MiscGenerationMode.GenerateOnly 
                ? Color.FromArgb(25, 20, 15) // Slightly amber tint
                : Color.FromArgb(15, 15, 15);
            
            // Update title colors based on selection
            foreach (Control c in _optionAddToCurrent.Controls)
            {
                if (c is Label lbl && lbl.Font.Bold)
                    lbl.ForeColor = mode == MiscGenerationMode.AddToCurrent ? Color.FromArgb(0, 255, 255) : Color.White;
            }
            foreach (Control c in _optionGenerateOnly.Controls)
            {
                if (c is Label lbl && lbl.Font.Bold)
                    lbl.ForeColor = mode == MiscGenerationMode.GenerateOnly ? Color.FromArgb(255, 180, 100) : Color.White;
            }

            _optionAddToCurrent.Invalidate();
            _optionGenerateOnly.Invalidate();
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

