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
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.App;

namespace ArdysaModsTools
{
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilter(uint message, uint flag);

        private const uint MSGFLT_ADD = 1;

        private static void AllowDragDropThroughUipi()
        {
            foreach (uint msg in new uint[] { 0x0233, 0x004A, 0x0049 })
            {
                try { ChangeWindowMessageFilter(msg, MSGFLT_ADD); }
                catch {  }
            }
        }

        [STAThread]
        static void Main()
        {
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
                        System.IO.File.WriteAllText(startupLogPath, line);
                        logInitialized = true;
                    }
                    else
                    {
                        System.IO.File.AppendAllText(startupLogPath, line);
                    }
                }
                catch {  }
            }

            try
            {
                Log("=== ArdysaModsTools startup begin ===");
                Log($"OS: {Environment.OSVersion}, .NET: {Environment.Version}, 64-bit: {Environment.Is64BitProcess}");
                Log($"Exe: {Environment.ProcessPath}");
                Log($"Dir: {AppDomain.CurrentDomain.BaseDirectory}");

                Log("Loading environment config...");
                EnvironmentConfig.LoadFromEnvFile();
                Log("Environment config loaded OK");

                Log("Building DI container...");
                var services = new ServiceCollection();
                services.AddArdysaServices();
                var serviceProvider = services.BuildServiceProvider();
                Log("DI container built OK");

                Log("Initializing localization...");
                var localizationService = serviceProvider.GetRequiredService<Core.Interfaces.ILocalizationService>();
                var configForLang = serviceProvider.GetRequiredService<Core.Interfaces.IConfigService>();
                var languageCode = configForLang.Language;
                if (string.IsNullOrEmpty(languageCode))
                {
                    languageCode = Core.Services.Localization.LocalizationService.ResolveSupported(
                        System.Globalization.CultureInfo.InstalledUICulture.Name);
                    configForLang.Language = languageCode;
                }
                localizationService.SetCulture(languageCode);
                Core.Services.Localization.Loc.Initialize(localizationService);
                Log($"Localization ready: {languageCode}");

                UI.Helpers.DpiLayout.UiScale = configForLang.GetValue("uiScale", 1.0);

                UI.Theme.SetTheme(configForLang.GetValue("theme", "dark") != "light");

                bool startMinimized = Environment.GetCommandLineArgs()
                    .Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                Log($"Start minimized: {startMinimized}");

                new AppLifecycleService().EnsureStartupPathCurrent();
                Log("Startup path check completed");
                

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

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                        Log($"FIRST-CHANCE: {e.Exception.GetType().FullName}: {e.Exception.Message}");
                }

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AllowDragDropThroughUipi();

                const string gameProcessName = "dota2";
                Log("Checking for Dota 2 process...");
                if (ProcessChecker.IsProcessRunning(gameProcessName))
                {
                    Log("BLOCKED: Dota 2 is running");
                    MessageBox.Show(
                        Core.Services.Localization.Loc.T("program.dota2Running.body"),
                        Core.Services.Localization.Loc.T("program.dota2Running.title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }
                Log("Dota 2 not running, OK");

                if (!AdminHelper.IsRunningAsAdmin() && AdminHelper.IsInProtectedPath())
                {
                    Log("Installed in protected path (Program Files), requesting elevation...");
                    if (AdminHelper.RestartAsAdmin("Application is installed under Program Files"))
                    {
                        Log("Elevated process launched, exiting current instance.");
                        return;
                    }
                    else
                    {
                        Log("WARNING: User declined elevation. App may have write permission issues.");
                    }
                }

                const string appMutexName = "Global\\ArdysaModsTools_SingleInstance_Mutex";
                bool createdNew;

                Log("Checking single instance mutex...");
                using (var mutex = new System.Threading.Mutex(true, appMutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        Log("BLOCKED: Another instance already running");
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

                        return;
                    }
                    Log("Single instance OK, creating main form...");

                    var webview2 = Core.Helpers.WebView2Runtime.Detect();
                    Log($"WebView2: source={webview2.Source} version={webview2.Version ?? "-"} diag={webview2.Diagnostic ?? "none"}");
                    if (!webview2.IsInstalled)
                    {
                        Log("WebView2 runtime not found — showing install prompt, exiting");
                        MessageBox.Show(
                            Core.Services.Localization.Loc.T("program.webview2Required.body"),
                            Core.Services.Localization.Loc.T("program.webview2Required.title"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    var mainFormFactory = serviceProvider.GetRequiredService<UI.Factories.IMainFormFactory>();
                    Log("MainFormFactory resolved, launching Application.Run...");
                    Application.Run(mainFormFactory.Create(startMinimized));
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

