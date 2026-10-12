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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Tests.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnProbeTests
    {
        [Test]
        public async Task ProbeAsync_SendsGetWithZeroByteRange_NotHead()
        {
            var handler = new FakeHttpMessageHandler((req, _) =>
                new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(new byte[] { 0x1 })
                    {
                        Headers = { ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, 1178116) }
                    }
                });
            using var client = new HttpClient(handler);

            await CdnProbe.ProbeAsync(client, "https://cdn.ardysamods.my.id/Assets/models/Anti-Mage/defiant.zip", default);

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            var sent = handler.Requests[0];
            Assert.That(sent.Method, Is.EqualTo(HttpMethod.Get), "must probe with GET, never HEAD");
            Assert.That(sent.Headers.Range, Is.Not.Null);
            Assert.That(sent.Headers.Range!.Ranges, Has.Count.EqualTo(1));
            Assert.That(sent.Headers.Range.Ranges.First().From, Is.EqualTo(0));
            Assert.That(sent.Headers.Range.Ranges.First().To, Is.EqualTo(0));
        }

        [Test]
        public async Task ProbeAsync_PartialContentResponse_ParsesTotalSizeFromContentRange()
        {
            var handler = new FakeHttpMessageHandler((req, _) =>
                new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(new byte[] { 0x1 })
                    {
                        Headers = { ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, 1178116) }
                    }
                });
            using var client = new HttpClient(handler);

            var result = await CdnProbe.ProbeAsync(client, "https://cdn.ardysamods.my.id/Assets/models/Anti-Mage/defiant.zip", default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalBytes, Is.EqualTo(1178116));
            Assert.That(result.SupportsRange, Is.True);
        }

        [Test]
        public async Task ProbeAsync_ServerIgnoresRangeAndReturns200_StillReportsSizeFromContentLength()
        {
            var handler = new FakeHttpMessageHandler((req, _) =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[1024])
                });
            using var client = new HttpClient(handler);

            var result = await CdnProbe.ProbeAsync(client, "https://example.test/file.bin", default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalBytes, Is.EqualTo(1024));
            Assert.That(result.SupportsRange, Is.False, "a 200 response means the server ignored Range");
        }

        [Test]
        public async Task ProbeAsync_NotFound_ReturnsFailureWithoutThrowing()
        {
            var handler = new FakeHttpMessageHandler((req, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
            using var client = new HttpClient(handler);

            var result = await CdnProbe.ProbeAsync(client, "https://cdn.ardysamods.my.id/missing.png", default);

            Assert.That(result.Success, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task ProbeAsync_TransportThrows_ReturnsFailureWithoutThrowing()
        {
            var handler = new FakeHttpMessageHandler((req, _) => throw new HttpRequestException("connection reset"));
            using var client = new HttpClient(handler);

            var result = await CdnProbe.ProbeAsync(client, "https://cdn.ardysamods.my.id/Assets/heroes.json", default);

            Assert.That(result.Success, Is.False);
        }
    }
}
