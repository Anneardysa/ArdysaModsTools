using System;
using System.Threading;
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Core.Services.Security
{
    /// <summary>
    /// Security manager that coordinates all security features.
    /// Initialize once at application startup.
    /// </summary>
    public static class SecurityManager
    {
        private static bool _initialized;
        private static readonly object _lock = new();

        /// <summary>
        /// Initializes security features. Call this at application startup.
        /// Returns false if security checks fail (debugger detected, tampering, etc.)
        /// </summary>
        /// <param name="exitOnFailure">If true, calls Environment.Exit(1) on security failure.</param>
        /// <returns>True if all security checks pass.</returns>
        public static bool Initialize(bool exitOnFailure = true)
        {
            lock (_lock)
            {
                if (_initialized) return true;

#if DEBUG
                // In debug mode, skip all security checks
                _initialized = true;
                return true;
#else
                // Check for debugger
                if (AntiDebug.IsBeingDebugged())
                {
                    if (exitOnFailure)
                    {
                        // Delay slightly to make debugging harder
                        Thread.Sleep(100);
                        Environment.Exit(1);
                    }
                    return false;
                }

                // Check for debugging tools
                if (AntiDebug.CheckForDebugTools())
                {
                    if (exitOnFailure)
                    {
                        Thread.Sleep(100);
                        Environment.Exit(1);
                    }
                    return false;
                }

                // Verify assembly integrity
                if (!IntegrityCheck.VerifyAssembly())
                {
                    if (exitOnFailure)
                    {
                        Thread.Sleep(100);
                        Environment.Exit(1);
                    }
                    return false;
                }

                // Hide main thread from debuggers
                AntiDebug.HideThreadFromDebugger();

                // Start periodic checks (every 10 seconds)
                AntiDebug.StartPeriodicCheck(10000, () =>
                {
                    // On detection, exit quietly
                    Environment.Exit(1);
                });

                _initialized = true;
                return true;
#endif
            }
        }

        /// <summary>
        /// Shuts down security features (call on application exit).
        /// </summary>
        public static void Shutdown()
        {
            AntiDebug.StopPeriodicCheck();
        }

        /// <summary>
        /// Performs an on-demand security check.
        /// Useful before sensitive operations.
        /// </summary>
        /// <returns>True if environment is clean.</returns>
        public static bool PerformCheck()
        {
#if DEBUG
            return true;
#else
            return !AntiDebug.IsBeingDebugged() && 
                   !AntiDebug.CheckForDebugTools();
#endif
        }
    }
}
