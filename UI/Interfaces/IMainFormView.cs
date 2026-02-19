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
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.UI.Interfaces
{
    /// <summary>
    /// Contract for MainForm UI operations.
    /// Allows the presenter to update UI without direct form references.
    /// Enables testability by allowing mock implementations.
    /// </summary>
    public interface IMainFormView
    {
        #region Status Updates

        /// <summary>
        /// Updates the mod status indicator (dot and text).
        /// </summary>
        /// <param name="isActive">True if mods are active (green), false otherwise</param>
        /// <param name="statusText">Status text to display</param>
        void SetModsStatus(bool isActive, string statusText);

        /// <summary>
        /// Updates the mod status with detailed information.
        /// </summary>
        /// <param name="statusInfo">Detailed status information</param>
        void SetModsStatusDetailed(ModStatusInfo statusInfo);

        /// <summary>
        /// Shows the "Checking..." state for the status indicator.
        /// Used when a file change is detected and status is being verified.
        /// </summary>
        void ShowCheckingState();

        /// <summary>
        /// Updates the version label.
        /// </summary>
        /// <param name="version">Version string to display</param>
        void SetVersion(string version);

        #endregion

        #region Button States

        /// <summary>
        /// Enables all buttons (when target path is set and ready).
        /// </summary>
        void EnableAllButtons();

        /// <summary>
        /// Enables only detection buttons (auto-detect, manual-detect).
        /// </summary>
        void EnableDetectionButtonsOnly();

        /// <summary>
        /// Disables all buttons (during operations).
        /// </summary>
        void DisableAllButtons();

        /// <summary>
        /// Sets whether a specific button is enabled.
        /// </summary>
        /// <param name="buttonName">Name of the button</param>
        /// <param name="enabled">Whether to enable the button</param>
        void SetButtonEnabled(string buttonName, bool enabled);

        #endregion

        #region Dialogs

        /// <summary>
        /// Shows a message box to the user.
        /// </summary>
        /// <param name="message">Message content</param>
        /// <param name="title">Dialog title</param>
        /// <param name="buttons">Buttons to display</param>
        /// <param name="icon">Icon to display</param>
        /// <returns>User's response</returns>
        DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);

        /// <summary>
        /// Shows a modern styled message dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Message content</param>
        /// <param name="type">Message type (Success, Warning, Error, Info)</param>
        /// <returns>User's response</returns>
        DialogResult ShowStyledMessage(string title, string message, Forms.StyledMessageType type);

        /// <summary>
        /// Shows a folder selection dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected folder path, or null if cancelled</returns>
        string? ShowFolderDialog(string title);

        /// <summary>
        /// Shows a file selection dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter (e.g., "VPK Files|*.vpk")</param>
        /// <returns>Selected file path, or null if cancelled</returns>
        string? ShowFileDialog(string title, string filter);

        /// <summary>
        /// Shows the install method selection dialog.
        /// </summary>
        /// <returns>True if auto-install selected, false if manual, null if cancelled</returns>
        bool? ShowInstallMethodDialog();

        /// <summary>
        /// Shows a Windows notification (balloon tip) via the system tray.
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="icon">Icon type (default: Info)</param>
        /// <param name="timeout">Duration in milliseconds (default: 3000)</param>
        void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000);

        #endregion

        #region Progress Overlay

        /// <summary>
        /// Shows the progress overlay.
        /// </summary>
        /// <returns>Task that completes when overlay is ready</returns>
        Task ShowProgressOverlayAsync();

        /// <summary>
        /// Hides the progress overlay.
        /// </summary>
        void HideProgressOverlay();

        /// <summary>
        /// Updates progress overlay status.
        /// </summary>
        /// <param name="percent">Progress percentage (0-100)</param>
        /// <param name="status">Status text</param>
        /// <param name="substatus">Optional sub-status text</param>
        Task UpdateProgressAsync(int percent, string status, string? substatus = null);

        #endregion

        #region Logging

        /// <summary>
        /// Logs a message to the console.
        /// </summary>
        /// <param name="message">Message to log</param>
        void Log(string message);

        /// <summary>
        /// Clears the console log.
        /// </summary>
        void ClearLog();

        #endregion

        #region Form State

        /// <summary>
        /// Gets whether the form is currently visible.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Invokes an action on the UI thread if required.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        void InvokeOnUIThread(Action action);

        /// <summary>
        /// Closes the form.
        /// </summary>
        void CloseForm();

        #endregion

        #region Extended Dialogs

        /// <summary>
        /// Shows the disable options dialog and returns user selection.
        /// </summary>
        /// <returns>Tuple of (ShouldProceed, DeletePermanently)</returns>
        (bool ShouldProceed, bool DeletePermanently) ShowDisableOptionsDialog();

        /// <summary>
        /// Shows the patch required dialog after installation.
        /// </summary>
        /// <param name="message">Success message to display</param>
        /// <returns>True if user clicked "Patch Now"</returns>
        bool ShowPatchRequiredDialog(string message);

        /// <summary>
        /// Shows the install required dialog when mods are not installed.
        /// </summary>
        /// <returns>True if user clicked Install Now</returns>
        bool ShowInstallRequiredDialog();

        /// <summary>
        /// Shows the restart app dialog.
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <returns>True if user clicked Restart</returns>
        bool ShowRestartAppDialog(string message);

        /// <summary>
        /// Shows the support dialog.
        /// </summary>
        void ShowSupportDialog();

        /// <summary>
        /// Shows the hero gallery and returns the generation result.
        /// </summary>
        (DialogResult Result, ModGenerationResult? GenerationResult) ShowHeroGallery();

        /// <summary>
        /// Shows the classic hero selector (fallback) and returns the generation result.
        /// </summary>
        (DialogResult Result, ModGenerationResult? GenerationResult) ShowClassicHeroSelector();
        
        /// <summary>
        /// Shows the miscellaneous form and returns the generation result.
        /// </summary>
        /// <param name="targetPath">Current target path</param>
        /// <returns>Dialog result and generation result</returns>
        (DialogResult Result, ModGenerationResult? GenerationResult) ShowMiscForm(string? targetPath);

        /// <summary>
        /// Shows the status details form.
        /// </summary>
        /// <param name="status">Current mod status info</param>
        /// <param name="patchAction">Action to execute patch</param>
        void ShowStatusDetails(ModStatusInfo status, Func<Task> patchAction);

        #endregion

        #region Extended Status Updates

        /// <summary>
        /// Updates the Patch Update button visual state based on mod status.
        /// </summary>
        /// <param name="status">Current mod status</param>
        /// <param name="isError">Whether an error occurred</param>
        void UpdatePatchButtonStatus(ModStatus? status, bool isError = false);

        /// <summary>
        /// Shows the patch context menu.
        /// </summary>
        void ShowPatchMenu();

        /// <summary>
        /// Updates buttons based on current mod status.
        /// </summary>
        /// <param name="statusInfo">Status information</param>
        void UpdateButtonsForStatus(ModStatusInfo statusInfo);

        /// <summary>
        /// Updates UI to show that a Dota 2 patch was detected.
        /// Sets status text and highlights patch button.
        /// </summary>
        void SetPatchDetectedStatus();

        #endregion

        #region Application Control

        /// <summary>
        /// Gets or sets the current target path (Dota 2 installation path).
        /// </summary>
        string? TargetPath { get; set; }

        /// <summary>
        /// Restarts the application.
        /// </summary>
        void RestartApplication();

        /// <summary>
        /// Runs an operation with progress overlay.
        /// </summary>
        /// <param name="initialStatus">Initial status message</param>
        /// <param name="operation">The operation to run</param>
        /// <param name="hideDownloadSpeed">Whether to hide download speed</param>
        /// <param name="showPreview">Whether to show the ModsPack preview panel</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> RunWithProgressOverlayAsync(
            string initialStatus,
            Func<ProgressContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed = false,
            bool showPreview = false);

        /// <summary>
        /// Gets the application path (directory where the exe is located).
        /// </summary>
        string AppPath { get; }

        #endregion
    }
}

