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

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Settings dialog form for application configuration.
    /// </summary>
    public partial class SettingsForm : Form
    {
        // UI Controls
        private CheckBox _runOnStartupCheckBox = null!;
        private CheckBox _minimizeToTrayCheckBox = null!;
        private CheckBox _showNotificationsCheckBox = null!;
        private Button _checkUpdatesButton = null!;
        private Button _clearCacheButton = null!;
        private Label _versionLabel = null!;
        private Label _cacheSizeLabel = null!;
        private Button _closeButton = null!;

        // Events for presenter
        public event EventHandler? RunOnStartupChanged;
        public event EventHandler? MinimizeToTrayChanged;
        public event EventHandler? ShowNotificationsChanged;
        public event EventHandler? CheckUpdatesClicked;
        public event EventHandler? ClearCacheClicked;

        // Properties for data binding
        public bool RunOnStartup
        {
            get => _runOnStartupCheckBox.Checked;
            set => _runOnStartupCheckBox.Checked = value;
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTrayCheckBox.Checked;
            set => _minimizeToTrayCheckBox.Checked = value;
        }

        public bool ShowNotifications
        {
            get => _showNotificationsCheckBox.Checked;
            set => _showNotificationsCheckBox.Checked = value;
        }

        public string VersionText
        {
            set => _versionLabel.Text = value;
        }

        public string CacheSizeText
        {
            set => _cacheSizeLabel.Text = value;
        }

        public SettingsForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Settings";
            this.Size = new Size(400, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);

            int yPos = 20;
            int leftMargin = 20;

            // === Startup Section ===
            var startupLabel = CreateSectionLabel("Startup", leftMargin, yPos);
            this.Controls.Add(startupLabel);
            yPos += 30;

            _runOnStartupCheckBox = CreateCheckBox("Run on Windows Start", leftMargin + 10, yPos);
            _runOnStartupCheckBox.CheckedChanged += (s, e) => RunOnStartupChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_runOnStartupCheckBox);
            yPos += 35;

            // === System Tray Section ===
            var trayLabel = CreateSectionLabel("System Tray", leftMargin, yPos);
            this.Controls.Add(trayLabel);
            yPos += 30;

            _minimizeToTrayCheckBox = CreateCheckBox("Minimize to System Tray", leftMargin + 10, yPos);
            _minimizeToTrayCheckBox.CheckedChanged += (s, e) => MinimizeToTrayChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_minimizeToTrayCheckBox);
            yPos += 35;

            _showNotificationsCheckBox = CreateCheckBox("Show Notifications", leftMargin + 10, yPos);
            _showNotificationsCheckBox.CheckedChanged += (s, e) => ShowNotificationsChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_showNotificationsCheckBox);
            yPos += 45;

            // === Actions Section ===
            var actionsLabel = CreateSectionLabel("Actions", leftMargin, yPos);
            this.Controls.Add(actionsLabel);
            yPos += 30;

            _checkUpdatesButton = CreateActionButton("Check for Updates", leftMargin + 10, yPos, 160);
            _checkUpdatesButton.Click += (s, e) => CheckUpdatesClicked?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_checkUpdatesButton);

            _clearCacheButton = CreateActionButton("Clear Cache", leftMargin + 180, yPos, 160);
            _clearCacheButton.Click += (s, e) => ClearCacheClicked?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_clearCacheButton);
            yPos += 45;

            // Cache size info
            _cacheSizeLabel = new Label
            {
                Location = new Point(leftMargin + 10, yPos),
                Size = new Size(340, 20),
                ForeColor = Color.Gray,
                Text = "Cache: Calculating..."
            };
            this.Controls.Add(_cacheSizeLabel);
            yPos += 35;

            // === Version Footer ===
            _versionLabel = new Label
            {
                Location = new Point(leftMargin, yPos),
                Size = new Size(340, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Text = "Version: Loading...",
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_versionLabel);
            yPos += 35;

            // Close button
            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point((this.ClientSize.Width - 100) / 2, yPos),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _closeButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(_closeButton);

            this.ResumeLayout(false);
        }

        private Label CreateSectionLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(340, 25),
                ForeColor = Color.FromArgb(0, 200, 150),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
        }

        private CheckBox CreateCheckBox(string text, int x, int y)
        {
            var checkBox = new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(320, 25),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            return checkBox;
        }

        private Button CreateActionButton(string text, int x, int y, int width)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 35),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
            return button;
        }

        /// <summary>
        /// Shows a status message temporarily.
        /// </summary>
        public void ShowStatus(string message, bool isSuccess = true)
        {
            MessageBox.Show(
                message,
                isSuccess ? "Success" : "Error",
                MessageBoxButtons.OK,
                isSuccess ? MessageBoxIcon.Information : MessageBoxIcon.Error
            );
        }

        /// <summary>
        /// Sets the busy state of action buttons.
        /// </summary>
        public void SetBusy(bool busy)
        {
            _checkUpdatesButton.Enabled = !busy;
            _clearCacheButton.Enabled = !busy;
            _closeButton.Enabled = !busy;
        }
    }
}
