/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Drawing;
using System.Windows.Forms;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Controls.Detection
{
    /// <summary>
    /// Detection panel with Auto Detect and Manual Detect buttons.
    /// Implements MVP pattern for testability.
    /// </summary>
    public partial class DetectionPanel : UserControl, IDetectionPanelView
    {
        private readonly RoundedButton _autoDetectButton;
        private readonly RoundedButton _manualDetectButton;
        private readonly Label _statusLabel;

        /// <inheritdoc />
        public event EventHandler? AutoDetectRequested;

        /// <inheritdoc />
        public event EventHandler? ManualDetectRequested;

        /// <summary>
        /// Creates a new DetectionPanel.
        /// </summary>
        public DetectionPanel()
        {
            SuspendLayout();

            // Configure control
            Size = new Size(163, 120);
            BackColor = Color.Transparent;

            // Auto Detect button
            _autoDetectButton = new RoundedButton
            {
                Text = "[ AUTO DETECT ]",
                Location = new Point(0, 0),
                Size = new Size(163, 41),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 9F)
            };
            _autoDetectButton.Click += (s, e) => AutoDetectRequested?.Invoke(this, EventArgs.Empty);

            // Manual Detect button
            _manualDetectButton = new RoundedButton
            {
                Text = "[ MANUAL DETECT ]",
                Location = new Point(0, 48),
                Size = new Size(163, 41),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 9F)
            };
            _manualDetectButton.Click += (s, e) => ManualDetectRequested?.Invoke(this, EventArgs.Empty);

            // Status label
            _statusLabel = new Label
            {
                Location = new Point(0, 96),
                Size = new Size(163, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("JetBrains Mono", 7F),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(_autoDetectButton);
            Controls.Add(_manualDetectButton);
            Controls.Add(_statusLabel);

            ResumeLayout(false);
            PerformLayout();
        }

        #region IDetectionPanelView Implementation

        /// <inheritdoc />
        public void SetDetectedPath(string? path)
        {
            InvokeIfRequired(() =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    _statusLabel.Text = "";
                    _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
                }
                else
                {
                    // Show truncated path
                    string displayPath = path.Length > 25 
                        ? "..." + path.Substring(path.Length - 22) 
                        : path;
                    _statusLabel.Text = displayPath;
                    _statusLabel.ForeColor = Color.FromArgb(0, 180, 80);
                }
            });
        }

        /// <inheritdoc />
        public void SetButtonsEnabled(bool enabled)
        {
            InvokeIfRequired(() =>
            {
                _autoDetectButton.Enabled = enabled;
                _manualDetectButton.Enabled = enabled;
            });
        }

        /// <inheritdoc />
        public void ShowDetectingState()
        {
            InvokeIfRequired(() =>
            {
                _statusLabel.Text = "Detecting...";
                _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            });
        }

        /// <inheritdoc />
        public void ShowSuccess(string path)
        {
            InvokeIfRequired(() =>
            {
                SetDetectedPath(path);
            });
        }

        /// <inheritdoc />
        public void ShowFailed(string message)
        {
            InvokeIfRequired(() =>
            {
                _statusLabel.Text = message;
                _statusLabel.ForeColor = Color.FromArgb(200, 80, 80);
            });
        }

        #endregion

        private void InvokeIfRequired(System.Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }
    }
}
