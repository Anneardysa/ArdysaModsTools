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

namespace ArdysaModsTools.UI.Controls.Actions
{
    /// <summary>
    /// Action panel with Install, Disable, and Cancel buttons.
    /// Implements MVP pattern for testability.
    /// </summary>
    public partial class ActionPanel : UserControl, IActionPanelView
    {
        private readonly RoundedButton _installButton;
        private readonly RoundedButton _disableButton;
        private readonly RoundedButton _cancelButton;
        private readonly Label _statusLabel;

        /// <inheritdoc />
        public event EventHandler? InstallRequested;

        /// <inheritdoc />
        public event EventHandler? DisableRequested;

        /// <inheritdoc />
        public event EventHandler? CancelRequested;

        /// <summary>
        /// Creates a new ActionPanel.
        /// </summary>
        public ActionPanel()
        {
            SuspendLayout();

            // Configure control
            Size = new Size(380, 60);
            BackColor = Color.Transparent;

            // Install button (left side, larger)
            _installButton = new RoundedButton
            {
                Text = "[ INSTALL ]",
                Location = new Point(0, 0),
                Size = new Size(150, 41),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(0, 200, 100),
                BorderColor = Color.FromArgb(0, 150, 75),
                HoverBackColor = Color.FromArgb(0, 30, 20),
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold)
            };
            _installButton.Click += (s, e) => InstallRequested?.Invoke(this, EventArgs.Empty);

            // Disable button (center)
            _disableButton = new RoundedButton
            {
                Text = "[ DISABLE ]",
                Location = new Point(158, 0),
                Size = new Size(150, 41),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(200, 100, 100),
                BorderColor = Color.FromArgb(150, 75, 75),
                HoverBackColor = Color.FromArgb(30, 10, 10),
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold)
            };
            _disableButton.Click += (s, e) => DisableRequested?.Invoke(this, EventArgs.Empty);

            // Cancel button (right side, initially hidden)
            _cancelButton = new RoundedButton
            {
                Text = "✕",
                Location = new Point(316, 0),
                Size = new Size(60, 41),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderColor = Color.FromArgb(80, 80, 80),
                HoverBackColor = Color.FromArgb(70, 70, 70),
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 12F, FontStyle.Bold),
                Visible = false
            };
            _cancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);

            // Status label (below buttons)
            _statusLabel = new Label
            {
                Location = new Point(0, 44),
                Size = new Size(380, 16),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("JetBrains Mono", 7F),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = ""
            };

            Controls.Add(_installButton);
            Controls.Add(_disableButton);
            Controls.Add(_cancelButton);
            Controls.Add(_statusLabel);

            ResumeLayout(false);
            PerformLayout();
        }

        #region IActionPanelView Implementation

        /// <inheritdoc />
        public void SetInstallEnabled(bool enabled)
        {
            InvokeIfRequired(() =>
            {
                _installButton.Enabled = enabled;
                _installButton.ForeColor = enabled
                    ? Color.FromArgb(0, 200, 100)
                    : Color.FromArgb(80, 80, 80);
            });
        }

        /// <inheritdoc />
        public void SetDisableEnabled(bool enabled)
        {
            InvokeIfRequired(() =>
            {
                _disableButton.Enabled = enabled;
                _disableButton.ForeColor = enabled
                    ? Color.FromArgb(200, 100, 100)
                    : Color.FromArgb(80, 80, 80);
            });
        }

        /// <inheritdoc />
        public void SetCancelVisible(bool visible)
        {
            InvokeIfRequired(() =>
            {
                _cancelButton.Visible = visible;
                UpdateButtonLayout(visible);
            });
        }

        /// <inheritdoc />
        public void SetAllButtonsEnabled(bool enabled)
        {
            InvokeIfRequired(() =>
            {
                SetInstallEnabled(enabled);
                SetDisableEnabled(enabled);
            });
        }

        /// <inheritdoc />
        public void ShowOperationInProgress(string message)
        {
            InvokeIfRequired(() =>
            {
                _statusLabel.Text = message;
                _statusLabel.ForeColor = Color.FromArgb(180, 180, 180);
            });
        }

        /// <inheritdoc />
        public void ShowOperationComplete()
        {
            InvokeIfRequired(() =>
            {
                _statusLabel.Text = "";
                _statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            });
        }

        #endregion

        /// <summary>
        /// Updates button layout based on Cancel visibility.
        /// When Cancel is visible, Install and Disable shrink.
        /// </summary>
        private void UpdateButtonLayout(bool cancelVisible)
        {
            if (cancelVisible)
            {
                // Shrink Install and Disable to make room for Cancel
                _installButton.Size = new Size(118, 41);
                _disableButton.Location = new Point(126, 0);
                _disableButton.Size = new Size(118, 41);
                _cancelButton.Location = new Point(252, 0);
                _cancelButton.Size = new Size(128, 41);
                _cancelButton.Text = "[ CANCEL ]";
            }
            else
            {
                // Restore original layout
                _installButton.Size = new Size(150, 41);
                _disableButton.Location = new Point(158, 0);
                _disableButton.Size = new Size(150, 41);
            }
        }

        private void InvokeIfRequired(System.Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }
    }
}
