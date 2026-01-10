using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.Core.Services
{
    public interface ILogger
    {
        void Log(string message);
        void FlushBufferedLogs();
    }

    public class Logger : ILogger
    {
        private readonly RetroTerminal? _terminal;
        private readonly RichTextBox? _richTextBox;
        private readonly List<string> _buffer = new();
        private readonly Queue<(string message, Color color)> _messageQueue = new();
        private bool _isTyping = false;
        private readonly object _lock = new();

        // Typewriter settings (no delay - instant logging)
        private const int CharDelayMs = 0;  // No delay per character
        private const int MessageDelayMs = 0;  // No delay between messages

        // Cyberpunk colors (for RichTextBox fallback)
        private static readonly Color CyberCyan = Color.FromArgb(0, 255, 255);
        private static readonly Color CyberGreen = Color.FromArgb(0, 255, 65);
        private static readonly Color CyberRed = Color.FromArgb(255, 50, 50);
        private static readonly Color CyberOrange = Color.FromArgb(255, 150, 0);
        private static readonly Color CyberWhite = Color.FromArgb(220, 220, 220);
        private static readonly Color CyberGrey = Color.FromArgb(100, 100, 100);

        /// <summary>
        /// Constructor for RetroTerminal (MainForm)
        /// </summary>
        public Logger(RetroTerminal terminal)
        {
            _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        }

        /// <summary>
        /// Constructor for RichTextBox (backward compatibility for MiscForm, etc.)
        /// </summary>
        public Logger(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
        }

        public void Log(string message)
        {
            if (_terminal != null)
                LogToTerminal(message);
            else if (_richTextBox != null)
                LogToRichTextBox(message);
        }

        private void LogToTerminal(string message)
        {
            if (!_terminal!.IsHandleCreated)
            {
                _buffer.Add(message);
                return;
            }

            var color = CategorizeMessage(message);
            string cleanMessage = CleanMessage(message).ToUpperInvariant();

            lock (_lock)
            {
                _messageQueue.Enqueue((cleanMessage, color));
            }

            if (!_isTyping)
            {
                _isTyping = true;
                _ = ProcessQueueAsync();
            }
        }

        private void LogToRichTextBox(string message)
        {
            if (!_richTextBox!.IsHandleCreated)
            {
                _buffer.Add(message);
                return;
            }

            _richTextBox.BeginInvoke((Action)(() =>
            {
                var color = CategorizeMessage(message);
                string cleanMessage = CleanMessage(message).ToUpperInvariant();
                
                // Write bar
                int pos = _richTextBox.TextLength;
                _richTextBox.AppendText("▌ ");
                _richTextBox.SelectionStart = pos;
                _richTextBox.SelectionLength = 1;
                _richTextBox.SelectionColor = color;
                
                // Write message
                pos = _richTextBox.TextLength;
                _richTextBox.AppendText(cleanMessage + "\r\n");
                _richTextBox.SelectionStart = pos;
                _richTextBox.SelectionLength = cleanMessage.Length;
                _richTextBox.SelectionColor = color;
                
                _richTextBox.SelectionStart = _richTextBox.TextLength;
                _richTextBox.ScrollToCaret();
            }));
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                (string message, Color color) item;
                
                lock (_lock)
                {
                    if (_messageQueue.Count == 0)
                    {
                        _isTyping = false;
                        return;
                    }
                    item = _messageQueue.Dequeue();
                }

                await TypeMessageAsync(item.message, item.color);
                await Task.Delay(MessageDelayMs);
            }
        }

        private async Task TypeMessageAsync(string message, Color color)
        {
            try
            {
                // Build message char by char with typewriter effect
                string currentText = "▌ ";
                
                foreach (char c in message)
                {
                    currentText += c;
                    await Task.Delay(CharDelayMs);
                }

                // Add completed line to terminal
                _terminal!.BeginInvoke((Action)(() =>
                {
                    _terminal.AppendLine(currentText, color);
                }));
            }
            catch
            {
                // Control might be disposed
            }
        }

        public void FlushBufferedLogs()
        {
            Control? ctrl = _terminal as Control ?? _richTextBox;
            if (_buffer.Count == 0 || ctrl == null || !ctrl.IsHandleCreated)
                return;

            foreach (var msg in _buffer)
                Log(msg);

            _buffer.Clear();
        }

        private static string CleanMessage(string message)
        {
            if (message.StartsWith("Error: ", StringComparison.OrdinalIgnoreCase))
                return message[7..];
            if (message.StartsWith("Warning: ", StringComparison.OrdinalIgnoreCase))
                return message[9..];
            if (message.StartsWith("Info: ", StringComparison.OrdinalIgnoreCase))
                return message[6..];
            return message;
        }

        private static Color CategorizeMessage(string message)
        {
            string lower = message.ToLowerInvariant();

            // Success - Neon Green
            if (lower.Contains("done") ||
                lower.Contains("success") ||
                lower.Contains("completed") ||
                lower.Contains("ready") ||
                lower.Contains("installed") ||
                lower.Contains("applied") ||
                lower.Contains("enabled") ||
                lower.Contains("generated") ||
                lower.Contains("up to date"))
                return CyberGreen;

            // Error - Red
            if (lower.Contains("error") ||
                lower.Contains("failed") ||
                lower.Contains("critical") ||
                lower.Contains("cannot") ||
                lower.Contains("exception") ||
                lower.Contains("not found") ||
                lower.Contains("invalid"))
                return CyberRed;

            // Warning - Orange
            if (lower.Contains("warning") ||
                lower.Contains("skipped") ||
                lower.Contains("missing") ||
                lower.Contains("canceled") ||
                lower.Contains("cancelled") ||
                lower.Contains("already") ||
                lower.Contains("please") ||
                lower.Contains("close"))
                return CyberOrange;

            // Progress - Cyan
            if (lower.Contains("fetching") ||
                lower.Contains("checking") ||
                lower.Contains("extracting") ||
                lower.Contains("downloading") ||
                lower.Contains("processing") ||
                lower.Contains("generating") ||
                lower.Contains("loading") ||
                lower.Contains("patching") ||
                lower.Contains("recompiling") ||
                lower.Contains("replacing") ||
                lower.Contains("detecting") ||
                lower.Contains("connecting") ||
                lower.Contains("installing") ||
                lower.Contains("updating") ||
                lower.Contains("disabling"))
                return CyberCyan;

            // Default - Grey
            return CyberGrey;
        }
    }
}
