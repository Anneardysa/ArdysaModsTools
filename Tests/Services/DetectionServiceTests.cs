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
using Moq;
using NUnit.Framework;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class DetectionServiceTests
    {
        private DetectionService _service = null!;
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;

        [SetUp]
        public void Setup()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new DetectionService(_logger);
        }

        [TearDown]
        public void TearDown()
        {
            _testConsole?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithLogger_CreatesInstance()
        {
            var service = new DetectionService(_logger);

            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region AutoDetect Tests

        [Test]
        public async Task AutoDetectAsync_CompletesWithoutException()
        {
            var result = await _service.AutoDetectAsync();

            Assert.Pass("AutoDetectAsync completed successfully");
        }

        [Test]
        public async Task AutoDetectAsync_ReturnsNullOrValidPath()
        {
            var result = await _service.AutoDetectAsync();

            if (result != null)
            {
                Assert.That(Directory.Exists(result), Is.True, 
                    "If a path is returned, it should exist");
            }
            else
            {
                Assert.Pass("Dota 2 not installed on test machine");
            }
        }

        #endregion
    }
}

