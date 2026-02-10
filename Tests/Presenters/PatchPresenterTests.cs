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
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Presenters
{
    /// <summary>
    /// Tests for PatchPresenter.
    /// Tests patch update, verification, and Dota 2 patch watching operations.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class PatchPresenterTests
    {
        private TestServiceFactory _factory = null!;
        private PatchPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _factory = new TestServiceFactory();
            _presenter = new PatchPresenter(_factory.ViewMock.Object, _factory.Logger);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _factory?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullView_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new PatchPresenter(null!, _factory.Logger);
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new PatchPresenter(_factory.ViewMock.Object, null!);
            });
        }

        [Test]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            Assert.That(_presenter, Is.Not.Null);
        }

        #endregion

        #region Operation Tests

        [Test]
        public async Task UpdatePatcherAsync_WhenNoTargetPath_DoesNotThrow()
        {
            // Arrange - no target path set
            _presenter.TargetPath = null;

            // Act & Assert
            await _presenter.UpdatePatcherAsync();
            // Should complete without throwing
        }

        [Test]
        public async Task VerifyModFilesAsync_WhenNoTargetPath_DoesNotThrow()
        {
            // Arrange - no target path set
            _presenter.TargetPath = null;

            // Act & Assert
            await _presenter.VerifyModFilesAsync();
            // Should complete without throwing
        }

        #endregion

        #region Event Tests

        [Test]
        public void PatchDetected_Event_CanBeSubscribed()
        {
            // Arrange
            bool eventRaised = false;
            _presenter.PatchDetected += () => { eventRaised = true; };

            // Assert - event subscription doesn't throw
            Assert.That(eventRaised, Is.False); // Not raised until triggered
        }

        [Test]
        public void StatusRefreshRequested_Event_CanBeSubscribed()
        {
            // Arrange
            bool eventRaised = false;
            _presenter.StatusRefreshRequested += async () => { eventRaised = true; await Task.CompletedTask; };

            // Assert - event subscription doesn't throw
            Assert.That(eventRaised, Is.False); // Not raised until triggered
        }

        #endregion
    }
}
