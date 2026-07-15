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
    public class ConflictDetectorTests
    {
        private ConflictDetector _detector = null!;
        private Mock<IAppLogger> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<IAppLogger>();
            _detector = new ConflictDetector(_mockLogger.Object);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            var detector = new ConflictDetector(null);

            Assert.That(detector, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithLogger_CreatesInstance()
        {
            var detector = new ConflictDetector(_mockLogger.Object);

            Assert.That(detector, Is.Not.Null);
        }

        #endregion

        #region DetectConflictsAsync Tests

        [Test]
        public async Task DetectConflictsAsync_WithEmptyList_ReturnsEmpty()
        {
            var mods = new List<ModSource>();

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Is.Empty);
        }

        [Test]
        public async Task DetectConflictsAsync_WithSingleMod_ReturnsEmpty()
        {
            var mods = new List<ModSource>
            {
                new ModSource { ModId = "mod1", ModName = "Test Mod", Category = "Test" }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Is.Empty);
        }

        [Test]
        public async Task DetectConflictsAsync_WithNoOverlap_ReturnsEmpty()
        {
            var mods = new List<ModSource>
            {
                new ModSource 
                { 
                    ModId = "mod1", 
                    ModName = "Mod 1", 
                    Category = "Weather",
                    AffectedFiles = new List<string> { "file1.txt", "file2.txt" }
                },
                new ModSource 
                { 
                    ModId = "mod2", 
                    ModName = "Mod 2", 
                    Category = "River",
                    AffectedFiles = new List<string> { "file3.txt", "file4.txt" }
                }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Is.Empty);
        }

        [Test]
        public async Task DetectConflictsAsync_WithOverlap_ReturnsConflict()
        {
            var mods = new List<ModSource>
            {
                new ModSource 
                { 
                    ModId = "mod1", 
                    ModName = "Mod 1", 
                    Category = "Weather",
                    AffectedFiles = new List<string> { "shared.txt", "file1.txt" }
                },
                new ModSource 
                { 
                    ModId = "mod2", 
                    ModName = "Mod 2", 
                    Category = "River",
                    AffectedFiles = new List<string> { "shared.txt", "file2.txt" }
                }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].AffectedFiles, Contains.Item("shared.txt"));
        }

        [Test]
        public async Task DetectConflictsAsync_WithMultipleOverlaps_ReturnsMultipleConflicts()
        {
            var mods = new List<ModSource>
            {
                new ModSource 
                { 
                    ModId = "mod1", 
                    ModName = "Mod 1",
                    AffectedFiles = new List<string> { "a.txt", "b.txt" }
                },
                new ModSource 
                { 
                    ModId = "mod2", 
                    ModName = "Mod 2",
                    AffectedFiles = new List<string> { "a.txt" }
                },
                new ModSource 
                { 
                    ModId = "mod3", 
                    ModName = "Mod 3",
                    AffectedFiles = new List<string> { "b.txt" }
                }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Has.Count.EqualTo(2));
        }

        #endregion

        #region CheckSingleConflictAsync Tests

        [Test]
        public async Task CheckSingleConflictAsync_NoOverlap_ReturnsNull()
        {
            var mod1 = new ModSource 
            { 
                ModId = "mod1",
                AffectedFiles = new List<string> { "file1.txt" }
            };
            var mod2 = new ModSource 
            { 
                ModId = "mod2",
                AffectedFiles = new List<string> { "file2.txt" }
            };

            var conflict = await _detector.CheckSingleConflictAsync(mod1, mod2);

            Assert.That(conflict, Is.Null);
        }

        [Test]
        public async Task CheckSingleConflictAsync_WithOverlap_ReturnsConflict()
        {
            var mod1 = new ModSource 
            { 
                ModId = "mod1",
                ModName = "Mod 1",
                AffectedFiles = new List<string> { "shared.txt" }
            };
            var mod2 = new ModSource 
            { 
                ModId = "mod2",
                ModName = "Mod 2",
                AffectedFiles = new List<string> { "shared.txt" }
            };

            var conflict = await _detector.CheckSingleConflictAsync(mod1, mod2);

            Assert.That(conflict, Is.Not.Null);
            Assert.That(conflict!.ConflictingSources, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task CheckSingleConflictAsync_ScriptFile_ReturnsScriptConflict()
        {
            var mod1 = new ModSource 
            { 
                ModId = "mod1",
                ModName = "Mod 1",
                AffectedFiles = new List<string> { "scripts/npc_abilities.txt" }
            };
            var mod2 = new ModSource 
            { 
                ModId = "mod2",
                ModName = "Mod 2",
                AffectedFiles = new List<string> { "scripts/npc_abilities.txt" }
            };

            var conflict = await _detector.CheckSingleConflictAsync(mod1, mod2);

            Assert.That(conflict, Is.Not.Null);
            Assert.That(conflict!.Type, Is.EqualTo(ConflictType.Script));
        }

        #endregion

        #region Severity Tests

        [Test]
        public async Task DetectConflicts_FewFiles_ReturnsMediumOrLowerSeverity()
        {
            var mods = new List<ModSource>
            {
                new ModSource 
                { 
                    ModId = "mod1",
                    ModName = "Mod 1",
                    AffectedFiles = new List<string> { "a.txt", "b.txt" }
                },
                new ModSource 
                { 
                    ModId = "mod2",
                    ModName = "Mod 2",
                    AffectedFiles = new List<string> { "a.txt", "b.txt" }
                }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Severity, Is.LessThanOrEqualTo(ConflictSeverity.Medium));
        }

        [Test]
        public async Task DetectConflicts_ManyFiles_ReturnsHighOrCriticalSeverity()
        {
            var sharedFiles = Enumerable.Range(1, 15).Select(i => $"file{i}.txt").ToList();
            var mods = new List<ModSource>
            {
                new ModSource 
                { 
                    ModId = "mod1",
                    ModName = "Mod 1",
                    Category = "Test",
                    AffectedFiles = sharedFiles
                },
                new ModSource 
                { 
                    ModId = "mod2",
                    ModName = "Mod 2",
                    Category = "Test",
                    AffectedFiles = sharedFiles
                }
            };

            var conflicts = await _detector.DetectConflictsAsync(mods, "C:\\Test");

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].Severity, Is.GreaterThanOrEqualTo(ConflictSeverity.High));
        }

        #endregion

        #region Helper Method Tests

        [Test]
        public void HasCriticalConflicts_NoCritical_ReturnsFalse()
        {
            var conflicts = new List<ModConflict>
            {
                new ModConflict { Severity = ConflictSeverity.Low },
                new ModConflict { Severity = ConflictSeverity.Medium }
            };

            var result = _detector.HasCriticalConflicts(conflicts);

            Assert.That(result, Is.False);
        }

        [Test]
        public void HasCriticalConflicts_WithCritical_ReturnsTrue()
        {
            var conflicts = new List<ModConflict>
            {
                new ModConflict { Severity = ConflictSeverity.Low },
                new ModConflict { Severity = ConflictSeverity.Critical }
            };

            var result = _detector.HasCriticalConflicts(conflicts);

            Assert.That(result, Is.True);
        }

        [Test]
        public void GetConflictsBySeverity_FiltersBySeverity()
        {
            var conflicts = new List<ModConflict>
            {
                new ModConflict { Severity = ConflictSeverity.Low },
                new ModConflict { Severity = ConflictSeverity.Medium },
                new ModConflict { Severity = ConflictSeverity.Medium },
                new ModConflict { Severity = ConflictSeverity.High }
            };

            var mediumConflicts = _detector.GetConflictsBySeverity(conflicts, ConflictSeverity.Medium);

            Assert.That(mediumConflicts, Has.Count.EqualTo(2));
            Assert.That(mediumConflicts.All(c => c.Severity == ConflictSeverity.Medium), Is.True);
        }

        #endregion
    }
}
