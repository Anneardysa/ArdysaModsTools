/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;
using System.IO;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Progress data for the uninstall pipeline.
    /// </summary>
    public record UninstallProgress(int Percent, string Status);

    /// <summary>
    /// Orchestrates the complete uninstallation pipeline:
    ///   1. Close running application instances
    ///   2. Remove Start Menu and Desktop shortcuts
    ///   3. Unregister from Windows Add/Remove Programs
    ///   4. Delete application files from the install directory
    ///
    /// Self-deletion of the uninstaller exe and install folder is handled
    /// separately via <see cref="ScheduleSelfDeletion"/> — called by the
    /// caller AFTER the process is about to exit (not during the pipeline,
    /// because the exe is still locked while the UI is showing).
    ///
    /// Supports both interactive (UI) and silent modes.
    /// Silent mode is used by Windows ARP and third-party tools (Revo Uninstaller).
    /// </summary>
    public sealed class UninstallService
    {
        private const string AppExeName = "ArdysaModsTools.exe";
        private const string UninstallerExeName = "unins000.exe";

        /// <summary>
        /// Runs the uninstall pipeline with progress reporting.
        /// Used by the interactive UI mode.
        ///
        /// IMPORTANT: Does NOT schedule self-deletion. The caller must call
        /// <see cref="ScheduleSelfDeletion"/> when the window is closing,
        /// so the exe file is no longer locked.
        /// </summary>
        public async Task RunUninstallAsync(
            string installPath,
            IProgress<UninstallProgress> progress,
            CancellationToken ct = default)
        {
            // Step 1: Close running app
            progress.Report(new UninstallProgress(5, "Closing running instances..."));
            await ProcessHelper.CloseRunningAppAsync(AppExeName, ct);

            // Step 2: Remove shortcuts
            progress.Report(new UninstallProgress(25, "Removing shortcuts..."));
            ShortcutHelper.RemoveShortcuts();

            // Step 3: Unregister from Windows
            progress.Report(new UninstallProgress(50, "Removing registry entries..."));
            RegistryHelper.UnregisterApp();

            // Step 4: Delete application files (except self — still running)
            progress.Report(new UninstallProgress(75, "Removing application files..."));
            await Task.Run(() => DeleteInstallDirectory(installPath), ct);

            progress.Report(new UninstallProgress(100, "Uninstall complete"));
            // Self-deletion is NOT called here — the caller handles it on exit.
        }

        /// <summary>
        /// Runs the full uninstall pipeline silently (no UI).
        /// Used by Windows Add/Remove Programs and third-party uninstallers.
        /// Returns true on success.
        ///
        /// Unlike the interactive mode, silent mode DOES schedule self-deletion
        /// because the process exits immediately after this method returns.
        /// </summary>
        public async Task<bool> RunSilentUninstallAsync(string installPath)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                await ProcessHelper.CloseRunningAppAsync(AppExeName, cts.Token);
                ShortcutHelper.RemoveShortcuts();
                RegistryHelper.UnregisterApp();
                await Task.Run(() => DeleteInstallDirectory(installPath), cts.Token);

                // Safe to schedule here — process exits immediately after
                ScheduleSelfDeletion(installPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the full path to the uninstaller executable
        /// in the install directory.
        /// </summary>
        public static string GetUninstallerPath(string installPath)
        {
            return Path.Combine(installPath, UninstallerExeName);
        }

        /// <summary>
        /// Schedules deletion of the uninstaller executable and install
        /// directory after this process exits.
        ///
        /// Uses cmd.exe with a process-wait loop: polls every 1 second
        /// until PID is gone, then force-deletes the exe and the directory.
        /// This is reliable regardless of how long the process takes to exit.
        ///
        /// Must be called just before the process exits (e.g., on Window.Closing
        /// or App.OnExit), NOT during the uninstall pipeline.
        /// </summary>
        public static void ScheduleSelfDeletion(string installPath)
        {
            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe)) return;

                var pid = Environment.ProcessId;

                // Build a cmd script that:
                //   1. Waits in a loop until this process (PID) exits
                //   2. Deletes the uninstaller exe
                //   3. Removes the entire install directory
                //
                // The :wait loop polls every 1 second using tasklist.
                // Once the PID is gone, it proceeds with deletion.
                var cmd =
                    $"/c " +
                    $"\"" +
                    $":wait & " +
                    $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && " +
                    $"(ping 127.0.0.1 -n 2 >nul & goto :wait) & " +
                    $"del /f /q \"{currentExe}\" & " +
                    $"rmdir /s /q \"{installPath}\"" +
                    $"\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch
            {
                // Best effort — user can manually delete the folder
            }
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Deletes all files in the install directory, skipping the
        /// uninstaller itself (since it's currently running). The
        /// uninstaller is cleaned up via <see cref="ScheduleSelfDeletion"/>.
        /// </summary>
        private static void DeleteInstallDirectory(string installPath)
        {
            if (!Directory.Exists(installPath)) return;

            var currentExe = Environment.ProcessPath ?? "";

            // Delete all files except the running uninstaller
            foreach (var file in Directory.GetFiles(
                installPath, "*", SearchOption.AllDirectories))
            {
                // Skip the currently running exe (self-deleted later)
                if (file.Equals(currentExe, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    // File locked or access denied — skip
                }
            }

            // Remove empty subdirectories (deepest first)
            try
            {
                foreach (var dir in Directory.GetDirectories(
                    installPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
            catch { /* best effort */ }
        }
    }
}
