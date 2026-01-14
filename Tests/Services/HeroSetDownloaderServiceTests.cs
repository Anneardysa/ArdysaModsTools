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
using NUnit.Framework;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Exceptions;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for HeroSetDownloaderService.
    /// </summary>
    [TestFixture]
    public class HeroSetDownloaderServiceTests
    {
        private HeroSetDownloaderService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroSetDownloaderService();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithDefaults_CreatesInstance()
        {
            // Arrange & Act
            var service = new HeroSetDownloaderService();

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithCustomFolder_CreatesInstance()
        {
            // Arrange
            var tempFolder = Path.GetTempPath();

            // Act
            var service = new HeroSetDownloaderService(tempFolder);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region DownloadAndExtractAsync Validation Tests

        [Test]
        public void DownloadAndExtractAsync_WithNullHeroId_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    null!,
                    "set1",
                    "https://example.com/set.zip",
                    msg => { });
            });
        }

        [Test]
        public void DownloadAndExtractAsync_WithEmptyHeroId_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    "",
                    "set1",
                    "https://example.com/set.zip",
                    msg => { });
            });
        }

        [Test]
        public void DownloadAndExtractAsync_WithEmptyUrl_ThrowsDownloadException()
        {
            // Arrange & Act & Assert
            var ex = Assert.ThrowsAsync<DownloadException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    "test_hero",
                    "set1",
                    "",
                    msg => { });
            });

            Assert.That(ex!.ErrorCode, Is.EqualTo(ErrorCodes.DL_INVALID_URL));
        }

        #endregion
    }
}

