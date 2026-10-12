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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Core.Services.App
{
    public static class GameSessionWatcher
    {
        public const string WaitArgument = "--wait-dota";

        public const string ResumedArgument = "--resumed";

        private const string MinimizedArgument = "--minimized";

        private const string GameProcessName = "dota2";

        private const string StubMutexName = @"Global\ArdysaModsTools_GameSessionWatcher_Mutex";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

        public static bool IsWaitLaunch(string[]? args) => HasArgument(args, WaitArgument);

        public static bool IsResumedLaunch(string[]? args) => HasArgument(args, ResumedArgument);

        internal static bool IsMinimizedLaunch(string[]? args) => HasArgument(args, MinimizedArgument);

        internal static string WaitArgumentsFor(bool minimized) =>
            minimized ? $"{WaitArgument} {MinimizedArgument}" : WaitArgument;

        public static bool StartStub(bool minimized)
        {
            try
            {
                return Spawn(WaitArgumentsFor(minimized));
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[GameSessionWatcher] Could not start the stub: {ex.Message}");
                return false;
            }
        }

        private static bool HasArgument(string[]? args, string flag)
        {
            if (args == null) return false;
            foreach (var a in args)
            {
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void WaitThenRelaunch()
        {
            Mutex? only = null;
            try
            {
                only = new Mutex(true, StubMutexName, out bool createdNew);
                if (!createdNew)
                {
                    FallbackLogger.LogFileOnly("[GameSessionWatcher] Another stub is already waiting — exiting.");
                    return;
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[GameSessionWatcher] Stub mutex unavailable: {ex.Message}");
            }

            ActiveProcessSentry? sentry = null;
            var lockedPackages = new List<string>();
            try
            {
                ProcessProtectionGuard.ProtectCurrentProcess();

                string? dotaPath = ResolveDotaPath(new MainConfigService(), new DetectionService());

                if (!string.IsNullOrEmpty(dotaPath))
                {
                    sentry = new ActiveProcessSentry(threatName =>
                    {
                        ActiveProcessSentry.KillRunningThreats();
                        return true;
                    });
                    sentry.Start(pollIntervalMs: 350);

                    LockMountedPackages(dotaPath, lockedPackages);
                }

                WaitUntilGoneAsync(GameIsRunning, PollInterval, CancellationToken.None)
                    .GetAwaiter().GetResult();

                try { sentry?.Stop(); } catch { }

                foreach (var pkg in lockedPackages)
                    try { FileSecurityGuard.ReleaseDaclLock(pkg); } catch { }
                lockedPackages.Clear();

                ProcessProtectionGuard.UnprotectCurrentProcess();

                string args = IsMinimizedLaunch(Environment.GetCommandLineArgs())
                    ? $"{ResumedArgument} {MinimizedArgument}"
                    : ResumedArgument;

                if (!Spawn(args))
                    FallbackLogger.LogFileOnly("[GameSessionWatcher] No executable path — cannot relaunch.");
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[GameSessionWatcher] Relaunch failed: {ex.Message}");
            }
            finally
            {
                foreach (var pkg in lockedPackages)
                    try { FileSecurityGuard.ReleaseDaclLock(pkg); } catch { }

                ProcessProtectionGuard.UnprotectCurrentProcess();
                sentry?.Dispose();
                try { only?.Dispose(); } catch {  }
            }
        }

        internal static string? ResolveDotaPath(IConfigService config, IDetectionService detection)
        {
            try
            {
                string? saved = config.GetLastTargetPath();
                if (detection.ValidateDotaPath(saved))
                    return saved;
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[GameSessionWatcher] Could not read the saved path: {ex.Message}");
            }

            try { return detection.AutoDetectAsync().GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"[GameSessionWatcher] Auto-detect failed: {ex.Message}");
                return null;
            }
        }

        private static void LockMountedPackages(string dotaPath, List<string> locked)
        {
            var packages = new[]
            {
                Path.Combine(dotaPath, DotaPaths.ModsVpk.Replace('/', Path.DirectorySeparatorChar)),
                ProtectedVpkStore.VpkPath(dotaPath),
            };

            foreach (var pkg in packages)
            {
                try
                {
                    if (!File.Exists(pkg)) continue;
                    if (!FileSecurityGuard.WaitUntilHeldByAnotherProcess(
                            pkg, HandleWaitTimeout, HandleWaitPoll, CancellationToken.None))
                        continue;
                    if (FileSecurityGuard.ApplyRuntimeDaclLock(pkg))
                        locked.Add(pkg);
                }
                catch (Exception ex)
                {
                    FallbackLogger.LogFileOnly($"[GameSessionWatcher] Could not lock '{pkg}': {ex.Message}");
                }
            }
        }

        private static readonly TimeSpan HandleWaitTimeout = TimeSpan.FromMinutes(2);

        private static readonly TimeSpan HandleWaitPoll = TimeSpan.FromSeconds(1);

        private static bool Spawn(string args)
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            using var started = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            });

            return started != null;
        }

        internal static async Task WaitUntilGoneAsync(Func<bool> isRunning, TimeSpan poll,
            CancellationToken ct, int settleChecks = 2)
        {
            if (settleChecks < 1) settleChecks = 1;
            int clean = 0;

            while (clean < settleChecks)
            {
                clean = isRunning() ? 0 : clean + 1;
                if (clean < settleChecks)
                    await Task.Delay(poll, ct).ConfigureAwait(false);
            }
        }

        private static bool GameIsRunning()
        {
            var processes = Process.GetProcessesByName(GameProcessName);
            foreach (var p in processes) p.Dispose();
            return processes.Length > 0;
        }
    }
}
