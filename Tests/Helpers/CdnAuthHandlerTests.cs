/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class CdnAuthHandlerTests
    {
        private static HttpClient Build(FakeHttpMessageHandler inner) =>
            new(new CdnAuthHandler(inner));

        private static FakeHttpMessageHandler Ok() =>
            new((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) });

        private static string? AuthHeader(HttpRequestMessage request) =>
            request.Headers.TryGetValues(CdnRequestSigner.HeaderName, out var values)
                ? values.FirstOrDefault()
                : null;

        [TestCase("https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip")]
        [TestCase("https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip.001")]
        [TestCase("https://cdn2.ardysamods.my.id/dataset/Drow_Ranger/base_arcana_1/abc.txt")]
        [TestCase("https://cdn.ardysamods.my.id/Assets/misc/courier/fancy.zip")]
        public async Task ProtectedCdnPath_IsSigned(string url)
        {
            var handler = Ok();
            using var client = Build(handler);

            await client.GetAsync(url);

            string? header = AuthHeader(handler.Requests[0]);
            Assert.That(header, Is.Not.Null, $"no signature sent for {url}");
            Assert.That(Regex.IsMatch(header!, "^1\\.[0-9]+\\.[0-9a-f]{64}$"), Is.True,
                "header shape must match what the Workers parse");
        }

        [TestCase("https://cdn.ardysamods.my.id/Assets/image/Abaddon/blightfall.png")]
        [TestCase("https://cdn.ardysamods.my.id/Assets/misc/courier/fancy.png")]
        [TestCase("https://cdn.ardysamods.my.id/Assets/heroes.json")]
        [TestCase("https://cdn.ardysamods.my.id/config/feature_access.json")]
        [TestCase("https://cdn.ardysamods.my.id/releases/releases.json")]
        [TestCase("https://cdn.ardysamods.my.id/")]
        public async Task OpenCdnPath_IsNotSigned(string url)
        {
            var handler = Ok();
            using var client = Build(handler);

            await client.GetAsync(url);

            Assert.That(AuthHeader(handler.Requests[0]), Is.Null,
                "signing an open path costs nothing but hands the header to anyone sniffing an unguarded route");
        }

        [TestCase("https://api.github.com/repos/Anneardysa/ModsPack/releases")]
        [TestCase("https://raw.githubusercontent.com/Anneardysa/ModsPack/main/Assets/models/x.zip")]
        [TestCase("https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/abaddon.png")]
        [TestCase("https://ardysamods.my.id/updates.json")]
        [TestCase("https://evil.example.com/Assets/models/x.zip")]
        public async Task ThirdPartyHost_NeverSeesTheSignature(string url)
        {
            var handler = Ok();
            using var client = Build(handler);

            await client.GetAsync(url);

            Assert.That(AuthHeader(handler.Requests[0]), Is.Null,
                "the signing key must never be presented to a host outside our own CDN chain");
        }

        [Test]
        public async Task Unauthorized_WithServerClock_RetriesOnceWithACorrectedExpiry()
        {
            long serverNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Random.Shared.Next(50_000, 500_000);

            var handler = new FakeHttpMessageHandler((_, call) =>
            {
                if (call == 0)
                {
                    var denied = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    denied.Headers.TryAddWithoutValidation(CdnRequestSigner.ServerTimeHeaderName, serverNow.ToString());
                    return denied;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
            });

            using var client = Build(handler);
            var response = await client.GetAsync("https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(handler.CallCount, Is.EqualTo(2), "expected exactly one corrected retry");

            long retriedExpiry = long.Parse(AuthHeader(handler.Requests[1])!.Split('.')[1]);
            Assert.That(retriedExpiry, Is.GreaterThan(serverNow),
                "the retry must be signed against the server's clock, not the local one");
        }

        [Test]
        public async Task Unauthorized_WithoutServerClock_DoesNotRetry()
        {
            var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            using var client = Build(handler);

            var response = await client.GetAsync("https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(handler.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Unauthorized_WhenTheCorrectedRetryAlsoFails_TheClockOffsetIsRolledBack()
        {
            long absurdServerNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 315_360_000;

            var handler = new FakeHttpMessageHandler((_, call) =>
            {
                var denied = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                if (call == 0)
                    denied.Headers.TryAddWithoutValidation(CdnRequestSigner.ServerTimeHeaderName, absurdServerNow.ToString());
                return denied;
            });

            using var client = Build(handler);
            const string url = "https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip";

            var first = await client.GetAsync(url);
            var second = await client.GetAsync(url);

            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(handler.CallCount, Is.EqualTo(3),
                "expected one corrected retry for the first request and no retry for the second");

            long original = SignedExpiry(handler.Requests[0]);
            long corrected = SignedExpiry(handler.Requests[1]);
            long afterRollback = SignedExpiry(handler.Requests[2]);

            Assert.That(corrected - original, Is.GreaterThan(300_000_000),
                "the retry should have been signed against the server's absurd clock");
            Assert.That(afterRollback, Is.EqualTo(original).Within(5),
                "the failed correction must not survive into the next request");
        }

        private static long SignedExpiry(HttpRequestMessage request) =>
            long.Parse(AuthHeader(request)!.Split('.')[1]);

        [Test]
        public async Task Unauthorized_OnAnUnsignedRequest_IsPassedStraightThrough()
        {
            var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            using var client = Build(handler);

            await client.GetAsync("https://api.github.com/user");

            Assert.That(handler.CallCount, Is.EqualTo(1));
        }
    }
}
