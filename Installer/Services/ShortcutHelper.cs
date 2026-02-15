/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using System.Runtime.InteropServices;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Creates Windows Start Menu and Desktop shortcuts using COM interop.
    /// Uses WScript.Shell via COM to create .lnk files without external dependencies.
    ///
    /// Context-aware: uses per-user shortcut locations for %LocalAppData% installs,
    /// common (all users) locations for Program Files installs.
    /// </summary>
    public static class ShortcutHelper
    {
        private const string AppName = "ArdysaModsTools";
        private const string AppExe = "ArdysaModsTools.exe";
        private const string AppDescription = "The Ultimate Dota 2 Mod Manager";

        /// <summary>
        /// Creates both Start Menu and Desktop shortcuts.
        /// Automatically uses per-user or common locations based on install path.
        /// </summary>
        public static void CreateShortcuts(string installPath)
        {
            var exePath = Path.Combine(installPath, AppExe);
            var isPerUser = RegistryHelper.IsPerUserInstall(installPath);

            // Start Menu shortcut
            CreateStartMenuShortcut(installPath, exePath, isPerUser);

            // Desktop shortcut
            CreateDesktopShortcut(exePath, isPerUser);
        }

        /// <summary>
        /// Removes all shortcuts created by the installer.
        /// Checks both per-user and common locations to handle any install type.
        /// </summary>
        public static void RemoveShortcuts()
        {
            // Remove from both locations — we don't know which was used
            RemoveStartMenuShortcut(perUser: true);
            RemoveStartMenuShortcut(perUser: false);
            RemoveDesktopShortcut(perUser: true);
            RemoveDesktopShortcut(perUser: false);
        }

        // ════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════

        private static void CreateStartMenuShortcut(string installPath, string exePath, bool perUser)
        {
            try
            {
                // Per-user: %AppData%\Microsoft\Windows\Start Menu\Programs
                // Common:   C:\ProgramData\Microsoft\Windows\Start Menu\Programs
                var folder = perUser
                    ? Environment.SpecialFolder.StartMenu
                    : Environment.SpecialFolder.CommonStartMenu;

                var startMenuPath = Path.Combine(
                    Environment.GetFolderPath(folder),
                    "Programs", AppName);
                Directory.CreateDirectory(startMenuPath);

                var shortcutPath = Path.Combine(startMenuPath, $"{AppName}.lnk");
                CreateShortcutFile(shortcutPath, exePath, installPath);
            }
            catch
            {
                // Non-critical — don't fail install over a shortcut
            }
        }

        private static void CreateDesktopShortcut(string exePath, bool perUser)
        {
            try
            {
                // Per-user: %USERPROFILE%\Desktop
                // Common:   C:\Users\Public\Desktop
                var folder = perUser
                    ? Environment.SpecialFolder.DesktopDirectory
                    : Environment.SpecialFolder.CommonDesktopDirectory;

                var desktopPath = Environment.GetFolderPath(folder);
                var shortcutPath = Path.Combine(desktopPath, $"{AppName}.lnk");
                CreateShortcutFile(shortcutPath, exePath, Path.GetDirectoryName(exePath)!);
            }
            catch
            {
                // Non-critical
            }
        }

        private static void RemoveStartMenuShortcut(bool perUser)
        {
            try
            {
                var folder = perUser
                    ? Environment.SpecialFolder.StartMenu
                    : Environment.SpecialFolder.CommonStartMenu;

                var startMenuPath = Path.Combine(
                    Environment.GetFolderPath(folder),
                    "Programs", AppName);
                if (Directory.Exists(startMenuPath))
                    Directory.Delete(startMenuPath, recursive: true);
            }
            catch { /* best effort */ }
        }

        private static void RemoveDesktopShortcut(bool perUser)
        {
            try
            {
                var folder = perUser
                    ? Environment.SpecialFolder.DesktopDirectory
                    : Environment.SpecialFolder.CommonDesktopDirectory;

                var desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(folder),
                    $"{AppName}.lnk");
                if (File.Exists(desktopShortcut))
                    File.Delete(desktopShortcut);
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Creates a .lnk shortcut file using Windows Script Host COM object.
        /// </summary>
        private static void CreateShortcutFile(string shortcutPath, string targetPath, string workingDir)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell not available");

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                var shortcut = shell.CreateShortcut(shortcutPath);
                try
                {
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = AppDescription;
                    shortcut.IconLocation = $"{targetPath},0";
                    shortcut.Save();
                }
                finally
                {
                    Marshal.ReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }
}
