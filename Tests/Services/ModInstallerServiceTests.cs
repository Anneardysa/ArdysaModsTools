using Moq;
using NUnit.Framework;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for ModInstallerService.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ModInstallerServiceTests
    {
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;
        private ModInstallerService _service = null!;

        [SetUp]
        public void Setup()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new ModInstallerService(_logger);
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
            // Arrange & Act
            var service = new ModInstallerService(_logger);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ModInstallerService(null!);
            });
        }

        #endregion

        #region ValidateVpkAsync Tests

        [Test]
        public async Task ValidateVpkAsync_WithNullPath_ReturnsInvalid()
        {
            // Arrange & Act
            var (isValid, errorMessage) = await _service.ValidateVpkAsync(null!);

            // Assert
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateVpkAsync_WithEmptyPath_ReturnsInvalid()
        {
            // Arrange & Act
            var (isValid, errorMessage) = await _service.ValidateVpkAsync("");

            // Assert
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateVpkAsync_WithNonExistentFile_ReturnsInvalid()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.vpk");

            // Act
            var (isValid, errorMessage) = await _service.ValidateVpkAsync(nonExistentPath);

            // Assert
            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Contains.Substring("not found"));
        }

        #endregion

        #region UpdatePatcherAsync Tests

        [Test]
        public async Task UpdatePatcherAsync_WithEmptyPath_ReturnsFailed()
        {
            // Arrange & Act
            var result = await _service.UpdatePatcherAsync("");

            // Assert - UpdatePatcherAsync returns PatchResult enum, not bool
            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        [Test]
        public async Task UpdatePatcherAsync_WithNullPath_ReturnsFailed()
        {
            // Arrange & Act
            var result = await _service.UpdatePatcherAsync(null!);

            // Assert - UpdatePatcherAsync returns PatchResult enum, not bool
            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        [Test]
        public async Task UpdatePatcherAsync_WithInvalidPath_ReturnsFailed()
        {
            // Arrange
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act
            var result = await _service.UpdatePatcherAsync(invalidPath);

            // Assert - UpdatePatcherAsync returns PatchResult enum, not bool
            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        #endregion

        #region DisableModsAsync Tests

        [Test]
        public async Task DisableModsAsync_WithEmptyPath_ReturnsTrue()
        {
            // Note: Returns true for empty path as documented - nothing to disable
            // Arrange & Act
            var result = await _service.DisableModsAsync("");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task DisableModsAsync_WithInvalidPath_ReturnsTrue()
        {
            // Note: Returns true for non-existent paths - nothing to disable
            // Arrange
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act
            var result = await _service.DisableModsAsync(invalidPath);

            // Assert
            Assert.That(result, Is.True);
        }

        #endregion
    }
}
