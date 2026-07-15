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
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class VpkExtractorServiceTests
    {
        private VpkExtractorService _service = null!;
        private Mock<IAppLogger> _loggerMock = null!;
        private List<string> _logMessages = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<IAppLogger>();
            _logMessages = new List<string>();
            _service = new VpkExtractorService(_loggerMock.Object);
        }

        [Test]
        public async Task ExtractAsync_WhenHlExtractNotFound_ReturnsFalse()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "HLExtract.exe");

            var result = await _service.ExtractAsync(
                nonExistentPath,
                "test.vpk",
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            Assert.That(result, Is.False);
        }

        [Test]
        public void ExtractAsync_WhenCancelled_ThrowsOrReturnsFalse()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            
            if (File.Exists(hlExtractPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                var tempVpk = Path.Combine(tempDir, "test.vpk");
                File.WriteAllText(tempVpk, "dummy");
                
                try
                {
                    Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    {
                        await _service.ExtractAsync(
                            hlExtractPath,
                            tempVpk,
                            Path.GetTempPath(),
                            msg => _logMessages.Add(msg),
                            cts.Token);
                    });
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            else
            {
                Assert.Pass("HLExtract.exe not found in test environment");
            }
        }

        [Test]
        public async Task ExtractAsync_WithNullVpkPath_ReturnsFalse()
        {
            var result = await _service.ExtractAsync(
                "HLExtract.exe",
                null!,
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ExtractAsync_WithEmptyPaths_ReturnsFalse()
        {
            var result = await _service.ExtractAsync(
                "",
                "",
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            Assert.That(result, Is.False);
        }

        [Test]
        public void Constructor_WithNullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new VpkExtractorService(null));
        }

        [Test]
        public void Constructor_CreatesInstance()
        {
            var service = new VpkExtractorService(null);

            Assert.That(service, Is.Not.Null);
        }
    }
}

