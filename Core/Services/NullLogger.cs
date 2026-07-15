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
#pragma warning disable CS0618
    public sealed class NullLogger : IAppLogger, ILogger
#pragma warning restore CS0618
    {
        public static readonly NullLogger Instance = new();

        private NullLogger() { }

        #region IAppLogger Implementation

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            WriteDebug($"[{level}] {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
            WriteDebug($"[Error] {fullMessage}");
        }

        public void LogWarning(string message) => WriteDebug($"[Warning] {message}");

        public void LogDebug(string message) => WriteDebug($"[Debug] {message}");

        public void FlushBufferedLogs()
        {
        }

        #endregion

        #region Legacy ILogger Implementation

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
