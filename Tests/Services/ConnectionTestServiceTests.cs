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
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ConnectionTestServiceTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } =
                req =>
                {
                    if (req.RequestUri != null && req.RequestUri.AbsolutePath.EndsWith("modspack-releases.json"))
                    {
                        string manifest = "{\"latest\":\"mods-v4.0\",\"releases\":{\"mods-v4.0\":{\"version\":\"mods-v4.0\",\"assets\":[{\"name\":\"mods-v4.0.zip\",\"url\":\"https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip\"}]}}}";
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{\"test\": true, \"data\": \"sample\"}"))
                    };
                };

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = Handler(request);
                return Task.FromResult(response);
            }
        }

        #region Scoring & Status Tests

        [Test]
        public void CalculateQualityScore_WhenUnreachable_ReturnsZero()
        {
            int score = ConnectionTestService.CalculateQualityScore(false, 20, 2, 15000, 95);
            Assert.That(score, Is.EqualTo(0));
        }

        [Test]
        public void CalculateQualityScore_WhenOptimalConditions_ReturnsHighScore()
        {
            int score = ConnectionTestService.CalculateQualityScore(true, 25, 2, 12000, 95);
            Assert.That(score, Is.GreaterThanOrEqualTo(85));
            Assert.That(score, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void CalculateQualityScore_WhenHighLatencyAndSlowSpeed_ReturnsLowerScore()
        {
            int goodScore = ConnectionTestService.CalculateQualityScore(true, 30, 2, 10000, 95);
            int poorScore = ConnectionTestService.CalculateQualityScore(true, 450, 25, 200, 60);

            Assert.That(poorScore, Is.LessThan(goodScore));
            Assert.That(poorScore, Is.GreaterThan(0));
        }

        [TestCase(true, 25, 5000, 95, "optimal")]
        [TestCase(true, 100, 2000, 85, "good")]
        [TestCase(true, 200, 500, 75, "fair")]
        [TestCase(true, 400, 100, 50, "slow")]
        [TestCase(false, 9999, 0, 0, "unreachable")]
        public void DetermineStatus_ReturnsExpectedStatus(bool reachable, long latency, long speed, int stability, string expected)
        {
            string status = ConnectionTestService.DetermineStatus(reachable, latency, speed, stability);
            Assert.That(status, Is.EqualTo(expected));
        }

        #endregion

        #region Benchmark Execution & Streaming Tests

        [Test]
        public async Task RunBenchmarkAsync_WhenAllServersOnline_ReturnsCompleteReportWithRecommendationAndStreamingMetrics()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = req =>
                {
                    if (req.RequestUri != null && req.RequestUri.AbsolutePath.EndsWith("modspack-releases.json"))
                    {
                        string manifest = "{\"latest\":\"mods-v4.0\",\"releases\":{\"mods-v4.0\":{\"version\":\"mods-v4.0\",\"assets\":[{\"name\":\"mods-v4.0.zip\",\"url\":\"https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip\"}]}}}";
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[1024 * 50])
                    };
                }
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var progressUpdates = new List<ConnectionTestProgress>();
            var progress = new Progress<ConnectionTestProgress>(p => progressUpdates.Add(p));

            var report = await service.RunBenchmarkAsync(progress, CancellationToken.None);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Servers.Count, Is.EqualTo(2));
            Assert.That(report.Servers, Has.All.Property(nameof(ServerConnectionResult.IsReachable)).True);
            Assert.That(report.RecommendedServerKey, Is.Not.Empty);
            Assert.That(report.DiagnosticSeverity, Is.EqualTo("success"));
            Assert.That(report.DiagnosticMessage, Does.Contain("optimal"));
            Assert.That(report.Servers, Has.Some.Property(nameof(ServerConnectionResult.IsRecommended)).True);

            foreach (var server in report.Servers)
            {
                Assert.That(server.DownloadSpeedKBps, Is.GreaterThan(0));
                Assert.That(server.StabilityPercent, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task ResolveLatestModsPackStreamPathAsync_ExtractsLatestAssetPathFromManifest()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = req =>
                {
                    string manifest = "{\"latest\":\"mods-v5.2\",\"releases\":{\"mods-v5.2\":{\"version\":\"mods-v5.2\",\"assets\":[{\"name\":\"mods-v5.2.zip\",\"url\":\"https://cdn.ardysamods.my.id/modspack-releases/mods-v5.2/mods-v5.2.zip\"}]}}}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                    };
                }
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            string path = await service.ResolveLatestModsPackStreamPathAsync(CancellationToken.None);
            Assert.That(path, Does.Contain("mods-v5.2.zip"));
        }

        [Test]
        public async Task MeasureSustainedThroughputAsync_CalculatesAccurateThroughputAndStability()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[1024 * 100])
                }
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var result = await service.MeasureSustainedThroughputAsync(
                "https://cdn.ardysamods.my.id",
                "Cloudflare R2",
                "modspack-releases/mods-v4.0/mods-v4.0.zip",
                1,
                3,
                null,
                CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.SustainedSpeedKBps, Is.GreaterThan(0));
            Assert.That(result.PeakSpeedMBps, Is.GreaterThan(0));
            Assert.That(result.StabilityPercent, Is.GreaterThanOrEqualTo(45));
        }

        [Test]
        public async Task RunBenchmarkAsync_WhenOneServerFails_DiagnosesIspBlockAndRecommendsWorkingServer()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = req =>
                {
                    if (req.RequestUri != null && req.RequestUri.AbsolutePath.EndsWith("modspack-releases.json"))
                    {
                        string manifest = "{\"latest\":\"mods-v4.0\",\"releases\":{\"mods-v4.0\":{\"version\":\"mods-v4.0\",\"assets\":[{\"name\":\"mods-v4.0.zip\",\"url\":\"https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip\"}]}}}";
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                        };
                    }

                    if (req.RequestUri != null && req.RequestUri.Host.Contains("cdn.ardysamods.my.id") && !req.RequestUri.Host.Contains("cdn2"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[1024 * 20])
                    };
                }
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var report = await service.RunBenchmarkAsync(null, CancellationToken.None);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.DiagnosticSeverity, Is.EqualTo("warning"));
            Assert.That(report.RecommendedServerKey, Is.EqualTo("eu_us"));
            Assert.That(report.DiagnosticMessage, Does.Contain("unreachable"));
            Assert.That(report.DiagnosticMessage, Does.Contain("operational"));
        }

        [Test]
        public async Task RunBenchmarkAsync_WhenAllServersFail_ReportsAllUnreachableDiagnostic()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var report = await service.RunBenchmarkAsync(null, CancellationToken.None);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.DiagnosticSeverity, Is.EqualTo("error"));
            Assert.That(report.DiagnosticMessage, Does.Contain("unreachable"));
            Assert.That(report.Servers, Has.All.Property(nameof(ServerConnectionResult.IsReachable)).False);
        }

        [Test]
        public void RunBenchmarkAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            var fakeHandler = new FakeHttpMessageHandler();
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await service.RunBenchmarkAsync(null, cts.Token);
            });
        }

        #endregion

        #region Failure classification (inner-exception chain)

        private static HttpRequestException TlsFailure(Exception inner) =>
            new("The SSL connection could not be established, see inner exception.", inner);

        private static HttpRequestException IspReset() => TlsFailure(
            new System.IO.IOException("Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.",
                new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset)));

        [Test]
        public void Classify_HandshakeResetByIsp_RecommendsWarp()
        {
            string msg = ConnectionTestService.ClassifyHttpError(IspReset());

            Assert.That(msg, Is.EqualTo(ConnectionTestService.IspBlockMessage));
            Assert.That(msg, Does.Contain("Cloudflare WARP"));
        }

        [Test]
        public void Classify_TransportClosedDuringAuthentication_IsIspBlock()
        {
            var ex = TlsFailure(new System.Security.Authentication.AuthenticationException(
                "Authentication failed because the remote party has closed the transport stream."));

            Assert.That(ConnectionTestService.ClassifyHttpError(ex), Is.EqualTo(ConnectionTestService.IspBlockMessage));
        }

        [Test]
        public void Classify_CertificateNotTimeValid_PointsAtThePcClock()
        {
            var ex = TlsFailure(new System.Security.Authentication.AuthenticationException(
                "The remote certificate is invalid because of errors in the certificate chain: NotTimeValid"));

            Assert.That(ConnectionTestService.ClassifyHttpError(ex), Is.EqualTo(ConnectionTestService.ClockMessage));
        }

        [Test]
        public void Classify_CertificateUntrustedRoot_PointsAtInterception()
        {
            var ex = TlsFailure(new System.Security.Authentication.AuthenticationException(
                "The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot"));

            Assert.That(ConnectionTestService.ClassifyHttpError(ex), Is.EqualTo(ConnectionTestService.InterceptMessage));
        }

        [Test]
        public void Classify_DnsFailure_StaysDns()
        {
            var ex = new HttpRequestException("No such host is known. (cdn.ardysamods.my.id:443)",
                new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound));

            Assert.That(ConnectionTestService.ClassifyHttpError(ex), Does.StartWith("DNS resolution failed"));
        }

        [Test]
        public void Classify_ConnectTimeout_StaysTimeout()
        {
            var ex = new HttpRequestException("A connection attempt failed",
                new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut));

            Assert.That(ConnectionTestService.ClassifyHttpError(ex), Does.StartWith("Connection timed out"));
        }

        [Test]
        public void Classify_UnknownFailure_ReportsTheDeepestCause_NeverSeeInnerException()
        {
            var ex = new HttpRequestException("Something failed, see inner exception.",
                new InvalidOperationException("The actual reason."));

            string msg = ConnectionTestService.ClassifyHttpError(ex);

            Assert.That(msg, Is.EqualTo("The actual reason."));
            Assert.That(msg, Does.Not.Contain("see inner exception"));
        }

        [Test]
        public async Task RunBenchmarkAsync_WhenIspResetsEveryHost_DiagnosisRecommendsWarp()
        {
            var fakeHandler = new FakeHttpMessageHandler { Handler = _ => throw IspReset() };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var report = await service.RunBenchmarkAsync(null, CancellationToken.None);

            Assert.That(report.Servers, Has.All.Property(nameof(ServerConnectionResult.ErrorDetail))
                .EqualTo(ConnectionTestService.IspBlockMessage));
            Assert.That(report.DiagnosticSeverity, Is.EqualTo("error"));
            Assert.That(report.DiagnosticMessage, Does.Contain("unreachable").And.Contain("Cloudflare WARP"));
            Assert.That(report.DiagnosticMessage, Does.Contain("Install ModsPack"));
        }

        [Test]
        public async Task RunBenchmarkAsync_WhenHostsFailDifferently_KeepsTheGenericDiagnosis()
        {
            var fakeHandler = new FakeHttpMessageHandler
            {
                Handler = req => req.RequestUri!.Host.StartsWith("cdn2")
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : throw IspReset()
            };
            using var client = new HttpClient(fakeHandler);
            var service = new ConnectionTestService(client);

            var report = await service.RunBenchmarkAsync(null, CancellationToken.None);

            Assert.That(report.DiagnosticMessage, Does.Contain("check your internet connection"));
        }

        #endregion
    }
}
