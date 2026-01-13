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
