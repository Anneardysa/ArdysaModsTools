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
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Contract for navigation and dialog operations.
    /// Handles opening forms, showing dialogs, and checking connectivity.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public interface INavigationPresenter
    {
        #region Properties

        /// <summary>
        /// Gets or sets the current Dota 2 target path.
        /// </summary>
        string? TargetPath { get; set; }

        /// <summary>
        /// Gets or sets the current mod status info for dialogs.
        /// </summary>
        ModStatusInfo? CurrentStatus { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when status should be refreshed.
        /// </summary>
        event Func<Task>? StatusRefreshRequested;

        /// <summary>
        /// Raised when patch operation is requested.
        /// </summary>
        event Func<Task>? PatchRequested;

        #endregion

        #region Form Navigation

        /// <summary>
        /// Opens the Miscellaneous options form.
        /// </summary>
        Task OpenMiscellaneousAsync();

        /// <summary>
        /// Opens the Hero Selection gallery.
        /// </summary>
        Task OpenHeroSelectionAsync();

        /// <summary>
        /// Shows status details in a dedicated form.
        /// </summary>
        void ShowStatusDetails();

        #endregion

        #region Dialog Navigation

        /// <summary>
        /// Shows install dialog if mods are not installed.
        /// Called after successful path detection for better UX.
        /// </summary>
        Task ShowInstallDialogIfNeededAsync();

        /// <summary>
        /// Check status after install and show PatchRequiredDialog if not Ready.
        /// </summary>
        /// <param name="successMessage">Message to show in dialog</param>
        /// <param name="fromDetection">If true, respects user's previous "Later" dismissal</param>
        Task ShowPatchRequiredIfNeededAsync(string? successMessage = null, bool fromDetection = false);

        #endregion

        #region Connectivity

        /// <summary>
        /// Checks if heroes.json is accessible from any CDN.
        /// Used before opening hero selection to ensure connectivity.
        /// </summary>
        /// <returns>True if connection is available</returns>
        Task<bool> CheckHeroesJsonAccessAsync();

        #endregion
    }
}
