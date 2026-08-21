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
using System.Security.AccessControl;
using System.Security.Principal;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Security;

public static class FileSecurityGuard
{
    public static bool ApplyRuntimeDaclLock(string filePath, IAppLogger? logger = null)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(filePath)) return false;

            var fileInfo = new FileInfo(filePath);
            var fileSecurity = fileInfo.GetAccessControl();

            var currentUser = WindowsIdentity.GetCurrent().User;
            var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var everyoneGroup = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            if (currentUser != null)
            {
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                    AccessControlType.Deny));
            }

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                usersGroup,
                FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                AccessControlType.Deny));

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                everyoneGroup,
                FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                AccessControlType.Deny));

            fileInfo.SetAccessControl(fileSecurity);
            logger?.LogDebug($"FileSecurityGuard: Applied NTFS Deny DACL to '{Path.GetFileName(filePath)}'.");
            return true;
        }
        catch (Exception ex)
        {
            logger?.Log($"FileSecurityGuard: Failed to apply DACL lock to '{filePath}': {ex.Message}");
            return false;
        }
    }

    public static bool ReleaseDaclLock(string filePath, IAppLogger? logger = null)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(filePath)) return false;

            var fileInfo = new FileInfo(filePath);
            var fileSecurity = fileInfo.GetAccessControl();

            var currentUser = WindowsIdentity.GetCurrent().User;
            var usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var everyoneGroup = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            if (currentUser != null)
            {
                fileSecurity.RemoveAccessRuleAll(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                    AccessControlType.Deny));
            }

            fileSecurity.RemoveAccessRuleAll(new FileSystemAccessRule(
                usersGroup,
                FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                AccessControlType.Deny));

            fileSecurity.RemoveAccessRuleAll(new FileSystemAccessRule(
                everyoneGroup,
                FileSystemRights.ReadData | FileSystemRights.ExecuteFile,
                AccessControlType.Deny));

            fileInfo.SetAccessControl(fileSecurity);
            logger?.LogDebug($"FileSecurityGuard: Released NTFS DACL lock on '{Path.GetFileName(filePath)}'.");
            return true;
        }
        catch (Exception ex)
        {
            logger?.Log($"FileSecurityGuard: Failed to release DACL lock on '{filePath}': {ex.Message}");
            return false;
        }
    }
}
