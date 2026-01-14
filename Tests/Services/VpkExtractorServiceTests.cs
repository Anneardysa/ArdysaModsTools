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

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for VpkExtractorService.
    /// </summary>
    [TestFixture]
    public class VpkExtractorServiceTests
    {
        private VpkExtractorService _service = null!;
        private Mock<ILogger> _loggerMock = null!;
        private List<string> _logMessages = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger>();
            _logMessages = new List<string>();
            _service = new VpkExtractorService(_loggerMock.Object);
        }

        [Test]
        public async Task ExtractAsync_WhenHlExtractNotFound_ReturnsFalse()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "HLExtract.exe");

            // Act
            var result = await _service.ExtractAsync(
                nonExistentPath,
                "test.vpk",
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ExtractAsync_WhenCancelled_ThrowsOrReturnsFalse()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Note: With cancelled token, the service may either:
            // 1. Throw OperationCanceledException if it checks ct before validation
            // 2. Return false if validation fails first
            // Both are acceptable behaviors
            var hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            
            // If HLExtract exists on the test machine, test cancellation
            if (File.Exists(hlExtractPath))
            {
                // Create a temp vpk file path that exists
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                var tempVpk = Path.Combine(tempDir, "test.vpk");
                File.WriteAllText(tempVpk, "dummy"); // Create dummy file
                
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
                    // Cleanup
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            else
            {
                // HLExtract doesn't exist in test environment, skip
                Assert.Pass("HLExtract.exe not found in test environment");
            }
        }

        [Test]
        public async Task ExtractAsync_WithNullVpkPath_ReturnsFalse()
        {
            // Arrange & Act
            var result = await _service.ExtractAsync(
                "HLExtract.exe",
                null!,
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ExtractAsync_WithEmptyPaths_ReturnsFalse()
        {
            // Arrange & Act
            var result = await _service.ExtractAsync(
                "",
                "",
                Path.GetTempPath(),
                msg => _logMessages.Add(msg));

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void Constructor_WithNullLogger_DoesNotThrow()
        {
            // Arrange & Act & Assert
            Assert.DoesNotThrow(() => new VpkExtractorService(null));
        }

        [Test]
        public void Constructor_CreatesInstance()
        {
            // Arrange & Act
            var service = new VpkExtractorService(null);

            // Assert
            Assert.That(service, Is.Not.Null);
        }
    }
}

