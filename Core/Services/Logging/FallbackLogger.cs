using System;
using System.IO;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Minimal logger for static helpers to write to file fallback and optionally to an injected UI logger.
    /// </summary>
    public static class FallbackLogger
    {
        private static string _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ardysa_fallback.log");

        /// <summary>
        /// Optional hook for UI logger. Set this from MainForm during startup:
        /// FallbackLogger.UserLogger = msg => myLogger.Log(msg);
        /// </summary>
        public static Action<string>? UserLogger { get; set; }

        public static void Log(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                // write to UI logger if available
                try { UserLogger?.Invoke(message); } catch { /* don't block on UI logger */ }

                // append to fallback file
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
                // best-effort only
            }
        }

        public static void SetLogFile(string path)
        {
            try { _logFile = path; } catch { }
        }
    }
}
