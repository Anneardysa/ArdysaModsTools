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
    /// Unit tests for ConflictResolver service.
    /// </summary>
    [TestFixture]
    public class ConflictResolverTests
    {
        private ConflictResolver _resolver = null!;
        private Mock<IAppLogger> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<IAppLogger>();
            _resolver = new ConflictResolver(_mockLogger.Object);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            // Arrange & Act
            var resolver = new ConflictResolver(null);

            // Assert
            Assert.That(resolver, Is.Not.Null);
        }

        #endregion

        #region ResolveAsync Tests

        [Test]
        public async Task ResolveAsync_HigherPriorityStrategy_SelectsLowerPriorityNumber()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1", Priority = 10 };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2", Priority = 50 };
            var conflict = ModConflict.CreateFileConflict(source1, source2, new[] { "test.txt" });

            // Act
            var result = await _resolver.ResolveAsync(conflict, ResolutionStrategy.HigherPriority);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WinningSource?.ModId, Is.EqualTo("mod1"));
        }

        [Test]
        public async Task ResolveAsync_MostRecentStrategy_SelectsNewerMod()
        {
            // Arrange
            var source1 = new ModSource 
            { 
                ModId = "mod1", 
                ModName = "Mod 1", 
                AppliedAt = DateTime.UtcNow.AddHours(-1) 
            };
            var source2 = new ModSource 
            { 
                ModId = "mod2", 
                ModName = "Mod 2", 
                AppliedAt = DateTime.UtcNow 
            };
            var conflict = ModConflict.CreateFileConflict(source1, source2, new[] { "test.txt" });

            // Act
            var result = await _resolver.ResolveAsync(conflict, ResolutionStrategy.MostRecent);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WinningSource?.ModId, Is.EqualTo("mod2"));
        }

        [Test]
        public async Task ResolveAsync_KeepExistingStrategy_SelectsFirstMod()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateFileConflict(source1, source2, new[] { "test.txt" });

            // Act
            var result = await _resolver.ResolveAsync(conflict, ResolutionStrategy.KeepExisting);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WinningSource?.ModId, Is.EqualTo("mod1"));
        }

        [Test]
        public async Task ResolveAsync_UseNewStrategy_SelectsSecondMod()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateFileConflict(source1, source2, new[] { "test.txt" });

            // Act
            var result = await _resolver.ResolveAsync(conflict, ResolutionStrategy.UseNew);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WinningSource?.ModId, Is.EqualTo("mod2"));
        }

        [Test]
        public async Task ResolveAsync_InteractiveStrategy_FailsWithoutUserChoice()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateFileConflict(source1, source2, new[] { "test.txt" });

            // Act
            var result = await _resolver.ResolveAsync(conflict, ResolutionStrategy.Interactive);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("user choice"));
        }

        #endregion

        #region ResolveAllAsync Tests

        [Test]
        public async Task ResolveAllAsync_WithAutoResolve_ResolvesNonCritical()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1", Priority = 10 };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2", Priority = 50 };
            var conflicts = new List<ModConflict>
            {
                ModConflict.CreateFileConflict(source1, source2, new[] { "a.txt" }),
                ModConflict.CreateFileConflict(source2, source1, new[] { "b.txt" })
            };
            var config = ModPriorityConfig.CreateDefault();

            // Act
            var results = await _resolver.ResolveAllAsync(conflicts, config);

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.All(r => r.Success), Is.True);
        }

        [Test]
        public async Task ResolveAllAsync_WithCriticalConflict_FailsResolution()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflicts = new List<ModConflict>
            {
                ModConflict.CreateCriticalConflict(source1, source2, "Test reason")
            };
            var config = ModPriorityConfig.CreateDefault();

            // Act
            var results = await _resolver.ResolveAllAsync(conflicts, config);

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Success, Is.False);
        }

        #endregion

        #region CanAutoResolve Tests

        [Test]
        public void CanAutoResolve_CriticalConflict_ReturnsFalse()
        {
            // Arrange
            var conflict = new ModConflict { Severity = ConflictSeverity.Critical };
            var config = ModPriorityConfig.CreateDefault();

            // Act
            var canResolve = _resolver.CanAutoResolve(conflict, config);

            // Assert
            Assert.That(canResolve, Is.False);
        }

        [Test]
        public void CanAutoResolve_LowSeverity_ReturnsTrue()
        {
            // Arrange
            var conflict = new ModConflict { Severity = ConflictSeverity.Low };
            var config = ModPriorityConfig.CreateDefault();

            // Act
            var canResolve = _resolver.CanAutoResolve(conflict, config);

            // Assert
            Assert.That(canResolve, Is.True);
        }

        [Test]
        public void CanAutoResolve_MediumWithAutoResolveEnabled_ReturnsTrue()
        {
            // Arrange
            var conflict = new ModConflict { Severity = ConflictSeverity.Medium };
            var config = new ModPriorityConfig { AutoResolveNonBreaking = true };

            // Act
            var canResolve = _resolver.CanAutoResolve(conflict, config);

            // Assert
            Assert.That(canResolve, Is.True);
        }

        [Test]
        public void CanAutoResolve_MediumWithAutoResolveDisabled_ReturnsFalse()
        {
            // Arrange
            var conflict = new ModConflict { Severity = ConflictSeverity.Medium };
            var config = new ModPriorityConfig { AutoResolveNonBreaking = false };

            // Act
            var canResolve = _resolver.CanAutoResolve(conflict, config);

            // Assert
            Assert.That(canResolve, Is.False);
        }

        #endregion

        #region ApplyUserChoiceAsync Tests

        [Test]
        public async Task ApplyUserChoiceAsync_WithValidChoice_Succeeds()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateCriticalConflict(source1, source2, "Test");
            var choice = new ConflictResolutionOption
            {
                Id = "user_choice",
                Strategy = ResolutionStrategy.KeepExisting,
                PreferredSource = source1
            };

            // Act
            var result = await _resolver.ApplyUserChoiceAsync(conflict, choice);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WinningSource?.ModId, Is.EqualTo("mod1"));
            Assert.That(conflict.SelectedResolution, Is.EqualTo(choice));
        }

        [Test]
        public async Task ApplyUserChoiceAsync_WithNullSource_FailsForNonMerge()
        {
            // Arrange
            var source1 = new ModSource { ModId = "mod1", ModName = "Mod 1" };
            var source2 = new ModSource { ModId = "mod2", ModName = "Mod 2" };
            var conflict = ModConflict.CreateCriticalConflict(source1, source2, "Test");
            var choice = new ConflictResolutionOption
            {
                Id = "bad_choice",
                Strategy = ResolutionStrategy.KeepExisting,
                PreferredSource = null
            };

            // Act
            var result = await _resolver.ApplyUserChoiceAsync(conflict, choice);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        #endregion
    }
}
