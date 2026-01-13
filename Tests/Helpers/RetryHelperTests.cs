using System;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class RetryHelperTests
    {
        [Test]
        public async Task ExecuteAsync_SucceedsOnFirstAttempt()
        {
            // Arrange
            int callCount = 0;

            // Act
            var result = await RetryHelper.ExecuteAsync(async () =>
            {
                callCount++;
                await Task.Delay(1);
                return "success";
            });

            // Assert
            Assert.That(result, Is.EqualTo("success"));
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_RetriesOnTransientFailure()
        {
            // Arrange
            int callCount = 0;

            // Act
            var result = await RetryHelper.ExecuteAsync(async () =>
            {
                callCount++;
                if (callCount < 3)
                    throw new System.Net.Http.HttpRequestException("Simulated failure");
                
                await Task.Delay(1);
                return "success after retry";
            }, maxAttempts: 3, initialDelayMs: 10);

            // Assert
            Assert.That(result, Is.EqualTo("success after retry"));
            Assert.That(callCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ExecuteAsync_FailsAfterMaxAttempts()
        {
            // Arrange
            int callCount = 0;

            // Act & Assert
            Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(async () =>
            {
                await RetryHelper.ExecuteAsync<string>(async () =>
                {
                    callCount++;
                    await Task.Delay(1);
                    throw new System.Net.Http.HttpRequestException("Persistent failure");
                }, maxAttempts: 3, initialDelayMs: 10);
            });

            await Task.Delay(100); // Allow retries to complete
            Assert.That(callCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ExecuteAsync_DoesNotRetryNonTransientExceptions()
        {
            // Arrange
            int callCount = 0;

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await RetryHelper.ExecuteAsync<string>(async () =>
                {
                    callCount++;
                    await Task.Delay(1);
                    throw new ArgumentException("Non-transient");
                }, maxAttempts: 3, initialDelayMs: 10);
            });

            await Task.Delay(50);
            // Should only be called once since ArgumentException is not transient
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_CallsOnRetryCallback()
        {
            // Arrange
            int retryCount = 0;
            int callCount = 0;

            // Act
            await RetryHelper.ExecuteAsync(async () =>
            {
                callCount++;
                if (callCount < 3)
                    throw new System.Net.Http.HttpRequestException("Fail");
                
                await Task.Delay(1);
                return true;
            },
            maxAttempts: 3,
            initialDelayMs: 10,
            onRetry: (attempt, ex) => retryCount++);

            // Assert
            Assert.That(retryCount, Is.EqualTo(2)); // Called on attempt 1 and 2 failures
        }

        [Test]
        public void ExecuteAsync_RespectsCancellation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await RetryHelper.ExecuteAsync(async () =>
                {
                    await Task.Delay(100);
                    return "never";
                }, ct: cts.Token);
            });
        }

        [TestCase(System.Net.HttpStatusCode.RequestTimeout, true)]
        [TestCase(System.Net.HttpStatusCode.TooManyRequests, true)]
        [TestCase(System.Net.HttpStatusCode.InternalServerError, true)]
        [TestCase(System.Net.HttpStatusCode.BadGateway, true)]
        [TestCase(System.Net.HttpStatusCode.ServiceUnavailable, true)]
        [TestCase(System.Net.HttpStatusCode.GatewayTimeout, true)]
        [TestCase(System.Net.HttpStatusCode.OK, false)]
        [TestCase(System.Net.HttpStatusCode.NotFound, false)]
        [TestCase(System.Net.HttpStatusCode.BadRequest, false)]
        public void IsTransientStatusCode_ReturnsCorrectResult(System.Net.HttpStatusCode code, bool expected)
        {
            Assert.That(RetryHelper.IsTransientStatusCode(code), Is.EqualTo(expected));
        }
    }
}
