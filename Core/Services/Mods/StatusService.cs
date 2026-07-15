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
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Mods;
using ArdysaModsTools.UI.Styles;

namespace ArdysaModsTools.Core.Services
{
    public sealed class StatusService : IStatusService
    {
        #region Private Fields

        private readonly IAppLogger? _logger;
        private readonly DotaVersionService _versionService;
        private string? _lastLoggedBuildChange;

        #endregion

        #region Constructor

        public StatusService(IAppLogger? logger = null)
        {
            _logger = logger;
            _versionService = new DotaVersionService(logger ?? NullLogger.Instance);
        }

        #endregion

        #region Public Methods

        public async Task<ModStatusInfo> GetDetailedStatusAsync(
            string? targetPath, 
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return CreateStatus(ModStatus.NotChecked, "status.pathNotSet.text",
                    "status.pathNotSet.desc");
            }

            try
            {
                var dotaCheck = await ValidateDotaInstallation(targetPath, ct);
                if (dotaCheck != null) return dotaCheck;

                var modsResult = await CheckModsInstalled(targetPath, ct);
                if (!modsResult.IsInstalled)
                    return modsResult.NotInstalledStatus!;
                
                var (version, lastModified) = (modsResult.Version, modsResult.LastModified);

                var gameInfoCheck = await CheckGameInfoPatched(targetPath, ct);
                if (gameInfoCheck != null) 
                    return gameInfoCheck with { Version = version, LastModified = lastModified };

                var sigCheck = await CheckSignaturesPatched(targetPath, version, lastModified, ct);
                if (sigCheck.Status != ModStatus.Ready)
                    return sigCheck;

                var buildCheck = await CheckBuildVersionAsync(targetPath, version, lastModified, ct);
                if (buildCheck != null)
                    return buildCheck;

                return sigCheck;
            }
            catch (OperationCanceledException)
            {
                return CreateStatus(ModStatus.NotChecked, "status.cancelled.text",
                    "status.cancelled.desc");
            }
            catch (Exception ex)
            {
                _logger?.Log($"[STATUS] Error: {ex.Message}");
                return CreateStatus(ModStatus.Error, "status.error.text",
                    "status.error.desc",
                    errorMessage: ex.Message,
                    descVars: new { message = ex.Message });
            }
        }

        #endregion

        #region Step-Based Validation Methods

        private async Task<ModStatusInfo?> ValidateDotaInstallation(string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string dota2Exe = Path.Combine(targetPath, DotaPaths.Dota2Exe);
            string signatures = Path.Combine(targetPath, DotaPaths.Signatures);

            if (!File.Exists(dota2Exe))
            {
                return CreateStatus(ModStatus.Error, "status.invalidPath.text",
                    "status.invalidPath.desc",
                    errorMessage: "Missing game/bin/win64/dota2.exe");
            }

            if (!File.Exists(signatures))
            {
                return CreateStatus(ModStatus.Error, "status.coreMissing.text",
                    "status.coreMissing.desc",
                    errorMessage: "Missing core files - run Steam verify");
            }

            return null;
        }

        private readonly struct ModsInstalledResult
        {
            public bool IsInstalled { get; init; }
            public ModStatusInfo? NotInstalledStatus { get; init; }
            public string? Version { get; init; }
            public DateTime? LastModified { get; init; }
        }

        private async Task<ModsInstalledResult> CheckModsInstalled(
            string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string vpkFile = Path.Combine(targetPath, DotaPaths.ModsVpk);
            string versionFile = Path.Combine(targetPath, DotaPaths.ModsVersion);

            if (!File.Exists(vpkFile))
            {
                return new ModsInstalledResult
                {
                    IsInstalled = false,
                    NotInstalledStatus = CreateStatus(ModStatus.NotInstalled, "status.notInstalled.text",
                        "status.notInstalled.desc",
                        action: RecommendedAction.Install,
                        actionText: Loc.T("status.action.install"))
                };
            }

            string? version = await GetVersionAsync(versionFile, ct);
            DateTime? lastModified = GetLastModified(vpkFile);

            return new ModsInstalledResult
            {
                IsInstalled = true,
                Version = version,
                LastModified = lastModified
            };
        }

        private async Task<ModStatusInfo?> CheckGameInfoPatched(string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string gameInfoFile = Path.Combine(targetPath, DotaPaths.GameInfo);

            if (!File.Exists(gameInfoFile))
            {
                return CreateStatus(ModStatus.Disabled, "status.disabled.text",
                    "status.disabled.desc",
                    action: RecommendedAction.Enable,
                    actionText: Loc.T("status.action.patchUpdate"));
            }

            string content = await ReadFileFreshAsync(gameInfoFile, ct);
            bool isPatched = content.Contains(ModConstants.GameInfoMarker, StringComparison.OrdinalIgnoreCase);

            if (!isPatched)
            {
                return CreateStatus(ModStatus.Disabled, "status.disabled.text",
                    "status.disabled.desc",
                    action: RecommendedAction.Enable,
                    actionText: Loc.T("status.action.patchUpdate"));
            }

            return null;
        }

        private async Task<ModStatusInfo> CheckSignaturesPatched(
            string targetPath, string? version, DateTime? lastModified, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string signaturesFile = Path.Combine(targetPath, DotaPaths.Signatures);
            string content = await ReadFileFreshAsync(signaturesFile, ct);

            int digestIndex = content.IndexOf("DIGEST:", StringComparison.Ordinal);
            if (digestIndex < 0)
            {
                return CreateStatus(ModStatus.Error, "status.invalidCore.text",
                    "status.invalidCore.desc",
                    errorMessage: "DIGEST not found in core files");
            }

            string afterDigest = content.Substring(digestIndex);
            
            bool hasCorrectPatch = afterDigest.Contains(
                ModConstants.ModPatchLine,
                StringComparison.Ordinal);

            bool hasSha1Only = !hasCorrectPatch && afterDigest.Contains(
                $"gameinfo_branchspecific.gi~SHA1:{ModConstants.ModPatchSHA1}",
                StringComparison.OrdinalIgnoreCase);

            if (hasCorrectPatch)
            {
                return CreateStatus(ModStatus.Ready, "status.ready.text",
                    "status.ready.desc",
                    action: RecommendedAction.None,
                    version: version,
                    lastModified: lastModified);
            }
            else if (hasSha1Only)
            {
                return CreateStatus(ModStatus.Error, "status.invalidPatch.text",
                    "status.invalidPatch.desc",
                    action: RecommendedAction.Fix,
                    actionText: Loc.T("status.action.patchUpdate"),
                    errorMessage: "Signature patch line has incorrect path format");
            }
            else
            {
                return CreateStatus(ModStatus.NeedUpdate, "status.updateRequired.text",
                    "status.updateRequired.desc",
                    action: RecommendedAction.Update,
                    actionText: Loc.T("status.action.patchUpdate"),
                    version: version,
                    lastModified: lastModified);
            }
        }

        private async Task<ModStatusInfo?> CheckBuildVersionAsync(
            string targetPath, string? version, DateTime? lastModified, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var (matches, currentBuild, patchedBuild) =
                    await _versionService.ComparePatchedVersionAsync(targetPath);

                if (patchedBuild == "Not patched yet" || string.IsNullOrEmpty(patchedBuild))
                {
                    return null;
                }

                if (!matches)
                {
                    string buildChangeKey = $"{patchedBuild}→{currentBuild}";
                    if (_lastLoggedBuildChange != buildChangeKey)
                    {
                        _lastLoggedBuildChange = buildChangeKey;
                        _logger?.Log($"[STATUS] Build changed: {patchedBuild} → {currentBuild}");
                    }
                    
                    return CreateStatus(ModStatus.NeedUpdate, "status.updateRequired.text",
                        "status.updateRequiredBuild.desc",
                        action: RecommendedAction.Update,
                        actionText: Loc.T("status.action.patchUpdate"),
                        version: version,
                        lastModified: lastModified,
                        descVars: new { build = currentBuild });
                }

                _lastLoggedBuildChange = null;
                return null;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[STATUS] Build version check failed: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private static ModStatusInfo CreateStatus(
            ModStatus status,
            string statusTextKey,
            string descKey,
            RecommendedAction action = RecommendedAction.None,
            string actionText = "",
            string? version = null,
            DateTime? lastModified = null,
            string? errorMessage = null,
            object? descVars = null)
        {
            var color = StatusColors.ForStatus(status);

            return new ModStatusInfo
            {
                Status = status,
                StatusText = Loc.T(statusTextKey),
                StatusTextKey = statusTextKey,
                Description = descVars != null ? Loc.T(descKey, descVars) : Loc.T(descKey),
                DescriptionKey = descKey,
                DescriptionVars = descVars,
                Action = action,
                ActionButtonText = actionText,
                Version = version,
                LastModified = lastModified,
                ErrorMessage = errorMessage,
                StatusColor = color
            };
        }

        private static async Task<string?> GetVersionAsync(string versionFile, CancellationToken ct)
        {
            try
            {
                if (File.Exists(versionFile))
                {
                    return (await File.ReadAllTextAsync(versionFile, ct).ConfigureAwait(false)).Trim();
                }
            }
            catch { }
            return null;
        }

        private static DateTime? GetLastModified(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return File.GetLastWriteTime(filePath);
                }
            }
            catch { }
            return null;
        }

        private static async Task<string> ReadFileFreshAsync(string filePath, CancellationToken ct)
        {
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            return await reader.ReadToEndAsync(ct);
        }

        #endregion
    }
}

