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
using System.Linq;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Tests.Helpers
{
    public class TestLogger : IAppLogger
    {
        private readonly List<LogEntry> _logs = new();
        private readonly object _lock = new();

        public IReadOnlyList<LogEntry> Logs
        {
            get
            {
                lock (_lock) { return _logs.ToList(); }
            }
        }

        public int ErrorCount => Logs.Count(l => l.Level == LogLevel.Error);

        public int WarningCount => Logs.Count(l => l.Level == LogLevel.Warning);

        public IEnumerable<string> Messages => Logs.Select(l => l.Message);

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry(message, level, DateTime.UtcNow));
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            Log(fullMessage, LogLevel.Error);
        }

        public void LogWarning(string message) => Log(message, LogLevel.Warning);

        public void LogDebug(string message) => Log(message, LogLevel.Debug);

        public void FlushBufferedLogs() { }

        #region Assertion Helpers

        public bool HasLogContaining(string text)
            => Logs.Any(l => l.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

        public bool HasLogContaining(string text, LogLevel level)
            => Logs.Any(l => l.Level == level && 
                l.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> GetMessagesAtLevel(LogLevel level)
            => Logs.Where(l => l.Level == level).Select(l => l.Message);

        public void Clear()
        {
            lock (_lock) { _logs.Clear(); }
        }

        #endregion
    }

    public record LogEntry(string Message, LogLevel Level, DateTime Timestamp);
}
