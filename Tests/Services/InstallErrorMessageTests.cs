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
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class InstallErrorMessageTests
    {
        private static IOException Io(int hresult) => new IOException("boom") { HResult = hresult };

        [TestCase(unchecked((int)0x80070070), "disk space")]
        [TestCase(unchecked((int)0x80070027), "disk space")]
        [TestCase(unchecked((int)0x80070020), "Dota 2")]
        [TestCase(unchecked((int)0x80070021), "Dota 2")]
        [TestCase(unchecked((int)0x800704C8), "Dota 2")]
        public void Describe_NamesTheCause_ForKnownWin32Codes(int hresult, string expectedFragment)
        {
            var msg = InstallErrorMessage.Describe(Io(hresult));

            Assert.That(msg, Does.Contain(expectedFragment));
            Assert.That(msg, Does.Not.Contain("Unexpected error"));
        }

        [Test]
        public void Describe_CoversAccessDeniedAndDownloadFailures()
        {
            Assert.That(InstallErrorMessage.Describe(new UnauthorizedAccessException()),
                Does.Contain("Access denied"));
            Assert.That(InstallErrorMessage.Describe(new HttpRequestException("no route")),
                Does.Contain("internet connection"));
        }

        [Test]
        public void Describe_FallsBackToGenericIo_ThenToUnexpected()
        {
            Assert.That(InstallErrorMessage.Describe(Io(unchecked((int)0x8007001F))),
                Does.Contain("could not be read or written"));

            Assert.That(InstallErrorMessage.Describe(new InvalidOperationException()),
                Is.EqualTo("Unexpected error — please try again."));
        }
    }
}
