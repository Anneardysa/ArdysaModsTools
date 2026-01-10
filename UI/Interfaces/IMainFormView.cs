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
    }
}
