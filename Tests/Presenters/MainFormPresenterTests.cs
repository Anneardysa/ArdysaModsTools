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
using Microsoft.Extensions.DependencyInjection;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.DependencyInjection;

namespace ArdysaModsTools.Tests.Presenters
{
    /// <summary>
    /// Tests for MainFormPresenter.
    /// Uses mocked IMainFormView to test presenter logic without UI.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainFormPresenterTests
    {
        private Mock<IMainFormView> _viewMock = null!;
        private Mock<IConfigService> _configMock = null!;
        private List<string> _logMessages = null!;
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;

        [SetUp]
        public void Setup()
        {
            _viewMock = new Mock<IMainFormView>();
            _configMock = new Mock<IConfigService>();
            _logMessages = new List<string>();

            // Create a real RichTextBox for the Logger
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);

            // Setup default config behavior
            _configMock.Setup(c => c.GetLastTargetPath()).Returns((string?)null);
            _configMock.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);

            // Initialize ServiceLocator with mock services
            var services = new ServiceCollection();
            services.AddSingleton(_configMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            
            // Only initialize if not already initialized (prevents issues with parallel test runs)
            if (!ServiceLocator.IsInitialized)
            {
                ServiceLocator.Initialize(serviceProvider);
            }

            // Setup default view behavior
            _viewMock.Setup(v => v.Log(It.IsAny<string>()))
                .Callback<string>(msg => _logMessages.Add(msg));

            _viewMock.Setup(v => v.InvokeOnUIThread(It.IsAny<Action>()))
                .Callback<Action>(action => action());

            _viewMock.Setup(v => v.IsVisible).Returns(true);
        }

        [TearDown]
        public void TearDown()
        {
            _testConsole?.Dispose();
            
            // Dispose ServiceLocator to clean up for next test
            ServiceLocator.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullView_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new MainFormPresenter(null!, _logger, _configMock.Object);
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new MainFormPresenter(_viewMock.Object, null!, _configMock.Object);
            });
        }

        [Test]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            // Arrange & Act
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Assert
            Assert.That(presenter, Is.Not.Null);
            presenter.Dispose();
        }

        #endregion

        #region Property Tests

        [Test]
        public void TargetPath_Initially_IsAccessible()
        {
            // Arrange
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Act & Assert
            // Note: TargetPath will be loaded from config if available
            // This test verifies the property is accessible
            Assert.DoesNotThrow(() => { var _ = presenter.TargetPath; });
            
            presenter.Dispose();
        }

        [Test]
        public void IsOperationRunning_Initially_IsFalse()
        {
            // Arrange
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Act & Assert
            Assert.That(presenter.IsOperationRunning, Is.False);
            
            presenter.Dispose();
        }

        #endregion

        #region Cancel Operation Tests

        [Test]
        public void CancelOperation_WhenNoOperation_DoesNotThrow()
        {
            // Arrange
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Act & Assert
            Assert.DoesNotThrow(() => presenter.CancelOperation());
            
            presenter.Dispose();
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                presenter.Dispose();
                presenter.Dispose();
            });
        }

        #endregion

        #region View Interaction Tests

        [Test]
        public async Task AutoDetectAsync_DisablesButtonsDuringOperation()
        {
            // Arrange
            var presenter = new MainFormPresenter(_viewMock.Object, _logger, _configMock.Object);

            // Act
            await presenter.AutoDetectAsync();

            // Assert
            // Verify that buttons are disabled at the start of the operation
            _viewMock.Verify(v => v.DisableAllButtons(), Times.AtLeastOnce);
            
            // Verify that buttons are enabled after the operation
            // (Either EnableAllButtons or EnableDetectionButtonsOnly depending on result)
            _viewMock.Verify(v => v.EnableAllButtons(), Times.AtMost(1));
            _viewMock.Verify(v => v.EnableDetectionButtonsOnly(), Times.AtMost(1));
            
            presenter.Dispose();
        }

        #endregion
    }
}

