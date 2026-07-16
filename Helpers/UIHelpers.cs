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
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.Helpers
{
    public static class UIHelpers
    {
        #region Window Styling

        public static void SlideInFromRight(Form form, Rectangle targetBounds, int durationMs = 300)
        {
            var fps = 60;
            var interval = 1000 / fps;
            var steps = Math.Max(1, durationMs / interval);
            var start = new Point(Screen.FromControl(form).WorkingArea.Right, targetBounds.Y);
            var end = new Point(targetBounds.X, targetBounds.Y);
            var deltaX = (end.X - start.X) / (double)steps;
            var timer = new System.Windows.Forms.Timer { Interval = interval };
            int current = 0;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(start, targetBounds.Size);
            form.Show();
            timer.Tick += (s, e) =>
            {
                current++;
                var newX = (int)Math.Round(start.X + deltaX * current);
                form.Left = newX;
                if (current >= steps)
                {
                    timer.Stop();
                    form.Left = end.X;
                }
            };
            timer.Start();
        }

        #endregion

        #region URL Handling

        public static bool OpenUrl(string url, Action<string>? errorCallback = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                errorCallback?.Invoke("URL is empty or null.");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorCallback?.Invoke($"Failed to open URL: {ex.Message}");
                return false;
            }
        }

        public static void OpenUrlWithErrorDialog(string url, string urlName, Action<string>? errorCallback = null)
        {
            if (!OpenUrl(url, errorCallback))
            {
                ShowError(Loc.T("error.openLink", new { name = urlName }));
            }
        }

        private const string DiscordInviteUrl = "https://discord.gg/ffXw265Z7e";

        public static async Task ShowFeatureUnavailableAsync(
            IMainFormView view,
            string featureDisplayName,
            string message,
            Action<string>? log = null)
        {
            bool joinDiscord = await view.ShowShellConfirmAsync(
                eyebrow: Loc.T("feature.unavailable.title"),
                heading: featureDisplayName,
                body: message,
                confirmText: Loc.T("feature.unavailable.joinDiscord"),
                cancelText: Loc.T("common.close"),
                accent: "warn");

            if (joinDiscord)
                OpenUrlWithErrorDialog(DiscordInviteUrl, "Discord", log);
        }

        #endregion

        #region Thread-Safe UI Updates

        public static void SafeInvoke(this Control control, Action action)
        {
            if (control == null || control.IsDisposed)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            else
            {
                action();
            }
        }

        public static void SafeBeginInvoke(this Control control, Action action)
        {
            if (control == null || control.IsDisposed)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            else
            {
                action();
            }
        }

        #endregion

        #region Standard Dialogs

        public static void ShowError(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowWarning(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowInfo(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.information"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool ShowConfirm(string message, string? title = null)
        {
            return MessageBox.Show(message, title ?? Loc.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public static bool ShowConfirmWarning(string message, string? title = null)
        {
            return MessageBox.Show(message, title ?? Loc.T("common.warning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        #endregion
    }
}

