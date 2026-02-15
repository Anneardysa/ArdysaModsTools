/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using Microsoft.Win32;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Manages Windows registry entries for the application:
    /// - Add/Remove Programs (ARP) entry
    /// - App Paths registration
    ///
    /// Context-aware: uses HKCU for per-user installs (%LocalAppData%),
    /// HKLM for system-wide installs (Program Files). This ensures
    /// registry entries match the install scope.
    /// </summary>
    public static class RegistryHelper
    {
        private const string AppName = "ArdysaModsTools";
        private const string AppExe = "ArdysaModsTools.exe";
        private const string Publisher = "Ardysa";
        private const string AppUrl = "https://github.com/Anneardysa/ArdysaModsTools";

        // Must match the AppId in the original .iss file for upgrade compatibility
        private const string UninstallGuid = "{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}";

        private static string UninstallKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}";

        // Legacy Inno Setup key (has _is1 suffix)
        private static string LegacyUninstallKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallGuid}_is1";

        private static string AppPathsKeyPath =>
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{AppExe}";

        // ================================================================
        // PUBLIC API
        // ================================================================

        /// <summary>
        /// Returns the current install path from the registry, or null if not installed.
        /// Checks both HKCU (per-user) and HKLM (system-wide), plus legacy keys.
        /// </summary>
        public static string? GetInstalledPath()
        {
            // Check HKCU first (per-user installs)
            var path = GetInstalledPathFromHive(Registry.CurrentUser);
            if (path != null) return path;

            // Check HKLM (system-wide installs)
            path = GetInstalledPathFromHive(Registry.LocalMachine);
            if (path != null) return path;

            return null;
        }

        /// <summary>
        /// Determines if the given install path is a per-user location.
        /// Per-user: %LocalAppData%, %AppData%, or anywhere under the user profile.
        /// System-wide: Program Files, Program Files (x86), or anything else.
        /// </summary>
        public static bool IsPerUserInstall(string installPath)
        {
            var normalizedPath = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar);

            // Check %LocalAppData% — the default install location
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData) &&
                normalizedPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check %AppData% (Roaming)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData) &&
                normalizedPath.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check user profile root (%USERPROFILE%)
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) &&
                normalizedPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Registers the application in Windows Add/Remove Programs.
        /// Automatically selects the correct registry hive (HKCU or HKLM)
        /// based on the install path scope.
        /// </summary>
        public static void RegisterApp(string installPath)
        {
            var exePath = Path.Combine(installPath, AppExe);
            var version = GetVersionFromExe(exePath);
            var hive = GetRegistryHive(installPath);

            // ────────────────────────────────────────────────
            // Add/Remove Programs entry
            // ────────────────────────────────────────────────
            using (var key = hive.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", Publisher);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("InstallLocation", installPath);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

                // Uninstall command
                key.SetValue("UninstallString",
                    $"\"{exePath}\" --uninstall");
                key.SetValue("QuietUninstallString",
                    $"\"{exePath}\" --uninstall --silent");

                // Metadata
                key.SetValue("URLInfoAbout", AppUrl);
                key.SetValue("URLUpdateInfo", $"{AppUrl}/releases");
                key.SetValue("HelpLink", $"{AppUrl}/issues");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                // Estimated size (in KB)
                var sizeKb = GetDirectorySizeKb(installPath);
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
            }

            // ────────────────────────────────────────────────
            // App Paths (allows running from Win+R / Start)
            // ────────────────────────────────────────────────
            using (var key = hive.CreateSubKey(AppPathsKeyPath))
            {
                key.SetValue("", exePath); // Default value = full path to exe
                key.SetValue("Path", installPath);
            }

            // ────────────────────────────────────────────────
            // Clean up legacy Inno Setup key (migration)
            // Try both hives — legacy could be in either
            // ────────────────────────────────────────────────
            CleanupLegacyKeys();
        }

        /// <summary>
        /// Removes all registry entries for the application.
        /// Cleans up from both HKCU and HKLM to handle any install scope.
        /// Called during uninstallation.
        /// </summary>
        public static void UnregisterApp()
        {
            // Clean from both hives — we don't know which was used at uninstall time
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    hive.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
                    hive.DeleteSubKeyTree(AppPathsKeyPath, throwOnMissingSubKey: false);
                }
                catch
                {
                    // Best effort — don't fail uninstall over registry cleanup
                }
            }

            CleanupLegacyKeys();
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Returns the correct registry hive for the given install path.
        /// Per-user locations → HKCU, system-wide locations → HKLM.
        /// </summary>
        private static RegistryKey GetRegistryHive(string installPath)
        {
            return IsPerUserInstall(installPath)
                ? Registry.CurrentUser
                : Registry.LocalMachine;
        }

        /// <summary>
        /// Searches a single registry hive for the install path.
        /// Checks the new key first, then the legacy _is1 key.
        /// </summary>
        private static string? GetInstalledPathFromHive(RegistryKey hive)
        {
            try
            {
                // New WPF installer key
                using var key = hive.OpenSubKey(UninstallKeyPath);
                if (key?.GetValue("InstallLocation") is string path && !string.IsNullOrEmpty(path))
                    return path;

                // Legacy Inno Setup key
                using var legacyKey = hive.OpenSubKey(LegacyUninstallKeyPath);
                if (legacyKey?.GetValue("InstallLocation") is string legacyPath && !string.IsNullOrEmpty(legacyPath))
                    return legacyPath;
            }
            catch
            {
                // HKLM may fail without admin — no problem, try next hive
            }

            return null;
        }

        /// <summary>
        /// Removes legacy Inno Setup _is1 keys from both hives.
        /// </summary>
        private static void CleanupLegacyKeys()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    hive.DeleteSubKeyTree(LegacyUninstallKeyPath, throwOnMissingSubKey: false);
                }
                catch { /* best effort */ }
            }
        }

        private static string GetVersionFromExe(string exePath)
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                var version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "1.0.0";

                // Strip +commitHash build metadata (SemVer)
                // "2.1.12-beta+abc123" → "2.1.12-beta"
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
