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

