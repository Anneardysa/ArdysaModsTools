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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ArdysaModsTools.Helpers
{
    /// <summary>
    /// Common UI utility methods for consistent behavior across forms.
    /// </summary>
    public static class UIHelpers
    {
        #region Window Styling

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        /// <summary>
        /// Applies rounded corners to a form.
        /// </summary>
        /// <param name="form">The form to apply rounded corners to.</param>
        /// <param name="radius">Corner radius in pixels.</param>
        public static void ApplyRoundedCorner(Form form, int radius = 16)
        {
            if (form == null) return;
            var region = CreateRoundRectRgn(0, 0, form.Width + 1, form.Height + 1, radius, radius);
            form.Region = Region.FromHrgn(region);
        }

        /// <summary>
        /// Animates a form sliding in from the right edge of the screen.
        /// </summary>
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

        /// <summary>
        /// Opens a URL in the default browser with error handling.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <param name="errorCallback">Optional callback for logging errors.</param>
        /// <returns>True if successful, false otherwise.</returns>
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

        /// <summary>
        /// Opens a URL and shows an error dialog if it fails.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <param name="urlName">Display name for the URL (e.g., "Discord", "YouTube").</param>
        /// <param name="errorCallback">Optional callback for logging errors.</param>
        public static void OpenUrlWithErrorDialog(string url, string urlName, Action<string>? errorCallback = null)
        {
            if (!OpenUrl(url, errorCallback))
            {
                ShowError($"Failed to open {urlName} link.", "Error");
            }
        }

        #endregion

        #region Thread-Safe UI Updates

        /// <summary>
        /// Executes an action on the UI thread, invoking if necessary.
        /// </summary>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="action">The action to execute.</param>
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
                    // Control was disposed before invoke completed
                }
                catch (InvalidOperationException)
                {
                    // Handle was not created or control is disposing
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Executes an action on the UI thread asynchronously, invoking if necessary.
        /// </summary>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="action">The action to execute.</param>
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
                    // Control was disposed before invoke completed
                }
                catch (InvalidOperationException)
                {
                    // Handle was not created or control is disposing
                }
            }
            else
            {
                action();
            }
        }

        #endregion

        #region Standard Dialogs

        /// <summary>
        /// Shows a standard error dialog.
        /// </summary>
        /// <param name="message">Error message to display.</param>
        /// <param name="title">Dialog title.</param>
        public static void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Shows a standard warning dialog.
        /// </summary>
        /// <param name="message">Warning message to display.</param>
        /// <param name="title">Dialog title.</param>
        public static void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Shows a standard information dialog.
        /// </summary>
        /// <param name="message">Information message to display.</param>
        /// <param name="title">Dialog title.</param>
        public static void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Shows a confirmation dialog and returns the user's choice.
        /// </summary>
        /// <param name="message">Question to ask.</param>
        /// <param name="title">Dialog title.</param>
        /// <returns>True if user clicked Yes, false otherwise.</returns>
        public static bool ShowConfirm(string message, string title = "Confirm")
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        /// <summary>
        /// Shows a confirmation dialog with a warning icon.
        /// </summary>
        /// <param name="message">Warning question to ask.</param>
        /// <param name="title">Dialog title.</param>
        /// <returns>True if user clicked Yes, false otherwise.</returns>
        public static bool ShowConfirmWarning(string message, string title = "Warning")
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        #endregion
    }
}

