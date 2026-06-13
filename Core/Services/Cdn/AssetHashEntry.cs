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
namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// Expected integrity metadata for a single downloadable asset, sourced from the
    /// server-published <c>Assets/asset_hashes.json</c> manifest.
    /// </summary>
    public sealed class AssetHashEntry
    {
        /// <summary>Expected SHA-256 of the asset content, as uppercase hex.</summary>
        public string Sha256 { get; init; } = "";

        /// <summary>Expected size of the asset in bytes (0 = unknown).</summary>
        public long Size { get; init; }
    }
}
