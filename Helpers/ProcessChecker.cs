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
        
        // Token information class constants
        private const int TokenElevation = 20;           // TOKEN_ELEVATION - whether token is elevated
        private const int TokenElevationType = 18;       // TOKEN_ELEVATION_TYPE - how elevation occurred

        // Elevation type values
        private enum ElevationType
        {
            TokenElevationTypeDefault = 1,  // Process started by user with no UAC, e.g., built-in admin with UAC disabled
            TokenElevationTypeFull = 2,     // Process was explicitly elevated via UAC ("Run as administrator")
            TokenElevationTypeLimited = 3   // Normal user token (not elevated, even if admin account with UAC enabled)
        }

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
        /// Uses TokenElevationType to accurately determine if a process was explicitly elevated via UAC.
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
                        if (IsProcessTrulyElevated(proc))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Can't access this process, skip - assume NOT elevated
                        // This is safer than assuming elevated (avoids false positives)
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
        /// Checks if a process was explicitly elevated via UAC ("Run as administrator").
        /// This is more accurate than just checking TokenElevation, which can give false positives.
        /// 
        /// Logic:
        /// 1. First check TokenElevationType - this tells us HOW the token was created
        ///    - TokenElevationTypeFull: Process was explicitly elevated via UAC (this is admin)
        ///    - TokenElevationTypeLimited: Normal user token, NOT elevated (even if user is admin with UAC on)
        ///    - TokenElevationTypeDefault: No split token (UAC disabled, or built-in Administrator)
        /// 
        /// 2. For TokenElevationTypeDefault, we need additional check with TokenElevation
        ///    to see if running with admin privileges (UAC disabled scenarios)
        /// </summary>
        private static bool IsProcessTrulyElevated(Process process)
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
                {
                    // Can't open token - likely access denied. Assume NOT elevated to avoid false positives.
                    return false;
                }

                // First, get the elevation TYPE (more reliable than just elevation status)
                ElevationType elevationType = GetTokenElevationType(tokenHandle);
                
                switch (elevationType)
                {
                    case ElevationType.TokenElevationTypeFull:
                        // Process was explicitly elevated via UAC - this IS running as admin
                        return true;
                        
                    case ElevationType.TokenElevationTypeLimited:
                        // This is a filtered/limited token - NOT running as admin
                        // This is the token type for normal apps even if user is admin
                        return false;
                        
                    case ElevationType.TokenElevationTypeDefault:
                        // No split token exists. This happens when:
                        // 1. User is the built-in Administrator account
                        // 2. UAC is completely disabled
                        // 3. Running on older Windows without UAC
                        // Need to do additional check with TokenElevation
                        return GetTokenElevationStatus(tokenHandle);
                        
                    default:
                        // Unknown type - assume not elevated to be safe
                        return false;
                }
            }
            catch
            {
                // Access denied or other error - assume NOT elevated to avoid false positives
                return false;
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }

        /// <summary>
        /// Gets the TOKEN_ELEVATION_TYPE for a token handle.
        /// Returns Default if unable to determine.
        /// </summary>
        private static ElevationType GetTokenElevationType(IntPtr tokenHandle)
        {
            IntPtr elevationTypePtr = Marshal.AllocHGlobal(4);
            try
            {
                if (GetTokenInformation(tokenHandle, TokenElevationType, elevationTypePtr, 4, out uint returnLength))
                {
                    int elevationType = Marshal.ReadInt32(elevationTypePtr);
                    if (Enum.IsDefined(typeof(ElevationType), elevationType))
                    {
                        return (ElevationType)elevationType;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(elevationTypePtr);
            }
            
            // If we can't get the type, return Default (will require additional check)
            return ElevationType.TokenElevationTypeDefault;
        }

        /// <summary>
        /// Gets the TOKEN_ELEVATION status (whether token is elevated).
        /// Used as fallback for TokenElevationTypeDefault scenarios.
        /// </summary>
        private static bool GetTokenElevationStatus(IntPtr tokenHandle)
        {
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
            
            return false;
        }
    }
}

