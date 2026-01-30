/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls.Status
{
    /// <summary>
    /// A reusable status indicator control with dot, text, and refresh button.
    /// Displays mod status with configurable colors.
    /// </summary>
    public partial class StatusIndicator : UserControl, IStatusIndicatorView
    {
        private readonly Label _dotLabel;
        private readonly Label _textLabel;
        private readonly Label _refreshButton;

        /// <inheritdoc />
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        [Browsable(true), Category("Appearance")]
        public string StatusText
        {
            get => _textLabel.Text;
            set => _textLabel.Text = value;
        }

        /// <summary>
        /// Gets or sets the status color.
        /// </summary>
        [Browsable(true), Category("Appearance")]
        public Color StatusColor
        {
            get => _dotLabel.BackColor;
            set
            {
                _dotLabel.BackColor = value;
                _textLabel.ForeColor = value;
            }
        }

        /// <summary>
        /// Creates a new StatusIndicator.
        /// </summary>
        public StatusIndicator()
        {
            SuspendLayout();

            // Configure control
            Size = new Size(173, 25);
            BackColor = Color.Transparent;

            // Create dot indicator
            _dotLabel = new Label
            {
                BackColor = StatusColors.Grey,
                Location = new Point(4, 7),
                Size = new Size(12, 12),
                TabIndex = 0
            };

            // Create text label
            _textLabel = new Label
            {
                AutoSize = true,
                Font = new Font("JetBrains Mono", 8.5F),
                ForeColor = StatusColors.Grey,
                Location = new Point(23, 5),
                Text = "- NOT CHECKED",
                TabIndex = 1
            };

            // Create refresh button
            _refreshButton = new Label
            {
                AutoSize = true,
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 14F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(143, -2),
                Text = "↻",
                TabIndex = 2
            };

            _refreshButton.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            _refreshButton.MouseEnter += (s, e) => _refreshButton.ForeColor = Color.White;
            _refreshButton.MouseLeave += (s, e) => _refreshButton.ForeColor = Color.FromArgb(100, 100, 100);

            Controls.Add(_dotLabel);
            Controls.Add(_textLabel);
            Controls.Add(_refreshButton);

            ResumeLayout(false);
            PerformLayout();
        }

        #region IStatusIndicatorView Implementation

        /// <inheritdoc />
        public void SetStatus(Color statusColor, string statusText)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStatus(statusColor, statusText)));
                return;
            }

            StatusColor = statusColor;
            StatusText = statusText;
        }

        /// <inheritdoc />
        public void ShowCheckingState()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ShowCheckingState));
                return;
            }

            StatusColor = StatusColors.Grey;
            StatusText = "Checking...";
        }

        /// <inheritdoc />
        public void ShowError(string errorMessage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowError(errorMessage)));
                return;
            }

            StatusColor = StatusColors.Red;
            StatusText = errorMessage;
        }

        /// <inheritdoc />
        public void SetRefreshEnabled(bool enabled)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetRefreshEnabled(enabled)));
                return;
            }

            _refreshButton.Enabled = enabled;
            _refreshButton.ForeColor = enabled 
                ? Color.FromArgb(100, 100, 100) 
                : Color.FromArgb(50, 50, 50);
        }

        #endregion
    }
}
