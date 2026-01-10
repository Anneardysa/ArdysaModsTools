using Moq;
using NUnit.Framework;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for DetectionService.
    /// Note: This service requires a concrete Logger instance with RichTextBox.
    /// </summary>
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
            // Arrange & Act
            var service = new DetectionService(_logger);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region AutoDetect Tests

        [Test]
        public async Task AutoDetectAsync_CompletesWithoutException()
        {
            // Arrange & Act
            var result = await _service.AutoDetectAsync();

            // Assert
            // Result can be null (no Dota 2 installed) or a valid path
            // We just verify it doesn't throw
            Assert.Pass("AutoDetectAsync completed successfully");
        }

        [Test]
        public async Task AutoDetectAsync_ReturnsNullOrValidPath()
        {
            // Arrange & Act
            var result = await _service.AutoDetectAsync();

            // Assert
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
