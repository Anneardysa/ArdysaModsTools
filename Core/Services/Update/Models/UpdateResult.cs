namespace ArdysaModsTools.Core.Services.Update.Models
{
    /// <summary>
    /// Result of an update operation.
    /// </summary>
    public class UpdateResult
    {
        /// <summary>
        /// Whether the update was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the update failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Whether the application needs to restart to complete the update.
        /// </summary>
        public bool RequiresRestart { get; set; }

        /// <summary>
        /// Path to the downloaded update file (for reference).
        /// </summary>
        public string? DownloadedFilePath { get; set; }

        public static UpdateResult Succeeded(bool requiresRestart = true) => new()
        {
            Success = true,
            RequiresRestart = requiresRestart
        };

        public static UpdateResult Failed(string error) => new()
        {
            Success = false,
            ErrorMessage = error
        };
    }
}
