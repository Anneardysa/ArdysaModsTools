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

