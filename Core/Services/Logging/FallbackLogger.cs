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
    public static class FallbackLogger
    {
        private static string _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ardysa_fallback.log");

        public static Action<string>? UserLogger { get; set; }

        public static void Log(string message) => Write(message, toUi: true);

        public static void LogFileOnly(string message) => Write(message, toUi: false);

        private static void Write(string message, bool toUi)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                if (toUi)
                    try { UserLogger?.Invoke(message); } catch {  }

                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static void SetLogFile(string path)
        {
            try { _logFile = path; } catch { }
        }
    }
}

