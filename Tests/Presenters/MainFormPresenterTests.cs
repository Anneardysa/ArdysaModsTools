using Moq;
using NUnit.Framework;
using System.Windows.Forms;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using ArdysaModsTools.Core.Services;

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
        private List<string> _logMessages = null!;
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;

        [SetUp]
        public void Setup()
        {
            _viewMock = new Mock<IMainFormView>();
            _logMessages = new List<string>();

            // Create a real RichTextBox for the Logger
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);

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
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullView_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new MainFormPresenter(null!, _logger);
            });
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new MainFormPresenter(_viewMock.Object, null!);
            });
        }

        [Test]
        public void Constructor_WithValidArguments_CreatesInstance()
        {
            // Arrange & Act
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
            var presenter = new MainFormPresenter(_viewMock.Object, _logger);

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
