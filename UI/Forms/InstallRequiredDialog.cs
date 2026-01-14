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
    /// Dialog shown when mods are not installed.
    /// Informs user that ModsPack installation is required and offers action.
    /// </summary>
    public sealed class InstallRequiredDialog : Form
    {
        /// <summary>
        /// Result of the dialog - true if user clicked Install Now.
        /// </summary>
        public bool ShouldInstall { get; private set; }

        public InstallRequiredDialog()
        {
            InitializeComponent();
            UI.FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent()
        {
            // Form settings
            Text = "ModsPack Required";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(500, 300);
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
                Text = "[ INSTALL REQUIRED ]",
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 50), // Yellow
                BackColor = Color.FromArgb(30, 30, 30),
                AutoSize = false,
                Size = new Size(180, 28),
                Location = new Point((Width - 180) / 2, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badgeLabel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 220, 50), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, badgeLabel.Width - 1, badgeLabel.Height - 1);
            };
            mainContainer.Controls.Add(badgeLabel);

            // Title - centered
            var titleLabel = new Label
            {
                Text = "MODSPACK NOT INSTALLED",
                Font = new Font("JetBrains Mono", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(Width - 60, 32),
                Location = new Point(30, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(titleLabel);

            // Description - centered
            var descLabel = new Label
            {
                Text = "Dota 2 path detected successfully!\n\nHowever, the ModsPack is not installed yet.\nClick 'Install Now' to download and install the mods,\nor 'Later' to do it manually.",
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(170, 170, 170),
                AutoSize = false,
                Size = new Size(Width - 60, 85),
                Location = new Point(30, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(descLabel);

            // Buttons container
            int buttonY = 200;
            int buttonWidth = 140;
            int buttonSpacing = 20;
            int buttonsStartX = (Width - (buttonWidth * 2 + buttonSpacing)) / 2;

            // Install Now button (primary - yellow highlight)
            var installButton = new Controls.RoundedButton
            {
                Text = "INSTALL NOW",
                Size = new Size(buttonWidth, 42),
                Location = new Point(buttonsStartX, buttonY),
                BackColor = Color.FromArgb(255, 220, 50), // Yellow
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(255, 220, 50),
                HoverBackColor = Color.FromArgb(255, 240, 100),
                HoverForeColor = Color.Black
            };
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Click += (s, e) =>
            {
                ShouldInstall = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            mainContainer.Controls.Add(installButton);

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
                ShouldInstall = false;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            mainContainer.Controls.Add(laterButton);

            // Border paint (yellow border for highlight)
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 220, 50), 1);
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

