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
    public partial class App : Application
    {
        private const string MutexName = "Global\\ArdysaModsTools_Installer_SingleInstance";
        private Mutex? _singleInstanceMutex;

        private bool _ownsMutex;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, MutexName, out _ownsMutex);
            if (!_ownsMutex)
            {
                System.Windows.MessageBox.Show(
                    "ArdysaModsTools Setup is already running.",
                    "ArdysaModsTools Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

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
                    Shutdown(0);
                }
                return;
            }


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

            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ownsMutex)
            {
                try { _singleInstanceMutex?.ReleaseMutex(); } catch {  }
            }
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
