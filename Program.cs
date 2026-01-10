using System;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools
{
    static class Program
    {
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

            Application.Run(new MainForm());
        }
    }
}