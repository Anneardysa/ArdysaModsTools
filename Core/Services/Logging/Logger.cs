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
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Localization;

namespace ArdysaModsTools.Core.Services
{
    [Obsolete("Use IAppLogger instead. Will be removed in v3.0.")]
    public interface ILogger
    {
        void Log(string message);
        void FlushBufferedLogs();
    }

    public readonly struct LogSegment
    {
        public string? Literal { get; }
        public string? Key { get; }
        public object? Vars { get; }

        private LogSegment(string? literal, string? key, object? vars)
        {
            Literal = literal;
            Key = key;
            Vars = vars;
        }

        public static LogSegment Text(string text) => new(text, null, null);

        public static LogSegment T(string key, object? vars = null) => new(null, key, vars);
    }

#pragma warning disable CS0618
    public class Logger : IAppLogger, ILogger
#pragma warning restore CS0618
    {
        private readonly RichTextBox? _richTextBox;
        private readonly Action<string, string>? _webSink;
        private readonly Action<string, string>? _webI18nSink;

        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private readonly List<string> _buffer = new();

        private static readonly Color CyberCyan = Color.FromArgb(0, 255, 255);
        private static readonly Color CyberGreen = Color.FromArgb(0, 255, 65);
        private static readonly Color CyberRed = Color.FromArgb(255, 50, 50);
        private static readonly Color CyberOrange = Color.FromArgb(255, 150, 0);
        private static readonly Color CyberWhite = Color.FromArgb(220, 220, 220);
        private static readonly Color CyberGrey = Color.FromArgb(100, 100, 100);

        public Logger(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
        }

        public Logger(Action<string, string> webSink, Action<string, string>? webI18nSink = null)
        {
            _webSink = webSink ?? throw new ArgumentNullException(nameof(webSink));
            _webI18nSink = webI18nSink;
        }

        public void Log(string message)
        {
            if (message.Contains("dota.signatures", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("gameinfo_branchspecific", StringComparison.OrdinalIgnoreCase))
                return;

            if (_richTextBox != null)
                LogToRichTextBox(message, CategorizeMessage(message));
            else
                _webSink?.Invoke(CleanMessage(message), CategorizeKey(message));
        }

        public void LogCategorized(string message, string category)
        {
            if (message.Contains("dota.signatures", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("gameinfo_branchspecific", StringComparison.OrdinalIgnoreCase))
                return;

            if (_richTextBox != null)
                LogToRichTextBox(message, ColorForCategory(category));
            else
                _webSink?.Invoke(CleanMessage(message), category);
        }

        public void LogLocalized(string category, params LogSegment[] segments)
        {
            if (segments == null || segments.Length == 0) return;

            if (_richTextBox == null && _webI18nSink != null)
            {
                _webI18nSink(SerializeSegments(segments), category);
                return;
            }

            LogCategorized(ResolveSegments(segments), category);
        }

        private static string ResolveSegments(LogSegment[] segments)
        {
            var sb = new StringBuilder();
            foreach (var s in segments)
            {
                if (s.Literal != null) sb.Append(s.Literal);
                else if (s.Key != null) sb.Append(s.Vars != null ? Loc.T(s.Key, s.Vars) : Loc.T(s.Key));
            }
            return sb.ToString();
        }

        private static string SerializeSegments(LogSegment[] segments)
        {
            var arr = new List<object>(segments.Length);
            foreach (var s in segments)
            {
                if (s.Literal != null) arr.Add(s.Literal);
                else arr.Add(new { k = s.Key, v = s.Vars });
            }
            return JsonSerializer.Serialize(arr, LogJsonOptions);
        }

        private void LogToRichTextBox(string message, Color color)
        {
            if (!_richTextBox!.IsHandleCreated)
            {
                _buffer.Add(message);
                return;
            }

            _richTextBox.BeginInvoke((Action)(() =>
            {
                string cleanMessage = CleanMessage(message).ToUpperInvariant();
                
                int pos = _richTextBox.TextLength;
                _richTextBox.AppendText("▌ ");
                _richTextBox.SelectionStart = pos;
                _richTextBox.SelectionLength = 1;
                _richTextBox.SelectionColor = color;
                
                pos = _richTextBox.TextLength;
                _richTextBox.AppendText(cleanMessage + "\r\n");
                _richTextBox.SelectionStart = pos;
                _richTextBox.SelectionLength = cleanMessage.Length;
                _richTextBox.SelectionColor = color;
                
                _richTextBox.SelectionStart = _richTextBox.TextLength;
                _richTextBox.ScrollToCaret();
            }));
        }

        public void FlushBufferedLogs()
        {
            Control? ctrl = _richTextBox;
            if (_buffer.Count == 0 || ctrl == null || !ctrl.IsHandleCreated)
                return;

            foreach (var msg in _buffer)
                Log(msg);

            _buffer.Clear();
        }

        #region IAppLogger Implementation

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            string prefixedMessage = level switch
            {
                LogLevel.Debug => $"[DEBUG] {message}",
                LogLevel.Warning => $"Warning: {message}",
                LogLevel.Error => $"Error: {message}",
                _ => message
            };
            Log(prefixedMessage);
        }

        public void LogError(string message, Exception? ex = null)
        {
            string fullMessage = ex != null 
                ? $"{message}: {ex.Message}" 
                : message;
            Log(fullMessage, LogLevel.Error);
        }

        public void LogWarning(string message) => Log(message, LogLevel.Warning);

        public void LogDebug(string message) => Log(message, LogLevel.Debug);

        #endregion


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

        private static Color CategorizeMessage(string message) => ColorForCategory(CategorizeKey(message));

        private static Color ColorForCategory(string category) => category switch
        {
            "success" => CyberGreen,
            "error" => CyberRed,
            "warning" => CyberOrange,
            "progress" => CyberCyan,
            _ => CyberGrey
        };

        private static string CategorizeKey(string message)
        {
            string lower = message.ToLowerInvariant();

            if (lower.Contains("done") ||
                lower.Contains("success") ||
                lower.Contains("completed") ||
                lower.Contains("ready") ||
                lower.Contains("installed") ||
                lower.Contains("applied") ||
                lower.Contains("enabled") ||
                lower.Contains("generated") ||
                lower.Contains("up to date"))
                return "success";

            if (lower.Contains("error") ||
                lower.Contains("failed") ||
                lower.Contains("critical") ||
                lower.Contains("cannot") ||
                lower.Contains("exception") ||
                lower.Contains("not found") ||
                lower.Contains("invalid"))
                return "error";

            if (lower.Contains("warning") ||
                lower.Contains("skipped") ||
                lower.Contains("missing") ||
                lower.Contains("canceled") ||
                lower.Contains("cancelled") ||
                lower.Contains("already") ||
                lower.Contains("please") ||
                lower.Contains("close") ||
                lower.Contains("[status]"))
                return "warning";

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
                return "progress";

            return "default";
        }
    }
}

