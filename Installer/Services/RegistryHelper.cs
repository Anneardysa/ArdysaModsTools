/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using Microsoft.Win32;

namespace ArdysaModsTools.Installer.Services
{
    public static class RegistryHelper
    {
        private const string AppName = "ArdysaModsTools";
        private const string AppExe = "ArdysaModsTools.exe";
        private const string Publisher = "Ardysa";
        private const string AppUrl = "https://ardysamods.my.id";

        private const string UninstallGuid = "{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}";

        private static string UninstallKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}";

        private static string LegacyUninstallKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}_is1";

        private static string AppPathsKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{AppExe}";


        public static string? GetInstalledPath()
        {
            var path = GetInstalledPathFromHive(Registry.CurrentUser);
            if (path != null) return path;

            path = GetInstalledPathFromHive(Registry.LocalMachine);
            if (path != null) return path;

            return null;
        }

        public static bool IsPerUserInstall(string installPath)
        {
            var normalizedPath = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar);

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData) &&
                normalizedPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                return true;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData) &&
                normalizedPath.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
                return true;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) &&
                normalizedPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static void RegisterApp(string installPath)
        {
            var exePath = Path.Combine(installPath, AppExe);
            var version = GetVersionFromExe(exePath);
            var hive = GetRegistryHive(installPath);

            using (var key = hive.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", Publisher);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("InstallLocation", installPath);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

                var uninstallerPath = UninstallService.GetUninstallerPath(installPath);
                key.SetValue("UninstallString",
                    $"\"{uninstallerPath}\" --uninstall");
                key.SetValue("QuietUninstallString",
                    $"\"{uninstallerPath}\" --uninstall --silent");

                key.SetValue("URLInfoAbout", AppUrl);
                key.SetValue("URLUpdateInfo", $"{AppUrl}/releases");
                key.SetValue("HelpLink", $"{AppUrl}/issues");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                var sizeKb = GetDirectorySizeKb(installPath);
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
            }

            using (var key = hive.CreateSubKey(AppPathsKeyPath))
            {
                key.SetValue("", exePath);
                key.SetValue("Path", installPath);
            }

            CleanupLegacyKeys();
        }

        public static void UnregisterApp()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    hive.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
                    hive.DeleteSubKeyTree(AppPathsKeyPath, throwOnMissingSubKey: false);
                }
                catch
                {
                }
            }

            CleanupLegacyKeys();
        }


        private static RegistryKey GetRegistryHive(string installPath)
        {
            return IsPerUserInstall(installPath)
                ? Registry.CurrentUser
                : Registry.LocalMachine;
        }

        private static string? GetInstalledPathFromHive(RegistryKey hive)
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

            return null;
        }

        private static void CleanupLegacyKeys()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    hive.DeleteSubKeyTree(LegacyUninstallKeyPath, throwOnMissingSubKey: false);
                }
                catch {  }
            }
        }

        private static string GetVersionFromExe(string exePath)
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                var version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "1.0.0";

                var plusIdx = version.IndexOf('+');
                if (plusIdx > 0)
                    version = version[..plusIdx];

                return version;
            }
            catch
            {
                return "1.0.0";
            }
        }

        private static int GetDirectorySizeKb(string path)
        {
            try
            {
                var totalBytes = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f => new System.IO.FileInfo(f).Length);
                return (int)(totalBytes / 1024);
            }
            catch
            {
                return 0;
            }
        }
    }
}
