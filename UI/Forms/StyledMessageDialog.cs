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
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    public enum StyledMessageType
    {
        Success,
        Warning,
        Error,
        Info
    }

    public sealed class StyledMessageDialog : Form
    {
        private static readonly Color CyberGreen = Color.FromArgb(0, 255, 65);
        private static readonly Color CyberOrange = Color.FromArgb(255, 150, 0);
        private static readonly Color CyberRed = Color.FromArgb(255, 50, 50);
        private static readonly Color CyberCyan = Color.FromArgb(0, 255, 255);
        private static readonly Color BackgroundDark = Color.FromArgb(32, 32, 32);
        private static readonly Color BorderColor = Color.FromArgb(64, 64, 64);

        public StyledMessageDialog(string title, string message, StyledMessageType type = StyledMessageType.Success)
        {
            InitializeComponent(title, message, type);
            UI.FontHelper.ApplyToForm(this);
        }

        public static DialogResult ShowSuccess(IWin32Window? owner, string title, string message)
        {
            using var dialog = new StyledMessageDialog(title, message, StyledMessageType.Success);
            return dialog.ShowDialog(owner);
        }

        public static DialogResult ShowWarning(IWin32Window? owner, string title, string message)
        {
            using var dialog = new StyledMessageDialog(title, message, StyledMessageType.Warning);
            return dialog.ShowDialog(owner);
        }

        public static DialogResult ShowError(IWin32Window? owner, string title, string message)
        {
            using var dialog = new StyledMessageDialog(title, message, StyledMessageType.Error);
            return dialog.ShowDialog(owner);
        }

        public static DialogResult ShowInfo(IWin32Window? owner, string title, string message)
        {
            using var dialog = new StyledMessageDialog(title, message, StyledMessageType.Info);
            return dialog.ShowDialog(owner);
        }

        private void InitializeComponent(string title, string message, StyledMessageType type)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 180);
            ArdysaModsTools.UI.Helpers.DpiLayout.AttachClamp(this);
            BackColor = BackgroundDark;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); } };

            var iconColor = type switch
            {
                StyledMessageType.Success => CyberGreen,
                StyledMessageType.Warning => CyberOrange,
                StyledMessageType.Error => CyberRed,
                StyledMessageType.Info => CyberCyan,
                _ => CyberGreen
            };

            var iconSymbol = type switch
            {
                StyledMessageType.Success => "✓",
                StyledMessageType.Warning => "⚠",
                StyledMessageType.Error => "✕",
                StyledMessageType.Info => "ℹ",
                _ => "✓"
            };

            var iconPanel = new Panel
            {
                Size = new Size(40, 40),
                Location = new Point(30, 35),
                BackColor = Color.Transparent
            };
            iconPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                
                using var circleBrush = new SolidBrush(Color.FromArgb(40, iconColor));
                e.Graphics.FillEllipse(circleBrush, 0, 0, 38, 38);
                
                using var circlePen = new Pen(iconColor, 2);
                e.Graphics.DrawEllipse(circlePen, 1, 1, 36, 36);
                
                using var font = new Font("Segoe UI", 16f, FontStyle.Bold);
                using var brush = new SolidBrush(iconColor);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(iconSymbol, font, brush, new RectangleF(0, 0, 40, 40), sf);
            };
            Controls.Add(iconPanel);

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("JetBrains Mono", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(85, 35),
                BackColor = Color.Transparent
            };
            Controls.Add(titleLabel);

            var messageLabel = new Label
            {
                Text = message,
                Font = new Font("JetBrains Mono", 9f),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = false,
                Size = new Size(300, 40),
                Location = new Point(85, 60),
                BackColor = Color.Transparent
            };
            Controls.Add(messageLabel);

            var okButton = new Controls.RoundedButton
            {
                Text = "OK",
                Size = new Size(80, 36),
                Location = new Point(Width - 110, Height - 55),
                BackColor = Color.FromArgb(48, 48, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 8,
                BorderColor = BorderColor,
                HoverBackColor = Color.FromArgb(64, 64, 64),
                HoverForeColor = Color.White
            };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okButton);

            Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                
                using var pen = new Pen(BorderColor, 1);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = CreateRoundedRectangle(rect, 12);
                e.Graphics.DrawPath(pen, path);
            };

            Region = new Region(CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), 12));

            titleLabel.MouseDown += HandleDragStart;
            iconPanel.MouseDown += HandleDragStart;
        }

        private void HandleDragStart(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
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
