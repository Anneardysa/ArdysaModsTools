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
