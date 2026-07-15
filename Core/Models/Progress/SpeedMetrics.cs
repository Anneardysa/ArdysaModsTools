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
    public struct SpeedMetrics
    {
        public string? DownloadSpeed { get; set; }

        public string? WriteSpeed { get; set; }

        public string? ProgressDetails { get; set; }
        
        public long DownloadedBytes { get; set; }
        
        public long TotalBytes { get; set; }
        
        public int CurrentFile { get; set; }
        
        public int TotalFiles { get; set; }

        public static SpeedMetrics Empty => new SpeedMetrics { DownloadSpeed = "-- MB/S", WriteSpeed = "-- MB/S", ProgressDetails = "" };
    }
}

