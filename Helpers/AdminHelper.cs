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
    public static class AdminHelper
    {

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

        public static bool IsInProtectedPath()
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return IsUnderProgramFiles(appDir);
        }

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
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Core.Services.FallbackLogger.Log("AdminHelper: User declined UAC elevation prompt.");
                return false;
            }
            catch (Exception ex)
            {
                Core.Services.FallbackLogger.Log($"AdminHelper: Elevation failed: {ex.Message}");
                return false;
            }
        }


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
