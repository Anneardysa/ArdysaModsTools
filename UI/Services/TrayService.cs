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
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.UI.Services
{
    /// <summary>
    /// Manages the system tray icon and Windows notifications.
    /// Provides minimize-to-tray functionality and balloon/toast notifications.
    /// </summary>
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Form _parentForm;
        private readonly IConfigService _configService;
        private readonly ContextMenuStrip _contextMenu;
        private bool _disposed;

        // Config keys
        private const string ConfigKeyMinimizeToTray = "MinimizeToTray";
        private const string ConfigKeyShowNotifications = "ShowNotifications";

        /// <summary>
        /// Event raised when user clicks "Support" in the tray menu.
        /// </summary>
        public event EventHandler? SupportClicked;

        /// <summary>
        /// Creates a new TrayService instance.
        /// </summary>
        /// <param name="parentForm">The main form to control visibility.</param>
        /// <param name="configService">Configuration service for settings.</param>
        /// <param name="appIcon">Optional icon for the tray. Uses form icon if null.</param>
        public TrayService(Form parentForm, IConfigService configService, Icon? appIcon = null)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            // Create context menu
            _contextMenu = CreateContextMenu();

            // Create NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon ?? parentForm.Icon ?? SystemIcons.Application,
                Text = "ArdysaModsTools",
                Visible = false,
                ContextMenuStrip = _contextMenu
            };

            // Handle double-click to restore
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

            // Handle balloon click
            _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        }

        /// <summary>
        /// Gets or sets whether minimize-to-tray is enabled.
        /// </summary>
        public bool MinimizeToTrayEnabled
        {
            get => _configService.GetValue(ConfigKeyMinimizeToTray, false);
            set => _configService.SetValue(ConfigKeyMinimizeToTray, value);
        }

        /// <summary>
        /// Gets or sets whether notifications are enabled.
        /// </summary>
        public bool NotificationsEnabled
        {
            get => _configService.GetValue(ConfigKeyShowNotifications, true);
            set => _configService.SetValue(ConfigKeyShowNotifications, value);
        }

        /// <summary>
        /// Sets whether notifications are enabled (for external callers).
        /// </summary>
        /// <param name="enabled">True to enable notifications, false to disable.</param>
        public void SetNotificationsEnabled(bool enabled)
        {
            NotificationsEnabled = enabled;
        }

        /// <summary>
        /// Handles the form's Resize event. Minimizes to tray if enabled.
        /// Call this from MainForm.Resize event.
        /// </summary>
        public void HandleFormResize()
        {
            if (_parentForm.WindowState == FormWindowState.Minimized && MinimizeToTrayEnabled)
            {
                MinimizeToTray();
            }
        }

        /// <summary>
        /// Minimizes the application to the system tray.
        /// </summary>
        public void MinimizeToTray()
        {
            _parentForm.Hide();
            _notifyIcon.Visible = true;
        }

        /// <summary>
        /// Restores the application from the system tray.
        /// </summary>
        public void RestoreFromTray()
        {
            _notifyIcon.Visible = false;
            _parentForm.Show();
            _parentForm.WindowState = FormWindowState.Normal;
            _parentForm.Activate();
        }

        /// <summary>
        /// Shows a Windows notification (balloon tip).
        /// </summary>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message.</param>
        /// <param name="icon">Icon type (default: Info).</param>
        /// <param name="timeout">Duration in milliseconds (default: 3000).</param>
        /// <param name="forceShow">If true, shows even if notifications are disabled.</param>
        public void ShowNotification(string title, string message, 
            ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000, bool forceShow = false)
        {
            if (!forceShow && !NotificationsEnabled)
                return;

            // Ensure icon is visible for notification to show
            bool wasVisible = _notifyIcon.Visible;
            _notifyIcon.Visible = true;

            _notifyIcon.ShowBalloonTip(timeout, title, message, icon);

            // If we're not minimized, hide the icon after a delay
            if (!wasVisible && _parentForm.WindowState != FormWindowState.Minimized)
            {
                // Use a timer to hide the icon after balloon disappears
                var timer = new System.Windows.Forms.Timer { Interval = timeout + 500 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (_parentForm.WindowState != FormWindowState.Minimized)
                        _notifyIcon.Visible = false;
                };
                timer.Start();
            }
        }

        /// <summary>
        /// Shows the donation reminder notification.
        /// This is called on every app startup.
        /// </summary>
        public void ShowDonationReminder()
        {
            ShowNotification(
                "Support ArdysaModsTools ❤️",
                "If you enjoy this tool, consider supporting me! Click to learn more.",
                ToolTipIcon.Info,
                5000,
                forceShow: true  // Always show donation reminder
            );
        }

        /// <summary>
        /// Shows an update available notification.
        /// </summary>
        /// <param name="version">New version string.</param>
        public void ShowUpdateNotification(string version)
        {
            ShowNotification(
                "Update Available",
                $"A new version (v{version}) is available. Click to update!",
                ToolTipIcon.Info,
                5000
            );
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open ArdysaModsTools");
            openItem.Click += (s, e) => RestoreFromTray();
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            menu.Items.Add(openItem);

            menu.Items.Add(new ToolStripSeparator());

            var supportItem = new ToolStripMenuItem("Support Me ❤️");
            supportItem.Click += (s, e) => SupportClicked?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(supportItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => Application.Exit();
            menu.Items.Add(exitItem);

            return menu;
        }

        private void OnBalloonTipClicked(object? sender, EventArgs e)
        {
            // Restore the app when balloon is clicked
            RestoreFromTray();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }
    }
}
