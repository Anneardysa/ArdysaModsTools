/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;

namespace ArdysaModsTools.Installer.Services
{
    public static class ProcessHelper
    {
        public static async Task CloseRunningAppAsync(string processName, CancellationToken ct = default)
        {
            var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

            var processes = Process.GetProcessesByName(name);
            if (processes.Length == 0)
                return;

            foreach (var process in processes)
            {
                try
                {
                    if (process.CloseMainWindow())
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                        try
                        {
                            await process.WaitForExitAsync(timeoutCts.Token);
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            process.Kill();
                            await Task.Delay(500, ct);
                        }
                    }
                    else
                    {
                        process.Kill();
                        await Task.Delay(500, ct);
                    }
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            await Task.Delay(1000, ct);
        }
    }
}
