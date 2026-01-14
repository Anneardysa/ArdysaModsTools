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
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Tests.Presenters
{
    /// <summary>
    /// Tests for SelectHeroPresenter.
    /// Uses mocked ISelectHeroView to test presenter logic without UI.
    /// </summary>
    [TestFixture]
    public class SelectHeroPresenterTests
    {
        private Mock<ISelectHeroView> _viewMock = null!;

        [SetUp]
        public void Setup()
        {
            _viewMock = new Mock<ISelectHeroView>();

            // Setup default view behavior
            _viewMock.Setup(v => v.InvokeOnUIThread(It.IsAny<Action>()))
                .Callback<Action>(action => action());

            _viewMock.Setup(v => v.GetSelections())
                .Returns(Enumerable.Empty<(string, string)>());
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullView_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SelectHeroPresenter(null!));
        }

        [Test]
        public void Constructor_WithValidView_CreatesInstance()
        {
            // Arrange & Act
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Assert
            Assert.That(presenter, Is.Not.Null);
        }

        #endregion

        #region Selection Tests

        [Test]
        public void SetSelection_WithValidData_StoresSelection()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            presenter.SetSelection("npc_dota_hero_juggernaut", "Bladeform Legacy");

            // Assert
            var selection = presenter.GetSelection("npc_dota_hero_juggernaut");
            Assert.That(selection, Is.EqualTo("Bladeform Legacy"));
        }

        [Test]
        public void SetSelection_WithEmptySetName_RemovesSelection()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);
            presenter.SetSelection("npc_dota_hero_juggernaut", "Set1");

            // Act
            presenter.SetSelection("npc_dota_hero_juggernaut", "");

            // Assert
            var selection = presenter.GetSelection("npc_dota_hero_juggernaut");
            Assert.That(selection, Is.Null);
        }

        [Test]
        public void GetSelection_WhenNotSet_ReturnsNull()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            var selection = presenter.GetSelection("nonexistent_hero");

            // Assert
            Assert.That(selection, Is.Null);
        }

        [Test]
        public void ClearSelections_RemovesAllSelections()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);
            presenter.SetSelection("hero1", "set1");
            presenter.SetSelection("hero2", "set2");

            // Act
            presenter.ClearSelections();

            // Assert
            Assert.That(presenter.GetSelections(), Is.Empty);
            _viewMock.Verify(v => v.ClearSelections(), Times.Once);
        }

        #endregion

        #region Favorites Tests

        [Test]
        public void ToggleFavorite_WhenNotFavorite_AddsFavorite()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            var result = presenter.ToggleFavorite("npc_dota_hero_juggernaut");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(presenter.IsFavorite("npc_dota_hero_juggernaut"), Is.True);
        }

        [Test]
        public void ToggleFavorite_WhenFavorite_RemovesFavorite()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);
            presenter.ToggleFavorite("npc_dota_hero_juggernaut"); // Add

            // Act
            var result = presenter.ToggleFavorite("npc_dota_hero_juggernaut"); // Remove

            // Assert
            Assert.That(result, Is.False);
            Assert.That(presenter.IsFavorite("npc_dota_hero_juggernaut"), Is.False);
        }

        [Test]
        public void IsFavorite_WithEmptyId_ReturnsFalse()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act & Assert
            Assert.That(presenter.IsFavorite(""), Is.False);
        }

        #endregion

        #region Filtering Tests

        [Test]
        public void SetCategoryFilter_UpdatesFilter()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            presenter.SetCategoryFilter("strength");

            // Assert
            _viewMock.Verify(v => v.ApplyCategoryFilter("strength"), Times.Once);
        }

        [Test]
        public void SetSearchFilter_UpdatesFilter()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            presenter.SetSearchFilter("jugg");

            // Assert
            _viewMock.Verify(v => v.ApplySearchFilter("jugg"), Times.Once);
        }

        [Test]
        public void SetSearchFilter_WithNull_UsesEmptyString()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            presenter.SetSearchFilter(null!);

            // Assert
            _viewMock.Verify(v => v.ApplySearchFilter(""), Times.Once);
        }

        #endregion

        #region Generation Tests

        [Test]
        public async Task GenerateAsync_WhenNoSelections_ShowsWarning()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act
            var result = await presenter.GenerateAsync();

            // Assert
            Assert.That(result, Is.False);
            _viewMock.Verify(v => v.ShowMessageBox(
                It.Is<string>(s => s.Contains("No selections")),
                It.IsAny<string>(),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning), Times.Once);
        }

        [Test]
        public void IsGenerating_Initially_IsFalse()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Assert
            Assert.That(presenter.IsGenerating, Is.False);
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var presenter = new SelectHeroPresenter(_viewMock.Object);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                presenter.Dispose();
                presenter.Dispose();
            });
        }

        #endregion
    }
}

