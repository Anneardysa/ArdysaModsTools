using NUnit.Framework;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for HeroSetPatcherService.
    /// Note: These tests focus on service instantiation and basic validation.
    /// The ParseIndexFile method requires file I/O, so we test at a higher level.
    /// </summary>
    [TestFixture]
    public class HeroSetPatcherServiceTests
    {
        private HeroSetPatcherService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroSetPatcherService();
        }

        #region Service Instance Tests

        [Test]
        public void Constructor_CreatesInstance()
        {
            // Arrange & Act
            var service = new HeroSetPatcherService();

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Service_ImplementsInterface()
        {
            // Arrange & Act
            var service = new HeroSetPatcherService();

            // Assert
            Assert.That(service, Is.InstanceOf<IHeroSetPatcher>());
        }

        #endregion

        #region ParseIndexFile Parameter Validation Tests

        [Test]
        public void ParseIndexFile_WithNullContent_ReturnsNull()
        {
            // Arrange
            var heroId = "test_hero";
            var itemIds = new List<int> { 12345 };

            // Act
            var result = _service.ParseIndexFile(null!, heroId, itemIds);

            // Assert
            // The method returns null for invalid input
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithEmptyHeroId_ReturnsNull()
        {
            // Arrange
            var content = "test content";
            var itemIds = new List<int> { 12345 };

            // Act
            var result = _service.ParseIndexFile(content, "", itemIds);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithNullItemIds_ReturnsNull()
        {
            // Arrange
            var content = "test content";
            var heroId = "test_hero";

            // Act
            var result = _service.ParseIndexFile(content, heroId, null!);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithEmptyItemIds_ReturnsNull()
        {
            // Arrange
            var content = "test content";
            var heroId = "test_hero";
            var itemIds = new List<int>();

            // Act
            var result = _service.ParseIndexFile(content, heroId, itemIds);

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion
    }
}
