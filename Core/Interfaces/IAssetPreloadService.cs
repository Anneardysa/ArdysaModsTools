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

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Phase of a "Launching State" asset preload run.
    /// </summary>
    public enum AssetPreloadPhase
    {
        /// <summary>Collecting the list of asset URLs to download.</summary>
        Enumerating,
        /// <summary>Downloading uncached assets into the local cache.</summary>
        Downloading,
        /// <summary>All assets are cached (or attempted) — nothing left to do.</summary>
        Complete
    }

    /// <summary>
    /// Progress snapshot for an asset preload run. <paramref name="Failed"/> is the number of assets
    /// that could not be downloaded (reported on <see cref="AssetPreloadPhase.Complete"/>), so the
    /// launch console can report an honest partial result instead of an unconditional "ready".
    /// </summary>
    public sealed record AssetPreloadProgress(AssetPreloadPhase Phase, int Current, int Total, int Failed = 0);

    /// <summary>
    /// "Launching State" — proactively downloads all gallery asset images (misc + hero set
    /// thumbnails) into the local asset cache so the Miscellaneous and Skin Selector panels open
    /// instantly and work offline. Runs in the background; safe to call on every launch.
    /// </summary>
    public interface IAssetPreloadService
    {
        /// <summary>True while a preload run is in progress.</summary>
        bool IsRunning { get; }

        /// <summary>True once a run has finished with no remaining uncached assets.</summary>
        bool IsComplete { get; }

        /// <summary>
        /// Enumerates all gallery asset URLs, skips those already cached or known-missing, and
        /// downloads the rest into the local cache. Re-entrant: a no-op if a run is already in
        /// progress. Never throws — enumeration/network failures are logged and swallowed.
        /// </summary>
        /// <param name="progress">Optional progress reporter (phase + current/total counts).</param>
        /// <param name="ct">Cancellation token; cancelled cleanly on app shutdown.</param>
        Task PreloadAllAsync(IProgress<AssetPreloadProgress>? progress = null, CancellationToken ct = default);
    }
}
