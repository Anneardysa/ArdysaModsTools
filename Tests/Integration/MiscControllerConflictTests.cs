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

namespace ArdysaModsTools.Tests.Integration
{
    /// <summary>
    /// Integration tests for the conflict resolution workflow.
    /// Tests the integration between ConflictDetector, ConflictResolver, and ModPriorityService.
    /// </summary>
    [TestFixture]
    public class ConflictResolutionWorkflowTests
    {
        private ConflictDetector _detector = null!;
        private ConflictResolver _resolver = null!;
        private ModPriorityService _priorityService = null!;
        private Mock<IAppLogger> _mockLogger = null!;
        private string _testPath = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<IAppLogger>();
            _detector = new ConflictDetector(_mockLogger.Object);
            _resolver = new ConflictResolver(_mockLogger.Object);
            _priorityService = new ModPriorityService(_mockLogger.Object);
            
            _testPath = Path.Combine(Path.GetTempPath(), $"ConflictTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testPath))
            {
                try { Directory.Delete(_testPath, true); }
                catch { /* Ignore cleanup errors */ }
            }
        }

        #region Full Workflow Tests

        [Test]
        public async Task FullWorkflow_DetectAndResolve_WithNonCriticalConflict()
        {
            // Arrange - Two mods with overlapping files
            var mod1 = new ModSource
            {
                ModId = "Weather_Ash",
                ModName = "Ash Weather",
                Category = "Weather",
                Priority = 10,
                AffectedFiles = new List<string> { "particles/effects.txt", "shared/config.txt" }
            };
            var mod2 = new ModSource
            {
                ModId = "River_Lava",
                ModName = "Lava River",
                Category = "River",
                Priority = 50,
                AffectedFiles = new List<string> { "particles/river.txt", "shared/config.txt" }
            };

            var mods = new List<ModSource> { mod1, mod2 };

            // Act - Detect conflicts
            var conflicts = await _detector.DetectConflictsAsync(mods, _testPath);

            // Assert detection
            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].AffectedFiles, Contains.Item("shared/config.txt"));
            Assert.That(_detector.HasCriticalConflicts(conflicts), Is.False);

            // Act - Resolve conflicts
            var config = ModPriorityConfig.CreateDefault();
            var resolutions = await _resolver.ResolveAllAsync(conflicts, config);

            // Assert resolution
            Assert.That(resolutions, Has.Count.EqualTo(1));
            Assert.That(resolutions[0].Success, Is.True);
            // One of the mods should win - the specific winner depends on resolver strategy
            Assert.That(resolutions[0].WinningSource, Is.Not.Null);
            Assert.That(resolutions[0].WinningSource!.ModId, Is.AnyOf("Weather_Ash", "River_Lava"));
        }

        [Test]
        public async Task FullWorkflow_DetectCriticalConflict_BlocksAutoResolve()
        {
            // Arrange - Many overlapping files in same category (critical)
            var sharedFiles = Enumerable.Range(1, 15)
                .Select(i => $"data/file{i}.txt")
                .ToList();

            var mod1 = new ModSource
            {
                ModId = "Shader_A",
                ModName = "Shader A",
                Category = "Shader",
                AffectedFiles = sharedFiles
            };
            var mod2 = new ModSource
            {
                ModId = "Shader_B",
                ModName = "Shader B",
                Category = "Shader",
                AffectedFiles = sharedFiles
            };

            var mods = new List<ModSource> { mod1, mod2 };

            // Act - Detect
            var conflicts = await _detector.DetectConflictsAsync(mods, _testPath);

            // Assert - Should be critical
            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Severity, Is.EqualTo(ConflictSeverity.Critical));
            Assert.That(_detector.HasCriticalConflicts(conflicts), Is.True);
            Assert.That(_detector.RequiresUserIntervention(conflicts), Is.True);

            // Act - Try to auto-resolve
            var config = ModPriorityConfig.CreateDefault();
            var canAuto = _resolver.CanAutoResolve(conflicts[0], config);

            // Assert - Cannot auto-resolve critical
            Assert.That(canAuto, Is.False);
        }

        [Test]
        public async Task FullWorkflow_PriorityBasedResolution_RespectsConfig()
        {
            // Arrange
            var config = ModPriorityConfig.CreateDefault();
            config.SetPriority("high_priority_mod", "High Priority", "Test", 1);
            config.SetPriority("low_priority_mod", "Low Priority", "Test", 100);
            await _priorityService.SaveConfigAsync(config, _testPath);

            // Create mods with overlapping files
            var highPriorityMod = new ModSource
            {
                ModId = "high_priority_mod",
                ModName = "High Priority",
                Category = "Test",
                AffectedFiles = new List<string> { "overlap.txt" }
            };
            var lowPriorityMod = new ModSource
            {
                ModId = "low_priority_mod",
                ModName = "Low Priority",
                Category = "Test",
                AffectedFiles = new List<string> { "overlap.txt" }
            };

            // Apply priorities
            _priorityService.InvalidateCache();
            var orderedMods = await _priorityService.ApplyPrioritiesAsync(
                new[] { lowPriorityMod, highPriorityMod },
                _testPath);

            // Assert priorities applied
            var highMod = orderedMods.First(m => m.ModId == "high_priority_mod");
            var lowMod = orderedMods.First(m => m.ModId == "low_priority_mod");
            Assert.That(highMod.Priority, Is.LessThan(lowMod.Priority));

            // Detect and resolve
            var conflicts = await _detector.DetectConflictsAsync(orderedMods, _testPath);
            Assert.That(conflicts, Has.Count.EqualTo(1));

            var loadedConfig = await _priorityService.LoadConfigAsync(_testPath);
            var resolutions = await _resolver.ResolveAllAsync(conflicts, loadedConfig);

            // Assert high priority wins
            Assert.That(resolutions[0].WinningSource?.ModId, Is.EqualTo("high_priority_mod"));
        }

        #endregion

        #region Multi-Mod Scenarios

        [Test]
        public async Task MultiMod_ThreeWayConflict_DetectsAllPairs()
        {
            // Arrange - Three mods all sharing one file
            var sharedFile = "shared/common.txt";
            var mod1 = new ModSource { ModId = "mod1", ModName = "Mod 1", AffectedFiles = new List<string> { sharedFile } };
            var mod2 = new ModSource { ModId = "mod2", ModName = "Mod 2", AffectedFiles = new List<string> { sharedFile } };
            var mod3 = new ModSource { ModId = "mod3", ModName = "Mod 3", AffectedFiles = new List<string> { sharedFile } };

            var mods = new List<ModSource> { mod1, mod2, mod3 };

            // Act
            var conflicts = await _detector.DetectConflictsAsync(mods, _testPath);

            // Assert - Should detect pairwise conflicts (3 pairs: 1-2, 1-3, 2-3)
            Assert.That(conflicts, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task MultiMod_IndependentMods_NoConflicts()
        {
            // Arrange - Three mods with no overlap
            var mod1 = new ModSource { ModId = "mod1", AffectedFiles = new List<string> { "a.txt" } };
            var mod2 = new ModSource { ModId = "mod2", AffectedFiles = new List<string> { "b.txt" } };
            var mod3 = new ModSource { ModId = "mod3", AffectedFiles = new List<string> { "c.txt" } };

            var mods = new List<ModSource> { mod1, mod2, mod3 };

            // Act
            var conflicts = await _detector.DetectConflictsAsync(mods, _testPath);

            // Assert
            Assert.That(conflicts, Is.Empty);
        }

        #endregion

        #region User Choice Resolution

        [Test]
        public async Task UserChoice_ApplySelectedOption_MarksConflictResolved()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateCriticalConflict(source1, source2, "Test conflict");

            // User selects first option
            var chosenOption = conflict.AvailableResolutions.First();

            // Act
            var result = await _resolver.ApplyUserChoiceAsync(conflict, chosenOption);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(conflict.IsResolved, Is.True);
            Assert.That(conflict.SelectedResolution, Is.Not.Null);
        }

        #endregion
    }
}
