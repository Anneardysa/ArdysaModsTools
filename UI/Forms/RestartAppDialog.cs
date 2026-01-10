using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Dialog to prompt user to restart the application.
    /// Styled to match GenerationPreviewForm theme.
    /// </summary>
    public sealed class RestartAppDialog : Form
    {
        public RestartAppDialog(string message = "Please restart the application for changes to take effect.")
        {
            InitializeComponent(message);
            UI.FontHelper.ApplyToForm(this);
        }

        private void InitializeComponent(string message)
        {
            // Form settings
            Text = "Restart Required";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(400, 200);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(1); // Leave room for border
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            // Main container (inside padding)
            var mainContainer = new Panel
            {
                Location = new Point(1, 1),
                Size = new Size(Width - 2, Height - 2),
                BackColor = Color.Black
            };
            Controls.Add(mainContainer);

            // Header
            var headerLabel = new Label
            {
                Text = "⚠ RESTART REQUIRED ⚠",
                Font = new Font("JetBrains Mono", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(380, 40),
                Location = new Point(10, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(headerLabel);

            // Message
            var messageLabel = new Label
            {
                Text = message,
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = false,
                Size = new Size(360, 50),
                Location = new Point(20, 70),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            mainContainer.Controls.Add(messageLabel);

            // OK Button
            var okButton = new Controls.RoundedButton
            {
                Text = "[ OK ]",
                Size = new Size(120, 44),
                Location = new Point((Width - 120) / 2, 135),
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.Black,
                HoverForeColor = Color.White
            };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            mainContainer.Controls.Add(okButton);

            // Border paint
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.White, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // Make draggable
            headerLabel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
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
