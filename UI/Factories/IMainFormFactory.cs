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

namespace ArdysaModsTools.UI.Factories
{
    /// <summary>
    /// Factory interface for creating MainForm instances with dependency injection.
    /// Enables full constructor injection for the main form without using ServiceLocator.
    /// </summary>
    /// <remarks>
    /// WinForms main forms cannot receive constructor injection directly because
    /// Application.Run(new MainForm()) is called in Program.cs. This factory pattern
    /// bridges DI with WinForms by resolving dependencies and passing them to the form.
    /// </remarks>
    public interface IMainFormFactory
    {
        /// <summary>
        /// Creates a new MainForm instance with all dependencies injected.
        /// </summary>
        /// <returns>A fully initialized MainForm ready to be shown.</returns>
        MainForm Create();
    }
}
