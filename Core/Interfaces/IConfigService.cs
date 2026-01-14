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
namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for application configuration management.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Gets the last saved Dota 2 target path.
        /// </summary>
        /// <returns>The saved path, or null if not set.</returns>
        string? GetLastTargetPath();

        /// <summary>
        /// Sets the last saved Dota 2 target path.
        /// </summary>
        /// <param name="path">The path to save, or null to clear.</param>
        void SetLastTargetPath(string? path);

        /// <summary>
        /// Gets a configuration value by key.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">Default value if not found.</param>
        /// <returns>The configuration value.</returns>
        T GetValue<T>(string key, T defaultValue);

        /// <summary>
        /// Sets a configuration value by key.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The value to set.</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// Saves all pending configuration changes.
        /// </summary>
        void Save();
    }
}

