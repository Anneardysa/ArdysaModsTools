using System;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
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

            // Global exception handlers for unhandled exceptions
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"ThreadException: {e.Exception}");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"UnhandledException: {ex}");
            };

            // Cleanup security on exit
            Application.ApplicationExit += (s, e) =>
            {
                SecurityManager.Shutdown();
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

                Application.Run(new MainForm());
            }
        }
    }
}