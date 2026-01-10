using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Dialog for showing error logs to user with clipboard copy functionality.
    /// Allows users to easily share logs with developer for troubleshooting.
    /// </summary>
    public sealed class ErrorLogDialog : Form
    {
        private readonly TextBox _logTextBox;
        private readonly string _logContent;

        public ErrorLogDialog(string title, string description, string logContent)
        {
            _logContent = logContent;
            
            // Form settings
            Text = "Error Details";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(700, 500);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            // Main container
            var mainContainer = new Panel
            {
                Location = new Point(1, 1),
                Size = new Size(Width - 2, Height - 2),
                BackColor = Color.Black
            };
            Controls.Add(mainContainer);

            // Top badge - error style
            var badgeLabel = new Label
            {
                Text = "[ ERROR ]",
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 80, 80), // Red
                BackColor = Color.FromArgb(30, 30, 30),
                AutoSize = false,
                Size = new Size(100, 28),
                Location = new Point((Width - 100) / 2, 15),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badgeLabel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 80, 80), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, badgeLabel.Width - 1, badgeLabel.Height - 1);
            };
            mainContainer.Controls.Add(badgeLabel);

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("JetBrains Mono", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(Width - 60, 28),
                Location = new Point(30, 52),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(titleLabel);

            var descLabel = new Label
            {
                Text = description,
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(170, 170, 170),
                AutoSize = false,
                Size = new Size(Width - 60, 22),
                Location = new Point(30, 82),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(descLabel);

            // Log textbox
            _logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("JetBrains Mono", 8.5f),
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.FromArgb(200, 200, 200),
                BorderStyle = BorderStyle.None,
                Location = new Point(30, 115),
                Size = new Size(Width - 60, 290),
                Text = logContent
            };
            mainContainer.Controls.Add(_logTextBox);

            // Log border
            var logBorder = new Panel
            {
                Location = new Point(29, 114),
                Size = new Size(Width - 58, 292),
                BackColor = Color.FromArgb(51, 51, 51)
            };
            logBorder.SendToBack();
            mainContainer.Controls.Add(logBorder);

            // Buttons container
            int buttonY = 420;
            int buttonWidth = 160;
            int buttonSpacing = 20;
            int buttonsStartX = (Width - (buttonWidth * 2 + buttonSpacing)) / 2;

            // Copy to Clipboard button (primary)
            var copyButton = new Controls.RoundedButton
            {
                Text = "> COPY LOG",
                Size = new Size(buttonWidth, 42),
                Location = new Point(buttonsStartX, buttonY),
                BackColor = Color.FromArgb(255, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(255, 80, 80),
                HoverBackColor = Color.FromArgb(255, 100, 100),
                HoverForeColor = Color.White
            };
            copyButton.FlatAppearance.BorderSize = 0;
            copyButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_logContent);
                    copyButton.Text = "✓ COPIED!";
                    copyButton.BackColor = Color.FromArgb(80, 200, 120);
                    copyButton.BorderColor = Color.FromArgb(80, 200, 120);
                    
                    // Reset after 2 seconds
                    var timer = new System.Windows.Forms.Timer { Interval = 2000 };
                    timer.Tick += (ts, te) =>
                    {
                        copyButton.Text = "> COPY LOG";
                        copyButton.BackColor = Color.FromArgb(255, 80, 80);
                        copyButton.BorderColor = Color.FromArgb(255, 80, 80);
                        timer.Stop();
                        timer.Dispose();
                    };
                    timer.Start();
                }
                catch { }
            };
            mainContainer.Controls.Add(copyButton);

            // Close button (secondary)
            var closeButton = new Controls.RoundedButton
            {
                Text = "CLOSE",
                Size = new Size(buttonWidth, 42),
                Location = new Point(buttonsStartX + buttonWidth + buttonSpacing, buttonY),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                HoverForeColor = Color.White
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            mainContainer.Controls.Add(closeButton);

            // Border paint (red for error)
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(255, 80, 80), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make form draggable from title
            titleLabel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            UI.FontHelper.ApplyToForm(this);
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }
    }
}
