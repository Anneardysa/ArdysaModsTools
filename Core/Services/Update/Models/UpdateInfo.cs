namespace ArdysaModsTools.Core.Services.Update.Models
{
    /// <summary>
    /// Contains information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// The version string of the latest release (e.g., "2.1.0").
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Download URL for the installer version (*_Setup_*.exe).
        /// </summary>
        public string? InstallerDownloadUrl { get; set; }

        /// <summary>
        /// Download URL for the portable version (*_Portable_*.zip).
        /// </summary>
        public string? PortableDownloadUrl { get; set; }

        /// <summary>
        /// Release notes / changelog from GitHub.
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// Whether an update is available compared to current version.
        /// </summary>
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// The current application version for reference.
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;
    }
}
