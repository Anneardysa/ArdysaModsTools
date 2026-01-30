/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Drawing;

namespace ArdysaModsTools.UI.Controls.Detection
{
    /// <summary>
    /// View interface for the DetectionPanel control.
    /// Enables presenter testing without UI dependencies.
    /// </summary>
    public interface IDetectionPanelView
    {
        /// <summary>
        /// Shows the current detected path.
        /// </summary>
        /// <param name="path">The path to display, or null for no path.</param>
        void SetDetectedPath(string? path);

        /// <summary>
        /// Enables or disables all detection buttons.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        void SetButtonsEnabled(bool enabled);

        /// <summary>
        /// Shows a loading/detecting state.
        /// </summary>
        void ShowDetectingState();

        /// <summary>
        /// Shows detection success state with path.
        /// </summary>
        /// <param name="path">The detected path.</param>
        void ShowSuccess(string path);

        /// <summary>
        /// Shows detection failed state.
        /// </summary>
        /// <param name="message">The error or info message.</param>
        void ShowFailed(string message);

        /// <summary>
        /// Event raised when user clicks Auto Detect.
        /// </summary>
        event EventHandler AutoDetectRequested;

        /// <summary>
        /// Event raised when user clicks Manual Detect.
        /// </summary>
        event EventHandler ManualDetectRequested;
    }
}
