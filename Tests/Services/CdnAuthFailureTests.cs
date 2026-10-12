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
using System.Net;
using System.Net.Http;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnAuthFailureTests
    {
        [Test]
        public void IsUnauthorized_Http401_IsRecognised()
        {
            var ex = new HttpRequestException("HTTP 401", null, HttpStatusCode.Unauthorized);

            Assert.That(CdnAuthFailure.IsUnauthorized(ex), Is.True);
        }

        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void IsUnauthorized_OtherStatuses_AreNot(HttpStatusCode status)
        {
            var ex = new HttpRequestException($"HTTP {(int)status}", null, status);

            Assert.That(CdnAuthFailure.IsUnauthorized(ex), Is.False,
                "only a 401 may abandon the chain — everything else still deserves the next host");
        }

        [Test]
        public void IsUnauthorized_StatuslessHttpRequestException_IsNot()
        {
            Assert.That(CdnAuthFailure.IsUnauthorized(new HttpRequestException("connection reset")), Is.False);
        }

        [Test]
        public void IsUnauthorized_UnrelatedExceptions_AreNot()
        {
            Assert.That(CdnAuthFailure.IsUnauthorized(null), Is.False);
            Assert.That(CdnAuthFailure.IsUnauthorized(new TimeoutException()), Is.False);
            Assert.That(CdnAuthFailure.IsUnauthorized(new InvalidOperationException()), Is.False);
        }

        [Test]
        public void ToException_CarriesDl011AndTheUrl()
        {
            const string url = "https://cdn.ardysamods.my.id/Assets/models/Abaddon/blightfall.zip";

            var ex = CdnAuthFailure.ToException(url);

            Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCodes.DL_CDN_UNAUTHORIZED));
            Assert.That(ex.FailedUrl, Is.EqualTo(url));

            Assert.That(ex.Message, Does.Contain(ErrorCodes.DL_CDN_UNAUTHORIZED));
            Assert.That(ex.Message, Does.Contain(CdnAuthFailure.Reason));
        }

        [Test]
        public void ToException_IsNotTransient_SoNoRetryPolicyWillRepeatIt()
        {
            Assert.That(
                ArdysaModsTools.Core.Helpers.RetryHelper.IsTransientException(CdnAuthFailure.ToException()),
                Is.False);
        }
    }
}
