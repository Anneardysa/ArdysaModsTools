using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Enhanced service for checking mod installation status.
    /// Clean, step-based validation with cancellation support.
    /// </summary>
    public sealed class StatusService : IStatusService
    {
        #region Private Fields

        private readonly ILogger _logger;
        private System.Threading.Timer? _autoRefreshTimer;
        private string? _currentTargetPath;
        private ModStatusInfo? _lastStatus;
        private bool _disposed;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        #endregion

        #region Constants

        private static class Paths
        {
            public const string ModsVpk = "game/_ArdysaMods/pak01_dir.vpk";
            public const string ModsVersion = "game/_ArdysaMods/version.txt";
            public const string Signatures = "game/bin/win64/dota.signatures";
            public const string GameInfo = "game/dota/gameinfo_branchspecific.gi";
            public const string Dota2Exe = "game/bin/win64/dota2.exe";
        }

        private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

        #endregion

        #region Events

        /// <summary>Event fired when status changes.</summary>
        public event Action<ModStatusInfo>? OnStatusChanged;

        #endregion

        #region Constructor

        public StatusService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            // Step 1: Validate path is set
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return CreateStatus(ModStatus.NotChecked, "Path Not Set",
                    "Please detect or select your Dota 2 folder.");
            }

            try
            {
                // Step 2: Validate Dota 2 installation
                var dotaCheck = await ValidateDotaInstallation(targetPath, ct);
                if (dotaCheck != null) return dotaCheck;

                // Step 3: Check if mods are installed
                var modsCheck = await CheckModsInstalled(targetPath, ct);
                if (modsCheck != null) return modsCheck.Value.Result;
                
                var (version, lastModified) = modsCheck?.Metadata ?? (null, null);

                // Step 4: Check if gameinfo is patched
                var gameInfoCheck = await CheckGameInfoPatched(targetPath, ct);
                if (gameInfoCheck != null) 
                    return gameInfoCheck with { Version = version, LastModified = lastModified };

                // Step 5: Check if signatures are patched
                var sigCheck = await CheckSignaturesPatched(targetPath, version, lastModified, ct);
                if (sigCheck.Status != ModStatus.Ready)
                    return sigCheck;

                // Step 6: Check if build version matches patched version
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
                _logger.Log($"[STATUS] Error: {ex.Message}");
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
        /// Start auto-refresh timer.
        /// </summary>
        public void StartAutoRefresh(string targetPath)
        {
            StopAutoRefresh();
            _currentTargetPath = targetPath;
            
            _autoRefreshTimer = new System.Threading.Timer(
                async _ => await RefreshStatusAsync(_currentTargetPath),
                null,
                TimeSpan.Zero,
                AutoRefreshInterval);
            
            _logger.Log("[STATUS] Auto-refresh started (30s interval)");
        }

        /// <summary>
        /// Stop auto-refresh timer.
        /// </summary>
        public void StopAutoRefresh()
        {
            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = null;
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

            string dota2Exe = Path.Combine(targetPath, Paths.Dota2Exe);
            string signatures = Path.Combine(targetPath, Paths.Signatures);

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
        private async Task<(ModStatusInfo Result, (string? Version, DateTime? LastModified) Metadata)?> CheckModsInstalled(
            string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string vpkFile = Path.Combine(targetPath, Paths.ModsVpk);
            string versionFile = Path.Combine(targetPath, Paths.ModsVersion);

            if (!File.Exists(vpkFile))
            {
                return (CreateStatus(ModStatus.NotInstalled, "Not Installed",
                    "ModsPack is not installed. Click 'Install' to get started.",
                    action: RecommendedAction.Install,
                    actionText: "Install ModsPack"), (null, null));
            }

            // Get metadata
            string? version = await GetVersionAsync(versionFile, ct);
            DateTime? lastModified = GetLastModified(vpkFile);

            return null; // Mods are installed
        }

        /// <summary>
        /// Step 4: Check if gameinfo is patched.
        /// Returns null if patched, or disabled status.
        /// </summary>
        private async Task<ModStatusInfo?> CheckGameInfoPatched(string targetPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string gameInfoFile = Path.Combine(targetPath, Paths.GameInfo);

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

            string signaturesFile = Path.Combine(targetPath, Paths.Signatures);
            string content = await ReadFileFreshAsync(signaturesFile, ct);

            // Find DIGEST line
            int digestIndex = content.IndexOf("DIGEST:", StringComparison.Ordinal);
            if (digestIndex < 0)
            {
                return CreateStatus(ModStatus.Error, "Invalid Core Files",
                    "The core files are corrupted. Try reinstalling or verify game files.",
                    errorMessage: "DIGEST not found in core files");
            }

            // Check if our mod line exists AFTER DIGEST
            string afterDigest = content.Substring(digestIndex);
            bool hasModPatch = afterDigest.Contains(
                $"gameinfo_branchspecific.gi~SHA1:{ModConstants.ModPatchSHA1}",
                StringComparison.OrdinalIgnoreCase);

            if (hasModPatch)
            {
                return CreateStatus(ModStatus.Ready, "Ready",
                    "ModsPack is active and up-to-date. Enjoy your game!",
                    action: RecommendedAction.None,
                    version: version,
                    lastModified: lastModified);
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
                var versionService = new DotaVersionService(_logger);
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
                    _logger.Log($"[STATUS] Build changed: {patchedBuild} → {currentBuild}");
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
                _logger.Log($"[STATUS] Build version check failed: {ex.Message}");
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
            var color = status switch
            {
                ModStatus.Ready => Color.FromArgb(80, 200, 120),        // Green
                ModStatus.NeedUpdate => Color.FromArgb(255, 180, 50),   // Orange
                ModStatus.Disabled => Color.FromArgb(150, 150, 180),    // Blue-gray
                ModStatus.NotInstalled => Color.FromArgb(120, 120, 120), // Gray
                ModStatus.Error => Color.FromArgb(255, 80, 80),         // Red
                _ => Color.FromArgb(150, 150, 150)                      // Gray
            };

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

        #region Legacy Compatibility

        /// <summary>
        /// Update UI labels based on status.
        /// </summary>
        public void UpdateStatusUI(ModStatusInfo result, Label dotLabel, Label textLabel)
        {
            SetLabelStatus(dotLabel, textLabel, result.StatusText, result.StatusColor);
        }

        /// <summary>
        /// Check status and update UI in one call.
        /// </summary>
        public async Task CheckAndUpdateUIAsync(string? targetPath, Label dotLabel, Label textLabel)
        {
            var result = await GetDetailedStatusAsync(targetPath);
            UpdateStatusUI(result, dotLabel, textLabel);
        }

        private void SetLabelStatus(Label dotLabel, Label textLabel, string text, Color color)
        {
            if (dotLabel.InvokeRequired)
            {
                dotLabel.BeginInvoke(new Action(() => SetLabelStatus(dotLabel, textLabel, text, color)));
                return;
            }

            dotLabel.BackColor = color;
            textLabel.Text = text;
            textLabel.ForeColor = color;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            StopAutoRefresh();
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
