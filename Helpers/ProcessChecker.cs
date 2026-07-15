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
        private const int TokenElevationType = 18;

        private enum ElevationType
        {
            TokenElevationTypeDefault = 1,
            TokenElevationTypeFull = 2,
            TokenElevationTypeLimited = 3
        }

        #endregion
        
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
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"ProcessChecker.IsProcessRunning failed for '{name}': {ex.Message}");
                return false;
            }
        }

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

        private static bool IsProcessTrulyElevated(Process process)
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
                {
                    return false;
                }

                ElevationType elevationType = GetTokenElevationType(tokenHandle);
                
                switch (elevationType)
                {
                    case ElevationType.TokenElevationTypeFull:
                        return true;
                        
                    case ElevationType.TokenElevationTypeLimited:
                        return false;
                        
                    case ElevationType.TokenElevationTypeDefault:
                        return GetTokenElevationStatus(tokenHandle);
                        
                    default:
                        return false;
                }
            }
            catch
            {
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
            
            return ElevationType.TokenElevationTypeDefault;
        }

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

