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
    /// <summary>
    /// Represents a success/failure result returned by services and controllers.
    /// Use static factory methods for cleaner instantiation.
    /// </summary>
    public class OperationResult
    {
        /// <summary>Whether the operation completed successfully.</summary>
        public bool Success { get; init; }
        
        /// <summary>Human-readable message describing the result.</summary>
        public string? Message { get; init; }
        
        /// <summary>Exception that caused failure, if any.</summary>
        public Exception? Exception { get; init; }
        
        /// <summary>
        /// List of failed items with reasons (for batch operations).
        /// Each tuple contains (itemName, failureReason).
        /// </summary>
        public List<(string name, string reason)>? FailedItems { get; init; }
        
        /// <summary>Number of items that succeeded (for batch operations).</summary>
        public int SuccessCount { get; init; }

        /// <summary>
        /// If true, the operation failed because of unresolved conflicts
        /// that need user intervention. Check Conflicts property for details.
        /// </summary>
        public bool RequiresConflictResolution { get; init; }

        /// <summary>
        /// List of conflicts that require user resolution.
        /// Populated when RequiresConflictResolution is true.
        /// </summary>
        public IReadOnlyList<ModConflict>? Conflicts { get; init; }

        #region Static Factory Methods

        /// <summary>Create a successful result.</summary>
        public static OperationResult Ok(string? message = null) => 
            new() { Success = true, Message = message };

        /// <summary>Create a successful batch result.</summary>
        public static OperationResult Ok(int successCount, string? message = null) => 
            new() { Success = true, SuccessCount = successCount, Message = message };

        /// <summary>Create a failed result with error message.</summary>
        public static OperationResult Fail(string message) => 
            new() { Success = false, Message = message };

        /// <summary>Create a failed result with exception.</summary>
        public static OperationResult Fail(Exception ex) => 
            new() { Success = false, Message = ex.Message, Exception = ex };

        /// <summary>Create a failed batch result with failed items.</summary>
        public static OperationResult Fail(string message, List<(string name, string reason)> failedItems) => 
            new() { Success = false, Message = message, FailedItems = failedItems };

        /// <summary>Create a result indicating conflicts need user resolution.</summary>
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

    /// <summary>
    /// Generic result type that can carry typed data on success.
    /// Enables clean error handling without exceptions for expected failures.
    /// </summary>
    /// <typeparam name="T">Type of data returned on success.</typeparam>
    /// <example>
    /// <code>
    /// // Usage pattern:
    /// var result = await service.DoSomethingAsync();
    /// if (!result.Success)
    /// {
    ///     logger.Log($"Error: {result.ErrorMessage}");
    ///     return;
    /// }
    /// var data = result.Data; // Use the typed data
    /// </code>
    /// </example>
    public class OperationResult<T>
    {
        /// <summary>Whether the operation completed successfully.</summary>
        public bool Success { get; init; }
        
        /// <summary>The data returned on success. Null on failure.</summary>
        public T? Data { get; init; }
        
        /// <summary>Error message on failure.</summary>
        public string? ErrorMessage { get; init; }
        
        /// <summary>Exception that caused failure, if any.</summary>
        public Exception? Exception { get; init; }

        /// <summary>Implicit bool conversion for if checks.</summary>
        public static implicit operator bool(OperationResult<T> result) => result.Success;

        #region Static Factory Methods

        /// <summary>Create a successful result with data.</summary>
        public static OperationResult<T> Ok(T data) => 
            new() { Success = true, Data = data };

        /// <summary>Create a failed result with error message.</summary>
        public static OperationResult<T> Fail(string error) => 
            new() { Success = false, ErrorMessage = error };

        /// <summary>Create a failed result with exception.</summary>
        public static OperationResult<T> Fail(Exception ex) => 
            new() { Success = false, ErrorMessage = ex.Message, Exception = ex };

        /// <summary>Create a failed result with error message and exception.</summary>
        public static OperationResult<T> Fail(string error, Exception ex) => 
            new() { Success = false, ErrorMessage = error, Exception = ex };

        #endregion
    }

    /// <summary>
    /// Extension methods for OperationResult types.
    /// </summary>
    public static class OperationResultExtensions
    {
        /// <summary>
        /// Converts a generic result to non-generic, preserving error info.
        /// </summary>
        public static OperationResult ToNonGeneric<T>(this OperationResult<T> result) =>
            new()
            {
                Success = result.Success,
                Message = result.ErrorMessage,
                Exception = result.Exception
            };

        /// <summary>
        /// Maps the data of a successful result to a new type.
        /// </summary>
        public static OperationResult<TNew> Map<T, TNew>(
            this OperationResult<T> result, 
            Func<T, TNew> mapper) =>
            result.Success && result.Data != null
                ? OperationResult<TNew>.Ok(mapper(result.Data))
                : OperationResult<TNew>.Fail(result.ErrorMessage ?? "Unknown error");
    }
}

