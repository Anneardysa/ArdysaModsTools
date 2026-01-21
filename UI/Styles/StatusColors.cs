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
using System.Drawing;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Styles
{
    /// <summary>
    /// Centralized status color definitions for consistent UI appearance.
    /// </summary>
    public static class StatusColors
    {
        #region Status Colors

        /// <summary>Green - Mods active and ready.</summary>
        public static Color Ready => Color.FromArgb(80, 200, 120);

        /// <summary>Orange - Update required.</summary>
        public static Color NeedUpdate => Color.FromArgb(255, 180, 50);

        /// <summary>Blue-gray - Mods disabled.</summary>
        public static Color Disabled => Color.FromArgb(150, 150, 180);

        /// <summary>Gray - Mods not installed.</summary>
        public static Color NotInstalled => Color.FromArgb(120, 120, 120);

        /// <summary>Red - Error state.</summary>
        public static Color Error => Color.FromArgb(255, 80, 80);

        /// <summary>Gray - Default/unknown state.</summary>
        public static Color Default => Color.FromArgb(150, 150, 150);

        /// <summary>Gray - Not checked yet.</summary>
        public static Color NotChecked => Color.FromArgb(150, 150, 150);

        /// <summary>Cyan - Currently checking status.</summary>
        public static Color Checking => Color.FromArgb(100, 180, 220);

        #endregion

        #region Button Colors

        /// <summary>Background for ready state button.</summary>
        public static Color ButtonReady => Color.FromArgb(42, 42, 55);

        /// <summary>Background for need update state button.</summary>
        public static Color ButtonNeedUpdate => Color.FromArgb(60, 80, 60);

        /// <summary>Background for disabled state button.</summary>
        public static Color ButtonDisabled => Color.FromArgb(50, 60, 70);

        /// <summary>Background for not installed state button.</summary>
        public static Color ButtonNotInstalled => Color.FromArgb(35, 35, 45);

        /// <summary>Background for error state button.</summary>
        public static Color ButtonError => Color.FromArgb(60, 40, 40);

        /// <summary>Default button background.</summary>
        public static Color ButtonDefault => Color.FromArgb(42, 42, 55);

        #endregion

        #region Methods

        /// <summary>
        /// Get the appropriate color for a mod status.
        /// </summary>
        public static Color ForStatus(ModStatus status) => status switch
        {
            ModStatus.Ready => Ready,
            ModStatus.NeedUpdate => NeedUpdate,
            ModStatus.Disabled => Disabled,
            ModStatus.NotInstalled => NotInstalled,
            ModStatus.Error => Error,
            ModStatus.NotChecked => NotChecked,
            ModStatus.Checking => Checking,
            _ => Default
        };

        /// <summary>
        /// Get the appropriate button background color for a mod status.
        /// </summary>
        public static Color ButtonForStatus(ModStatus status) => status switch
        {
            ModStatus.Ready => ButtonReady,
            ModStatus.NeedUpdate => ButtonNeedUpdate,
            ModStatus.Disabled => ButtonDisabled,
            ModStatus.NotInstalled => ButtonNotInstalled,
            ModStatus.Error => ButtonError,
            _ => ButtonDefault
        };

        #endregion
    }
}
