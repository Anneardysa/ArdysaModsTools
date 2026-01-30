/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using Moq;
using ArdysaModsTools.UI.Controls.Actions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Presenters
{
    /// <summary>
    /// Unit tests for ActionPresenter.
    /// Tests button state management and operation lifecycle without UI dependencies.
    /// </summary>
    [TestFixture]
    public class ActionPresenterTests
    {
        private Mock<IActionPanelView> _mockView = null!;
        private TestLogger _testLogger = null!;
        private ActionPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _mockView = new Mock<IActionPanelView>();
            _testLogger = new TestLogger();

            _presenter = new ActionPresenter(_mockView.Object, _testLogger);
        }

        #region Path and Button State Tests

        [Test]
        public void Constructor_InitializesWithButtonsDisabled()
        {
            // Assert - buttons disabled when no path
            _mockView.Verify(v => v.SetInstallEnabled(false), Times.AtLeastOnce);
            _mockView.Verify(v => v.SetDisableEnabled(false), Times.AtLeastOnce);
        }

        [Test]
        public void SetDotaPath_WithNullPath_DisablesButtons()
        {
            // Act
            _presenter.SetDotaPath(null);

            // Assert
            _mockView.Verify(v => v.SetInstallEnabled(false), Times.AtLeast(2));
            _mockView.Verify(v => v.SetDisableEnabled(false), Times.AtLeast(2));
        }

        [Test]
        public void SetDotaPath_WithValidPath_EnablesButtons()
        {
            // Act
            _presenter.SetDotaPath(@"C:\Dota2");

            // Assert
            _mockView.Verify(v => v.SetInstallEnabled(true), Times.AtLeastOnce);
            _mockView.Verify(v => v.SetDisableEnabled(true), Times.AtLeastOnce);
        }

        [Test]
        public void SetDotaPath_LogsPathChange()
        {
            // Act
            _presenter.SetDotaPath(@"C:\Dota2");

            // Assert
            Assert.That(_testLogger.HasLogContaining("C:\\Dota2"), Is.True);
        }

        #endregion

        #region Operation Lifecycle Tests

        [Test]
        public void StartOperation_ShowsProgressState()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");

            // Act
            var cts = _presenter.StartOperation("Testing...");

            // Assert
            _mockView.Verify(v => v.ShowOperationInProgress("Testing..."), Times.Once);
            _mockView.Verify(v => v.SetCancelVisible(true), Times.Once);
            Assert.That(cts, Is.Not.Null);
        }

        [Test]
        public void StartOperation_DisablesAllButtons()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");

            // Act
            _presenter.StartOperation("Testing...");

            // Assert
            _mockView.Verify(v => v.SetAllButtonsEnabled(false), Times.Once);
        }

        [Test]
        public void StartOperation_SetsOperationInProgressFlag()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");

            // Act
            _presenter.StartOperation("Testing...");

            // Assert
            Assert.That(_presenter.IsOperationInProgress, Is.True);
        }

        [Test]
        public void StartOperation_WhenAlreadyInProgress_Throws()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("First op");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _presenter.StartOperation("Second op"));
        }

        [Test]
        public void EndOperation_RestoresNormalState()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("Testing...");

            // Act
            _presenter.EndOperation(true);

            // Assert
            _mockView.Verify(v => v.ShowOperationComplete(), Times.Once);
            _mockView.Verify(v => v.SetCancelVisible(false), Times.AtLeastOnce);
            Assert.That(_presenter.IsOperationInProgress, Is.False);
        }

        [Test]
        public void EndOperation_ReEnablesButtonsWhenPathValid()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("Testing...");

            // Act
            _presenter.EndOperation(true);

            // Assert - Buttons should be re-enabled after operation
            _mockView.Verify(v => v.SetInstallEnabled(true), Times.AtLeast(2));
        }

        [Test]
        public void EndOperation_RaisesOperationEndedEvent()
        {
            // Arrange
            bool? successResult = null;
            _presenter.OperationEnded += success => successResult = success;
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("Testing...");

            // Act
            _presenter.EndOperation(true);

            // Assert
            Assert.That(successResult, Is.True);
        }

        #endregion

        #region Cancel Operation Tests

        [Test]
        public void CancelOperation_CancelsTokenSource()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");
            var cts = _presenter.StartOperation("Testing...");

            // Act
            _presenter.CancelOperation();

            // Assert
            Assert.That(cts.IsCancellationRequested, Is.True);
        }

        [Test]
        public void CancelOperation_RaisesOnCancelRequestedEvent()
        {
            // Arrange
            bool cancelRaised = false;
            _presenter.OnCancelRequested += () => cancelRaised = true;
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("Testing...");

            // Act
            _presenter.CancelOperation();

            // Assert
            Assert.That(cancelRaised, Is.True);
        }

        [Test]
        public void CancelOperation_WhenNotInProgress_DoesNothing()
        {
            // Arrange
            bool cancelRaised = false;
            _presenter.OnCancelRequested += () => cancelRaised = true;

            // Act
            _presenter.CancelOperation();

            // Assert
            Assert.That(cancelRaised, Is.False);
        }

        #endregion

        #region Event Handler Tests

        [Test]
        public void OperationStarted_RaisesEventWithOperationName()
        {
            // Arrange
            string? operationName = null;
            _presenter.OperationStarted += name => operationName = name;
            _presenter.SetDotaPath(@"C:\Dota2");

            // Act
            _presenter.StartOperation("Installing...");

            // Assert
            Assert.That(operationName, Is.EqualTo("Installing..."));
        }

        [Test]
        public void SetButtonsEnabled_WhenInOperation_DoesNotOverride()
        {
            // Arrange
            _presenter.SetDotaPath(@"C:\Dota2");
            _presenter.StartOperation("Testing...");
            _mockView.Invocations.Clear();

            // Act
            _presenter.SetButtonsEnabled(true);

            // Assert - Should not call SetAllButtonsEnabled during operation
            _mockView.Verify(v => v.SetAllButtonsEnabled(true), Times.Never);
        }

        #endregion
    }
}
