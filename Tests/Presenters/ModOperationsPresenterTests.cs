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
    /// Tests for ModOperationsPresenter.
    /// Tests install, reinstall, and disable operations.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ModOperationsPresenterTests
    {
        private TestServiceFactory _factory = null!;
        private ModOperationsPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _factory = new TestServiceFactory();
            _presenter = new ModOperationsPresenter(_factory.ViewMock.Object, _factory.Logger);
        }

        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullView_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ModOperationsPresenter(null!, _factory.Logger);
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ModOperationsPresenter(_factory.ViewMock.Object, null!);
            });
        }

        [Test]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            Assert.That(_presenter, Is.Not.Null);
        }

        #endregion

        #region Property Tests

        [Test]
        public void IsOperationRunning_Initially_IsFalse()
        {
            Assert.That(_presenter.IsOperationRunning, Is.False);
        }

        [Test]
        public void TargetPath_CanBeSetAndRetrieved()
        {
            // Arrange
            const string testPath = @"C:\Program Files\Steam\steamapps\common\dota 2 beta";

            // Act
            _presenter.TargetPath = testPath;

            // Assert
            Assert.That(_presenter.TargetPath, Is.EqualTo(testPath));
        }

        [Test]
        public void TargetPath_Initially_IsNull()
        {
            Assert.That(_presenter.TargetPath, Is.Null);
        }

        #endregion

        #region CancelOperation Tests

        [Test]
        public void CancelOperation_WhenNoOperation_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _presenter.CancelOperation());
        }

        #endregion

        #region Event Tests

        [Test]
        public async Task InstallAsync_WhenNoTargetPath_ReturnsEarlyWithoutException()
        {
            // Arrange - no target path set
            _presenter.TargetPath = null;

            // Act
            var result = await _presenter.InstallAsync();

            // Assert - should return false since no target path
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task DisableAsync_WhenNoTargetPath_DoesNotThrow()
        {
            // Arrange - no target path set
            _presenter.TargetPath = null;

            // Act & Assert
            await _presenter.DisableAsync();
            // Should complete without throwing
        }

        #endregion
    }
}
