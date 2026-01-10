using NUnit.Framework;
using ArdysaModsTools.Core.Exceptions;

namespace ArdysaModsTools.Tests.Exceptions
{
    /// <summary>
    /// Tests for custom exception types.
    /// </summary>
    [TestFixture]
    public class ArdysaExceptionTests
    {
        #region ArdysaException Tests

        [Test]
        public void ArdysaException_Constructor_SetsErrorCodeAndMessage()
        {
            // Arrange & Act
            var exception = new ArdysaException(ErrorCodes.VPK_EXTRACT_FAILED, "Test message");

            // Assert
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCodes.VPK_EXTRACT_FAILED));
            Assert.That(exception.Message, Does.Contain("VPK_001"));
            Assert.That(exception.Message, Does.Contain("Test message"));
        }

        [Test]
        public void ArdysaException_WithInnerException_PreservesInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("Inner error");

            // Act
            var exception = new ArdysaException(ErrorCodes.DL_NETWORK_ERROR, "Outer message", inner);

            // Assert
            Assert.That(exception.InnerException, Is.EqualTo(inner));
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCodes.DL_NETWORK_ERROR));
        }

        #endregion

        #region VpkException Tests

        [Test]
        public void VpkException_IsArdysaException()
        {
            // Arrange & Act
            var exception = new VpkException(ErrorCodes.VPK_FILE_NOT_FOUND, "VPK not found");

            // Assert
            Assert.That(exception, Is.InstanceOf<ArdysaException>());
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCodes.VPK_FILE_NOT_FOUND));
        }

        #endregion

        #region DownloadException Tests

        [Test]
        public void DownloadException_StoresFailedUrl()
        {
            // Arrange
            const string url = "https://example.com/file.zip";

            // Act
            var exception = new DownloadException(ErrorCodes.DL_FILE_NOT_FOUND, "Not found", url);

            // Assert
            Assert.That(exception.FailedUrl, Is.EqualTo(url));
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCodes.DL_FILE_NOT_FOUND));
        }

        [Test]
        public void DownloadException_WithoutUrl_UrlIsNull()
        {
            // Arrange & Act
            var exception = new DownloadException(ErrorCodes.DL_TIMEOUT, "Timed out");

            // Assert
            Assert.That(exception.FailedUrl, Is.Null);
        }

        #endregion

        #region PatchException Tests

        [Test]
        public void PatchException_StoresTargetFile()
        {
            // Arrange
            const string file = "items_game.txt";

            // Act
            var exception = new PatchException(ErrorCodes.PATCH_ITEMS_GAME_FAILED, "Patch failed", file);

            // Assert
            Assert.That(exception.TargetFile, Is.EqualTo(file));
        }

        #endregion

        #region GenerationException Tests

        [Test]
        public void GenerationException_StoresHeroAndSetInfo()
        {
            // Arrange
            const string hero = "Juggernaut";
            const string set = "Bladeform Legacy";

            // Act
            var exception = new GenerationException(
                ErrorCodes.GEN_DOWNLOAD_FAILED, 
                "Download failed", 
                hero, 
                set);

            // Assert
            Assert.That(exception.HeroName, Is.EqualTo(hero));
            Assert.That(exception.SetName, Is.EqualTo(set));
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCodes.GEN_DOWNLOAD_FAILED));
        }

        #endregion
    }
}
