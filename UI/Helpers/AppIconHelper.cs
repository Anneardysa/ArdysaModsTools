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
using System.IO;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Helpers
{
    public static class AppIconHelper
    {
        public static Icon? Load()
        {
            try
            {
                string relPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "AppIcon.ico");
                if (File.Exists(relPath))
                {
                    return new Icon(relPath);
                }

                string? devAssetsPath = Environment.GetEnvironmentVariable("AMT_DEV_ASSETS_PATH");
                if (!string.IsNullOrEmpty(devAssetsPath))
                {
                    string devPath = Path.Combine(devAssetsPath, "Icons", "AppIcon.ico");
                    if (File.Exists(devPath))
                    {
                        return new Icon(devPath);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static void Apply(Form form)
        {
            var icon = Load();
            if (icon != null)
            {
                form.Icon = icon;
                form.ShowIcon = true;
            }
        }
    }
}
