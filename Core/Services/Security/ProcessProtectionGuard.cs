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
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Security
{
    public static class ProcessProtectionGuard
    {
        private const int DaclSecurityInformation = 0x00000004;

        private const int ProcessTerminate = 0x0001;
        private const int ProcessVmOperation = 0x0008;
        private const int ProcessVmWrite = 0x0020;
        private const int ProcessSuspendResume = 0x0800;

        private static readonly object Lock = new();
        private static byte[]? _originalDescriptorBytes;
        private static bool _isProtected;

        public static bool IsProtected
        {
            get
            {
                lock (Lock) return _isProtected;
            }
        }

        #region Win32 Native Imports

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            [Out] byte[]? pSecurityDescriptor,
            uint nLength,
            out uint lpnLengthNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            [In] byte[] pSecurityDescriptor);

        #endregion

        public static bool ProtectCurrentProcess(IAppLogger? logger = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            lock (Lock)
            {
                if (_isProtected)
                    return true;

                try
                {
                    IntPtr processHandle = GetCurrentProcess();

                    GetKernelObjectSecurity(processHandle, DaclSecurityInformation, null, 0, out uint lengthNeeded);
                    if (lengthNeeded == 0)
                    {
                        logger?.LogDebug("ProcessProtectionGuard: Failed to query security descriptor length.");
                        return false;
                    }

                    byte[] currentDescBytes = new byte[lengthNeeded];
                    if (!GetKernelObjectSecurity(processHandle, DaclSecurityInformation, currentDescBytes, lengthNeeded, out _))
                    {
                        int err = Marshal.GetLastWin32Error();
                        logger?.LogDebug($"ProcessProtectionGuard: GetKernelObjectSecurity failed with code {err}.");
                        return false;
                    }

                    _originalDescriptorBytes = currentDescBytes;

                    var rawDescriptor = new RawSecurityDescriptor(currentDescBytes, 0);
                    var dacl = rawDescriptor.DiscretionaryAcl ?? new RawAcl(2, 4);

                    var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                    int deniedMask = ProcessTerminate | ProcessVmOperation | ProcessVmWrite | ProcessSuspendResume;
                    dacl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessDenied, deniedMask, everyoneSid, false, null));

                    rawDescriptor.DiscretionaryAcl = dacl;

                    byte[] newDescBytes = new byte[rawDescriptor.BinaryLength];
                    rawDescriptor.GetBinaryForm(newDescBytes, 0);

                    if (!SetKernelObjectSecurity(processHandle, DaclSecurityInformation, newDescBytes))
                    {
                        int err = Marshal.GetLastWin32Error();
                        logger?.LogDebug($"ProcessProtectionGuard: SetKernelObjectSecurity failed with code {err}.");
                        return false;
                    }

                    _isProtected = true;
                    logger?.LogDebug("ProcessProtectionGuard: Process termination and suspend protection ACTIVE.");
                    FallbackLogger.LogFileOnly("[ProcessProtectionGuard] Process DACL protection applied.");
                    return true;
                }
                catch (Exception ex)
                {
                    logger?.LogDebug($"ProcessProtectionGuard.ProtectCurrentProcess error: {ex.Message}");
                    FallbackLogger.LogFileOnly($"ProcessProtectionGuard.ProtectCurrentProcess error: {ex.Message}");
                    return false;
                }
            }
        }

        public static bool UnprotectCurrentProcess(IAppLogger? logger = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            lock (Lock)
            {
                if (!_isProtected || _originalDescriptorBytes == null)
                {
                    _isProtected = false;
                    return true;
                }

                try
                {
                    IntPtr processHandle = GetCurrentProcess();
                    if (SetKernelObjectSecurity(processHandle, DaclSecurityInformation, _originalDescriptorBytes))
                    {
                        _isProtected = false;
                        _originalDescriptorBytes = null;
                        logger?.LogDebug("ProcessProtectionGuard: Process protection released.");
                        FallbackLogger.LogFileOnly("[ProcessProtectionGuard] Process DACL protection released.");
                        return true;
                    }

                    int err = Marshal.GetLastWin32Error();
                    logger?.LogDebug($"ProcessProtectionGuard: Failed to release process protection (code {err}).");
                    return false;
                }
                catch (Exception ex)
                {
                    logger?.LogDebug($"ProcessProtectionGuard.UnprotectCurrentProcess error: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
