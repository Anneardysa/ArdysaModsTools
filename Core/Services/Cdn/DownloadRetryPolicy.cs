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
    /// <summary>
    /// Raised by a download operation for a transient HTTP condition (5xx, 429, 408)
    /// that should be retried against the same CDN. Optionally carries a server-supplied
    /// <c>Retry-After</c> wait. Distinct from a permanent failure (404/416) which must
    /// fall through to the next CDN without retrying.
    /// </summary>
    public sealed class TransientDownloadException : Exception
    {
        /// <summary>Server-requested wait before the next attempt, if provided.</summary>
        public TimeSpan? RetryAfter { get; }

        public TransientDownloadException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
            : base(message, inner)
        {
            RetryAfter = retryAfter;
        }
    }

    /// <summary>
    /// Stateless retry policy shared by the CDN download paths. Encodes transient-vs-permanent
    /// error classification, exponential backoff with jitter (honouring a capped
    /// <c>Retry-After</c>), and a bounded retry loop.
    ///
    /// Retries the SAME endpoint for transient errors; permanent errors (and outer
    /// cancellation) propagate immediately so the caller can fall to the next CDN.
    ///
    /// Distinct from the general-purpose <see cref="RetryHelper"/>: this policy honours a
    /// server <c>Retry-After</c> for the backoff delay and classifies an
    /// <see cref="HttpRequestException"/> by its <see cref="HttpRequestException.StatusCode"/>
    /// so a permanent 404 falls through to the next CDN instead of being retried. Status-code
    /// classification is delegated to <see cref="RetryHelper.IsTransientStatusCode"/>.
    /// </summary>
    // [AMT:OPUS] Governs retry/backoff for the entire download pipeline (ADR-0003 / ADR-0009).
    // Changing classification or backoff affects every asset download — analyze transient vs
    // permanent semantics and cancellation propagation before modifying.
    public static class DownloadRetryPolicy
    {
        /// <summary>
        /// True if an exception represents a transient failure worth retrying.
        /// Network/socket/TLS errors (<see cref="HttpRequestException"/> with no status),
        /// timeouts, mid-stream <see cref="IOException"/>, and <see cref="TransientDownloadException"/>
        /// are transient. An <see cref="HttpRequestException"/> carrying a status is classified
        /// by that status (so a 404 is permanent, a 503 is transient).
        /// </summary>
        public static bool IsTransientException(Exception ex)
        {
            switch (ex)
            {
                case TransientDownloadException:
                case TimeoutException:
                case IOException:
                    return true;
                case HttpRequestException hre:
                    // No status => network/socket/DNS/TLS error => transient.
                    return hre.StatusCode is null || RetryHelper.IsTransientStatusCode(hre.StatusCode.Value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extract a usable <c>Retry-After</c> wait from a response, or null if absent.
        /// Handles both delta-seconds and HTTP-date forms.
        /// </summary>
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

        /// <summary>
        /// Compute the backoff delay before the next attempt. A server-supplied
        /// <paramref name="retryAfter"/> takes precedence (capped at
        /// <see cref="CdnConfig.MaxRetryAfterSeconds"/>); otherwise an exponential backoff
        /// (<see cref="CdnConfig.RetryBaseDelayMs"/> · 2^(attempt-1)) capped at
        /// <see cref="CdnConfig.RetryMaxDelayMs"/> with up to +25% jitter.
        /// </summary>
        /// <param name="attempt">1-based attempt number that just failed.</param>
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

        /// <summary>
        /// Execute <paramref name="operation"/>, retrying transient failures up to
        /// <see cref="CdnConfig.MaxRetryPerCdn"/> attempts with backoff. Permanent failures
        /// and outer cancellation propagate immediately.
        /// </summary>
        /// <typeparam name="T">Operation result type.</typeparam>
        /// <param name="operation">The download attempt; receives the linked cancellation token.</param>
        /// <param name="log">Optional status logger for retry messages.</param>
        /// <param name="ct">Caller cancellation token. Cancellation is never retried.</param>
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
                    throw; // Outer cancellation — never retry.
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
                // Non-transient exceptions are not caught here — they propagate to the caller,
                // which falls through to the next CDN.
            }

            throw lastTransient ?? new InvalidOperationException("Retry policy exhausted with no recorded error.");
        }
    }
}
