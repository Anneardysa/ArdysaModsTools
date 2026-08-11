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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnFallbackServiceTests
    {
        private CdnFallbackService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _service = CdnFallbackService.Instance;
            _service.ResetStats();
        }

        [TearDown]
        public void TearDown()
        {
            _service.ResetStats();
        }

        [Test]
        public void GetStats_InitialState_ReturnsZeroCounts()
        {
            var stats = _service.GetStats();

            Assert.That(stats.total, Is.EqualTo(0));
            Assert.That(stats.r2, Is.EqualTo(0));
            Assert.That(stats.cdn2, Is.EqualTo(0));
            Assert.That(stats.failures, Is.EqualTo(0));
        }

        [Test]
        public async Task DownloadWithFallbackAsync_EmptyUrl_ReturnsFailureResult()
        {
            var result = await _service.DownloadWithFallbackAsync("");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("URL is empty"));
        }

        [Test]
        public async Task DownloadFromPrimaryAsync_EmptyUrl_ReturnsFailureResult()
        {
            var result = await _service.DownloadFromPrimaryAsync("");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("URL is empty"));
        }
    }
}
