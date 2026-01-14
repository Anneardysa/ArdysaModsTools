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
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for enhanced StatusService.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class StatusServiceTests
    {
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;
        private StatusService _service = null!;

        [SetUp]
        public void Setup()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new StatusService(_logger);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _testConsole?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithLogger_CreatesInstance()
        {
            var service = new StatusService(_logger);
            Assert.That(service, Is.Not.Null);
            service.Dispose();
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new StatusService(null!);
            });
        }

        #endregion

        #region GetDetailedStatusAsync Tests

        [Test]
        public async Task GetDetailedStatusAsync_WithNullPath_ReturnsNotChecked()
        {
            var result = await _service.GetDetailedStatusAsync(null);
            
            Assert.That(result.Status, Is.EqualTo(ModStatus.NotChecked));
            Assert.That(result.StatusText, Is.EqualTo("Path Not Set"));
        }

        [Test]
        public async Task GetDetailedStatusAsync_WithEmptyPath_ReturnsNotChecked()
        {
            var result = await _service.GetDetailedStatusAsync("");
            
            Assert.That(result.Status, Is.EqualTo(ModStatus.NotChecked));
        }

        [Test]
        public async Task GetDetailedStatusAsync_WithInvalidPath_ReturnsError()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            var result = await _service.GetDetailedStatusAsync(invalidPath);
            
            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.StatusText, Is.EqualTo("Invalid Path"));
        }

        #endregion

        #region ModStatusInfo Tests

        [Test]
        public void ModStatusInfo_Ready_HasCorrectProperties()
        {
            var info = new ModStatusInfo
            {
                Status = ModStatus.Ready,
                StatusText = "Ready",
                Description = "Mods are active",
                Action = RecommendedAction.None
            };

            Assert.That(info.Status, Is.EqualTo(ModStatus.Ready));
            Assert.That(info.StatusText, Is.EqualTo("Ready"));
            Assert.That(info.Action, Is.EqualTo(RecommendedAction.None));
        }

        [Test]
        public void ModStatusInfo_NeedUpdate_HasUpdateAction()
        {
            var info = new ModStatusInfo
            {
                Status = ModStatus.NeedUpdate,
                Action = RecommendedAction.Update,
                ActionButtonText = "Patch Update"
            };

            Assert.That(info.Status, Is.EqualTo(ModStatus.NeedUpdate));
            Assert.That(info.Action, Is.EqualTo(RecommendedAction.Update));
            Assert.That(info.ActionButtonText, Is.EqualTo("Patch Update"));
        }

        [Test]
        public void ModStatusInfo_NotInstalled_HasInstallAction()
        {
            var info = new ModStatusInfo
            {
                Status = ModStatus.NotInstalled,
                Action = RecommendedAction.Install,
                ActionButtonText = "Install ModsPack"
            };

            Assert.That(info.Action, Is.EqualTo(RecommendedAction.Install));
        }

        #endregion

        #region AutoRefresh Tests

        [Test]
        public void StartAutoRefresh_WithValidPath_DoesNotThrow()
        {
            var tempPath = Path.GetTempPath();
            
            Assert.DoesNotThrow(() =>
            {
                _service.StartAutoRefresh(tempPath);
                _service.StopAutoRefresh();
            });
        }

        [Test]
        public void StopAutoRefresh_WhenNotStarted_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _service.StopAutoRefresh();
            });
        }

        [Test]
        public void GetCachedStatus_Initially_ReturnsNull()
        {
            var cached = _service.GetCachedStatus();
            Assert.That(cached, Is.Null);
        }

        #endregion

        #region Event Tests

        [Test]
        public async Task RefreshStatusAsync_FiresOnStatusChangedEvent()
        {
            ModStatusInfo? receivedStatus = null;
            _service.OnStatusChanged += status => receivedStatus = status;

            await _service.RefreshStatusAsync(null);

            Assert.That(receivedStatus, Is.Not.Null);
            Assert.That(receivedStatus!.Status, Is.EqualTo(ModStatus.NotChecked));
        }

        #endregion
    }
}

