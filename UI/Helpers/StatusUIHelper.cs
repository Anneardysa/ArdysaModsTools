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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.UI.Styles;

namespace ArdysaModsTools.UI.Helpers
{
    /// <summary>
    /// Helper class for updating UI elements based on mod status.
    /// Extracted from StatusService to maintain separation of concerns.
    /// </summary>
    public static class StatusUIHelper
    {
        /// <summary>
        /// Update status indicator labels with the given status info.
        /// Thread-safe - handles cross-thread invocation automatically.
        /// </summary>
        /// <param name="status">The mod status info to display.</param>
        /// <param name="dotLabel">The dot/indicator label.</param>
        /// <param name="textLabel">The text label.</param>
        public static void UpdateLabels(ModStatusInfo status, Label dotLabel, Label textLabel)
        {
            if (status == null) return;

            UpdateLabels(dotLabel, textLabel, status.StatusText, status.StatusColor);
        }

        /// <summary>
        /// Update status indicator labels with custom text and color.
        /// Thread-safe - handles cross-thread invocation automatically.
        /// </summary>
        public static void UpdateLabels(Label dotLabel, Label textLabel, string text, Color color)
        {
            if (dotLabel == null || textLabel == null) return;

            if (dotLabel.InvokeRequired)
            {
                dotLabel.BeginInvoke(new Action(() => UpdateLabels(dotLabel, textLabel, text, color)));
                return;
            }

            dotLabel.BackColor = color;
            textLabel.Text = text;
            textLabel.ForeColor = color;
        }

        /// <summary>
        /// Show "Checking..." state with animation support.
        /// Thread-safe - handles cross-thread invocation automatically.
        /// </summary>
        public static void ShowCheckingState(Label dotLabel, Label textLabel, Label? refreshButton = null)
        {
            if (dotLabel == null || textLabel == null) return;

            if (dotLabel.InvokeRequired)
            {
                dotLabel.BeginInvoke(new Action(() => ShowCheckingState(dotLabel, textLabel, refreshButton)));
                return;
            }

            var checkingColor = StatusColors.Checking;
            dotLabel.BackColor = checkingColor;
            textLabel.Text = "Checking...";
            textLabel.ForeColor = checkingColor;

            // Animate refresh button if provided
            if (refreshButton != null)
            {
                refreshButton.ForeColor = checkingColor;
                refreshButton.Enabled = false;
            }
        }

        /// <summary>
        /// Update a button's appearance based on mod status.
        /// Thread-safe - handles cross-thread invocation automatically.
        /// </summary>
        public static void UpdateButton(Button button, ModStatus status, string? text = null)
        {
            if (button == null) return;

            if (button.InvokeRequired)
            {
                button.BeginInvoke(new Action(() => UpdateButton(button, status, text)));
                return;
            }

            button.BackColor = StatusColors.ButtonForStatus(status);
            
            if (text != null)
            {
                button.Text = text;
            }
        }

        /// <summary>
        /// Get border and text colors for status panel styling.
        /// </summary>
        public static (Color BorderColor, Color TextColor) GetPanelColors(ModStatus status)
        {
            var color = StatusColors.ForStatus(status);
            return (color, color);
        }
    }
}
