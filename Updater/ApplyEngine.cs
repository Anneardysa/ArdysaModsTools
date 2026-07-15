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
using System.Security.Cryptography;
using System.Text.Json;

namespace ArdysaModsTools.Updater
{
    public sealed record ApplyResult(bool Success, string Message, string? RelaunchPath);

    public static class ApplyEngine
    {
        public const string ExeName = "ArdysaModsTools.exe";
        public const string MarkerName = ".amt-update-in-progress";
        public const string BackupExtension = ".amtbak";
        public const string IncomingExtension = ".amtnew";
        public const string StagedOkMarker = ".staged-ok";
        public const string PlanFileName = "apply.json";

        private const int MaxAttempts = 5;
        private const int BaseDelayMs = 200;

        public static ApplyResult Run(
            string stagingDir, int waitPid, Action<string> log, bool dryRun = false, int waitTimeoutMs = 60_000)
        {
            ApplyPlan plan;
            try
            {
                plan = LoadPlan(stagingDir);
            }
            catch (Exception ex)
            {
                return new ApplyResult(false, $"Invalid update plan: {ex.Message}", null);
            }

            string targetDir = Path.GetFullPath(plan.TargetDir);
            string relaunch = Path.Combine(targetDir, ExeName);

            if (!File.Exists(Path.Combine(stagingDir, StagedOkMarker)))
                return new ApplyResult(false, "Staging is incomplete (.staged-ok missing).", relaunch);

            if (!Directory.Exists(targetDir))
                return new ApplyResult(false, $"Install folder not found: {targetDir}", null);

            if (!File.Exists(relaunch))
                return new ApplyResult(false, $"Not an AMT install folder: {targetDir}", null);

            if (plan.Files.Length == 0)
                return new ApplyResult(false, "Update plan contains no files.", relaunch);

            string filesRoot = Path.Combine(stagingDir, "files");
            foreach (var file in plan.Files)
            {
                if (!IsSafeRelPath(file.RelPath))
                    return new ApplyResult(false, $"Rejected unsafe path in plan: {file.RelPath}", relaunch);

                string staged = ResolveUnder(filesRoot, file.RelPath);
                if (!VerifyStaged(staged, file, out string why))
                    return new ApplyResult(false, $"Staged file failed verification ({file.RelPath}): {why}", relaunch);
            }

            foreach (var relPath in plan.Deletions)
            {
                if (!IsSafeRelPath(relPath))
                    return new ApplyResult(false, $"Rejected unsafe deletion path: {relPath}", relaunch);
            }

            log($"Verified {plan.Files.Length} staged file(s) for v{plan.Version}.");

            if (dryRun)
                return new ApplyResult(true, "Dry run: staging is valid.", relaunch);

            if (waitPid > 0 && !WaitForExit(waitPid, waitTimeoutMs, log))
                return new ApplyResult(false, "The application is still running — update aborted.", null);

            return Swap(plan, targetDir, filesRoot, relaunch, log);
        }

        #region Swap + rollback

        private static ApplyResult Swap(ApplyPlan plan, string targetDir, string filesRoot, string relaunch, Action<string> log)
        {
            string marker = Path.Combine(targetDir, MarkerName);
            var applied = new List<(string Target, string Backup)>();
            var incoming = new List<string>();

            try
            {
                File.WriteAllText(marker, plan.Version);

                foreach (var file in plan.Files)
                {
                    string staged = ResolveUnder(filesRoot, file.RelPath);
                    string target = ResolveUnder(targetDir, file.RelPath);
                    string backup = target + BackupExtension;
                    string incomingPath = target + IncomingExtension;

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    Retry(() =>
                    {
                        if (File.Exists(incomingPath)) File.Delete(incomingPath);
                        File.Copy(staged, incomingPath);
                    }, $"copy {file.RelPath}");
                    incoming.Add(incomingPath);

                    if (File.Exists(target))
                    {
                        Retry(() =>
                        {
                            if (File.Exists(backup)) File.Delete(backup);
                            File.Move(target, backup);
                        }, $"back up {file.RelPath}");
                        applied.Add((target, backup));
                    }
                    else
                    {
                        applied.Add((target, ""));
                    }

                    Retry(() => File.Move(incomingPath, target), $"install {file.RelPath}");
                    incoming.Remove(incomingPath);
                }

                foreach (var relPath in plan.Deletions)
                {
                    string target = ResolveUnder(targetDir, relPath);
                    if (!File.Exists(target)) continue;

                    string backup = target + BackupExtension;
                    Retry(() =>
                    {
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(target, backup);
                    }, $"remove {relPath}");
                    applied.Add((target, backup));
                }

                foreach (var (_, backup) in applied)
                {
                    if (backup.Length == 0) continue;
                    try { File.Delete(backup); } catch (Exception ex) { log($"Could not remove backup {Path.GetFileName(backup)}: {ex.Message} — the app will clean it up on its next start."); }
                }

                SafeDelete(marker);
                log($"Update applied: {plan.Files.Length} file(s) replaced, {plan.Deletions.Length} removed.");
                return new ApplyResult(true, $"Updated to v{plan.Version}.", relaunch);
            }
            catch (Exception ex)
            {
                log($"Apply failed: {ex.Message} — rolling back.");
                Rollback(applied, incoming, log);
                SafeDelete(marker);
                return new ApplyResult(false, $"Update failed and was rolled back: {ex.Message}", relaunch);
            }
        }

        private static void Rollback(List<(string Target, string Backup)> applied, List<string> incoming, Action<string> log)
        {
            foreach (var path in incoming)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch {  }
            }

            for (int i = applied.Count - 1; i >= 0; i--)
            {
                var (target, backup) = applied[i];
                try
                {
                    if (backup.Length == 0)
                    {
                        if (File.Exists(target)) File.Delete(target);
                        continue;
                    }

                    if (!File.Exists(backup)) continue;
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(backup, target);
                }
                catch (Exception ex)
                {
                    log($"Rollback could not restore {Path.GetFileName(target)}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Validation helpers

        private static ApplyPlan LoadPlan(string stagingDir)
        {
            string path = Path.Combine(stagingDir, PlanFileName);
            string json = File.ReadAllText(path);

            var plan = JsonSerializer.Deserialize(json, ApplyJsonContext.Default.ApplyPlan)
                ?? throw new InvalidDataException("apply.json is empty.");

            if (string.IsNullOrWhiteSpace(plan.TargetDir))
                throw new InvalidDataException("apply.json has no target directory.");

            return plan;
        }

        private static bool VerifyStaged(string path, ApplyFile expected, out string reason)
        {
            if (!File.Exists(path)) { reason = "missing"; return false; }

            var info = new FileInfo(path);
            if (expected.Size > 0 && info.Length != expected.Size) { reason = "size mismatch"; return false; }

            using var stream = File.OpenRead(path);
            string actual = Convert.ToHexString(SHA256.HashData(stream));

            if (!actual.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = "hash mismatch";
                return false;
            }

            reason = "";
            return true;
        }

        private static bool IsSafeRelPath(string relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return false;
            if (Path.IsPathRooted(relPath)) return false;
            if (relPath.Contains("..", StringComparison.Ordinal)) return false;
            if (relPath.Contains(':')) return false;
            return true;
        }

        private static string ResolveUnder(string root, string relPath)
        {
            string full = Path.GetFullPath(Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar)));
            string rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Path escapes the target folder: {relPath}");

            return full;
        }

        private static bool WaitForExit(int pid, int timeoutMs, Action<string> log)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                log($"Waiting for the application (PID {pid}) to exit...");
                return process.WaitForExit(timeoutMs);
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static void Retry(Action action, string what)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception) when (attempt < MaxAttempts)
                {
                    Thread.Sleep(BaseDelayMs * (int)Math.Pow(2, attempt - 1));
                }
                catch (Exception ex)
                {
                    throw new IOException($"Could not {what}: {ex.Message}", ex);
                }
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch {  }
        }

        #endregion
    }
}
