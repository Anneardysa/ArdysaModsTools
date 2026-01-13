using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Provides guard clauses for method argument validation.
    /// Fails fast with clear exception messages.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Throws if the value is null.
        /// </summary>
        public static T NotNull<T>(
            [NotNull] T? value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : class
        {
            if (value is null)
                throw new ArgumentNullException(paramName);
            return value;
        }

        /// <summary>
        /// Throws if the string is null or empty.
        /// </summary>
        public static string NotNullOrEmpty(
            [NotNull] string? value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be null or empty.", paramName);
            return value;
        }

        /// <summary>
        /// Throws if the string is null, empty, or whitespace.
        /// </summary>
        public static string NotNullOrWhiteSpace(
            [NotNull] string? value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
            return value;
        }

        /// <summary>
        /// Throws if the path doesn't exist as a file.
        /// </summary>
        public static string FileExists(
            [NotNull] string? path,
            [CallerArgumentExpression(nameof(path))] string? paramName = null)
        {
            NotNullOrWhiteSpace(path, paramName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}", path);
            return path;
        }

        /// <summary>
        /// Throws if the path doesn't exist as a directory.
        /// </summary>
        public static string DirectoryExists(
            [NotNull] string? path,
            [CallerArgumentExpression(nameof(path))] string? paramName = null)
        {
            NotNullOrWhiteSpace(path, paramName);
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            return path;
        }

        /// <summary>
        /// Throws if the condition is false.
        /// </summary>
        public static void Requires(
            [DoesNotReturnIf(false)] bool condition,
            string message = "Precondition failed.")
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Throws if the value is less than or equal to the minimum.
        /// </summary>
        public static int GreaterThan(
            int value,
            int minimum,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value <= minimum)
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than {minimum}.");
            return value;
        }

        /// <summary>
        /// Throws if the value is outside the specified range.
        /// </summary>
        public static int InRange(
            int value,
            int min,
            int max,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < min || value > max)
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}.");
            return value;
        }
    }
}
