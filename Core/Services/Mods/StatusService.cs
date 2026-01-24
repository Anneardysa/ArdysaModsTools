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
using ArdysaModsTools.Core.Services.Mods;
using ArdysaModsTools.UI.Styles;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Enhanced service for checking mod installation status.
    /// Clean, step-based validation with cancellation support.
    /// </summary>
    public sealed class StatusService : IStatusService
    {
        #region Private Fields

        private readonly ILogger? _logger;
        private readonly FileWatcherService? _fileWatcher;
        private System.Threading.Timer? _autoRefreshTimer;
        private string? _currentTargetPath;
        private ModStatusInfo? _lastStatus;
        private bool _disposed;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        #endregion

        #region Constants

        private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

        #endregion

        #region Events

        /// <summary>Event fired when status changes.</summary>
        public event Action<ModStatusInfo>? OnStatusChanged;

        /// <summary>Event fired when checking starts (for UI loading state).</summary>
        public event Action? OnCheckingStarted;

        #endregion

        #region Constructor

        public StatusService(ILogger? logger = null)
        {
            _logger = logger;
            _fileWatcher = new FileWatcherService(logger);
            
            // Wire up file watcher events
            _fileWatcher.OnChangeDetected += HandleFileChangeDetected;
            _fileWatcher.OnFilesChanged += HandleFilesChanged;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get detailed mod status with step-based validation.
        /// </summary>
        public async Task<ModStatusInfo> GetDetailedStatusAsync(
            string? targetPath, 
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return CreateStatus(ModStatus.NotChecked, "Path Not Set",
                    "Please detect or select your Dota 2 folder.");
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

                // All checks passed - Ready
                return sigCheck;
            }
            catch (OperationCanceledException)
            {
                return CreateStatus(ModStatus.NotChecked, "Cancelled",
                    "Status check was cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.Log($"[STATUS] Error: {ex.Message}");
                return CreateStatus(ModStatus.Error, "Error",
                    $"Failed to check status: {ex.Message}",
                    errorMessage: ex.Message);
            }
        }

        /// <summary>
        /// Force refresh status, clearing any cache.
        /// </summary>
        public async Task<ModStatusInfo> ForceRefreshAsync(
            string targetPath, 
            CancellationToken ct = default)
        {
            _lastStatus = null;
            var status = await GetDetailedStatusAsync(targetPath, ct);
            _lastStatus = status;
            OnStatusChanged?.Invoke(status);
            return status;
        }

        /// <summary>
        /// Refresh status and fire event if changed.
        /// </summary>
        public async Task RefreshStatusAsync(string? targetPath, CancellationToken ct = default)
        {
            // Prevent concurrent refresh
            if (!await _refreshLock.WaitAsync(0, ct))
                return;

            try
            {
                var newStatus = await GetDetailedStatusAsync(targetPath, ct);
                
                bool statusChanged = _lastStatus == null || 
                                     _lastStatus.Status != newStatus.Status ||
                                     _lastStatus.Version != newStatus.Version;
                
                _lastStatus = newStatus;
                
                if (statusChanged)
                {
                    OnStatusChanged?.Invoke(newStatus);
                }
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Start auto-refresh timer and file watcher.
        /// </summary>
        public void StartAutoRefresh(string targetPath)
        {
            StopAutoRefresh();
            _currentTargetPath = targetPath;
            
            // Start file system watcher for real-time updates
            _fileWatcher?.StartWatching(targetPath);
            
            // Fallback timer for periodic refresh (catches edge cases)
            _autoRefreshTimer = new System.Threading.Timer(
                async _ => await RefreshStatusAsync(_currentTargetPath),
                null,
                TimeSpan.Zero,
                AutoRefreshInterval);
            
            _logger?.Log("[STATUS] Auto-refresh started (FileWatcher + 30s fallback timer)");
        }

        /// <summary>
        /// Stop auto-refresh timer and file watcher.
        /// </summary>
        public void StopAutoRefresh()
        {
            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = null;
            _fileWatcher?.StopWatching();
        }

        /// <summary>
        /// Get the last cached status without checking.
        /// </summary>
        public ModStatusInfo? GetCachedStatus() => _lastStatus;

        #endregion

        #region Step-Based Validation Methods

        /// <summary>
        /// Step 2: Validate Dota 2 installation.
        /// Returns null if valid, or error status.
        /// </summary>
        private async Task<ModStatusInfo?> ValidateDotaInstallation(string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string dota2Exe = Path.Combine(targetPath, DotaPaths.Dota2Exe);
            string signatures = Path.Combine(targetPath, DotaPaths.Signatures);

            if (!File.Exists(dota2Exe))
            {
                return CreateStatus(ModStatus.Error, "Invalid Path",
                    "dota2.exe not found. Please select a valid Dota 2 folder.",
                    errorMessage: "Missing game/bin/win64/dota2.exe");
            }

            if (!File.Exists(signatures))
            {
                return CreateStatus(ModStatus.Error, "Error",
                    "Core files are missing. Verify game files in Steam.",
                    errorMessage: "Missing core files - run Steam verify");
            }

            return null; // Validation passed
        }

        /// <summary>
        /// Step 3: Check if mods are installed.
        /// Returns null if installed, or not-installed status.
        /// </summary>
        /// <summary>
        /// Result from CheckModsInstalled - indicates if mods are installed and provides metadata.
        /// </summary>
        private readonly struct ModsInstalledResult
        {
            public bool IsInstalled { get; init; }
            public ModStatusInfo? NotInstalledStatus { get; init; }
            public string? Version { get; init; }
            public DateTime? LastModified { get; init; }
        }

        /// <summary>
        /// Step 3: Check if mods are installed.
        /// Returns installed=true with metadata if installed, or installed=false with not-installed status.
        /// </summary>
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
                    NotInstalledStatus = CreateStatus(ModStatus.NotInstalled, "Not Installed",
                        "ModsPack is not installed. Click 'Install' to get started.",
                        action: RecommendedAction.Install,
                        actionText: "Install ModsPack")
                };
            }

            // Get metadata
            string? version = await GetVersionAsync(versionFile, ct);
            DateTime? lastModified = GetLastModified(vpkFile);

            return new ModsInstalledResult
            {
                IsInstalled = true,
                Version = version,
                LastModified = lastModified
            };
        }

        /// <summary>
        /// Step 4: Check if gameinfo is patched.
        /// Returns null if patched, or disabled status.
        /// </summary>
        private async Task<ModStatusInfo?> CheckGameInfoPatched(string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string gameInfoFile = Path.Combine(targetPath, DotaPaths.GameInfo);

            if (!File.Exists(gameInfoFile))
            {
                return CreateStatus(ModStatus.Disabled, "Disabled",
                    "ModsPack is installed but not active. Click 'Patch Update' to activate.",
                    action: RecommendedAction.Enable,
                    actionText: "Patch Update");
            }

            string content = await ReadFileFreshAsync(gameInfoFile, ct);
            bool isPatched = content.Contains(ModConstants.GameInfoMarker, StringComparison.OrdinalIgnoreCase);

            if (!isPatched)
            {
                return CreateStatus(ModStatus.Disabled, "Disabled",
                    "ModsPack is installed but not active. Click 'Patch Update' to activate.",
                    action: RecommendedAction.Enable,
                    actionText: "Patch Update");
            }

            return null; // GameInfo is patched
        }

        /// <summary>
        /// Step 5: Check if signatures are patched.
        /// Returns Ready or NeedUpdate status.
        /// </summary>
        private async Task<ModStatusInfo> CheckSignaturesPatched(
            string targetPath, string? version, DateTime? lastModified, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string signaturesFile = Path.Combine(targetPath, DotaPaths.Signatures);
            string content = await ReadFileFreshAsync(signaturesFile, ct);

            // Find DIGEST line
            int digestIndex = content.IndexOf("DIGEST:", StringComparison.Ordinal);
            if (digestIndex < 0)
            {
                return CreateStatus(ModStatus.Error, "Invalid Core Files",
                    "The core files are corrupted. Try reinstalling or verify game files.",
                    errorMessage: "DIGEST not found in core files");
            }

            string afterDigest = content.Substring(digestIndex);
            
            // Check for exact match of the correct patch line
            bool hasCorrectPatch = afterDigest.Contains(
                ModConstants.ModPatchLine,
                StringComparison.Ordinal);

            // Check if SHA1 is present but path might be wrong (malformed patch)
            bool hasSha1Only = !hasCorrectPatch && afterDigest.Contains(
                $"gameinfo_branchspecific.gi~SHA1:{ModConstants.ModPatchSHA1}",
                StringComparison.OrdinalIgnoreCase);

            if (hasCorrectPatch)
            {
                return CreateStatus(ModStatus.Ready, "Ready",
                    "ModsPack is active and up-to-date. Enjoy your game!",
                    action: RecommendedAction.None,
                    version: version,
                    lastModified: lastModified);
            }
            else if (hasSha1Only)
            {
                // SHA1 matches but path is wrong - this could trigger VAC!
                return CreateStatus(ModStatus.Error, "Invalid Patch Format",
                    "The patch format is incorrect and may cause matchmaking issues. Please run 'Patch Update' to fix.",
                    action: RecommendedAction.Fix,
                    actionText: "Patch Update",
                    errorMessage: "Signature patch line has incorrect path format");
            }
            else
            {
                return CreateStatus(ModStatus.NeedUpdate, "Update Required",
                    "Dota 2 was updated. Please run 'Patch Update' to fix.",
                    action: RecommendedAction.Update,
                    actionText: "Patch Update",
                    version: version,
                    lastModified: lastModified);
            }
        }

        /// <summary>
        /// Step 6: Check if steam.inf build version matches the patched version.
        /// Compares current steam.inf with saved version.json to detect Dota updates.
        /// Returns null if versions match (proceed to Ready), or NeedUpdate status.
        /// </summary>
        private async Task<ModStatusInfo?> CheckBuildVersionAsync(
            string targetPath, string? version, DateTime? lastModified, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var versionService = new DotaVersionService(_logger ?? NullLogger.Instance);
                var (matches, currentBuild, patchedBuild) = 
                    await versionService.ComparePatchedVersionAsync(targetPath);

                // If version.json doesn't exist yet (Not patched yet), skip this check
                if (patchedBuild == "Not patched yet" || string.IsNullOrEmpty(patchedBuild))
                {
                    return null; // Skip - will be created after first patch
                }

                // If versions don't match, Dota was updated
                if (!matches)
                {
                    _logger?.Log($"[STATUS] Build changed: {patchedBuild} → {currentBuild}");
                    return CreateStatus(ModStatus.NeedUpdate, "Update Required",
                        $"Dota 2 was updated ({currentBuild}). Run 'Patch Update' to fix.",
                        action: RecommendedAction.Update,
                        actionText: "Patch Update",
                        version: version,
                        lastModified: lastModified);
                }

                return null; // Versions match - proceed to Ready
            }
            catch (Exception ex)
            {
                _logger?.Log($"[STATUS] Build version check failed: {ex.Message}");
                return null; // If check fails, don't block - proceed to Ready
            }
        }

        #endregion

        #region Helper Methods

        private static ModStatusInfo CreateStatus(
            ModStatus status,
            string statusText,
            string description,
            RecommendedAction action = RecommendedAction.None,
            string actionText = "",
            string? version = null,
            DateTime? lastModified = null,
            string? errorMessage = null)
        {
            var color = StatusColors.ForStatus(status);

            return new ModStatusInfo
            {
                Status = status,
                StatusText = statusText,
                Description = description,
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

        #region File Watcher Handlers

        /// <summary>
        /// Called immediately when a file change is detected.
        /// Shows "Checking..." state in UI.
        /// </summary>
        private void HandleFileChangeDetected()
        {
            _logger?.Log("[STATUS] File change detected, starting check...");
            OnCheckingStarted?.Invoke();
            
            // Emit "Checking" status for immediate UI feedback
            var checkingStatus = CreateStatus(ModStatus.Checking, "Checking...",
                "Detecting changes, please wait...");
            OnStatusChanged?.Invoke(checkingStatus);
        }

        /// <summary>
        /// Called after debounce when files have changed.
        /// Triggers actual status refresh.
        /// </summary>
        private async void HandleFilesChanged()
        {
            if (string.IsNullOrEmpty(_currentTargetPath)) return;
            
            try
            {
                await RefreshStatusAsync(_currentTargetPath);
            }
            catch (Exception ex)
            {
                // Silent catch - file watcher events shouldn't crash the app
                System.Diagnostics.Debug.WriteLine($"[StatusService] HandleFilesChanged error: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            StopAutoRefresh();
            _fileWatcher?.Dispose();
            _refreshLock.Dispose();
            _disposed = true;
        }

        #endregion
    }

    /// <summary>
    /// Legacy status result record for backward compatibility.
    /// </summary>
    public record StatusResult(ModStatus Status, string? Message = null);
}

