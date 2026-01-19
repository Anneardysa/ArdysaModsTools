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
using System.Runtime.InteropServices;

namespace ArdysaModsTools.Helpers
{
    public static class ProcessChecker
    {
        #region P/Invoke for Admin Detection
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TOKEN_QUERY = 0x0008;
        private const int TokenElevation = 20;

        #endregion
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

        /// <summary>
        /// Returns true if any process with the given name is running with administrator privileges (elevated).
        /// </summary>
        public static bool IsProcessRunningAsAdmin(string exeNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(exeNameWithoutExtension))
                return false;

            var name = exeNameWithoutExtension.Trim();
            try
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var proc in processes)
                {
                    try
                    {
                        if (IsProcessElevated(proc))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Can't access this process, skip
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"ProcessChecker.IsProcessRunningAsAdmin failed for '{name}': {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Checks if a specific process is running elevated (as administrator).
        /// </summary>
        private static bool IsProcessElevated(Process process)
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
                {
                    return false;
                }

                // TOKEN_ELEVATION structure is just a single DWORD (4 bytes)
                IntPtr elevationPtr = Marshal.AllocHGlobal(4);
                try
                {
                    if (GetTokenInformation(tokenHandle, TokenElevation, elevationPtr, 4, out uint returnLength))
                    {
                        int elevation = Marshal.ReadInt32(elevationPtr);
                        return elevation != 0;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(elevationPtr);
                }
            }
            catch
            {
                // Access denied or other error - can't determine elevation
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }

            return false;
        }
    }
}

