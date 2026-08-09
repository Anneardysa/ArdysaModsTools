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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class LaunchPresenterTests
    {
        private const string Target = @"C:\Games\steamapps\common\dota 2 beta";

        private RichTextBox _console = null!;
        private Logger _logger = null!;
        private Mock<IMainFormView> _view = null!;
        private Mock<IItemsGameMergeService> _merge = null!;
        private Mock<ISteamAppStateService> _steam = null!;
        private List<string> _launched = null!;
        private List<LaunchPanelState?> _panels = null!;

        [SetUp]
        public void Setup()
        {
            _console = new RichTextBox();
            _logger = new Logger(_console);
            _view = new Mock<IMainFormView>();
            _merge = new Mock<IItemsGameMergeService>();
            _steam = new Mock<ISteamAppStateService>();
            _launched = new List<string>();
            _panels = new List<LaunchPanelState?>();

            _view.Setup(v => v.SetLaunchPanel(It.IsAny<LaunchPanelState>()))
                 .Callback<LaunchPanelState?>(s => { lock (_panels) _panels.Add(s); });

            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 4 });

            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.AlreadyCurrent });
        }

        [TearDown]
        public void TearDown() => _console?.Dispose();

        private LaunchPresenter NewPresenter() =>
            new(_view.Object, _logger, _merge.Object, _steam.Object,
                url => { _launched.Add(url); return true; });

        private LaunchPresenter NewPresenterThatSeesTheGameStart()
        {
            LaunchPresenter presenter = null!;
            presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _steam.Object,
                url =>
                {
                    _launched.Add(url);
                    presenter.NotifyDotaRunning(true);
                    return true;
                });
            return presenter;
        }

        [Test]
        public async Task Launch_WhenEverythingIsSettled_RepairsThenLaunches()
        {
            var order = new List<string>();
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .Callback(() => order.Add("merge"))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged, Applied = 5 });

            LaunchPresenter presenter = null!;
            presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _steam.Object,
                url => { order.Add("launch"); _launched.Add(url); presenter.NotifyDotaRunning(true); return true; });

            await presenter.LaunchAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(order, Is.EqualTo(new[] { "merge", "launch" }),
                    "the package must be repaired BEFORE the game is asked for");
                Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
            });
        }

        [Test]
        public async Task Launch_WhileSteamHasAnUpdateQueued_NeitherRepairsNorLaunches()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6 });

            var presenter = NewPresenter();
            using var cts = new CancellationTokenSource();

            var run = presenter.LaunchAsync(Target);
            await Task.Delay(250);
            presenter.Cancel();
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty, "must not hand the game to Steam mid-update");
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                              Times.Never, "repairing before the update lands is wasted work");
            });
        }

        [Test]
        public async Task Launch_AfterSteamFinishesUpdating_RepairsThenAsksBeforeStartingTheGame()
        {
            int reads = 0;
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(() => new SteamAppState
                  {
                      ManifestFound = true,
                      StateFlags = ++reads <= 2 ? 1042 : 4
                  });

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True, "the flow must park on the question");

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty, "an update must never start the game by itself");
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                              Times.Once, "the package is rebuilt before the question, not after it");
            });

            presenter.ConfirmLaunch();
            await run;

            Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
        }

        [Test]
        public async Task Launch_WhenTheUserDeclinesAfterAnUpdate_LeavesTheGameAlone()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(NthReadUpdating(1));

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True);

            presenter.Cancel();
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty);
                _view.Verify(v => v.SetLaunchPanel(null), Times.AtLeastOnce, "the panel must close");
            });
        }

        [Test]
        public async Task Launch_WhenTheGameAppearsWhileAsking_DoesNotAskSteamAgain()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(NthReadUpdating(1));

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True);

            presenter.NotifyDotaRunning(true);
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty);
                _view.Verify(v => v.SetLaunchPanel(null), Times.AtLeastOnce);
            });
        }

        [Test]
        public async Task Launch_WhenSteamStartsTheGameWhileWaitingOutAnUpdate_StopsInsteadOfRebuilding()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1042 });

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);

            await Task.Delay(150);
            presenter.NotifyDotaRunning(true);
            await run;

            Assert.Multiple(() =>
            {
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                              Times.Never, "never rebuild the package under a running game");
                Assert.That(_launched, Is.Empty);
                _view.Verify(v => v.SetLaunchPanel(null), Times.AtLeastOnce, "the panel must close");
            });
        }

        [Test]
        public async Task RepairOnly_WhenTheGameStartsWhileWaitingOutAnUpdate_StopsInsteadOfRebuilding()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1042 });

            var presenter = NewPresenter();
            var run = presenter.RepairOnlyAsync(Target);

            await Task.Delay(150);
            presenter.NotifyDotaRunning(true);
            await run;

            _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                            It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                          Times.Never);
        }

        [Test]
        public async Task Launch_WithNoPendingUpdate_LaunchesWithoutAsking()
        {
            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Has.Count.EqualTo(1));
                lock (_panels)
                    Assert.That(_panels.Any(p => p?.ConfirmKey != null), Is.False,
                        "nothing changed under the user, so there is nothing to confirm");
            });
        }

        private static Func<SteamAppState> NthReadUpdating(int times)
        {
            int reads = 0;
            return () => new SteamAppState
            {
                ManifestFound = true,
                StateFlags = ++reads <= times ? 1042 : 4
            };
        }

        private async Task<bool> WaitForTheQuestionAsync(int timeoutMs = 15000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (_panels)
                {
                    if (_panels.Any(p => p?.ConfirmKey != null)) return true;
                }
                await Task.Delay(25);
            }
            return false;
        }

        [Test]
        public async Task Launch_WhenTheRepairFails_DoesNotLaunchAndShowsTheReason()
        {
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(ItemsGameMergeResult.Fail("play.merge.repackFailed", "vpk.exe died"));

            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            Assert.That(_launched, Is.Empty, "launching on a failed repair is what crashes the game");
            _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s =>
                s.IsError && s.DetailKey == "play.merge.repackFailed")), Times.AtLeastOnce);
        }

        [Test]
        public async Task Launch_WhenThereIsNothingToMerge_StillLaunches()
        {
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.NothingToMerge });

            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            Assert.That(_launched, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Launch_WhenTheGameIsAlreadyRunning_DoesNothing()
        {
            var presenter = NewPresenter();
            presenter.NotifyDotaRunning(true);

            await presenter.LaunchAsync(Target);

            Assert.That(_launched, Is.Empty);
            _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                            It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                          Times.Never);
        }

        [Test]
        public async Task Launch_AnnouncesARepairSoTheStatusPipelineCanReCheck()
        {
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged });

            var presenter = NewPresenterThatSeesTheGameStart();
            int announced = 0;
            presenter.PackageRepaired += () => announced++;

            await presenter.LaunchAsync(Target);

            Assert.That(announced, Is.EqualTo(1));
        }

        [Test]
        public async Task Launch_WhenNothingWasRepaired_DoesNotAnnounce()
        {
            var presenter = NewPresenterThatSeesTheGameStart();
            int announced = 0;
            presenter.PackageRepaired += () => announced++;

            await presenter.LaunchAsync(Target);

            Assert.That(announced, Is.Zero);
        }

        [Test]
        public async Task Launch_HidesThePanelOnceTheGameIsRunning()
        {
            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            _view.Verify(v => v.SetLaunchPanel(null), Times.AtLeastOnce);
        }

        [Test]
        public async Task Launch_WhenSteamRefusesTheRequest_ReportsItInsteadOfWaitingForever()
        {
            var presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _steam.Object,
                _ => false);

            await presenter.LaunchAsync(Target);

            _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s =>
                s.IsError && s.DetailKey == "play.panel.steamFailed")), Times.AtLeastOnce);
        }


        [Test]
        public async Task RepairOnly_RebuildsThePackageButDoesNotStartTheGame()
        {
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged, Applied = 7 });

            var presenter = NewPresenter();
            await presenter.RepairOnlyAsync(Target);

            Assert.That(_launched, Is.Empty, "the user asked to fix the package, not to play");
            _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                            It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Once);
            _view.Verify(v => v.SetLaunchPanel(null), Times.AtLeastOnce, "the panel must close when it is done");
        }

        [Test]
        public async Task RepairOnly_WaitsForSteamRatherThanRebuildingIntoAPendingUpdate()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6 });

            var presenter = NewPresenter();
            var run = presenter.RepairOnlyAsync(Target);
            await Task.Delay(250);
            presenter.Cancel();
            await run;

            _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                            It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task RepairOnly_AnnouncesTheRepairSoTheStatusPipelineCanReCheck()
        {
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged });

            var presenter = NewPresenter();
            int announced = 0;
            presenter.PackageRepaired += () => announced++;

            await presenter.RepairOnlyAsync(Target);

            Assert.That(announced, Is.EqualTo(1));
        }

        [Test]
        public async Task Launch_WhileAlreadyLaunching_DoesNotStartASecondFlow()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1042 });

            var presenter = NewPresenter();
            var first = presenter.LaunchAsync(Target);
            await Task.Delay(150);

            await presenter.LaunchAsync(Target);
            presenter.Cancel();
            await first;

            _steam.Verify(s => s.Read(It.IsAny<string>()), Times.AtLeast(1));
            Assert.That(_launched, Is.Empty);
        }
    }
}
