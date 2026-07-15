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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class NavigationPresenterTests
    {
        private TestServiceFactory _factory = null!;
        private NavigationPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _factory = new TestServiceFactory();
            _presenter = new NavigationPresenter(_factory.ViewMock.Object, _factory.Logger, new StatusService(_factory.Logger));
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
                new NavigationPresenter(null!, _factory.Logger, new StatusService(_factory.Logger));
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new NavigationPresenter(_factory.ViewMock.Object, null!, new StatusService(_factory.Logger));
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
        public void TargetPath_CanBeSetAndRetrieved()
        {
            const string testPath = @"C:\Program Files\Steam\steamapps\common\dota 2 beta";

            _presenter.TargetPath = testPath;

            Assert.That(_presenter.TargetPath, Is.EqualTo(testPath));
        }

        [Test]
        public void TargetPath_Initially_IsNull()
        {
            Assert.That(_presenter.TargetPath, Is.Null);
        }

        [Test]
        public void CurrentStatus_CanBeSetAndRetrieved()
        {
            var testStatus = new ModStatusInfo { Status = ModStatus.Ready };

            _presenter.CurrentStatus = testStatus;

            Assert.That(_presenter.CurrentStatus, Is.EqualTo(testStatus));
        }

        #endregion

        #region Navigation Tests

        [Test]
        public async Task OpenHeroSelectionAsync_WhenNoTargetPath_DoesNotThrow()
        {
            _presenter.TargetPath = null;

            await _presenter.OpenHeroSelectionAsync();
        }

        [Test]
        public async Task OpenMiscellaneousAsync_WhenNoTargetPath_DoesNotThrow()
        {
            _presenter.TargetPath = null;

            await _presenter.OpenMiscellaneousAsync();
        }

        #endregion

        #region Event Tests

        [Test]
        public void StatusRefreshRequested_Event_CanBeSubscribed()
        {
            bool eventRaised = false;
            _presenter.StatusRefreshRequested += async () => { eventRaised = true; await Task.CompletedTask; };

            Assert.That(eventRaised, Is.False);
        }

        [Test]
        public void PatchRequested_Event_CanBeSubscribed()
        {
            bool eventRaised = false;
            _presenter.PatchRequested += async () => { eventRaised = true; await Task.CompletedTask; };

            Assert.That(eventRaised, Is.False);
        }

        #endregion
    }
}
