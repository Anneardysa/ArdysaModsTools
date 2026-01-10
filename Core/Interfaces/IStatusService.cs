using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for mod status checking operations.
    /// Provides detailed status information about mod installation state.
    /// </summary>
    public interface IStatusService : IDisposable
    {
        /// <summary>
        /// Event fired when status changes during auto-refresh.
        /// </summary>
        event Action<ModStatusInfo>? OnStatusChanged;

        /// <summary>
        /// Get detailed mod status with step-based validation.
        /// </summary>
        /// <param name="dotaPath">Path to Dota 2 game directory.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Detailed status information.</returns>
        Task<ModStatusInfo> GetDetailedStatusAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Force refresh status, clearing any cache.
        /// </summary>
        /// <param name="dotaPath">Path to Dota 2 game directory.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Detailed status information.</returns>
        Task<ModStatusInfo> ForceRefreshAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Refresh status and fire event if changed.
        /// </summary>
        /// <param name="dotaPath">Path to Dota 2 game directory.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RefreshStatusAsync(string dotaPath, CancellationToken ct = default);

        /// <summary>
        /// Start auto-refresh timer.
        /// </summary>
        /// <param name="dotaPath">Path to Dota 2 game directory.</param>
        /// <param name="intervalMs">Refresh interval in milliseconds.</param>
        void StartAutoRefresh(string dotaPath);

        /// <summary>
        /// Stop auto-refresh timer.
        /// </summary>
        void StopAutoRefresh();

        /// <summary>
        /// Get the last cached status without checking.
        /// </summary>
        /// <returns>Cached status or null if never checked.</returns>
        ModStatusInfo? GetCachedStatus();

        /// <summary>
        /// Update UI labels based on status.
        /// </summary>
        void UpdateStatusUI(ModStatusInfo status, Label statusLabel, Label actionLabel);

        /// <summary>
        /// Check status and update UI in one call.
        /// </summary>
        Task CheckAndUpdateUIAsync(
            string? dotaPath,
            Label dotLabel,
            Label textLabel);
    }
}
