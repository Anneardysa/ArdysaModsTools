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
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Provides retry logic with exponential backoff for network operations.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Default configuration for retry operations.
        /// </summary>
        public static class Defaults
        {
            public const int MaxAttempts = 3;
            public const int InitialDelayMs = 500;
            public const int MaxDelayMs = 5000;
            public const double BackoffMultiplier = 2.0;
        }

        /// <summary>
        /// Executes an async operation with retry logic and exponential backoff.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="maxAttempts">Maximum number of attempts (default: 3).</param>
        /// <param name="initialDelayMs">Initial delay in milliseconds (default: 500).</param>
        /// <param name="maxDelayMs">Maximum delay cap in milliseconds (default: 5000).</param>
        /// <param name="shouldRetry">Predicate to determine if exception is retryable (default: all).</param>
        /// <param name="onRetry">Optional callback on each retry attempt.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = Defaults.MaxAttempts,
            int initialDelayMs = Defaults.InitialDelayMs,
            int maxDelayMs = Defaults.MaxDelayMs,
            Func<Exception, bool>? shouldRetry = null,
            Action<int, Exception>? onRetry = null,
            CancellationToken ct = default)
        {
            Guard.NotNull(operation);
            Guard.GreaterThan(maxAttempts, 0);

            shouldRetry ??= IsTransientException;
            int attempt = 0;
            int delayMs = initialDelayMs;

            while (true)
            {
                attempt++;
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellation
                }
                catch (Exception ex) when (attempt < maxAttempts && shouldRetry(ex))
                {
                    onRetry?.Invoke(attempt, ex);

                    // Wait with exponential backoff
                    await Task.Delay(Math.Min(delayMs, maxDelayMs), ct).ConfigureAwait(false);
                    delayMs = (int)(delayMs * Defaults.BackoffMultiplier);
                }
            }
        }

        /// <summary>
        /// Executes an async operation with retry logic (no return value).
        /// </summary>
        public static async Task ExecuteAsync(
            Func<Task> operation,
            int maxAttempts = Defaults.MaxAttempts,
            int initialDelayMs = Defaults.InitialDelayMs,
            int maxDelayMs = Defaults.MaxDelayMs,
            Func<Exception, bool>? shouldRetry = null,
            Action<int, Exception>? onRetry = null,
            CancellationToken ct = default)
        {
            await ExecuteAsync(
                async () => { await operation().ConfigureAwait(false); return true; },
                maxAttempts,
                initialDelayMs,
                maxDelayMs,
                shouldRetry,
                onRetry,
                ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if an exception is transient and should trigger a retry.
        /// </summary>
        public static bool IsTransientException(Exception ex)
        {
            return ex switch
            {
                System.Net.Http.HttpRequestException => true,
                System.Net.WebException => true,
                System.IO.IOException when ex.Message.Contains("network") => true,
                TaskCanceledException tce when !tce.CancellationToken.IsCancellationRequested => true, // Timeout
                TimeoutException => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an HTTP status code indicates a transient failure.
        /// </summary>
        public static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                System.Net.HttpStatusCode.RequestTimeout => true,      // 408
                System.Net.HttpStatusCode.TooManyRequests => true,     // 429
                System.Net.HttpStatusCode.InternalServerError => true, // 500
                System.Net.HttpStatusCode.BadGateway => true,          // 502
                System.Net.HttpStatusCode.ServiceUnavailable => true,  // 503
                System.Net.HttpStatusCode.GatewayTimeout => true,      // 504
                _ => false
            };
        }
    }
}

