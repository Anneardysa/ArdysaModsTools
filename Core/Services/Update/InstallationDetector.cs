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
using System.Security.Principal;
using ArdysaModsTools.Core.Services.Update.Models;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Detects whether the application was installed via installer or is running as portable.
    ///
    /// Detection priority (first match wins):
    ///   1. Registry — checks both HKCU (per-user) and HKLM (system-wide)
    ///   2. LocalAppData path — default install location for the WPF installer
    ///   3. Program Files path — legacy Inno Setup installs
    ///   4. Fallback → Portable
    /// </summary>
    public static class InstallationDetector
    {
        private const string AppDataFolderName = "ArdysaModsTools";

        // Must match the GUID in Installer\Services\RegistryHelper.cs
        private const string UninstallGuid = "{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}";

        private static readonly string UninstallKeyPath =
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}";

        // Legacy Inno Setup key (has _is1 suffix)
        private static readonly string LegacyUninstallKeyPath =
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}_is1";

        /// <summary>
        /// Detects the installation type of the current application instance.
        /// </summary>
        public static InstallationType Detect()
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

            // ─────────────────────────────────────────────────
            // 1. Registry check — most reliable indicator
            //    Checks both HKCU (per-user) and HKLM (system-wide)
            // ─────────────────────────────────────────────────
            if (IsRegisteredInRegistry(appDir))
                return InstallationType.Installer;

            // ─────────────────────────────────────────────────
            // 2. LocalAppData path — WPF installer default location
            //    %LocalAppData%\ArdysaModsTools
            // ─────────────────────────────────────────────────
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var expectedInstallerPath = Path.Combine(localAppData, AppDataFolderName);

            if (!string.IsNullOrEmpty(localAppData) &&
                appDir.StartsWith(expectedInstallerPath, StringComparison.OrdinalIgnoreCase))
            {
                return InstallationType.Installer;
            }

            // ─────────────────────────────────────────────────
            // 3. Program Files — legacy Inno Setup installations
            // ─────────────────────────────────────────────────
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrEmpty(programFiles) &&
                appDir.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
            {
                return InstallationType.Installer;
            }

            if (!string.IsNullOrEmpty(programFilesX86) &&
                appDir.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
            {
                return InstallationType.Installer;
            }

            // ─────────────────────────────────────────────────
            // 4. Fallback → Portable
            // ─────────────────────────────────────────────────
            return InstallationType.Portable;
        }

        /// <summary>
        /// Checks if the application is registered in the Windows registry
        /// and the registered path matches the current running location.
        /// Searches both HKCU and HKLM, including legacy _is1 keys.
        /// </summary>
        private static bool IsRegisteredInRegistry(string appDir)
        {
            // Check both hives — per-user installs use HKCU, system-wide use HKLM
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                if (CheckHiveForPath(hive, UninstallKeyPath, appDir))
                    return true;

                if (CheckHiveForPath(hive, LegacyUninstallKeyPath, appDir))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks a single registry hive + key path for a matching InstallLocation.
        /// </summary>
        private static bool CheckHiveForPath(RegistryKey hive, string keyPath, string appDir)
        {
            try
            {
                using var key = hive.OpenSubKey(keyPath);
                if (key?.GetValue("InstallLocation") is string registeredPath &&
                    !string.IsNullOrEmpty(registeredPath))
                {
                    registeredPath = registeredPath.TrimEnd(Path.DirectorySeparatorChar);
                    if (IsPathMatch(appDir, registeredPath))
                        return true;
                }
            }
            catch
            {
                // HKLM may fail without admin — continue silently
            }

            return false;
        }

        private static bool IsPathMatch(string path1, string path2)
        {
            return string.Equals(
                path1?.TrimEnd('\\'),
                path2?.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the registered install path from registry, or null if not installed.
        /// Checks both HKCU and HKLM, including legacy keys.
        /// </summary>
        public static string? GetRegisteredInstallPath()
        {
            // Check HKCU first (per-user), then HKLM (system-wide)
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    // New key
                    using var key = hive.OpenSubKey(UninstallKeyPath);
                    if (key?.GetValue("InstallLocation") is string path && !string.IsNullOrEmpty(path))
                        return path;

                    // Legacy key
                    using var legacyKey = hive.OpenSubKey(LegacyUninstallKeyPath);
                    if (legacyKey?.GetValue("InstallLocation") is string legacyPath && !string.IsNullOrEmpty(legacyPath))
                        return legacyPath;
                }
                catch
                {
                    // HKLM may fail without admin — continue
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the installation directory of the currently running application.
        /// </summary>
        public static string GetInstallPath()
        {
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Checks if the current process has administrator privileges.
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
        /// Gets a user-friendly display name for the installation type.
        /// </summary>
        public static string GetInstallationTypeName(InstallationType type)
        {
            return type switch
            {
                InstallationType.Installer => "Installed",
                InstallationType.Portable => "Portable",
                _ => "Unknown"
            };
        }
    }
}
