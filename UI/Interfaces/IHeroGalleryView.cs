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
using System.Collections.Generic;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;

// ProgressOperationRunnerContext lives in the ArdysaModsTools.UI namespace.
using ArdysaModsTools.UI;

namespace ArdysaModsTools.UI.Interfaces
{
    /// <summary>
    /// View contract for the WebView2 hero gallery (Skin Selector).
    /// The presenter drives generation logic; the form owns the WebView2 bridge
    /// and implements these UI operations. No WinForms types leak through the contract.
    /// </summary>
    public interface IHeroGalleryView
    {
        /// <summary>
        /// Updates the status text shown in the gallery footer.
        /// </summary>
        Task UpdateStatusAsync(string status);

        /// <summary>
        /// Shows the "base hero selected without a set" confirmation dialog.
        /// The implementation MUST apply a timeout so a missing JS callback can never
        /// hang generation indefinitely.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="htmlMessage">HTML message body (rendered via innerHTML).</param>
        /// <returns>True if the user chose to proceed; false on cancel or timeout.</returns>
        Task<bool> ConfirmBaseNoSetAsync(string title, string htmlMessage);

        /// <summary>
        /// Shows the terminal generation result alert and waits for the user to dismiss it
        /// (bounded by a timeout).
        /// </summary>
        /// <param name="title">Alert title.</param>
        /// <param name="message">Alert body text.</param>
        /// <param name="hasFailures">True to render the warning icon, false for success.</param>
        Task ShowGenerationAlertAsync(string title, string message, bool hasFailures);

        /// <summary>
        /// Shows a simple informational alert (no dismissal wait).
        /// </summary>
        Task ShowAlertAsync(string title, string message);

        /// <summary>
        /// Shows the generation preview dialog and returns the user's confirmation.
        /// </summary>
        /// <returns>True if the user confirmed generation.</returns>
        bool ShowGenerationPreview(IReadOnlyList<(HeroModel hero, string setName, string? thumbnailUrl)> items);

        /// <summary>
        /// Shows a blocking warning message box (e.g. missing Dota 2 path).
        /// </summary>
        void ShowWarning(string message, string title);

        /// <summary>
        /// Shows the copyable error log dialog for a failed or unexpected operation.
        /// </summary>
        void ShowErrorDialog(string title, string subtitle, string details);

        /// <summary>
        /// Runs the generation operation behind the shared progress overlay.
        /// </summary>
        Task<OperationResult> RunGenerationWithProgressAsync(
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation);

        /// <summary>
        /// Persists the current highlighted-hero selections to disk.
        /// </summary>
        Task SaveSelectionsAsync();

        /// <summary>
        /// Stores the generation result for the parent form to read after close.
        /// Does not close the form.
        /// </summary>
        void StoreResult(ModGenerationResult result);

        /// <summary>
        /// Closes the gallery with a successful dialog result.
        /// </summary>
        void CloseWithSuccess();
    }
}
