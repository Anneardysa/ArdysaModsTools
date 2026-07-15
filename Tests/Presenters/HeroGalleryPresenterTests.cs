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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    public class HeroGalleryPresenterTests
    {
        private Mock<IHeroGenerationService> _generation = null!;
        private Mock<IConfigService> _config = null!;
        private Mock<IHeroGalleryView> _view = null!;
        private HeroGalleryPresenter _presenter = null!;

        [SetUp]
        public void Setup()
        {
            _generation = new Mock<IHeroGenerationService>();
            _config = new Mock<IConfigService>();
            _view = new Mock<IHeroGalleryView>();

            _config.Setup(c => c.GetLastTargetPath()).Returns(@"C:\Dota2");
            _view.Setup(v => v.UpdateStatusAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.SaveSelectionsAsync()).Returns(Task.CompletedTask);
            _view.Setup(v => v.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _view.Setup(v => v.ShowGenerationAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
                 .Returns(Task.CompletedTask);
            _view.Setup(v => v.ConfirmBaseNoSetAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _view.Setup(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()))
                 .Returns(true);

            _presenter = new HeroGalleryPresenter(_generation.Object, _config.Object);
            _presenter.SetView(_view.Object);
        }

        #region Helpers

        private static HeroModel Hero(string id, params string[] setNames)
        {
            var sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in setNames)
                sets[s] = new List<string> { $"https://cdn/{s}.zip", $"https://cdn/{s}.png" };

            return new HeroModel { HeroId = id, LocalizedName = id, ItemIds = new List<int> { 1 }, Sets = sets };
        }

        private static Dictionary<string, HeroSelectionState> Sel(
            string heroId, int? set = null, int? @base = null, params int[] items)
        {
            return new Dictionary<string, HeroSelectionState>
            {
                [heroId] = new HeroSelectionState
                {
                    SetIndex = set,
                    BaseIndex = @base,
                    ItemIndices = items.ToList()
                }
            };
        }

        private void SetupGenerationResult(OperationResult result)
        {
            _view.Setup(v => v.RunGenerationWithProgressAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .ReturnsAsync(result);
        }

        #endregion

        #region Constructor

        [Test]
        public void Constructor_NullGenerationService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new HeroGalleryPresenter(null!, _config.Object));
        }

        [Test]
        public void Constructor_NullConfigService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new HeroGalleryPresenter(_generation.Object, null!));
        }

        #endregion

        #region BuildPlan

        [Test]
        public void BuildPlan_CollectsSetItemsAndBaseLayers()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB", "SetC") };
            var selections = Sel("h1", set: 0, @base: 2, items: new[] { 1 });

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(
                plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "SetA", "SetB", "SetC" }));
            Assert.That(plan.HasSelections, Is.True);
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        [Test]
        public void BuildPlan_NegativeIndex_IsIgnored()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: -1);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets, Is.Empty);
        }

        [Test]
        public void BuildPlan_OutOfRangeIndex_IsIgnored()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: 5);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets, Is.Empty);
        }

        [Test]
        public void BuildPlan_SameSetInMultipleSlots_IsDeduplicated()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB") };
            var selections = Sel("h1", set: 0, @base: 0);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Count, Is.EqualTo(1));
            Assert.That(plan.Sets[0].setName, Is.EqualTo("SetA"));
        }

        [Test]
        public void BuildPlan_BaseSelectedWithoutSet_ReportsHero()
        {
            var heroes = new[] { Hero("h1", "SetA", "SetB") };
            var selections = Sel("h1", @base: 1);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.BaseWithoutSetHeroNames, Does.Contain("h1"));
        }

        [Test]
        public void BuildPlan_PrismaticWithBase_IsIncluded()
        {
            var heroes = new[] { Hero("h1", "SetA", "BaseB", "PrismaticC") };
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { BaseIndex = 1, PrismaticIndex = 2 }
            };

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "BaseB", "PrismaticC" }));
        }

        [Test]
        public void BuildPlan_PrismaticWithoutBase_IsDropped()
        {
            var heroes = new[] { Hero("h1", "SetA", "BaseB", "PrismaticC") };
            var selections = new Dictionary<string, HeroSelectionState>
            {
                ["h1"] = new HeroSelectionState { SetIndex = 0, PrismaticIndex = 2 }
            };

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.Sets.Select(s => s.setName).ToList(),
                Is.EquivalentTo(new[] { "SetA" }));
        }

        [Test]
        public void BuildPlan_UnknownHero_IsSkipped()
        {
            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("ghost", set: 0);

            var plan = _presenter.BuildPlan(heroes, selections);

            Assert.That(plan.HasSelections, Is.False);
        }

        [Test]
        public void BuildPlan_NullInputs_ReturnEmptyPlan()
        {
            var plan = _presenter.BuildPlan(null!, null!);
            Assert.That(plan.HasSelections, Is.False);
            Assert.That(plan.BaseWithoutSetHeroNames, Is.Empty);
        }

        #endregion

        #region GenerateAsync

        [Test]
        public async Task GenerateAsync_NoSelections_PromptsAndDoesNotGenerate()
        {
            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") },
                new Dictionary<string, HeroSelectionState>());

            _view.Verify(v => v.UpdateStatusAsync("hero.status.selectOne"), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_BaseNoSetDeclined_Cancels()
        {
            _view.Setup(v => v.ConfirmBaseNoSetAsync(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync(false);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA", "SetB") }, Sel("h1", @base: 1));

            _view.Verify(v => v.UpdateStatusAsync("hero.status.cancelled"), Times.Once);
            _view.Verify(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_NoTargetPath_ShowsWarning()
        {
            _config.Setup(c => c.GetLastTargetPath()).Returns(string.Empty);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowWarning(It.IsAny<string>(), "common.error"), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_OnlyDefaultSet_ShowsNoSelectionsAlert()
        {
            await _presenter.GenerateAsync(new[] { Hero("h1", "Default Set") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowAlertAsync("hero.noSelections.title", It.IsAny<string>()), Times.Once);
            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_PreviewDeclined_DoesNotGenerate()
        {
            _view.Setup(v => v.ShowGenerationPreview(It.IsAny<IReadOnlyList<(HeroModel, string, string?)>>()))
                 .Returns(false);

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Never);
            _view.Verify(v => v.StoreResult(It.IsAny<ModGenerationResult>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_Success_StoresResultAndCloses()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.SaveSelectionsAsync(), Times.Once);
            _view.Verify(v => v.ShowGenerationAlertAsync("hero.complete.title", It.IsAny<string>(), false, It.IsAny<string?>()), Times.Once);
            _view.Verify(v => v.StoreResult(It.Is<ModGenerationResult>(r => r.Success && r.Type == GenerationType.SkinSelector)), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_Failure_StoresResultAndShowsError()
        {
            SetupGenerationResult(new OperationResult { Success = false, Message = "boom" });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.StoreResult(It.Is<ModGenerationResult>(r => !r.Success && r.ErrorMessage == "boom")), Times.Once);
            _view.Verify(v => v.ShowErrorDialog("hero.failed.title", It.IsAny<string>(), "boom"), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_SuccessWithLogLines_PassesLogTextToAlert()
        {
            SetupGenerationResult(new OperationResult
            {
                Success = true,
                Message = "Done",
                SuccessCount = 1,
                LogLines = new[] { "12:00:00 step one", "12:00:01 DEBUG detail" }
            });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowGenerationAlertAsync(
                "hero.complete.title",
                It.IsAny<string>(),
                false,
                It.Is<string?>(t => t != null && t.Contains("step one") && t.Contains("DEBUG detail"))), Times.Once);
            _view.Verify(v => v.CloseWithSuccess(), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_SuccessWithoutLogLines_PassesNullLogText()
        {
            SetupGenerationResult(new OperationResult { Success = true, Message = "Done", SuccessCount = 1 });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowGenerationAlertAsync(
                "hero.complete.title", It.IsAny<string>(), false, null), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_FailureWithLogLines_AppendsLogToErrorDetails()
        {
            SetupGenerationResult(new OperationResult
            {
                Success = false,
                Message = "boom",
                LogLines = new[] { "12:00:00 step one" }
            });

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowErrorDialog(
                "hero.failed.title",
                It.IsAny<string>(),
                It.Is<string>(d => d.Contains("boom") && d.Contains("step one"))), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_Canceled_NoErrorDialog()
        {
            SetupGenerationResult(OperationResult.Canceled());

            await _presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0));

            _view.Verify(v => v.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _view.Verify(v => v.StoreResult(It.IsAny<ModGenerationResult>()), Times.Never);
        }

        [Test]
        public async Task GenerateAsync_IsReentrant_SecondCallIgnoredWhileBusy()
        {
            var gate = new TaskCompletionSource<OperationResult>();
            _view.Setup(v => v.RunGenerationWithProgressAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()))
                 .Returns(gate.Task);

            var heroes = new[] { Hero("h1", "SetA") };
            var selections = Sel("h1", set: 0);

            var first = _presenter.GenerateAsync(heroes, selections);
            var second = _presenter.GenerateAsync(heroes, selections);

            await second;
            gate.SetResult(new OperationResult { Success = true, Message = "Done" });
            await first;

            _view.Verify(v => v.RunGenerationWithProgressAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ProgressOperationRunnerContext, Task<OperationResult>>>()), Times.Once);
        }

        [Test]
        public async Task GenerateAsync_WithoutView_Throws()
        {
            var presenter = new HeroGalleryPresenter(_generation.Object, _config.Object);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await presenter.GenerateAsync(new[] { Hero("h1", "SetA") }, Sel("h1", set: 0)));
        }

        #endregion
    }
}
