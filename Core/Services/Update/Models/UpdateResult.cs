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
    /// Result of an update operation.
    /// </summary>
    public class UpdateResult
    {
        /// <summary>
        /// Whether the update was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the update failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Whether the application needs to restart to complete the update.
        /// </summary>
        public bool RequiresRestart { get; set; }

        /// <summary>
        /// Path to the downloaded update file (for reference).
        /// </summary>
        public string? DownloadedFilePath { get; set; }

        public static UpdateResult Succeeded(bool requiresRestart = true) => new()
        {
            Success = true,
            RequiresRestart = requiresRestart
        };

        public static UpdateResult Failed(string error) => new()
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

