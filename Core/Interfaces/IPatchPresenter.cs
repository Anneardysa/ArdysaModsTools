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
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Contract for patch-related operations.
    /// Handles patcher updates, verification, and Dota 2 patch watching.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public interface IPatchPresenter : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets or sets the current Dota 2 target path.
        /// </summary>
        string? TargetPath { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when a Dota 2 patch is detected.
        /// </summary>
        event Action? PatchDetected;

        /// <summary>
        /// Raised when status should be refreshed.
        /// </summary>
        event Func<Task>? StatusRefreshRequested;

        #endregion

        #region Patch Operations

        /// <summary>
        /// Updates the patcher (signatures and gameinfo) by reinstalling patch files.
        /// </summary>
        Task UpdatePatcherAsync();

        /// <summary>
        /// Executes the patch update operation with progress feedback.
        /// </summary>
        Task ExecutePatchAsync();

        /// <summary>
        /// Handles the patch button click with status-aware behavior.
        /// Shows menu or takes direct action based on current status.
        /// </summary>
        Task HandlePatchButtonClickAsync();

        /// <summary>
        /// Verifies mod files integrity.
        /// </summary>
        Task VerifyModFilesAsync();

        #endregion

        #region Patch Watcher

        /// <summary>
        /// Starts monitoring for Dota 2 updates.
        /// Will skip if already watching the same path.
        /// </summary>
        /// <param name="targetPath">The Dota 2 installation path to watch</param>
        Task StartPatchWatcherAsync(string targetPath);

        /// <summary>
        /// Stops the patch watcher and cleans up resources.
        /// </summary>
        void StopPatchWatcher();

        #endregion
    }
}
