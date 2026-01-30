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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Conflict;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Unit tests for ModPriorityService.
    /// </summary>
    [TestFixture]
    public class ModPriorityServiceTests
    {
        private ModPriorityService _service = null!;
        private Mock<IAppLogger> _mockLogger = null!;
        private string _testTempPath = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<IAppLogger>();
            _service = new ModPriorityService(_mockLogger.Object);
            _testTempPath = Path.Combine(Path.GetTempPath(), $"ArdysaTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testTempPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testTempPath))
            {
                try
                {
                    Directory.Delete(_testTempPath, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            // Arrange & Act
            var service = new ModPriorityService(null);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region LoadConfigAsync Tests

        [Test]
        public async Task LoadConfigAsync_NoExistingConfig_ReturnsDefault()
        {
            // Act
            var config = await _service.LoadConfigAsync(_testTempPath);

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config.DefaultStrategy, Is.EqualTo(ResolutionStrategy.HigherPriority));
            Assert.That(config.AutoResolveNonBreaking, Is.True);
        }

        [Test]
        public async Task LoadConfigAsync_Caches_ReturnsSameInstance()
        {
            // Act
            var config1 = await _service.LoadConfigAsync(_testTempPath);
            var config2 = await _service.LoadConfigAsync(_testTempPath);

            // Assert
            Assert.That(config1, Is.SameAs(config2));
        }

        [Test]
        public async Task LoadConfigAsync_DifferentPath_ReloadsConfig()
        {
            // Arrange
            var otherPath = Path.Combine(Path.GetTempPath(), $"ArdysaTests2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(otherPath);

            try
            {
                // Act
                var config1 = await _service.LoadConfigAsync(_testTempPath);
                var config2 = await _service.LoadConfigAsync(otherPath);

                // Assert
                Assert.That(config1, Is.Not.SameAs(config2));
            }
            finally
            {
                if (Directory.Exists(otherPath))
                    Directory.Delete(otherPath, true);
            }
        }

        #endregion

        #region SaveConfigAsync Tests

        [Test]
        public async Task SaveConfigAsync_CreatesConfigFile()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("test_mod", "Test Mod", "Test", 25);

            // Act
            await _service.SaveConfigAsync(config, _testTempPath);

            // Assert
            var configPath = ModPriorityConfig.GetConfigPath(_testTempPath);
            Assert.That(File.Exists(configPath), Is.True);
        }

        [Test]
        public async Task SaveConfigAsync_UpdatesCache()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("test_mod", "Test Mod", "Test", 25);

            // Act
            await _service.SaveConfigAsync(config, _testTempPath);
            var loadedConfig = await _service.LoadConfigAsync(_testTempPath);

            // Assert
            Assert.That(loadedConfig.GetPriority("test_mod"), Is.EqualTo(25));
        }

        #endregion

        #region GetModPriorityAsync Tests

        [Test]
        public async Task GetModPriorityAsync_UnknownMod_ReturnsDefault()
        {
            // Act
            var priority = await _service.GetModPriorityAsync("unknown_mod", _testTempPath);

            // Assert
            Assert.That(priority, Is.EqualTo(100)); // Default priority
        }

        [Test]
        public async Task GetModPriorityAsync_KnownMod_ReturnsConfiguredPriority()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("known_mod", "Known Mod", "Test", 42);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache(); // Force reload

            // Act
            var priority = await _service.GetModPriorityAsync("known_mod", _testTempPath);

            // Assert
            Assert.That(priority, Is.EqualTo(42));
        }

        #endregion

        #region SetModPriorityAsync Tests

        [Test]
        public async Task SetModPriorityAsync_UpdatesPriority()
        {
            // Arrange & Act
            await _service.SetModPriorityAsync("new_mod", "New Mod", "Test", 15, _testTempPath);
            _service.InvalidateCache();
            var priority = await _service.GetModPriorityAsync("new_mod", _testTempPath);

            // Assert
            Assert.That(priority, Is.EqualTo(15));
        }

        #endregion

        #region GetOrderedPrioritiesAsync Tests

        [Test]
        public async Task GetOrderedPrioritiesAsync_ReturnsOrderedList()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("mod_c", "Mod C", "Test", 30);
            config.SetPriority("mod_a", "Mod A", "Test", 10);
            config.SetPriority("mod_b", "Mod B", "Test", 20);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            // Act
            var priorities = await _service.GetOrderedPrioritiesAsync(_testTempPath);

            // Assert
            Assert.That(priorities, Has.Count.EqualTo(3));
            Assert.That(priorities[0].ModId, Is.EqualTo("mod_a"));
            Assert.That(priorities[1].ModId, Is.EqualTo("mod_b"));
            Assert.That(priorities[2].ModId, Is.EqualTo("mod_c"));
        }

        [Test]
        public async Task GetOrderedPrioritiesAsync_FiltersCategory()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("weather_mod", "Weather", "Weather", 10);
            config.SetPriority("river_mod", "River", "River", 20);
            config.SetPriority("shader_mod", "Shader", "Weather", 15);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            // Act
            var weatherPriorities = await _service.GetOrderedPrioritiesAsync(_testTempPath, "Weather");

            // Assert
            Assert.That(weatherPriorities, Has.Count.EqualTo(2));
            Assert.That(weatherPriorities.All(p => p.Category == "Weather"), Is.True);
        }

        #endregion

        #region ApplyPrioritiesAsync Tests

        [Test]
        public async Task ApplyPrioritiesAsync_UpdatesSourcePriorities()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("mod1", "Mod 1", "Test", 5);
            config.SetPriority("mod2", "Mod 2", "Test", 50);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var sources = new List<ModSource>
            {
                new ModSource { ModId = "mod1", ModName = "Mod 1" },
                new ModSource { ModId = "mod2", ModName = "Mod 2" },
                new ModSource { ModId = "mod3", ModName = "Mod 3" } // Unknown, gets default
            };

            // Act
            var result = await _service.ApplyPrioritiesAsync(sources, _testTempPath);

            // Assert
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0].Priority, Is.EqualTo(5));    // mod1
            Assert.That(result[1].Priority, Is.EqualTo(50));   // mod2
            Assert.That(result[2].Priority, Is.EqualTo(100));  // mod3 (default)
        }

        [Test]
        public async Task ApplyPrioritiesAsync_ReturnsSortedByPriority()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("mod_low", "Low Priority", "Test", 99);
            config.SetPriority("mod_high", "High Priority", "Test", 1);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var sources = new List<ModSource>
            {
                new ModSource { ModId = "mod_low", ModName = "Low Priority" },
                new ModSource { ModId = "mod_high", ModName = "High Priority" }
            };

            // Act
            var result = await _service.ApplyPrioritiesAsync(sources, _testTempPath);

            // Assert
            Assert.That(result[0].ModId, Is.EqualTo("mod_high"));
            Assert.That(result[1].ModId, Is.EqualTo("mod_low"));
        }

        #endregion

        #region Cache Invalidation Tests

        [Test]
        public async Task InvalidateCache_ForcesReload()
        {
            // Arrange
            var config1 = await _service.LoadConfigAsync(_testTempPath);
            config1.SetPriority("test", "Test", "Test", 10);
            await _service.SaveConfigAsync(config1, _testTempPath);

            // Act
            _service.InvalidateCache();
            var config2 = await _service.LoadConfigAsync(_testTempPath);

            // Assert - After invalidation, should get a fresh load (not same reference)
            // But content should match since we saved it
            Assert.That(config2.GetPriority("test"), Is.EqualTo(10));
        }

        #endregion
    }
}
