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
using ArdysaModsTools.Core.Services.Update.Models;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Services.Update
{
    public static class InstallationDetector
    {
        private const string AppDataFolderName = "ArdysaModsTools";

        private const string UninstallGuid = "{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}";

        private static readonly string UninstallKeyPath =
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}";

        private static readonly string LegacyUninstallKeyPath =
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}_is1";

        public static InstallationType Detect()
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

            if (IsRegisteredInRegistry(appDir))
                return InstallationType.Installer;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var expectedInstallerPath = Path.Combine(localAppData, AppDataFolderName);

            if (!string.IsNullOrEmpty(localAppData) &&
                appDir.StartsWith(expectedInstallerPath, StringComparison.OrdinalIgnoreCase))
            {
                return InstallationType.Installer;
            }

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

            return InstallationType.Portable;
        }

        private static bool IsRegisteredInRegistry(string appDir)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                if (CheckHiveForPath(hive, UninstallKeyPath, appDir))
                    return true;

                if (CheckHiveForPath(hive, LegacyUninstallKeyPath, appDir))
                    return true;
            }

            return false;
        }

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

        public static string? GetRegisteredInstallPath()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey(UninstallKeyPath);
                    if (key?.GetValue("InstallLocation") is string path && !string.IsNullOrEmpty(path))
                        return path;

                    using var legacyKey = hive.OpenSubKey(LegacyUninstallKeyPath);
                    if (legacyKey?.GetValue("InstallLocation") is string legacyPath && !string.IsNullOrEmpty(legacyPath))
                        return legacyPath;
                }
                catch
                {
                }
            }

            return null;
        }

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
