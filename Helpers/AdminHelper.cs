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
using System.IO;
using System.Security.Principal;

namespace ArdysaModsTools.Helpers
{
    /// <summary>
    /// Provides on-demand elevation utilities.
    /// 
    /// The main app runs as <c>asInvoker</c> (no admin) so Dota 2 files
    /// won't inherit admin-owned ACLs. This helper handles the rare cases
    /// where elevation IS needed:
    ///   1. Legacy installs under Program Files (can't write without admin)
    ///   2. Future features that explicitly require admin
    /// 
    /// Design notes:
    /// - <see cref="IsRunningAsAdmin"/> uses WindowsPrincipal, the standard .NET approach.
    /// - <see cref="IsInProtectedPath"/> checks if the exe lives under Program Files.
    /// - <see cref="RestartAsAdmin"/> re-launches the same exe with <c>Verb = "runas"</c>,
    ///   then exits the current process so there's only one instance.
    /// </summary>
    public static class AdminHelper
    {
        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns <c>true</c> if the current process is running with administrator privileges.
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application executable lives under a
        /// Windows-protected directory (Program Files or Program Files (x86)).
        /// These paths require admin privileges for write access.
        /// </summary>
        public static bool IsInProtectedPath()
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return IsUnderProgramFiles(appDir);
        }

        /// <summary>
        /// Re-launches the current application with administrator privileges
        /// and terminates the current (non-elevated) process.
        /// 
        /// If the user declines the UAC prompt, logs and returns <c>false</c>
        /// instead of crashing — the caller can decide what to do.
        /// </summary>
        /// <param name="reason">
        /// Optional reason for elevation (logged to startup_log.txt).
        /// </param>
        /// <returns>
        /// <c>true</c> if the elevated process was launched (caller should exit).
        /// <c>false</c> if the user cancelled the UAC prompt.
        /// </returns>
        public static bool RestartAsAdmin(string? reason = null)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    Core.Services.FallbackLogger.Log("AdminHelper: Cannot determine exe path for elevation.");
                    return false;
                }

                Core.Services.FallbackLogger.Log(
                    $"AdminHelper: Requesting elevation. Reason: {reason ?? "unspecified"}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppContext.BaseDirectory,
                };

                Process.Start(startInfo);
                return true; // Caller should exit
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED (1223) — user declined the UAC prompt
                Core.Services.FallbackLogger.Log("AdminHelper: User declined UAC elevation prompt.");
                return false;
            }
            catch (Exception ex)
            {
                Core.Services.FallbackLogger.Log($"AdminHelper: Elevation failed: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if a path starts with any known protected directory.
        /// </summary>
        private static bool IsUnderProgramFiles(string path)
        {
            string[] protectedRoots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            };

            foreach (var root in protectedRoots)
            {
                if (!string.IsNullOrEmpty(root) &&
                    path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
