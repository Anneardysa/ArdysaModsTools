using System;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for mod installation operations.
    /// Handles VPK validation, mod installation, patching, and removal.
    /// </summary>
    public interface IModInstallerService
    {
        /// <summary>
        /// Validate VPK file contains the required version/_ArdysaMods file.
        /// </summary>
        Task<(bool IsValid, string ErrorMessage)> ValidateVpkAsync(string vpkPath, CancellationToken ct = default);

        /// <summary>
        /// Check if required mod file is present in the Dota 2 directory.
        /// </summary>
        bool IsRequiredModFilePresent(string dotaPath);

        /// <summary>
        /// Check if a newer ModsPack is available.
        /// </summary>
        Task<(bool hasNewer, bool hasLocalInstall)> CheckForNewerModsPackAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Disable mods by removing mod files and restoring original gameinfo.
        /// </summary>
        Task<bool> DisableModsAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Install ModsPack.
        /// Returns (Success, IsUpToDate).
        /// </summary>
        Task<(bool Success, bool IsUpToDate)> InstallModsAsync(
            string targetPath,
            string appPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            bool force = false,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action<string>? statusCallback = null);

        /// <summary>
        /// Manual install: Copy user-provided VPK to _ArdysaMods/pak01_dir.vpk and patch gameinfo/signatures.
        /// </summary>
        Task<bool> ManualInstallModsAsync(
            string targetPath,
            string vpkFilePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);

        /// <summary>
        /// Update patcher (signatures and optionally gameinfo).
        /// </summary>
        Task<PatchResult> UpdatePatcherAsync(
            string dotaPath,
            PatchMode mode = PatchMode.Full,
            Action<string>? statusCallback = null,
            CancellationToken ct = default);
    }
}
