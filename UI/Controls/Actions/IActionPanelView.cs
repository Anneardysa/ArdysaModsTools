/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace ArdysaModsTools.UI.Controls.Actions
{
    /// <summary>
    /// View interface for the ActionPanel control.
    /// Enables presenter testing without UI dependencies.
    /// </summary>
    public interface IActionPanelView
    {
        /// <summary>
        /// Enables or disables the Install button.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        void SetInstallEnabled(bool enabled);

        /// <summary>
        /// Enables or disables the Disable button.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        void SetDisableEnabled(bool enabled);

        /// <summary>
        /// Shows or hides the Cancel button.
        /// </summary>
        /// <param name="visible">True to show, false to hide.</param>
        void SetCancelVisible(bool visible);

        /// <summary>
        /// Enables or disables all action buttons at once.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        void SetAllButtonsEnabled(bool enabled);

        /// <summary>
        /// Shows the operation in progress state with a message.
        /// </summary>
        /// <param name="message">The operation message to display.</param>
        void ShowOperationInProgress(string message);

        /// <summary>
        /// Resets the panel to normal state after operation completes.
        /// </summary>
        void ShowOperationComplete();

        /// <summary>
        /// Event raised when user clicks the Install button.
        /// </summary>
        event EventHandler InstallRequested;

        /// <summary>
        /// Event raised when user clicks the Disable button.
        /// </summary>
        event EventHandler DisableRequested;

        /// <summary>
        /// Event raised when user clicks the Cancel button.
        /// </summary>
        event EventHandler CancelRequested;
    }
}
