using System;
using System.IO;
using System.Security.Principal;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Detects whether the application was installed via installer or is running as portable.
    /// </summary>
    public static class InstallationDetector
    {
        private const string UninstallerFileName = "unins000.exe";
        private const string MarkerFileName = ".installed";
        private const string AppDataFolderName = "ArdysaModsTools";

        /// <summary>
        /// Detects the installation type of the current application instance.
        /// Priority: 1) Uninstaller presence, 2) Program Files location
        /// Marker file is NOT used for detection to avoid false positives.
        /// </summary>
        /// <returns>The detected installation type.</returns>
        public static InstallationType Detect()
        {
            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

            // Primary check: Inno Setup creates unins000.exe in the app directory
            // This is the most reliable indicator of an installed version
            string uninstallerPath = Path.Combine(appDir, UninstallerFileName);
            if (File.Exists(uninstallerPath))
            {
                return InstallationType.Installer;
            }

            // Secondary check: See if running from Program Files
            // Installed apps typically live in Program Files
            string? programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

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

            // No installer indicators found - this is a portable installation
            // Note: We intentionally do NOT check the marker file here because:
            // - A user might have previously installed the app, then run portable
            // - The marker file would incorrectly indicate "Installed"
            return InstallationType.Portable;
        }

        /// <summary>
        /// Gets the path to the installation marker file.
        /// </summary>
        public static string GetMarkerFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, AppDataFolderName, MarkerFileName);
        }

        /// <summary>
        /// Creates the installation marker file (called by installer).
        /// </summary>
        public static void CreateMarkerFile()
        {
            string markerPath = GetMarkerFilePath();
            string? directory = Path.GetDirectoryName(markerPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(markerPath, $"Installed: {DateTime.UtcNow:O}");
        }

        /// <summary>
        /// Removes the installation marker file (called by uninstaller).
        /// </summary>
        public static void RemoveMarkerFile()
        {
            string markerPath = GetMarkerFilePath();
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }

        /// <summary>
        /// Checks if the current process is running with administrator privileges.
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
        /// Gets the installation directory of the application.
        /// </summary>
        public static string GetInstallPath()
        {
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Gets a user-friendly name for the installation type.
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
