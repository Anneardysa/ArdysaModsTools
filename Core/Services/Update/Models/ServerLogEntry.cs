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
namespace ArdysaModsTools.Core.Services.Update.Models
{
    /// <summary>
    /// Status of a download server in the multi-CDN pipeline.
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>Server is queued but not yet tried.</summary>
        Standby,
        /// <summary>Currently downloading from this server.</summary>
        Active,
        /// <summary>Download completed successfully from this server.</summary>
        Success,
        /// <summary>Download failed from this server.</summary>
        Failed
    }

    /// <summary>
    /// Log entry for a download server shown in the progress overlay.
    /// Only displays "Server-XX" without revealing the underlying CDN provider.
    /// </summary>
    public class ServerLogEntry
    {
        /// <summary>
        /// Display name shown in UI (e.g. "Server-01").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Current status of this server.
        /// </summary>
        public ServerStatus Status { get; set; } = ServerStatus.Standby;

        /// <summary>
        /// Internal CDN label for logging (e.g. "R2", "jsDelivr", "GitHub").
        /// Not displayed in UI, only used for internal logger.
        /// </summary>
        public string InternalLabel { get; set; } = string.Empty;
    }
}
