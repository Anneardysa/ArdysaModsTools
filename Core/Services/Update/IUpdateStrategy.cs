using System;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Strategy interface for applying updates based on installation type.
    /// </summary>
    public interface IUpdateStrategy
    {
        /// <summary>
        /// Gets the installation type this strategy handles.
        /// </summary>
        InstallationType InstallationType { get; }

        /// <summary>
        /// Gets the filename pattern to match in GitHub release assets.
        /// </summary>
        string AssetPattern { get; }

        /// <summary>
        /// Whether this update strategy requires administrator privileges.
        /// </summary>
        bool RequiresAdminRights { get; }

        /// <summary>
        /// Applies the downloaded update file.
        /// </summary>
        /// <param name="downloadedFilePath">Path to the downloaded update file (exe or zip).</param>
        /// <param name="onProgress">Optional progress callback (0-100).</param>
        /// <param name="onStatusChanged">Optional status message callback.</param>
        /// <returns>Result of the update operation.</returns>
        Task<UpdateResult> ApplyUpdateAsync(
            string downloadedFilePath,
            Action<int>? onProgress = null,
            Action<string>? onStatusChanged = null);

        /// <summary>
        /// Validates that the update can be applied (permissions, disk space, etc.).
        /// </summary>
        /// <returns>Null if valid, or an error message if not.</returns>
        string? ValidateCanUpdate();
    }
}
