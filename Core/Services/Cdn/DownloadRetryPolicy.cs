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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Core.Services.Cdn
{
    public sealed class TransientDownloadException : Exception
    {
        public TimeSpan? RetryAfter { get; }

        public TransientDownloadException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
            : base(message, inner)
        {
            RetryAfter = retryAfter;
        }
    }

    public static class DownloadRetryPolicy
    {
        public static bool IsTransientException(Exception ex)
        {
            switch (ex)
            {
                case TransientDownloadException:
                case TimeoutException:
                case IOException:
                    return true;
                case HttpRequestException hre:
                    return hre.StatusCode is null || RetryHelper.IsTransientStatusCode(hre.StatusCode.Value);
                default:
                    return false;
            }
        }

        public static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var ra = response.Headers.RetryAfter;
            if (ra == null) return null;

            if (ra.Delta.HasValue)
                return ra.Delta.Value < TimeSpan.Zero ? TimeSpan.Zero : ra.Delta.Value;

            if (ra.Date.HasValue)
            {
                var diff = ra.Date.Value - DateTimeOffset.UtcNow;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            return null;
        }

        public static TimeSpan GetBackoffDelay(int attempt, TimeSpan? retryAfter = null)
        {
            if (retryAfter.HasValue)
            {
                double seconds = Math.Min(retryAfter.Value.TotalSeconds, CdnConfig.MaxRetryAfterSeconds);
                return TimeSpan.FromSeconds(Math.Max(0, seconds));
            }

            double exp = CdnConfig.RetryBaseDelayMs * Math.Pow(2, Math.Max(0, attempt - 1));
            double capped = Math.Min(exp, CdnConfig.RetryMaxDelayMs);
            double jitter = capped * 0.25 * Random.Shared.NextDouble();
            return TimeSpan.FromMilliseconds(capped + jitter);
        }

        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            Action<string>? log = null,
            CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int maxAttempts = Math.Max(1, CdnConfig.MaxRetryPerCdn);
            Exception? lastTransient = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await operation(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsTransientException(ex))
                {
                    lastTransient = ex;
                    if (attempt >= maxAttempts)
                        break;

                    TimeSpan? retryAfter = (ex as TransientDownloadException)?.RetryAfter;
                    TimeSpan delay = GetBackoffDelay(attempt, retryAfter);
                    log?.Invoke($"Attempt {attempt} failed ({ex.Message}); retrying in {delay.TotalSeconds:F1}s...");
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }

            throw lastTransient ?? new InvalidOperationException("Retry policy exhausted with no recorded error.");
        }
    }
}
