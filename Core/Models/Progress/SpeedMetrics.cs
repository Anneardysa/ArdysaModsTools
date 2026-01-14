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
namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents real-time performance metrics during operations.
    /// </summary>
    public struct SpeedMetrics
    {
        /// <summary>
        /// Download speed (e.g., "5.2 MB/s")
        /// </summary>
        public string? DownloadSpeed { get; set; }

        /// <summary>
        /// Write speed (e.g., "12.0 MB/s")
        /// </summary>
        public string? WriteSpeed { get; set; }

        /// <summary>
        /// Progress details (e.g., "135 / 399 MB")
        /// </summary>
        public string? ProgressDetails { get; set; }
        
        /// <summary>
        /// Downloaded bytes for progress display.
        /// </summary>
        public long DownloadedBytes { get; set; }
        
        /// <summary>
        /// Total bytes for progress display.
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// Current file index for file count progress (1-based).
        /// </summary>
        public int CurrentFile { get; set; }
        
        /// <summary>
        /// Total files for file count progress.
        /// </summary>
        public int TotalFiles { get; set; }

        public static SpeedMetrics Empty => new SpeedMetrics { DownloadSpeed = "-- MB/S", WriteSpeed = "-- MB/S", ProgressDetails = "" };
    }
}

