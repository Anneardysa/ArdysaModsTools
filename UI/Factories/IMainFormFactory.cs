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
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Factories
{
    /// <summary>
    /// Factory interface for creating the main window with dependency injection.
    /// Enables full constructor injection for the main form without using ServiceLocator.
    /// </summary>
    /// <remarks>
    /// WinForms main forms cannot receive constructor injection directly because
    /// Application.Run(...) is called in Program.cs. This factory pattern
    /// bridges DI with WinForms by resolving dependencies and passing them to the form.
    /// The concrete type is chosen at runtime — a WebView2 shell when a runtime is available,
    /// otherwise the classic WinForms shell — so the return type is the common <see cref="Form"/>.
    /// </remarks>
    public interface IMainFormFactory
    {
        /// <summary>
        /// Creates the main window with all dependencies injected. Returns a WebView2-based shell
        /// (<c>MainFormWebView</c>) when the Edge WebView2 runtime is available, otherwise the
        /// classic <c>MainForm</c> fallback.
        /// </summary>
        /// <param name="startMinimized">If true, the form starts minimized to the system tray (used for Windows startup).</param>
        /// <returns>A fully initialized main window ready to be shown.</returns>
        Form Create(bool startMinimized = false);
    }
}
