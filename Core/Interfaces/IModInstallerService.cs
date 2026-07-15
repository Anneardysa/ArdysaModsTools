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
    public interface IModInstallerService
    {
        Task<(bool IsValid, string ErrorMessage)> ValidateVpkAsync(string vpkPath, CancellationToken ct = default);

        Task<(VpkOrigin Origin, bool NeedsRebuild)> ClassifyVpkAsync(string vpkPath, CancellationToken ct = default);

        bool IsRequiredModFilePresent(string dotaPath);

        Task<bool> CheckForNewerModsPackAsync(string dotaPath, CancellationToken ct = default);

        Task<bool> DisableModsAsync(string dotaPath, CancellationToken ct = default);

        Task<(bool Success, bool IsUpToDate)> InstallModsAsync(
            string targetPath,
            string appPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            bool force = false,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action<string>? statusCallback = null);

        Task<bool> ManualInstallModsAsync(
            string targetPath,
            string vpkFilePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action<string>? statusCallback = null,
            bool rebuild = true);

        Task<PatchResult> UpdatePatcherAsync(
            string dotaPath,
            Action<string>? statusCallback = null,
            CancellationToken ct = default);

        void SetLogger(IAppLogger logger);
    }
}

