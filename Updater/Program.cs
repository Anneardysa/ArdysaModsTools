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
using System.Diagnostics;

namespace ArdysaModsTools.Updater
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string? stagingDir = GetArg(args, "--staging");
            int pid = int.TryParse(GetArg(args, "--pid"), out int p) ? p : 0;
            bool dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
                return 2;

            string logPath = Path.Combine(stagingDir, "update.log");
            void Log(string message)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); }
                catch {  }
            }

            Log($"AMT.Updater started (pid={pid}, dryRun={dryRun}).");

            ApplyResult result;
            try
            {
                result = ApplyEngine.Run(stagingDir, pid, Log, dryRun);
            }
            catch (Exception ex)
            {
                Log($"Unexpected failure: {ex}");
                result = new ApplyResult(false, ex.Message, null);
            }

            Log(result.Success ? $"OK: {result.Message}" : $"FAILED: {result.Message}");

            if (dryRun)
                return result.Success ? 0 : 1;

            Relaunch(result.RelaunchPath, Log);

            if (result.Success)
                CleanStaging(stagingDir, Log);

            return result.Success ? 0 : 1;
        }

        private static void Relaunch(string? exePath, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                log("Not relaunching — the app is still running.");
                return;
            }

            if (!File.Exists(exePath))
            {
                log($"Cannot relaunch — the app executable is missing: {exePath}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath)!,
                    UseShellExecute = true
                });
                log($"Relaunched {exePath}");
            }
            catch (Exception ex)
            {
                log($"Could not relaunch the app: {ex.Message}");
            }
        }

        private static void CleanStaging(string stagingDir, Action<string> log)
        {
            try
            {
                string self = Environment.ProcessPath ?? "";

                foreach (var file in Directory.EnumerateFiles(stagingDir, "*", SearchOption.AllDirectories))
                {
                    if (file.Equals(self, StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith("update.log", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try { File.Delete(file); } catch {  }
                }
            }
            catch (Exception ex)
            {
                log($"Staging cleanup skipped: {ex.Message}");
            }
        }

        private static string? GetArg(string[] args, string name)
        {
            int index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
