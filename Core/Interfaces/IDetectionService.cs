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
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for Dota 2 installation detection operations.
    /// </summary>
    public interface IDetectionService
    {
        /// <summary>
        /// Automatically detect Dota 2 installation folder via registry and Steam libraries.
        /// </summary>
        /// <returns>The detected Dota 2 path, or null if not found.</returns>
        Task<string?> AutoDetectAsync();

        /// <summary>
        /// Opens a folder picker dialog for manual Dota 2 folder selection.
        /// </summary>
        /// <returns>The selected path, or null if cancelled or invalid.</returns>
        string? ManualDetect();

        /// <summary>
        /// Validates if the given path is a valid Dota 2 installation.
        /// Checks for the existence of dota2.exe at the expected location.
        /// </summary>
        /// <param name="path">The path to validate (should be "dota 2 beta" folder)</param>
        /// <returns>True if the path contains a valid Dota 2 installation</returns>
        bool ValidateDotaPath(string? path);
    }
}

