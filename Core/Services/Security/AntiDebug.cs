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
using System.Runtime.InteropServices;

namespace ArdysaModsTools.Core.Services.Security
{
    /// <summary>
    /// Provides anti-debugging functionality to detect and prevent debugging attempts.
    /// Only active in Release builds to allow normal development.
    /// </summary>
    public static class AntiDebug
    {
        #region Win32 Imports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref IntPtr processInformation,
            int processInformationLength,
            ref int returnLength);

        [DllImport("kernel32.dll")]
        private static extern void OutputDebugString(string lpOutputString);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationThread(
            IntPtr threadHandle,
            int threadInformationClass,
            IntPtr threadInformation,
            int threadInformationLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        private const int ThreadHideFromDebugger = 0x11;
        private const int ProcessDebugPort = 0x7;
        private const int ProcessDebugObjectHandle = 0x1E;

        #endregion

        private static System.Threading.Timer? _periodicCheckTimer;
#pragma warning disable CS0169 // Field is used in Release builds only
        private static Action? _onDebuggerDetected;
#pragma warning restore CS0169

        /// <summary>
        /// Performs comprehensive debugger detection.
        /// Returns true if a debugger is detected.
        /// </summary>
        public static bool IsBeingDebugged()
        {
#if DEBUG
            // Disable anti-debug in Debug builds for development
            return false;
#else
            return CheckManagedDebugger() ||
                   CheckNativeDebugger() ||
                   CheckRemoteDebugger() ||
                   CheckDebugPort() ||
                   CheckTimingAnomaly();
#endif
        }

        /// <summary>
        /// Initializes periodic anti-debug checks.
        /// </summary>
        /// <param name="intervalMs">Check interval in milliseconds.</param>
        /// <param name="onDetected">Action to execute when debugger is detected.</param>
        public static void StartPeriodicCheck(int intervalMs = 5000, Action? onDetected = null)
        {
#if DEBUG
            return; // Disabled in debug builds
#else
            _onDebuggerDetected = onDetected ?? (() => Environment.Exit(1));
            
            _periodicCheckTimer = new System.Threading.Timer(_ =>
            {
                if (IsBeingDebugged())
                {
                    _onDebuggerDetected?.Invoke();
                }
            }, null, intervalMs, intervalMs);
#endif
        }

        /// <summary>
        /// Stops periodic anti-debug checks.
        /// </summary>
        public static void StopPeriodicCheck()
        {
            _periodicCheckTimer?.Dispose();
            _periodicCheckTimer = null;
        }

        /// <summary>
        /// Hides the current thread from debuggers.
        /// </summary>
        public static void HideThreadFromDebugger()
        {
#if !DEBUG
            try
            {
                NtSetInformationThread(GetCurrentThread(), ThreadHideFromDebugger, IntPtr.Zero, 0);
            }
            catch
            {
                // Ignore - might fail on some systems
            }
#endif
        }

        #region Detection Methods

        /// <summary>
        /// Checks for .NET managed debugger.
        /// </summary>
        private static bool CheckManagedDebugger()
        {
            return Debugger.IsAttached || Debugger.IsLogging();
        }

        /// <summary>
        /// Checks for native debugger using Win32 API.
        /// </summary>
        private static bool CheckNativeDebugger()
        {
            try
            {
                return IsDebuggerPresent();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks for remote debugger attachment.
        /// </summary>
        private static bool CheckRemoteDebugger()
        {
            try
            {
                bool isDebuggerPresent = false;
                CheckRemoteDebuggerPresent(GetCurrentProcess(), ref isDebuggerPresent);
                return isDebuggerPresent;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks the debug port via NtQueryInformationProcess.
        /// </summary>
        private static bool CheckDebugPort()
        {
            try
            {
                IntPtr debugPort = IntPtr.Zero;
                int returnLength = 0;
                int status = NtQueryInformationProcess(
                    GetCurrentProcess(),
                    ProcessDebugPort,
                    ref debugPort,
                    IntPtr.Size,
                    ref returnLength);

                return status == 0 && debugPort != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects single-stepping by measuring execution time.
        /// </summary>
        private static bool CheckTimingAnomaly()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                
                // Perform simple operations that should be very fast
                int dummy = 0;
                for (int i = 0; i < 1000; i++)
                {
                    dummy += i;
                }
                
                sw.Stop();
                
                // If these simple operations take more than 100ms, likely being single-stepped
                return sw.ElapsedMilliseconds > 100;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Detection of Common Tools

        /// <summary>
        /// Checks for common debugging and reverse engineering tools.
        /// </summary>
        public static bool CheckForDebugTools()
        {
#if DEBUG
            return false;
#else
            string[] debuggerProcesses = new[]
            {
                "dnspy", "x64dbg", "x32dbg", "ollydbg", "ida", "ida64",
                "idaq", "idaq64", "windbg", "immunitydebugger", "cheatengine",
                "processhacker", "fiddler", "wireshark", "httpanalyzer"
            };

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        string name = process.ProcessName.ToLowerInvariant();
                        foreach (var debugger in debuggerProcesses)
                        {
                            if (name.Contains(debugger))
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Access denied for some processes
                    }
                }
            }
            catch
            {
                // Ignore enumeration errors
            }

            return false;
#endif
        }

        #endregion
    }
}

