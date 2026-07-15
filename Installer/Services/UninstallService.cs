/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;
using System.IO;

namespace ArdysaModsTools.Installer.Services
{
    public record UninstallProgress(int Percent, string Status);

    public sealed class UninstallService
    {
        private const string AppExeName = "ArdysaModsTools.exe";
        private const string UninstallerExeName = "unins000.exe";

        public async Task RunUninstallAsync(
            string installPath,
            IProgress<UninstallProgress> progress,
            CancellationToken ct = default)
        {
            progress.Report(new UninstallProgress(5, "Closing running instances..."));
            await ProcessHelper.CloseRunningAppAsync(AppExeName, ct);

            progress.Report(new UninstallProgress(25, "Removing shortcuts..."));
            ShortcutHelper.RemoveShortcuts();

            progress.Report(new UninstallProgress(50, "Removing registry entries..."));
            RegistryHelper.UnregisterApp();

            progress.Report(new UninstallProgress(75, "Removing application files..."));
            await Task.Run(() => DeleteInstallDirectory(installPath), ct);

            progress.Report(new UninstallProgress(100, "Uninstall complete"));
        }

        public async Task<bool> RunSilentUninstallAsync(string installPath)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                await ProcessHelper.CloseRunningAppAsync(AppExeName, cts.Token);
                ShortcutHelper.RemoveShortcuts();
                RegistryHelper.UnregisterApp();
                await Task.Run(() => DeleteInstallDirectory(installPath), cts.Token);

                ScheduleSelfDeletion(installPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetUninstallerPath(string installPath)
        {
            return Path.Combine(installPath, UninstallerExeName);
        }

        public static void ScheduleSelfDeletion(string installPath)
        {
            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe)) return;

                if (IsDangerousDeleteTarget(installPath)) return;

                var pid = Environment.ProcessId;

                var script =
                    "@echo off\r\n" +
                    ":wait\r\n" +
                    $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
                    "if not errorlevel 1 (\r\n" +
                    "  ping 127.0.0.1 -n 2 >nul\r\n" +
                    "  goto wait\r\n" +
                    ")\r\n" +
                    $"del /f /q \"{currentExe}\"\r\n" +
                    $"rmdir /s /q \"{installPath}\"\r\n" +
                    "del /f /q \"%~f0\"\r\n";

                var scriptPath = Path.Combine(
                    Path.GetTempPath(), $"ArdysaModsTools_Cleanup_{pid}.bat");
                File.WriteAllText(scriptPath, script);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch
            {
            }
        }

        internal static bool IsDangerousDeleteTarget(string path)
        {
            try
            {
                var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

                var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                    return true;

                Environment.SpecialFolder[] protectedFolders =
                [
                    Environment.SpecialFolder.UserProfile,
                    Environment.SpecialFolder.DesktopDirectory,
                    Environment.SpecialFolder.MyDocuments,
                    Environment.SpecialFolder.MyPictures,
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolder.ApplicationData,
                    Environment.SpecialFolder.ProgramFiles,
                    Environment.SpecialFolder.ProgramFilesX86,
                    Environment.SpecialFolder.CommonApplicationData,
                    Environment.SpecialFolder.Windows,
                ];

                foreach (var folder in protectedFolders)
                {
                    var special = Environment.GetFolderPath(folder);
                    if (!string.IsNullOrEmpty(special) && string.Equals(
                            full,
                            special.TrimEnd(Path.DirectorySeparatorChar),
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }


        private static void DeleteInstallDirectory(string installPath)
        {
            if (!Directory.Exists(installPath)) return;
            if (IsDangerousDeleteTarget(installPath)) return;

            var currentExe = Environment.ProcessPath ?? "";

            foreach (var file in Directory.GetFiles(
                installPath, "*", SearchOption.AllDirectories))
            {
                if (file.Equals(currentExe, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                }
            }

            try
            {
                foreach (var dir in Directory.GetDirectories(
                    installPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
            catch {  }
        }
    }
}
