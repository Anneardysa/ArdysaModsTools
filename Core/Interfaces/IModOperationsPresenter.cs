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
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Contract for mod installation and management operations.
    /// Handles install, reinstall, disable, and related operations.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public interface IModOperationsPresenter
    {
        #region Properties

        /// <summary>
        /// Gets whether an operation is currently running.
        /// </summary>
        bool IsOperationRunning { get; }

        /// <summary>
        /// Gets or sets the current Dota 2 target path.
        /// </summary>
        string? TargetPath { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when an operation starts.
        /// </summary>
        event Action? OperationStarted;

        /// <summary>
        /// Raised when an operation ends.
        /// </summary>
        event Action? OperationEnded;

        /// <summary>
        /// Raised when status should be refreshed.
        /// </summary>
        event Func<Task>? StatusRefreshRequested;

        #endregion

        #region Install Operations

        /// <summary>
        /// Initiates the mod installation process.
        /// Shows install method dialog and handles both auto and manual install.
        /// </summary>
        /// <returns>True if installation was successful</returns>
        Task<bool> InstallAsync();

        /// <summary>
        /// Reinstalls the ModsPack with force flag.
        /// </summary>
        /// <returns>True if reinstallation was successful</returns>
        Task<bool> ReinstallAsync();

        #endregion

        #region Disable Operations

        /// <summary>
        /// Disables all mods by removing the VPK file.
        /// Simple version that just removes the VPK.
        /// </summary>
        Task DisableAsync();

        /// <summary>
        /// Disables mods with user options (simple disable or permanent delete).
        /// Shows options dialog and handles both cases including temp folder cleanup.
        /// </summary>
        Task DisableWithOptionsAsync();

        #endregion

        #region Operation Control

        /// <summary>
        /// Cancels the current operation.
        /// </summary>
        void CancelOperation();

        #endregion
    }
}
