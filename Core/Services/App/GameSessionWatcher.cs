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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;

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
                FallbackLogger.Log($"[GameSessionWatcher] Could not start the stub: {ex.Message}");
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
                    FallbackLogger.Log("[GameSessionWatcher] Another stub is already waiting — exiting.");
                    return;
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[GameSessionWatcher] Stub mutex unavailable: {ex.Message}");
            }

            try
            {
                WaitUntilGoneAsync(GameIsRunning, PollInterval, CancellationToken.None)
                    .GetAwaiter().GetResult();

                string args = IsMinimizedLaunch(Environment.GetCommandLineArgs())
                    ? $"{ResumedArgument} {MinimizedArgument}"
                    : ResumedArgument;

                if (!Spawn(args))
                    FallbackLogger.Log("[GameSessionWatcher] No executable path — cannot relaunch.");
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[GameSessionWatcher] Relaunch failed: {ex.Message}");
            }
            finally
            {
                try { only?.Dispose(); } catch {  }
            }
        }

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
