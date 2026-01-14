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
using System.Diagnostics;
using System.Linq;

namespace ArdysaModsTools.Helpers
{
    public static class ProcessChecker
    {
        /// <summary>
        /// Returns true if any process with the given executable name (without extension)
        /// is running. Example: "dota2" to detect "dota2.exe".
        /// </summary>
        public static bool IsProcessRunning(string exeNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(exeNameWithoutExtension))
                throw new ArgumentException("Process name required", nameof(exeNameWithoutExtension));

            var name = exeNameWithoutExtension.Trim();
            try
            {
                var procs = Process.GetProcesses();
                return procs.Any(p =>
                {
                    try
                    {
                        return string.Equals(p.ProcessName?.Trim(), name, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                // Conservative behavior retained, but log the reason.
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"ProcessChecker.IsProcessRunning failed for '{name}': {ex.Message}");
                return true; // keep conservative default
            }
        }
    }
}

