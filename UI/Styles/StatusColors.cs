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
    public static class StatusColors
    {
        #region Status Colors

        public static Color Ready => Color.FromArgb(80, 200, 120);

        public static Color NeedUpdate => Color.FromArgb(255, 180, 50);

        public static Color Disabled => Color.FromArgb(150, 150, 180);

        public static Color NotInstalled => Color.FromArgb(120, 120, 120);

        public static Color Error => Color.FromArgb(255, 80, 80);

        public static Color Default => Color.FromArgb(150, 150, 150);

        public static Color NotChecked => Color.FromArgb(150, 150, 150);

        public static Color Checking => Color.FromArgb(100, 180, 220);

        #endregion

        #region Methods

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

        #endregion
    }
}
