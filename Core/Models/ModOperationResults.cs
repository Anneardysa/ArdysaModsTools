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
    public sealed class InstallResult
    {
        public bool Success { get; init; }

        public bool IsUpToDate { get; init; }

        public bool WasCancelled { get; init; }

        public string? ErrorMessage { get; init; }

        public string? ErrorCode { get; init; }

        public static InstallResult Succeeded() => new() { Success = true };

        public static InstallResult UpToDate() => new() { Success = true, IsUpToDate = true };

        public static InstallResult Failed(string? errorMessage = null, string? errorCode = null) 
            => new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };

        public static InstallResult Cancelled() => new() { Success = false, WasCancelled = true };

        public static InstallResult FromTuple((bool Success, bool IsUpToDate) tuple)
            => new() { Success = tuple.Success, IsUpToDate = tuple.IsUpToDate };
    }

    public sealed class DisableResult
    {
        public bool Success { get; init; }

        public bool AlreadyDisabled { get; init; }

        public string? ErrorMessage { get; init; }

        public static DisableResult Succeeded() => new() { Success = true };
        public static DisableResult AlreadyDone() => new() { Success = true, AlreadyDisabled = true };
        public static DisableResult Failed(string? errorMessage = null) => new() { Success = false, ErrorMessage = errorMessage };
    }

    public sealed class UpdateCheckResult
    {
        public bool HasNewerVersion { get; init; }

        public bool HasLocalInstall { get; init; }

        public bool Success { get; init; }

        public string? ErrorMessage { get; init; }

        public static UpdateCheckResult NewVersionAvailable() => new() { Success = true, HasNewerVersion = true, HasLocalInstall = true };
        public static UpdateCheckResult UpToDate() => new() { Success = true, HasNewerVersion = false, HasLocalInstall = true };
        public static UpdateCheckResult NoLocalInstall() => new() { Success = true, HasNewerVersion = true, HasLocalInstall = false };
        public static UpdateCheckResult Failed(string? errorMessage = null) => new() { Success = false, ErrorMessage = errorMessage };
    }
}

