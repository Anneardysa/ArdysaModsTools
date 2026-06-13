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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Tests.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    /// <summary>
    /// Tests for GitHubTokenHandler — token attachment scoping and the anonymous retry on a
    /// rejected token (401/403). Drives the token via the GITHUB_TOKEN environment variable
    /// that <c>EnvironmentConfig.GitHubToken</c> reads.
    /// </summary>
    [TestFixture]
    public class GitHubTokenHandlerTests
    {
        private string? _originalToken;

        [SetUp]
        public void Setup()
        {
            _originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "fake-token-123");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", _originalToken);
        }

        private static HttpResponseMessage Respond(HttpStatusCode code) => new HttpResponseMessage(code);

        [Test]
        public async Task GitHubHost_TokenRejected_RetriesAnonymouslyAndSucceeds()
        {
            // First call (with token) -> 401; anonymous retry -> 200.
            var fake = new FakeHttpMessageHandler((req, callIndex) =>
                Respond(callIndex == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));

            using var invoker = new HttpMessageInvoker(new GitHubTokenHandler(fake));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://raw.githubusercontent.com/Anneardysa/ModsPack/main/Assets/heroes.json");

            var response = await invoker.SendAsync(request, CancellationToken.None);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(fake.CallCount, Is.EqualTo(2), "should retry once anonymously");
            Assert.That(fake.Requests[0].Headers.Authorization, Is.Not.Null, "first attempt carries the token");
            Assert.That(fake.Requests[1].Headers.Authorization, Is.Null, "retry drops the Authorization header");
        }

        [Test]
        public async Task GitHubHost_Succeeds_NoRetry()
        {
            var fake = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK));

            using var invoker = new HttpMessageInvoker(new GitHubTokenHandler(fake));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://raw.githubusercontent.com/Anneardysa/ModsPack/main/Assets/heroes.json");

            var response = await invoker.SendAsync(request, CancellationToken.None);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(fake.CallCount, Is.EqualTo(1));
            Assert.That(fake.Requests[0].Headers.Authorization, Is.Not.Null, "token attached for github host");
        }

        [Test]
        public async Task NonGitHubHost_NeverAttachesToken_AndDoesNotRetry()
        {
            // A non-github host returning 401 must NOT trigger the anonymous retry, and must
            // never receive the token in the first place.
            var fake = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.Unauthorized));

            using var invoker = new HttpMessageInvoker(new GitHubTokenHandler(fake));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://cdn.ardysamods.my.id/Assets/heroes.json");

            var response = await invoker.SendAsync(request, CancellationToken.None);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(fake.CallCount, Is.EqualTo(1), "no token => no anonymous retry");
            Assert.That(fake.Requests[0].Headers.Authorization, Is.Null, "token must not leak to third-party CDNs");
        }
    }
}
