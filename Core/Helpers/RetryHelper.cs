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
    public static class RetryHelper
    {
        public static class Defaults
        {
            public const int MaxAttempts = 3;
            public const int InitialDelayMs = 500;
            public const int MaxDelayMs = 5000;
            public const double BackoffMultiplier = 2.0;
        }

        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = Defaults.MaxAttempts,
            int initialDelayMs = Defaults.InitialDelayMs,
            int maxDelayMs = Defaults.MaxDelayMs,
            Func<Exception, bool>? shouldRetry = null,
            Action<int, Exception>? onRetry = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAttempts, 0);

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
                    throw;
                }
                catch (Exception ex) when (attempt < maxAttempts && shouldRetry(ex))
                {
                    onRetry?.Invoke(attempt, ex);

                    await Task.Delay(Math.Min(delayMs, maxDelayMs), ct).ConfigureAwait(false);
                    delayMs = (int)(delayMs * Defaults.BackoffMultiplier);
                }
            }
        }

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

        public static bool IsTransientException(Exception ex)
        {
            return ex switch
            {
                System.Net.Http.HttpRequestException => true,
                System.Net.WebException => true,
                System.IO.IOException when ex.Message.Contains("network") => true,
                TaskCanceledException tce when !tce.CancellationToken.IsCancellationRequested => true,
                TimeoutException => true,
                _ => false
            };
        }

        public static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                System.Net.HttpStatusCode.RequestTimeout => true,
                System.Net.HttpStatusCode.TooManyRequests => true,
                System.Net.HttpStatusCode.InternalServerError => true,
                System.Net.HttpStatusCode.BadGateway => true,
                System.Net.HttpStatusCode.ServiceUnavailable => true,
                System.Net.HttpStatusCode.GatewayTimeout => true,
                _ => false
            };
        }
    }
}

