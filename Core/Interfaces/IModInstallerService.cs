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
        /// Update patcher (signatures and gameinfo).
        /// </summary>
        Task<PatchResult> UpdatePatcherAsync(
            string dotaPath,
            Action<string>? statusCallback = null,
            CancellationToken ct = default);
    }
}

