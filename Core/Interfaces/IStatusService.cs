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
using ArdysaModsTools.Core.Models;

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
        /// Event fired when checking starts (for UI loading state).
        /// </summary>
        event Action? OnCheckingStarted;

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
    }
}

