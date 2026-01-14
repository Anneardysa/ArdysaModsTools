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
using System.Diagnostics.CodeAnalysis;

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents the result of an operation that can either succeed with a value or fail with an error.
    /// Provides a functional approach to error handling instead of exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    public readonly struct Result<T>
    {
        private readonly T? _value;
        private readonly string? _error;
        private readonly string? _errorCode;

        /// <summary>
        /// Gets whether the operation was successful.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets whether the operation failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the success value. Only valid when IsSuccess is true.
        /// </summary>
        public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value on a failed result.");

        /// <summary>
        /// Gets the error message. Only valid when IsFailure is true.
        /// </summary>
        public string Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on a successful result.");

        /// <summary>
        /// Gets the error code (optional). Only valid when IsFailure is true.
        /// </summary>
        public string? ErrorCode => IsFailure ? _errorCode : null;

        private Result(T value)
        {
            IsSuccess = true;
            _value = value;
            _error = null;
            _errorCode = null;
        }

        private Result(string error, string? errorCode = null)
        {
            IsSuccess = false;
            _value = default;
            _error = error;
            _errorCode = errorCode;
        }

        /// <summary>
        /// Creates a successful result with the specified value.
        /// </summary>
        public static Result<T> Success(T value) => new(value);

        /// <summary>
        /// Creates a failed result with the specified error message.
        /// </summary>
        public static Result<T> Failure(string error, string? errorCode = null) => new(error, errorCode);

        /// <summary>
        /// Implicitly converts a value to a successful result.
        /// </summary>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Gets the value or a default if the result is a failure.
        /// </summary>
        public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? _value! : defaultValue;

        /// <summary>
        /// Matches the result to one of two functions based on success or failure.
        /// </summary>
        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        {
            return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
        }

        /// <summary>
        /// Executes an action if the result is successful.
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess) action(_value!);
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure.
        /// </summary>
        public Result<T> OnFailure(Action<string> action)
        {
            if (IsFailure) action(_error!);
            return this;
        }

        public override string ToString()
        {
            return IsSuccess ? $"Success({_value})" : $"Failure({_error})";
        }
    }

    /// <summary>
    /// Represents the result of an operation that can either succeed or fail (no return value).
    /// </summary>
    public readonly struct Result
    {
        private readonly string? _error;
        private readonly string? _errorCode;

        /// <summary>
        /// Gets whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets whether the operation failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the error message. Only valid when IsFailure is true.
        /// </summary>
        public string Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on a successful result.");

        /// <summary>
        /// Gets the error code (optional). Only valid when IsFailure is true.
        /// </summary>
        public string? ErrorCode => IsFailure ? _errorCode : null;

        private Result(bool success, string? error = null, string? errorCode = null)
        {
            IsSuccess = success;
            _error = error;
            _errorCode = errorCode;
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Success() => new(true);

        /// <summary>
        /// Creates a failed result with the specified error message.
        /// </summary>
        public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);

        /// <summary>
        /// Creates a result from a boolean value.
        /// </summary>
        public static Result FromBool(bool success, string errorIfFailed = "Operation failed.") 
            => success ? Success() : Failure(errorIfFailed);

        /// <summary>
        /// Matches the result to one of two functions based on success or failure.
        /// </summary>
        public TResult Match<TResult>(Func<TResult> onSuccess, Func<string, TResult> onFailure)
        {
            return IsSuccess ? onSuccess() : onFailure(_error!);
        }

        /// <summary>
        /// Executes an action if the result is successful.
        /// </summary>
        public Result OnSuccess(Action action)
        {
            if (IsSuccess) action();
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure.
        /// </summary>
        public Result OnFailure(Action<string> action)
        {
            if (IsFailure) action(_error!);
            return this;
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"Failure({_error})";
        }
    }
}

