using System;
using System.Diagnostics;
using System.Linq;

namespace ArdysaModsTools.Helpers
{
    public static class ProcessChecker
    {
        /// <summary>
        /// Returns true if any process with the given executable name (without extension)
        /// is running. Example: "dota2" to detect "dota2.exe".
        /// </summary>
        public static bool IsProcessRunning(string exeNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(exeNameWithoutExtension))
                throw new ArgumentException("Process name required", nameof(exeNameWithoutExtension));

            var name = exeNameWithoutExtension.Trim();
            try
            {
                var procs = Process.GetProcesses();
                return procs.Any(p =>
                {
                    try
                    {
                        return string.Equals(p.ProcessName?.Trim(), name, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                // Conservative behavior retained, but log the reason.
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"ProcessChecker.IsProcessRunning failed for '{name}': {ex.Message}");
                return true; // keep conservative default
            }
        }
    }
}
