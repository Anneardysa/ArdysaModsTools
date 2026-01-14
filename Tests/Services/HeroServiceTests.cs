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
    /// Tests for HeroService.
    /// </summary>
    [TestFixture]
    public class HeroServiceTests
    {
        private HeroService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroService(AppDomain.CurrentDomain.BaseDirectory);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithBaseFolder_CreatesInstance()
        {
            // Arrange & Act
            var service = new HeroService(Path.GetTempPath());

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullFolder_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new HeroService(null!);
            });
        }

        #endregion

        #region LoadHeroesAsync Tests

        [Test]
        public async Task LoadHeroesAsync_ReturnsListOrThrows()
        {
            // Arrange & Act
            try
            {
                var result = await _service.LoadHeroesAsync();
                
                // Assert - if we get a result, it should be a list
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<List<HeroSummary>>());
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or FileNotFoundException)
            {
                // Expected if no network or local file
                Assert.Pass("Network or file not available in test environment");
            }
        }

        [Test]
        public async Task LoadHeroesAsync_ReturnsHeroesWithRequiredProperties()
        {
            // Arrange & Act
            try
            {
                var result = await _service.LoadHeroesAsync();
                
                // Assert - if we get heroes, verify they have required properties
                if (result.Count > 0)
                {
                    var firstHero = result[0];
                    Assert.That(firstHero.Name, Is.Not.Null.And.Not.Empty);
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or FileNotFoundException)
            {
                Assert.Pass("Network or file not available in test environment");
            }
        }

        #endregion
    }
}

