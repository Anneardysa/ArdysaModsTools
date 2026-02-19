/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System.Threading;
using System.Windows;
using ArdysaModsTools.Installer.Services;
using Application = System.Windows.Application;

namespace ArdysaModsTools.Installer
{
    /// <summary>
    /// Application entry point.
    /// - Single-instance enforcement via named Mutex
    /// - Silent uninstall handling (--uninstall --silent)
    /// - Global unhandled-exception safety net
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "Global\\ArdysaModsTools_Installer_SingleInstance";
        private Mutex? _singleInstanceMutex;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ─────────────────────────────────────────────
            // Single-instance guard — prevent concurrent installs
            // which would cause file-locking conflicts
            // ─────────────────────────────────────────────
            _singleInstanceMutex = new Mutex(true, MutexName, out var isNewInstance);
            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show(
                    "ArdysaModsTools Setup is already running.",
                    "ArdysaModsTools Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            // ─────────────────────────────────────────────
            // Silent uninstall — used by Windows ARP
            // (QuietUninstallString) and third-party tools
            // like Revo Uninstaller. No UI is shown.
            // ─────────────────────────────────────────────
            var args = Environment.GetCommandLineArgs();
            var isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
            var isSilent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

            if (isUninstall && isSilent)
            {
                var installPath = RegistryHelper.GetInstalledPath();
                if (installPath != null)
                {
                    var service = new UninstallService();
                    var success = await service.RunSilentUninstallAsync(installPath);
                    Shutdown(success ? 0 : 1);
                }
                else
                {
                    Shutdown(0); // Nothing to uninstall
                }
                return;
            }

            // ─────────────────────────────────────────────
            // Interactive mode continues to MainWindow
            // (--uninstall without --silent is handled there)
            // ─────────────────────────────────────────────

            // Global exception handler — prevents silent crashes.
            DispatcherUnhandledException += (_, exArgs) =>
            {
                var message = exArgs.Exception.InnerException?.Message
                              ?? exArgs.Exception.Message;

                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred:\n\n{message}\n\n" +
                    "The installer will now close. Please try again.",
                    "ArdysaModsTools Setup — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                exArgs.Handled = true;
                Shutdown(1);
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release the mutex so a new instance can run
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
