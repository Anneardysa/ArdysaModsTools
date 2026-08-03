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
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HttpClientProviderTests
    {
        [Test]
        public void UserAgent_CarriesProductVersionAndBuild()
        {
            var v = AppVersion.Current;

            Assert.That(HttpClientProvider.UserAgent, Does.StartWith("ArdysaModsTools/"));
            Assert.That(HttpClientProvider.UserAgent, Does.Contain(v.Version));
            Assert.That(HttpClientProvider.UserAgent, Does.Contain(v.BuildNumber.ToString()));
        }

        [Test]
        public void UserAgent_IsNotTheLegacyLiteral()
        {
            Assert.That(HttpClientProvider.UserAgent, Is.Not.EqualTo("ArdysaModsTools/1.0"));
        }

        [Test]
        public void UserAgent_IsASingleHeaderLine()
        {
            Assert.That(HttpClientProvider.UserAgent, Does.Not.Contain("\n"));
            Assert.That(HttpClientProvider.UserAgent, Does.Not.Contain("\r"));
        }

        [Test]
        public void Client_SendsTheVersionedUserAgent()
        {
            var sent = string.Join(" ", HttpClientProvider.Client.DefaultRequestHeaders
                .GetValues("User-Agent"));

            Assert.That(sent, Is.EqualTo(HttpClientProvider.UserAgent));
        }

        [Test]
        public void Client_DoesNotStackMultipleUserAgents()
        {
            var sent = string.Join(" ", HttpClientProvider.Client.DefaultRequestHeaders
                .GetValues("User-Agent"));

            Assert.That(sent.Split("ArdysaModsTools/").Length - 1, Is.EqualTo(1));
        }
    }
}
