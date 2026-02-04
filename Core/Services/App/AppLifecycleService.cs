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
using System.Windows.Forms;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Services.App
{
    /// <summary>
    /// Manages application lifecycle settings such as "Run on Windows Startup".
    /// Uses the current user's registry (HKCU) to avoid requiring admin privileges.
    /// </summary>
    public class AppLifecycleService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ArdysaModsTools";

        /// <summary>
        /// Gets whether the application is configured to run on Windows startup.
        /// </summary>
        public bool IsRunOnStartupEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                    return key?.GetValue(AppName) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Enables or disables the application to run on Windows startup.
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        public bool SetRunOnStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                    return false;

                if (enable)
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[AppLifecycleService] Failed to set startup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restarts the application.
        /// </summary>
        public void RestartApplication()
        {
            try
            {
                System.Diagnostics.Process.Start(Application.ExecutablePath);
                Application.Exit();
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[AppLifecycleService] Failed to restart: {ex.Message}");
            }
        }
    }
}
