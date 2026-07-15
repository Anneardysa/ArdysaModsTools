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
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Form _parentForm;
        private readonly IConfigService _configService;
        private readonly ContextMenuStrip _contextMenu;
        private bool _disposed;

        private const string ConfigKeyMinimizeToTray = "MinimizeToTray";
        private const string ConfigKeyShowNotifications = "ShowNotifications";

        public event EventHandler? SupportClicked;

        public TrayService(Form parentForm, IConfigService configService, Icon? appIcon = null)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            _contextMenu = CreateContextMenu();

            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon ?? parentForm.Icon ?? SystemIcons.Application,
                Text = "ArdysaModsTools",
                Visible = false,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

            _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        }

        public bool MinimizeToTrayEnabled
        {
            get => _configService.GetValue(ConfigKeyMinimizeToTray, false);
            set { _configService.SetValue(ConfigKeyMinimizeToTray, value); _configService.Save(); }
        }

        public bool NotificationsEnabled
        {
            get => _configService.GetValue(ConfigKeyShowNotifications, true);
            set { _configService.SetValue(ConfigKeyShowNotifications, value); _configService.Save(); }
        }

        public void SetNotificationsEnabled(bool enabled)
        {
            NotificationsEnabled = enabled;
        }

        public void HandleFormResize()
        {
            if (_parentForm.WindowState == FormWindowState.Minimized && MinimizeToTrayEnabled)
            {
                MinimizeToTray();
            }
        }

        public void MinimizeToTray()
        {
            _parentForm.Hide();
            _notifyIcon.Visible = true;
        }

        public void RestoreFromTray()
        {
            _notifyIcon.Visible = false;
            _parentForm.Show();
            _parentForm.WindowState = FormWindowState.Normal;
            _parentForm.Activate();
        }

        public void ShowNotification(string title, string message, 
            ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000, bool forceShow = false)
        {
            if (!forceShow && !NotificationsEnabled)
                return;

            bool wasVisible = _notifyIcon.Visible;
            _notifyIcon.Visible = true;

            _notifyIcon.ShowBalloonTip(timeout, title, message, icon);

            if (!wasVisible && _parentForm.WindowState != FormWindowState.Minimized)
            {
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

        public void ShowDonationReminder()
        {
            ShowNotification(
                "Support ArdysaModsTools ❤️",
                "If you enjoy this tool, consider supporting me! Click to learn more.",
                ToolTipIcon.Info,
                5000,
                forceShow: true
            );
        }

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
            RestoreFromTray();
        }

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
