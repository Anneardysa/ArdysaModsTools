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
namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Result of a mod installation operation.
    /// Provides clear semantics for all possible outcomes.
    /// </summary>
    public sealed class InstallResult
    {
        /// <summary>
        /// Gets whether the installation was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets whether the mods were already up to date (no action needed).
        /// </summary>
        public bool IsUpToDate { get; init; }

        /// <summary>
        /// Gets whether the operation was cancelled by the user.
        /// </summary>
        public bool WasCancelled { get; init; }

        /// <summary>
        /// Gets an optional error message if the installation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the error code if applicable.
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// Creates a successful installation result.
        /// </summary>
        public static InstallResult Succeeded() => new() { Success = true };

        /// <summary>
        /// Creates an up-to-date result (success, but no changes needed).
        /// </summary>
        public static InstallResult UpToDate() => new() { Success = true, IsUpToDate = true };

        /// <summary>
        /// Creates a failed installation result.
        /// </summary>
        public static InstallResult Failed(string? errorMessage = null, string? errorCode = null) 
            => new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };

        /// <summary>
        /// Creates a cancelled installation result.
        /// </summary>
        public static InstallResult Cancelled() => new() { Success = false, WasCancelled = true };

        /// <summary>
        /// Converts a tuple result to InstallResult.
        /// </summary>
        public static InstallResult FromTuple((bool Success, bool IsUpToDate) tuple)
            => new() { Success = tuple.Success, IsUpToDate = tuple.IsUpToDate };
    }

    /// <summary>
    /// Result of a mod disable operation.
    /// </summary>
    public sealed class DisableResult
    {
        /// <summary>
        /// Gets whether the disable operation was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets whether the mods were already disabled.
        /// </summary>
        public bool AlreadyDisabled { get; init; }

        /// <summary>
        /// Gets an optional error message if the disable failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        public static DisableResult Succeeded() => new() { Success = true };
        public static DisableResult AlreadyDone() => new() { Success = true, AlreadyDisabled = true };
        public static DisableResult Failed(string? errorMessage = null) => new() { Success = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// Result of checking for newer mods.
    /// </summary>
    public sealed class UpdateCheckResult
    {
        /// <summary>
        /// Gets whether a newer version is available.
        /// </summary>
        public bool HasNewerVersion { get; init; }

        /// <summary>
        /// Gets whether a local install exists.
        /// </summary>
        public bool HasLocalInstall { get; init; }

        /// <summary>
        /// Gets whether the check could be performed.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets an optional error message if the check failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        public static UpdateCheckResult NewVersionAvailable() => new() { Success = true, HasNewerVersion = true, HasLocalInstall = true };
        public static UpdateCheckResult UpToDate() => new() { Success = true, HasNewerVersion = false, HasLocalInstall = true };
        public static UpdateCheckResult NoLocalInstall() => new() { Success = true, HasNewerVersion = true, HasLocalInstall = false };
        public static UpdateCheckResult Failed(string? errorMessage = null) => new() { Success = false, ErrorMessage = errorMessage };
    }
}

