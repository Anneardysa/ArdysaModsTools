using System;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Utility class for consistent speed calculation and formatting.
    /// </summary>
    public static class SpeedCalculator
    {
        /// <summary>
        /// Formats speed given bytes and elapsed seconds.
        /// </summary>
        public static string FormatSpeed(long bytes, double seconds)
        {
            if (seconds <= 0) return "0.0 MB/S";
            
            double mb = bytes / 1024.0 / 1024.0;
            double speed = mb / seconds;
            
            return $"{speed:F1} MB/S";
        }

        /// <summary>
        /// Calculates speed in MB/S.
        /// </summary>
        public static double CalculateMbPerSecond(long bytes, double seconds)
        {
            if (seconds <= 0) return 0;
            return (bytes / 1024.0 / 1024.0) / seconds;
        }
    }
}
