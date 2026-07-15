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
using System.Collections.Generic;
using System.Linq;

namespace ArdysaModsTools.Core.Models
{
    public class OperationResult
    {
        public bool Success { get; init; }
        
        public string? Message { get; init; }
        
        public Exception? Exception { get; init; }

        public string? ErrorCode { get; init; }

        public List<(string name, string reason)>? FailedItems { get; init; }
        
        public List<string>? Warnings { get; init; }
        
        public int SuccessCount { get; init; }

        public IReadOnlyList<string>? LogLines { get; init; }

        public bool RequiresConflictResolution { get; init; }

        public bool WasCanceled { get; init; }

        public IReadOnlyList<ModConflict>? Conflicts { get; init; }

        #region Static Factory Methods

        public static OperationResult Ok(string? message = null) => 
            new() { Success = true, Message = message };

        public static OperationResult Ok(int successCount, string? message = null) => 
            new() { Success = true, SuccessCount = successCount, Message = message };

        public static OperationResult Fail(string message) => 
            new() { Success = false, Message = message };

        public static OperationResult Fail(Exception ex) => 
            new() { Success = false, Message = ex.Message, Exception = ex };

        public static OperationResult Fail(string message, List<(string name, string reason)> failedItems) => 
            new() { Success = false, Message = message, FailedItems = failedItems };

        public static OperationResult Canceled(string? message = null) =>
            new() { Success = false, WasCanceled = true, Message = message ?? "Canceled by user." };

        public static OperationResult NeedsConflictResolution(IEnumerable<ModConflict> conflicts, string? message = null) =>
            new()
            {
                Success = false,
                RequiresConflictResolution = true,
                Conflicts = conflicts.ToList().AsReadOnly(),
                Message = message ?? "Critical conflicts require user resolution."
            };

        #endregion
    }

    public class OperationResult<T>
    {
        public bool Success { get; init; }
        
        public T? Data { get; init; }
        
        public string? ErrorMessage { get; init; }
        
        public Exception? Exception { get; init; }

        public static implicit operator bool(OperationResult<T> result) => result.Success;

        #region Static Factory Methods

        public static OperationResult<T> Ok(T data) => 
            new() { Success = true, Data = data };

        public static OperationResult<T> Fail(string error) => 
            new() { Success = false, ErrorMessage = error };

        public static OperationResult<T> Fail(Exception ex) => 
            new() { Success = false, ErrorMessage = ex.Message, Exception = ex };

        public static OperationResult<T> Fail(string error, Exception ex) => 
            new() { Success = false, ErrorMessage = error, Exception = ex };

        #endregion
    }

    public static class OperationResultExtensions
    {
        public static OperationResult<TNew> Map<T, TNew>(
            this OperationResult<T> result, 
            Func<T, TNew> mapper) =>
            result.Success && result.Data != null
                ? OperationResult<TNew>.Ok(mapper(result.Data))
                : OperationResult<TNew>.Fail(result.ErrorMessage ?? "Unknown error");
    }
}

