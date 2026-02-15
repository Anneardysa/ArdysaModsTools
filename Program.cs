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
            // Diagnostic log path (next to the exe).
            // Overwritten each session — only the most recent startup is kept.
            var startupLogPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt");
            var logInitialized = false;

            void Log(string message)
            {
                try
                {
                    var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n";

                    if (!logInitialized)
                    {
                        // First write of this session — overwrite the file
                        System.IO.File.WriteAllText(startupLogPath, line);
                        logInitialized = true;
                    }
                    else
                    {
                        System.IO.File.AppendAllText(startupLogPath, line);
                    }
                }
                catch { /* ignore logging errors */ }
            }

            try
            {
                Log("=== ArdysaModsTools startup begin ===");
                Log($"OS: {Environment.OSVersion}, .NET: {Environment.Version}, 64-bit: {Environment.Is64BitProcess}");
                Log($"Exe: {Environment.ProcessPath}");
                Log($"Dir: {AppDomain.CurrentDomain.BaseDirectory}");

                // ═══════════════════════════════════════════════════════════════
                // ENVIRONMENT CONFIGURATION
                // Load configuration from .env file (for open-source deployment)
                // ═══════════════════════════════════════════════════════════════
                Log("Loading environment config...");
                EnvironmentConfig.LoadFromEnvFile();
                Log("Environment config loaded OK");

                // ═══════════════════════════════════════════════════════════════
                // SECURITY INITIALIZATION (Release builds only)
                // Checks for debuggers, tampering, and reverse engineering tools
                // ═══════════════════════════════════════════════════════════════
                Log("Security check starting...");
                if (!SecurityManager.Initialize(exitOnFailure: true))
                {
                    Log("BLOCKED: Security check failed");
                    return; // Security check failed
                }
                Log("Security check passed OK");

                // ═══════════════════════════════════════════════════════════════
                // DEPENDENCY INJECTION SETUP
                // Build service container for clean architecture
                // ═══════════════════════════════════════════════════════════════
                Log("Building DI container...");
                var services = new ServiceCollection();
                services.AddArdysaServices();
                var serviceProvider = services.BuildServiceProvider();
                Log("DI container built OK");
                
                // NOTE: ServiceLocator has been removed. All DI now uses constructor injection.
                // MainForm is created via IMainFormFactory.Create() which handles DI properly.

                // ═══════════════════════════════════════════════════════════════
                // GLOBAL EXCEPTION HANDLING
                // Centralized error handling with user-friendly messages
                // ═══════════════════════════════════════════════════════════════
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                
                Application.ThreadException += (s, e) =>
                {
                    Log($"THREAD EXCEPTION: {e.Exception}");
                    Core.Helpers.GlobalExceptionHandler.Handle(e.Exception, showDialog: true);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        Log($"UNHANDLED EXCEPTION: {ex}");
                        Core.Helpers.GlobalExceptionHandler.Handle(ex, showDialog: true);
                    }
                };

                // Cleanup on exit
                Application.ApplicationExit += (s, e) =>
                {
                    SecurityManager.Shutdown();
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Check for dota2 process before creating any forms
                const string gameProcessName = "dota2"; // without .exe
                Log("Checking for Dota 2 process...");
                if (ProcessChecker.IsProcessRunning(gameProcessName))
                {
                    Log("BLOCKED: Dota 2 is running");
                    MessageBox.Show(
                        "ArdysaModsTools cannot run while dota2.exe is active. Please close Dota 2 and try again.",
                        "Cannot start — Dota 2 is running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return; // exit application
                }
                Log("Dota 2 not running, OK");

                // ═══════════════════════════════════════════════════════════════
                // SINGLE INSTANCE ENFORCEMENT
                // ═══════════════════════════════════════════════════════════════
                const string appMutexName = "Global\\ArdysaModsTools_SingleInstance_Mutex";
                bool createdNew;

                Log("Checking single instance mutex...");
                using (var mutex = new System.Threading.Mutex(true, appMutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        Log("BLOCKED: Another instance already running");
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
                    Log("Single instance OK, creating main form...");

                    // Use factory pattern for proper DI (avoids obsolete constructor warning)
                    var mainFormFactory = serviceProvider.GetRequiredService<UI.Factories.IMainFormFactory>();
                    Log("MainFormFactory resolved, launching Application.Run...");
                    Application.Run(mainFormFactory.Create());
                    Log("Application.Run exited normally");
                }
            }
            catch (Exception ex)
            {
                Log($"FATAL CRASH: {ex}");
                MessageBox.Show(
                    $"ArdysaModsTools failed to start:\n\n{ex.Message}\n\nPlease send the startup_log.txt file to the developer.",
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}

