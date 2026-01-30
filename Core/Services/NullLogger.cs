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
using System.Diagnostics;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// A null-object implementation of IAppLogger that safely handles all log calls.
    /// Writes to Debug output in DEBUG builds, otherwise does nothing.
    /// Use this instead of null checks: _logger = logger ?? NullLogger.Instance
    /// </summary>
    /// <remarks>
    /// This follows the Null Object Pattern, providing a safe default
    /// that eliminates null checks throughout the codebase.
    /// </remarks>
#pragma warning disable CS0618 // ILogger is obsolete - NullLogger still supports it for compatibility
    public sealed class NullLogger : IAppLogger, ILogger
#pragma warning restore CS0618
    {
        /// <summary>
        /// Singleton instance of NullLogger.
        /// </summary>
        public static readonly NullLogger Instance = new();

        private NullLogger() { }

        #region IAppLogger Implementation

        /// <inheritdoc />
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            WriteDebug($"[{level}] {message}");
        }

        /// <inheritdoc />
        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            WriteDebug($"[Error] {fullMessage}");
        }

        /// <inheritdoc />
        public void LogWarning(string message) => WriteDebug($"[Warning] {message}");

        /// <inheritdoc />
        public void LogDebug(string message) => WriteDebug($"[Debug] {message}");

        /// <inheritdoc />
        public void FlushBufferedLogs()
        {
            // No-op for null logger - nothing to flush
        }

        #endregion

        #region Legacy ILogger Implementation

        /// <inheritdoc />
        public void Log(string message)
        {
            WriteDebug(message);
        }

        #endregion

        [Conditional("DEBUG")]
        private static void WriteDebug(string message)
        {
            Debug.WriteLine($"[NullLogger] {message}");
        }
    }
}
