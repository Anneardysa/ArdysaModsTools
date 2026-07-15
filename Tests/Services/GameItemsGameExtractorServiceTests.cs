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
using System.IO;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class GameItemsGameExtractorServiceTests
    {
        private GameItemsGameExtractorService _service = null!;
        private List<string> _logMessages = null!;

        [SetUp]
        public void Setup()
        {
            _service = new GameItemsGameExtractorService(null);
            _logMessages = new List<string>();
        }

        [Test]
        public void Constructor_WithNullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new GameItemsGameExtractorService(null));
        }

        [Test]
        public async Task RefreshFromGameAsync_WhenTargetPathEmpty_ReturnsFalse()
        {
            var result = await _service.RefreshFromGameAsync(
                "", Path.GetTempPath(), msg => _logMessages.Add(msg));

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task RefreshFromGameAsync_WhenTargetPathWhitespace_ReturnsFalse()
        {
            var result = await _service.RefreshFromGameAsync(
                "   ", Path.GetTempPath(), msg => _logMessages.Add(msg));

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task RefreshFromGameAsync_WhenExtractDirEmpty_ReturnsFalse()
        {
            string tempTarget = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempTarget);

            try
            {
                var result = await _service.RefreshFromGameAsync(
                    tempTarget, "", msg => _logMessages.Add(msg));

                Assert.That(result, Is.False);
            }
            finally
            {
                try { Directory.Delete(tempTarget, true); } catch { }
            }
        }

        [Test]
        public async Task RefreshFromGameAsync_WhenGameVpkMissing_ReturnsFalse()
        {
            string tempTarget = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string tempExtract = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempTarget);
            Directory.CreateDirectory(tempExtract);

            try
            {
                var result = await _service.RefreshFromGameAsync(
                    tempTarget, tempExtract, msg => _logMessages.Add(msg));

                Assert.That(result, Is.False);
                Assert.That(File.Exists(Path.Combine(tempExtract, "scripts", "items", "items_game.txt")), Is.False);
            }
            finally
            {
                try { Directory.Delete(tempTarget, true); } catch { }
                try { Directory.Delete(tempExtract, true); } catch { }
            }
        }
    }
}
