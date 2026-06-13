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
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for DownloadRetryPolicy — transient/permanent classification, backoff with a
    /// capped Retry-After, and the bounded retry loop.
    /// </summary>
    [TestFixture]
    public class DownloadRetryPolicyTests
    {
        #region IsTransientException

        [Test]
        public void IsTransientException_TransientDownloadException_IsTransient()
        {
            Assert.That(DownloadRetryPolicy.IsTransientException(new TransientDownloadException("x")), Is.True);
        }

        [Test]
        public void IsTransientException_TimeoutAndIo_AreTransient()
        {
            Assert.That(DownloadRetryPolicy.IsTransientException(new TimeoutException()), Is.True);
            Assert.That(DownloadRetryPolicy.IsTransientException(new IOException()), Is.True);
        }

        [Test]
        public void IsTransientException_HttpRequestExceptionWithoutStatus_IsTransient()
        {
            // No status => network/socket/DNS/TLS error.
            Assert.That(DownloadRetryPolicy.IsTransientException(new HttpRequestException("socket")), Is.True);
        }

        [Test]
        public void IsTransientException_HttpRequestExceptionWith404_IsPermanent()
        {
            var ex = new HttpRequestException("not found", null, System.Net.HttpStatusCode.NotFound);
            Assert.That(DownloadRetryPolicy.IsTransientException(ex), Is.False);
        }

        [Test]
        public void IsTransientException_HttpRequestExceptionWith503_IsTransient()
        {
            var ex = new HttpRequestException("unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
            Assert.That(DownloadRetryPolicy.IsTransientException(ex), Is.True);
        }

        [Test]
        public void IsTransientException_UnrelatedException_IsPermanent()
        {
            Assert.That(DownloadRetryPolicy.IsTransientException(new ArgumentException()), Is.False);
        }

        #endregion

        #region GetRetryAfter

        [Test]
        public void GetRetryAfter_WithDeltaHeader_ReturnsDelta()
        {
            using var response = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(4));

            var result = DownloadRetryPolicy.GetRetryAfter(response);

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(4)));
        }

        [Test]
        public void GetRetryAfter_WithoutHeader_ReturnsNull()
        {
            using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            Assert.That(DownloadRetryPolicy.GetRetryAfter(response), Is.Null);
        }

        #endregion

        #region GetBackoffDelay

        [Test]
        public void GetBackoffDelay_HonoursRetryAfter_CappedAtMax()
        {
            var huge = TimeSpan.FromSeconds(CdnConfig.MaxRetryAfterSeconds + 60);
            var delay = DownloadRetryPolicy.GetBackoffDelay(1, huge);

            Assert.That(delay.TotalSeconds, Is.EqualTo(CdnConfig.MaxRetryAfterSeconds).Within(0.001));
        }

        [Test]
        public void GetBackoffDelay_Exponential_StaysWithinCeilingPlusJitter()
        {
            // Large attempt number => exponential term saturates at the ceiling; jitter adds <=25%.
            var delay = DownloadRetryPolicy.GetBackoffDelay(10);

            double maxAllowed = CdnConfig.RetryMaxDelayMs * 1.25;
            Assert.That(delay.TotalMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(delay.TotalMilliseconds, Is.LessThanOrEqualTo(maxAllowed));
        }

        #endregion

        #region ExecuteWithRetryAsync

        [Test]
        public async Task ExecuteWithRetryAsync_SucceedsFirstAttempt_CallsOnce()
        {
            int calls = 0;
            var result = await DownloadRetryPolicy.ExecuteWithRetryAsync(_ =>
            {
                calls++;
                return Task.FromResult("ok");
            });

            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteWithRetryAsync_TransientThenSuccess_Retries()
        {
            int calls = 0;
            var result = await DownloadRetryPolicy.ExecuteWithRetryAsync<string>(_ =>
            {
                calls++;
                if (calls < 2)
                    throw new TransientDownloadException("flaky");
                return Task.FromResult("recovered");
            });

            Assert.That(result, Is.EqualTo("recovered"));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteWithRetryAsync_PersistentTransient_ThrowsAfterMaxAttempts()
        {
            int calls = 0;
            Assert.ThrowsAsync<TransientDownloadException>(async () =>
            {
                await DownloadRetryPolicy.ExecuteWithRetryAsync<string>(_ =>
                {
                    calls++;
                    throw new TransientDownloadException("always");
                });
            });

            Assert.That(calls, Is.EqualTo(CdnConfig.MaxRetryPerCdn));
        }

        [Test]
        public void ExecuteWithRetryAsync_PermanentError_DoesNotRetry()
        {
            int calls = 0;
            Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await DownloadRetryPolicy.ExecuteWithRetryAsync<string>(_ =>
                {
                    calls++;
                    throw new HttpRequestException("gone", null, System.Net.HttpStatusCode.NotFound);
                });
            });

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteWithRetryAsync_CancelledToken_ThrowsWithoutCalling()
        {
            int calls = 0;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await DownloadRetryPolicy.ExecuteWithRetryAsync<string>(_ =>
                {
                    calls++;
                    return Task.FromResult("never");
                }, log: null, ct: cts.Token);
            });

            Assert.That(calls, Is.EqualTo(0));
        }

        #endregion
    }
}
