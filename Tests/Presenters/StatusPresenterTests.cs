/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Drawing;
using NUnit.Framework;
using Moq;
using ArdysaModsTools.UI.Controls.Status;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Presenters
{
    /// <summary>
    /// Unit tests for StatusPresenter.
    /// Tests business logic without requiring actual UI controls.
    /// </summary>
    [TestFixture]
    public class StatusPresenterTests
    {
        private Mock<IStatusIndicatorView> _mockView = null!;
        private Mock<IStatusService> _mockStatusService = null!;
        private TestLogger _testLogger = null!;
        private StatusPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _mockView = new Mock<IStatusIndicatorView>();
            _mockStatusService = new Mock<IStatusService>();
            _testLogger = new TestLogger();

            _presenter = new StatusPresenter(
                _mockView.Object,
                _mockStatusService.Object,
                _testLogger);
        }

        [Test]
        public async Task SetPathAndCheckAsync_WithNullPath_ShowsNotCheckedState()
        {
            // Act
            await _presenter.SetPathAndCheckAsync(null);

            // Assert
            _mockView.Verify(v => v.SetStatus(
                It.IsAny<Color>(),
                "Not Checked"), Times.Once);
        }

        [Test]
        public async Task SetPathAndCheckAsync_WithValidPath_ShowsCheckingThenStatus()
        {
            // Arrange
            var statusInfo = new ModStatusInfo
            {
                Status = ModStatus.Ready,
                StatusText = "Mods Ready"
            };
            _mockStatusService
                .Setup(s => s.GetDetailedStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(statusInfo);

            // Act
            await _presenter.SetPathAndCheckAsync(@"C:\Dota2");

            // Assert
            _mockView.Verify(v => v.ShowCheckingState(), Times.Once);
            _mockView.Verify(v => v.SetStatus(
                It.IsAny<Color>(),
                "Mods Ready"), Times.Once);
        }

        [Test]
        public async Task SetPathAndCheckAsync_WhenServiceThrows_ShowsError()
        {
            // Arrange
            _mockStatusService
                .Setup(s => s.GetDetailedStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            // Act
            await _presenter.SetPathAndCheckAsync(@"C:\Dota2");

            // Assert
            _mockView.Verify(v => v.ShowError("Error"), Times.Once);
            Assert.That(_testLogger.ErrorCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RefreshStatusAsync_WithNoPath_ShowsNoPathMessage()
        {
            // Act
            await _presenter.RefreshStatusAsync();

            // Assert
            _mockView.Verify(v => v.SetStatus(
                It.IsAny<Color>(),
                "No path set"), Times.Once);
        }

        [Test]
        public async Task RefreshStatusAsync_DisablesRefreshButton()
        {
            // Arrange
            await _presenter.SetPathAndCheckAsync(@"C:\Dota2");

            var statusInfo = new ModStatusInfo
            {
                Status = ModStatus.Ready,
                StatusText = "Mods Ready"
            };
            _mockStatusService
                .Setup(s => s.ForceRefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(statusInfo);

            // Act
            await _presenter.RefreshStatusAsync();

            // Assert - SetRefreshEnabled should have been called
            _mockView.Verify(v => v.SetRefreshEnabled(false), Times.AtLeastOnce);
            _mockView.Verify(v => v.SetRefreshEnabled(true), Times.AtLeastOnce);
        }

        [Test]
        public async Task StatusChanged_RaisesEventWithStatusInfo()
        {
            // Arrange
            ModStatusInfo? receivedStatus = null;
            _presenter.StatusChanged += status => receivedStatus = status;

            var statusInfo = new ModStatusInfo
            {
                Status = ModStatus.Ready,
                StatusText = "Test Status"
            };
            _mockStatusService
                .Setup(s => s.GetDetailedStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(statusInfo);

            // Act
            await _presenter.SetPathAndCheckAsync(@"C:\Dota2");

            // Assert
            Assert.That(receivedStatus, Is.Not.Null);
            Assert.That(receivedStatus?.StatusText, Is.EqualTo("Test Status"));
        }
    }
}
