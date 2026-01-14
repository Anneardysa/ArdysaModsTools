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
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Dialog shown after ModsPack install when patching is required.
    /// Informs user that Patch Update is needed and offers action.
    /// </summary>
    public sealed class PatchRequiredDialog : Form
    {
        /// <summary>
        /// Result of the dialog - true if user clicked Patch Update.
        /// </summary>
        public bool ShouldPatch { get; private set; }

        public PatchRequiredDialog(string successMessage = "ModsPack installed successfully!")
        {
            InitializeComponent(successMessage);
            UI.FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent(string successMessage)
        {
            // Form settings
            Text = "Patch Required";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(500, 280);
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
                Text = "[ ATTENTION ]",
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 180, 50), // Orange
                BackColor = Color.FromArgb(30, 30, 30),
                AutoSize = false,
                Size = new Size(140, 28),
                Location = new Point((Width - 140) / 2, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badgeLabel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 180, 50), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, badgeLabel.Width - 1, badgeLabel.Height - 1);
            };
            mainContainer.Controls.Add(badgeLabel);

            // Title - centered
            var titleLabel = new Label
            {
                Text = "PATCH UPDATE REQUIRED",
                Font = new Font("JetBrains Mono", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(Width - 60, 32),
                Location = new Point(30, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(titleLabel);

            // Description - centered (uses successMessage parameter)
            var descLabel = new Label
            {
                Text = $"{successMessage}\n\nHowever, Dota 2 files need to be patched for mods to work.\nClick 'Patch Now' to complete the installation.",
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(170, 170, 170),
                AutoSize = false,
                Size = new Size(Width - 60, 70),
                Location = new Point(30, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(descLabel);

            // Buttons container
            int buttonY = 185;
            int buttonWidth = 140;
            int buttonSpacing = 20;
            int buttonsStartX = (Width - (buttonWidth * 2 + buttonSpacing)) / 2;

            // Patch Now button (primary)
            var patchButton = new Controls.RoundedButton
            {
                Text = "PATCH NOW",
                Size = new Size(buttonWidth, 42),
                Location = new Point(buttonsStartX, buttonY),
                BackColor = Color.FromArgb(255, 180, 50),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(255, 180, 50),
                HoverBackColor = Color.FromArgb(255, 200, 80),
                HoverForeColor = Color.Black
            };
            patchButton.FlatAppearance.BorderSize = 0;
            patchButton.Click += (s, e) =>
            {
                ShouldPatch = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            mainContainer.Controls.Add(patchButton);

            // Later button (secondary)
            var laterButton = new Controls.RoundedButton
            {
                Text = "LATER",
                Size = new Size(buttonWidth, 42),
                Location = new Point(buttonsStartX + buttonWidth + buttonSpacing, buttonY),
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
            laterButton.FlatAppearance.BorderSize = 0;
            laterButton.Click += (s, e) =>
            {
                ShouldPatch = false;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            mainContainer.Controls.Add(laterButton);

            // Border paint (orange border for attention)
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 180, 50), 1);
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

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }
    }
}

