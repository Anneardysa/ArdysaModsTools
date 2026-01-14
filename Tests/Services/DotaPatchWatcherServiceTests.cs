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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Update;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for DotaPatchWatcherService.
    /// </summary>
    [TestFixture]
    public class DotaPatchWatcherServiceTests
    {
        private DotaPatchWatcherService _service = null!;

        [SetUp]
        public void Setup()
        {
            // Create service without logger for testing
            _service = new DotaPatchWatcherService();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithoutLogger_CreatesInstance()
        {
            using var service = new DotaPatchWatcherService();
            Assert.That(service, Is.Not.Null);
            Assert.That(service.IsWatching, Is.False);
        }

        #endregion

        #region StartWatching Tests

        [Test]
        public void StartWatchingAsync_WithNullPath_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await _service.StartWatchingAsync(null!);
            });
            Assert.That(_service.IsWatching, Is.False);
        }

        [Test]
        public void StartWatchingAsync_WithEmptyPath_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await _service.StartWatchingAsync("");
            });
            Assert.That(_service.IsWatching, Is.False);
        }

        [Test]
        public async Task StartWatchingAsync_WithValidPath_StartsWatching()
        {
            // Use temp directory as a valid path (won't have Dota files but should not crash)
            var tempPath = Path.GetTempPath();
            
            await _service.StartWatchingAsync(tempPath);
            
            // Should be watching even if files don't exist
            Assert.That(_service.IsWatching, Is.True);
        }

        #endregion

        #region StopWatching Tests

        [Test]
        public void StopWatching_WhenNotStarted_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _service.StopWatching();
            });
        }

        [Test]
        public async Task StopWatching_AfterStarting_StopsWatching()
        {
            var tempPath = Path.GetTempPath();
            await _service.StartWatchingAsync(tempPath);
            
            _service.StopWatching();
            
            Assert.That(_service.IsWatching, Is.False);
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_WhenNotStarted_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _service.Dispose();
            });
        }

        [Test]
        public async Task Dispose_AfterStarting_StopsWatching()
        {
            var tempPath = Path.GetTempPath();
            await _service.StartWatchingAsync(tempPath);
            
            _service.Dispose();
            
            Assert.That(_service.IsWatching, Is.False);
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _service.Dispose();
                _service.Dispose();
            });
        }

        #endregion

        #region PatchDetectedEventArgs Tests

        [Test]
        public void PatchDetectedEventArgs_ChangeSummary_WithVersionChange_IncludesVersion()
        {
            var args = new PatchDetectedEventArgs
            {
                OldVersion = "Dec 20 2025",
                NewVersion = "Jan 05 2026",
                OldDigest = "ABC123",
                NewDigest = "ABC123"
            };

            Assert.That(args.ChangeSummary, Does.Contain("Dec 20 2025"));
            Assert.That(args.ChangeSummary, Does.Contain("Jan 05 2026"));
        }

        [Test]
        public void PatchDetectedEventArgs_ChangeSummary_WithDigestChange_IncludesDigest()
        {
            var args = new PatchDetectedEventArgs
            {
                OldVersion = "Same",
                NewVersion = "Same",
                OldDigest = "ABCDEF123456789",
                NewDigest = "ZYXWVU987654321"
            };

            Assert.That(args.ChangeSummary, Does.Contain("DIGEST"));
        }

        [Test]
        public void PatchDetectedEventArgs_RequiresRepatch_DefaultsFalse()
        {
            var args = new PatchDetectedEventArgs();
            Assert.That(args.RequiresRepatch, Is.False);
        }

        #endregion
    }
}

