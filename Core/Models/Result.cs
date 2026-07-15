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
    public readonly struct Result<T>
    {
        private readonly T? _value;
        private readonly string? _error;
        private readonly string? _errorCode;

        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value on a failed result.");

        public string Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on a successful result.");

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

        public static Result<T> Success(T value) => new(value);

        public static Result<T> Failure(string error, string? errorCode = null) => new(error, errorCode);

        public static implicit operator Result<T>(T value) => Success(value);

        public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? _value! : defaultValue;

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        {
            return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
        }

        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess) action(_value!);
            return this;
        }

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

    public readonly struct Result
    {
        private readonly string? _error;
        private readonly string? _errorCode;

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public string Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on a successful result.");

        public string? ErrorCode => IsFailure ? _errorCode : null;

        private Result(bool success, string? error = null, string? errorCode = null)
        {
            IsSuccess = success;
            _error = error;
            _errorCode = errorCode;
        }

        public static Result Success() => new(true);

        public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);

        public static Result FromBool(bool success, string errorIfFailed = "Operation failed.") 
            => success ? Success() : Failure(errorIfFailed);

        public TResult Match<TResult>(Func<TResult> onSuccess, Func<string, TResult> onFailure)
        {
            return IsSuccess ? onSuccess() : onFailure(_error!);
        }

        public Result OnSuccess(Action action)
        {
            if (IsSuccess) action();
            return this;
        }

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

