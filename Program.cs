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
using Microsoft.Extensions.DependencyInjection;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools
{
    static class Program
    {
        // P/Invoke for window management
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            // ═══════════════════════════════════════════════════════════════
            // ENVIRONMENT CONFIGURATION
            // Load configuration from .env file (for open-source deployment)
            // ═══════════════════════════════════════════════════════════════
            EnvironmentConfig.LoadFromEnvFile();

            // ═══════════════════════════════════════════════════════════════
            // SECURITY INITIALIZATION (Release builds only)
            // Checks for debuggers, tampering, and reverse engineering tools
            // ═══════════════════════════════════════════════════════════════
            if (!SecurityManager.Initialize(exitOnFailure: true))
            {
                return; // Security check failed
            }

            // ═══════════════════════════════════════════════════════════════
            // DEPENDENCY INJECTION SETUP
            // Build service container for clean architecture
            // ═══════════════════════════════════════════════════════════════
            var services = new ServiceCollection();
            services.AddArdysaServices();
            var serviceProvider = services.BuildServiceProvider();
            ServiceLocator.Initialize(serviceProvider);

            // ═══════════════════════════════════════════════════════════════
            // GLOBAL EXCEPTION HANDLING
            // Centralized error handling with user-friendly messages
            // ═══════════════════════════════════════════════════════════════
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            
            Application.ThreadException += (s, e) =>
            {
                Core.Helpers.GlobalExceptionHandler.Handle(e.Exception, showDialog: true);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Core.Helpers.GlobalExceptionHandler.Handle(ex, showDialog: true);
                }
            };

            // Cleanup on exit
            Application.ApplicationExit += (s, e) =>
            {
                SecurityManager.Shutdown();
                ServiceLocator.Dispose();
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check for dota2 process before creating any forms
            const string gameProcessName = "dota2"; // without .exe
            if (ProcessChecker.IsProcessRunning(gameProcessName))
            {
                MessageBox.Show(
                    "ArdysaModsTools cannot run while dota2.exe is active. Please close Dota 2 and try again.",
                    "Cannot start — Dota 2 is running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return; // exit application
            }

            // ═══════════════════════════════════════════════════════════════
            // SINGLE INSTANCE ENFORCEMENT
            // ═══════════════════════════════════════════════════════════════
            const string appMutexName = "Global\\ArdysaModsTools_SingleInstance_Mutex";
            bool createdNew;

            using (var mutex = new System.Threading.Mutex(true, appMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // App is already running - find it and bring to front
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    var existingProcess = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
                    
                    foreach (var process in existingProcess)
                    {
                        if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                            SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }

                    return; // Exit immediately
                }

                // Use classic WinForms MainForm
                // Note: WebView2 dashboard (MainFormWebView) requires more integration work
                Application.Run(new MainForm());
            }
        }
    }
}

