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
                catch {  }
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            var service = new ModPriorityService(null);

            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region LoadConfigAsync Tests

        [Test]
        public async Task LoadConfigAsync_NoExistingConfig_ReturnsDefault()
        {
            var config = await _service.LoadConfigAsync(_testTempPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.DefaultStrategy, Is.EqualTo(ResolutionStrategy.HigherPriority));
            Assert.That(config.AutoResolveNonBreaking, Is.True);
        }

        [Test]
        public async Task LoadConfigAsync_Caches_ReturnsSameInstance()
        {
            var config1 = await _service.LoadConfigAsync(_testTempPath);
            var config2 = await _service.LoadConfigAsync(_testTempPath);

            Assert.That(config1, Is.SameAs(config2));
        }

        [Test]
        public async Task LoadConfigAsync_DifferentPath_ReloadsConfig()
        {
            var otherPath = Path.Combine(Path.GetTempPath(), $"ArdysaTests2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(otherPath);

            try
            {
                var config1 = await _service.LoadConfigAsync(_testTempPath);
                var config2 = await _service.LoadConfigAsync(otherPath);

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
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("test_mod", "Test Mod", "Test", 25);

            await _service.SaveConfigAsync(config, _testTempPath);

            var configPath = ModPriorityConfig.GetConfigPath(_testTempPath);
            Assert.That(File.Exists(configPath), Is.True);
        }

        [Test]
        public async Task SaveConfigAsync_UpdatesCache()
        {
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("test_mod", "Test Mod", "Test", 25);

            await _service.SaveConfigAsync(config, _testTempPath);
            var loadedConfig = await _service.LoadConfigAsync(_testTempPath);

            Assert.That(loadedConfig.GetPriority("test_mod"), Is.EqualTo(25));
        }

        #endregion

        #region GetModPriorityAsync Tests

        [Test]
        public async Task GetModPriorityAsync_UnknownMod_ReturnsDefault()
        {
            var priority = await _service.GetModPriorityAsync("unknown_mod", _testTempPath);

            Assert.That(priority, Is.EqualTo(100));
        }

        [Test]
        public async Task GetModPriorityAsync_KnownMod_ReturnsConfiguredPriority()
        {
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("known_mod", "Known Mod", "Test", 42);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var priority = await _service.GetModPriorityAsync("known_mod", _testTempPath);

            Assert.That(priority, Is.EqualTo(42));
        }

        #endregion

        #region SetModPriorityAsync Tests

        [Test]
        public async Task SetModPriorityAsync_UpdatesPriority()
        {
            await _service.SetModPriorityAsync("new_mod", "New Mod", "Test", 15, _testTempPath);
            _service.InvalidateCache();
            var priority = await _service.GetModPriorityAsync("new_mod", _testTempPath);

            Assert.That(priority, Is.EqualTo(15));
        }

        #endregion

        #region GetOrderedPrioritiesAsync Tests

        [Test]
        public async Task GetOrderedPrioritiesAsync_ReturnsOrderedList()
        {
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("mod_c", "Mod C", "Test", 30);
            config.SetPriority("mod_a", "Mod A", "Test", 10);
            config.SetPriority("mod_b", "Mod B", "Test", 20);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var priorities = await _service.GetOrderedPrioritiesAsync(_testTempPath);

            Assert.That(priorities, Has.Count.EqualTo(3));
            Assert.That(priorities[0].ModId, Is.EqualTo("mod_a"));
            Assert.That(priorities[1].ModId, Is.EqualTo("mod_b"));
            Assert.That(priorities[2].ModId, Is.EqualTo("mod_c"));
        }

        [Test]
        public async Task GetOrderedPrioritiesAsync_FiltersCategory()
        {
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("weather_mod", "Weather", "Weather", 10);
            config.SetPriority("river_mod", "River", "River", 20);
            config.SetPriority("shader_mod", "Shader", "Weather", 15);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var weatherPriorities = await _service.GetOrderedPrioritiesAsync(_testTempPath, "Weather");

            Assert.That(weatherPriorities, Has.Count.EqualTo(2));
            Assert.That(weatherPriorities.All(p => p.Category == "Weather"), Is.True);
        }

        #endregion

        #region ApplyPrioritiesAsync Tests

        [Test]
        public async Task ApplyPrioritiesAsync_UpdatesSourcePriorities()
        {
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("mod1", "Mod 1", "Test", 5);
            config.SetPriority("mod2", "Mod 2", "Test", 50);
            await _service.SaveConfigAsync(config, _testTempPath);
            _service.InvalidateCache();

            var sources = new List<ModSource>
            {
                new ModSource { ModId = "mod1", ModName = "Mod 1" },
                new ModSource { ModId = "mod2", ModName = "Mod 2" },
                new ModSource { ModId = "mod3", ModName = "Mod 3" }
            };

            var result = await _service.ApplyPrioritiesAsync(sources, _testTempPath);

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0].Priority, Is.EqualTo(5));
            Assert.That(result[1].Priority, Is.EqualTo(50));
            Assert.That(result[2].Priority, Is.EqualTo(100));
        }

        [Test]
        public async Task ApplyPrioritiesAsync_ReturnsSortedByPriority()
        {
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

            var result = await _service.ApplyPrioritiesAsync(sources, _testTempPath);

            Assert.That(result[0].ModId, Is.EqualTo("mod_high"));
            Assert.That(result[1].ModId, Is.EqualTo("mod_low"));
        }

        #endregion

        #region Cache Invalidation Tests

        [Test]
        public async Task InvalidateCache_ForcesReload()
        {
            var config1 = await _service.LoadConfigAsync(_testTempPath);
            config1.SetPriority("test", "Test", "Test", 10);
            await _service.SaveConfigAsync(config1, _testTempPath);

            _service.InvalidateCache();
            var config2 = await _service.LoadConfigAsync(_testTempPath);

            Assert.That(config2.GetPriority("test"), Is.EqualTo(10));
        }

        #endregion
    }
}
