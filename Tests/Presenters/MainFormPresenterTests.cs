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
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainFormPresenterTests
    {
        private TestServiceFactory _factory = null!;
        private Mock<IMainFormView> _viewMock = null!;
        private Mock<IConfigService> _configMock = null!;
        private List<string> _logMessages = null!;
        private Logger _logger = null!;
        private IStatusService _statusService = null!;

        [SetUp]
        public void Setup()
        {
            _factory = new TestServiceFactory();
            _viewMock = _factory.ViewMock;
            _configMock = _factory.ConfigMock;
            _logMessages = _factory.LogMessages;
            _logger = _factory.Logger;
            _statusService = new StatusService(_logger);
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
                new MainFormPresenter(null!, _logger, _configMock.Object, _statusService);
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new MainFormPresenter(_viewMock.Object, null!, _configMock.Object, _statusService);
            });
        }

        [Test]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.That(presenter, Is.Not.Null);
            presenter.Dispose();
        }

        #endregion

        #region Property Tests

        [Test]
        public void TargetPath_Initially_IsAccessible()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.DoesNotThrow(() => { var _ = presenter.TargetPath; });
            
            presenter.Dispose();
        }

        [Test]
        public void IsOperationRunning_Initially_IsFalse()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.That(presenter.IsOperationRunning, Is.False);
            
            presenter.Dispose();
        }

        #endregion

        #region Cancel Operation Tests

        [Test]
        public void CancelOperation_WhenNoOperation_DoesNotThrow()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.DoesNotThrow(() => presenter.CancelOperation());
            
            presenter.Dispose();
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.DoesNotThrow(() =>
            {
                presenter.Dispose();
                presenter.Dispose();
            });
        }

        #endregion

        #region Shutdown Tests

        [Test]
        public async Task ShutdownAsync_WhenIdle_CompletesPromptlyAndDisposes()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            var shutdown = presenter.ShutdownAsync();
            var finished = await Task.WhenAny(shutdown, Task.Delay(2000)) == shutdown;

            Assert.That(finished, Is.True, "ShutdownAsync should complete promptly when idle.");
            Assert.That(presenter.IsOperationRunning, Is.False);

            Assert.DoesNotThrow(() => presenter.Dispose());
        }

        [Test]
        public async Task ShutdownAsync_CanBeCalledWithoutPriorOperation()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            Assert.DoesNotThrowAsync(async () => await presenter.ShutdownAsync());
            await Task.CompletedTask;
        }

        #endregion

        #region View Interaction Tests

        [Test]
        public async Task AutoDetectAsync_DisablesButtonsDuringOperation()
        {
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object, _statusService);

            await presenter.AutoDetectAsync();

            _viewMock.Verify(v => v.DisableAllButtons(), Times.AtLeastOnce);
            
            _viewMock.Verify(v => v.EnableAllButtons(), Times.AtMost(1));
            _viewMock.Verify(v => v.EnableDetectionButtonsOnly(), Times.AtMost(1));
            
            presenter.Dispose();
        }

        #endregion
    }
}

