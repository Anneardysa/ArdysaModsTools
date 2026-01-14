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
using System.IO;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Minimal logger for static helpers to write to file fallback and optionally to an injected UI logger.
    /// </summary>
    public static class FallbackLogger
    {
        private static string _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ardysa_fallback.log");

        /// <summary>
        /// Optional hook for UI logger. Set this from MainForm during startup:
        /// FallbackLogger.UserLogger = msg => myLogger.Log(msg);
        /// </summary>
        public static Action<string>? UserLogger { get; set; }

        public static void Log(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                // write to UI logger if available
                try { UserLogger?.Invoke(message); } catch { /* don't block on UI logger */ }

                // append to fallback file
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
                // best-effort only
            }
        }

        public static void SetLogFile(string path)
        {
            try { _logFile = path; } catch { }
        }
    }
}

