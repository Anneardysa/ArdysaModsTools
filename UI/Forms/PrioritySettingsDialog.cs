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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Conflict;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Dialog for managing mod priorities.
    /// Wraps the PriorityManagerPanel in a borderless dark-themed dialog.
    /// </summary>
    public sealed class PrioritySettingsDialog : Form
    {
        private readonly string _targetPath;
        private readonly IModPriorityService _priorityService;
        private PriorityManagerPanel _priorityPanel = null!;
        private RoundedButton _btnClose = null!;

        /// <summary>
        /// Creates a new PrioritySettingsDialog.
        /// </summary>
        /// <param name="targetPath">Target path for loading/saving priority config.</param>
        /// <param name="priorityService">Optional priority service (uses default if null).</param>
        public PrioritySettingsDialog(string targetPath, IModPriorityService? priorityService = null)
        {
            _targetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
            _priorityService = priorityService ?? new ModPriorityService();
            InitializeComponent();
            FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent()
        {
            // Form settings - borderless modern dark theme
            Text = "Mod Priority Settings";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(460, 550);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            // Main container
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(20)
            };
            Controls.Add(mainContainer);

            // Header
            var headerPanel = CreateHeaderPanel();
            mainContainer.Controls.Add(headerPanel);

            // Priority panel
            _priorityPanel = new PriorityManagerPanel(_priorityService)
            {
                Location = new Point(15, 70),
                Size = new Size(Width - 30, Height - 150)
            };
            mainContainer.Controls.Add(_priorityPanel);

            // Close button
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.Black
            };

            _btnClose = new RoundedButton
            {
                Text = "[ CLOSE ]",
                Size = new Size(120, 44),
                BackColor = Color.FromArgb(26, 26, 26),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(40, 40, 40),
                HoverForeColor = Color.White
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => Close();

            buttonPanel.Controls.Add(_btnClose);
            buttonPanel.Resize += (s, e) =>
            {
                _btnClose.Location = new Point((buttonPanel.Width - _btnClose.Width) / 2, 8);
            };

            mainContainer.Controls.Add(buttonPanel);

            // Border paint
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make form draggable
            headerPanel.MouseDown += HandleDrag;
            foreach (Control c in headerPanel.Controls)
            {
                c.MouseDown += HandleDrag;
            }

            // Load priorities when shown
            Load += async (s, e) => await _priorityPanel.LoadPrioritiesAsync(_targetPath);
        }

        private Panel CreateHeaderPanel()
        {
            var headerPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.Black
            };

            var titleLabel = new Label
            {
                Text = "⚙ MOD PRIORITY SETTINGS",
                Font = new Font("JetBrains Mono", 12f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                AutoSize = true,
                Location = new Point(15, 15)
            };

            var helpLabel = new Label
            {
                Text = "Drag to reorder • Higher position = Higher priority",
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = true,
                Location = new Point(Width - 300, 20)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(helpLabel);

            return headerPanel;
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
