/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using System.Runtime.InteropServices;

namespace ArdysaModsTools.Installer.Services
{
    public static class ShortcutHelper
    {
        private const string AppName = "ArdysaModsTools";
        private const string AppExe = "ArdysaModsTools.exe";
        private const string AppDescription = "The Ultimate Dota 2 Mod Manager";

        public static void CreateShortcuts(string installPath)
        {
            var exePath = Path.Combine(installPath, AppExe);
            var isPerUser = RegistryHelper.IsPerUserInstall(installPath);

            CreateStartMenuShortcut(installPath, exePath, isPerUser);

            CreateDesktopShortcut(exePath, isPerUser);
        }

        public static void RemoveShortcuts()
        {
            RemoveStartMenuShortcut(perUser: true);
            RemoveStartMenuShortcut(perUser: false);
            RemoveDesktopShortcut(perUser: true);
            RemoveDesktopShortcut(perUser: false);
        }


        private static void CreateStartMenuShortcut(string installPath, string exePath, bool perUser)
        {
            try
            {
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
            }
        }

        private static void CreateDesktopShortcut(string exePath, bool perUser)
        {
            try
            {
                var folder = perUser
                    ? Environment.SpecialFolder.DesktopDirectory
                    : Environment.SpecialFolder.CommonDesktopDirectory;

                var desktopPath = Environment.GetFolderPath(folder);
                var shortcutPath = Path.Combine(desktopPath, $"{AppName}.lnk");
                CreateShortcutFile(shortcutPath, exePath, Path.GetDirectoryName(exePath)!);
            }
            catch
            {
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
            catch {  }
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
            catch {  }
        }

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
