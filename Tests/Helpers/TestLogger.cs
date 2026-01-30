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
    /// <summary>
    /// Test logger that captures all log messages for assertion in unit tests.
    /// Provides helper methods for verifying logging behavior.
    /// </summary>
    /// <example>
    /// <code>
    /// var logger = new TestLogger();
    /// await service.DoWorkAsync(logger);
    /// 
    /// Assert.That(logger.HasLogContaining("success"), Is.True);
    /// Assert.That(logger.ErrorCount, Is.EqualTo(0));
    /// </code>
    /// </example>
    public class TestLogger : IAppLogger
    {
        private readonly List<LogEntry> _logs = new();
        private readonly object _lock = new();

        /// <summary>
        /// All captured log entries with message, level, and timestamp.
        /// </summary>
        public IReadOnlyList<LogEntry> Logs
        {
            get
            {
                lock (_lock) { return _logs.ToList(); }
            }
        }

        /// <summary>
        /// Count of error-level log entries.
        /// </summary>
        public int ErrorCount => Logs.Count(l => l.Level == LogLevel.Error);

        /// <summary>
        /// Count of warning-level log entries.
        /// </summary>
        public int WarningCount => Logs.Count(l => l.Level == LogLevel.Warning);

        /// <summary>
        /// All log messages as strings (convenience property).
        /// </summary>
        public IEnumerable<string> Messages => Logs.Select(l => l.Message);

        /// <inheritdoc />
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry(message, level, DateTime.UtcNow));
            }
        }

        /// <inheritdoc />
        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            Log(fullMessage, LogLevel.Error);
        }

        /// <inheritdoc />
        public void LogWarning(string message) => Log(message, LogLevel.Warning);

        /// <inheritdoc />
        public void LogDebug(string message) => Log(message, LogLevel.Debug);

        /// <inheritdoc />
        public void FlushBufferedLogs() { } // No buffering in test logger

        #region Assertion Helpers

        /// <summary>
        /// Checks if any log message contains the specified text (case-insensitive).
        /// </summary>
        public bool HasLogContaining(string text)
            => Logs.Any(l => l.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Checks if any log message contains the text at the specified level.
        /// </summary>
        public bool HasLogContaining(string text, LogLevel level)
            => Logs.Any(l => l.Level == level && 
                l.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets all messages at a specific log level.
        /// </summary>
        public IEnumerable<string> GetMessagesAtLevel(LogLevel level)
            => Logs.Where(l => l.Level == level).Select(l => l.Message);

        /// <summary>
        /// Clears all captured logs (useful between test scenarios).
        /// </summary>
        public void Clear()
        {
            lock (_lock) { _logs.Clear(); }
        }

        #endregion
    }

    /// <summary>
    /// Represents a single log entry with metadata.
    /// </summary>
    public record LogEntry(string Message, LogLevel Level, DateTime Timestamp);
}
