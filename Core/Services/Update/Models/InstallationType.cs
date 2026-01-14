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
namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Represents how the application was installed.
    /// </summary>
    public enum InstallationType
    {
        /// <summary>
        /// Unable to determine installation type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Installed via the setup installer (Inno Setup).
        /// Located in Program Files, has uninstaller, requires admin for updates.
        /// </summary>
        Installer,

        /// <summary>
        /// Portable/standalone version extracted from ZIP.
        /// Can be anywhere, no uninstaller, updates in-place without admin.
        /// </summary>
        Portable
    }
}

