/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Drawing;

namespace ArdysaModsTools.UI.Controls.Status
{
    /// <summary>
    /// View interface for the StatusIndicator control.
    /// Enables presenter testing without UI dependencies.
    /// </summary>
    public interface IStatusIndicatorView
    {
        /// <summary>
        /// Sets the status display with color and text.
        /// </summary>
        /// <param name="statusColor">The color for the indicator dot.</param>
        /// <param name="statusText">The status text to display.</param>
        void SetStatus(Color statusColor, string statusText);

        /// <summary>
        /// Shows the "checking" state (animated or grey).
        /// </summary>
        void ShowCheckingState();

        /// <summary>
        /// Shows an error state with the specified message.
        /// </summary>
        /// <param name="errorMessage">The error message to display.</param>
        void ShowError(string errorMessage);

        /// <summary>
        /// Enables or disables the refresh button.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        void SetRefreshEnabled(bool enabled);

        /// <summary>
        /// Event raised when the user clicks the refresh button.
        /// </summary>
        event EventHandler RefreshRequested;
    }
}
