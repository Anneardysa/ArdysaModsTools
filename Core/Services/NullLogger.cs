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

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// A null-object implementation of ILogger that safely handles all log calls.
    /// Writes to Debug output in DEBUG builds, otherwise does nothing.
    /// Use this instead of null checks: _logger = logger ?? NullLogger.Instance
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        /// <summary>
        /// Singleton instance of NullLogger.
        /// </summary>
        public static readonly NullLogger Instance = new();

        private NullLogger() { }

        /// <inheritdoc />
        public void Log(string message)
        {
            WriteDebug(message);
        }

        /// <inheritdoc />
        public void FlushBufferedLogs()
        {
            // No-op for null logger - nothing to flush
        }

        [Conditional("DEBUG")]
        private static void WriteDebug(string message)
        {
            Debug.WriteLine($"[NullLogger] {message}");
        }
    }
}
