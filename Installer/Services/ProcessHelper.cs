/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Handles detection and termination of running application instances.
    /// Required before updating files in the install directory.
    /// </summary>
    public static class ProcessHelper
    {
        /// <summary>
        /// Checks if the application is running and closes it gracefully.
        /// Waits up to 5 seconds for the process to exit before force-killing.
        /// </summary>
        public static async Task CloseRunningAppAsync(string processName, CancellationToken ct = default)
        {
            // Strip .exe extension for Process.GetProcessesByName
            var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

            var processes = Process.GetProcessesByName(name);
            if (processes.Length == 0)
                return;

            foreach (var process in processes)
            {
                try
                {
                    // Try graceful shutdown first
                    process.CloseMainWindow();

                    // Wait up to 5 seconds for graceful exit
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                    try
                    {
                        await process.WaitForExitAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Timeout — force kill
                        process.Kill(entireProcessTree: true);
                        await Task.Delay(500, ct);
                    }
                }
                catch
                {
                    // Process already exited or access denied — skip
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Give OS time to release file handles
            await Task.Delay(1000, ct);
        }
    }
}
