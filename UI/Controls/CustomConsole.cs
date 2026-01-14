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
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Controls
{
    public class CustomConsole : UserControl
    {
        private readonly RichTextBox _console;

        public CustomConsole()
        {
            _console = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                ForeColor = Color.LightGray
            };
            Controls.Add(_console);
        }

        public RichTextBox Inner => _console;

        public void Append(string message, Color? color = null)
        {
            if (!_console.IsHandleCreated) return;
            _console.Invoke((Action)(() =>
            {
                int start = _console.TextLength;
                string time = DateTime.Now.ToString("HH:mm");
                string line = $"[{time}] {message}\r\n";
                _console.AppendText(line);

                if (color.HasValue)
                {
                    _console.Select(start, line.Length);
                    _console.SelectionColor = color.Value;
                }

                _console.Select(_console.TextLength, 0);
                _console.ScrollToCaret();
            }));
        }

        public void Clear() => _console.Clear();
    }
}

