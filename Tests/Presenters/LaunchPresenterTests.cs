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

        private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(5);

        private RichTextBox _console = null!;
        private Logger _logger = null!;
        private Mock<IMainFormView> _view = null!;
        private Mock<IItemsGameMergeService> _merge = null!;
        private Mock<IModInstallerService> _modInstaller = null!;
        private Mock<ISteamAppStateService> _steam = null!;
        private List<string> _launched = null!;

        private Func<bool> _steamRunning = null!;
        private List<LaunchPanelState?> _panels = null!;

        [SetUp]
        public void Setup()
        {
            _console = new RichTextBox();
            _logger = new Logger(_console);
            _view = new Mock<IMainFormView>();
            _merge = new Mock<IItemsGameMergeService>();
            _modInstaller = new Mock<IModInstallerService>();
            _steam = new Mock<ISteamAppStateService>();
            _launched = new List<string>();
            _steamRunning = () => true;
            _panels = new List<LaunchPanelState?>();

            _view.Setup(v => v.SetLaunchPanel(It.IsAny<LaunchPanelState>()))
                 .Callback<LaunchPanelState?>(s => { lock (_panels) _panels.Add(s); });

            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 4 });

            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.AlreadyCurrent });

            _modInstaller.Setup(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                           It.IsAny<CancellationToken>()))
                         .ReturnsAsync(PatchResult.Success);
        }

        [TearDown]
        public void TearDown() => _console?.Dispose();

        private LaunchPresenter NewPresenter() =>
            new(_view.Object, _logger, _merge.Object, _modInstaller.Object, _steam.Object,
                url => { _launched.Add(url); return true; }, () => _steamRunning(), Fast);

        private LaunchPresenter NewPresenterThatSeesTheGameStart()
        {
            LaunchPresenter presenter = null!;
            presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _modInstaller.Object, _steam.Object,
                url =>
                {
                    _launched.Add(url);
                    presenter.NotifyDotaRunning(true);
                    return true;
                }, () => _steamRunning(), Fast);
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
            presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _modInstaller.Object, _steam.Object,
                url => { order.Add("launch"); _launched.Add(url); presenter.NotifyDotaRunning(true); return true; },
                () => _steamRunning());

            await presenter.LaunchAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(order, Is.EqualTo(new[] { "merge", "launch" }),
                    "the package must be repaired BEFORE the game is asked for");
                Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
            });
        }

        [Test]
        public async Task Launch_WhenNothingSaysAPatchIsNeeded_NeverCallsThePatcher()
        {
            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            _modInstaller.Verify(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                            It.IsAny<CancellationToken>()), Times.Never,
                "patching on every launch would cost a network round-trip nobody asked for");
        }

        [Test]
        public async Task Launch_WhenAPatchIsNeeded_PatchesBeforeRepairingAndLaunching()
        {
            var order = new List<string>();
            _modInstaller.Setup(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                           It.IsAny<CancellationToken>()))
                         .Callback(() => order.Add("patch"))
                         .ReturnsAsync(PatchResult.Success);
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .Callback(() => order.Add("merge"))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged });

            LaunchPresenter presenter = null!;
            presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _modInstaller.Object, _steam.Object,
                url => { order.Add("launch"); _launched.Add(url); presenter.NotifyDotaRunning(true); return true; },
                () => _steamRunning());

            await presenter.LaunchAsync(Target, needsPatch: true);

            Assert.That(order, Is.EqualTo(new[] { "patch", "merge", "launch" }));
        }

        [Test]
        public async Task Launch_WhenThePatchFails_NeitherRepairsNorLaunches()
        {
            _modInstaller.Setup(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                           It.IsAny<CancellationToken>()))
                         .ReturnsAsync(PatchResult.Failed);

            var presenter = NewPresenter();
            await presenter.LaunchAsync(Target, needsPatch: true);

            Assert.Multiple(() =>
            {
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                              Times.Never, "repairing on top of a failed config restore is wasted work");
                Assert.That(_launched, Is.Empty);
                _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s => s.IsError)), Times.AtLeastOnce);
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
            var presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _modInstaller.Object, _steam.Object,
                _ => false, () => _steamRunning());

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


        [Test]
        public async Task Launch_WhenSteamIsClosedWithAnUpdateWaiting_SaysToOpenSteamInsteadOfWaitingForever()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6 });
            _steamRunning = () => false;

            await NewPresenter().LaunchAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty,
                    "handing the game to Steam here would let Steam install the update and start the game itself");
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                              Times.Never, "rebuilding against data the pending update will replace is wasted work");
                _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s =>
                    s.IsError && s.DetailKey == "play.panel.steamClosedDetail")), Times.AtLeastOnce);
                _view.Verify(v => v.ShowShellToast("play.panel.steamClosed", "play.panel.steamClosedDetail",
                                                   "warning", It.IsAny<int>()), Times.Once,
                             "the user is being asked to do something, so it has to reach them as a notification");
            });
        }

        [Test]
        public async Task Launch_WhenSteamIsClosedAndNothingIsPending_LaunchesAnyway()
        {
            _steamRunning = () => false;

            await NewPresenterThatSeesTheGameStart().LaunchAsync(Target);

            Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
        }

        [Test]
        public async Task RepairOnly_WhenSteamIsClosedWithAnUpdateWaiting_StopsInsteadOfWaitingForever()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6 });
            _steamRunning = () => false;

            await NewPresenter().RepairOnlyAsync(Target);

            _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                            It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }


        [Test]
        public async Task Launch_WhenDota2ChangesWhileTheRepairRuns_RebuildsOnTheNewItemDataBeforeLaunching()
        {
            int merges = 0;
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(() => new ItemsGameMergeResult
                  {
                      Outcome = ++merges == 1
                          ? ItemsGameMergeOutcome.GameChanged
                          : ItemsGameMergeOutcome.Merged
                  });

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(merges, Is.EqualTo(2), "the superseded rebuild must not be the one that ships");
                Assert.That(_launched, Is.Empty, "nothing may launch until the package matches the game");
                _modInstaller.Verify(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                               It.IsAny<CancellationToken>()), Times.Once,
                    "the update that landed mid-repair is exactly what reverts the game config");
            });

            presenter.ConfirmLaunch();
            await run;

            Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
        }

        [Test]
        public async Task Launch_WhenDota2KeepsChangingUnderTheRepair_StopsInsteadOfLaunching()
        {
            int merges = 0;
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(() => { merges++; return new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.GameChanged }; });

            await NewPresenter().LaunchAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty);
                Assert.That(merges, Is.EqualTo(3), "bounded: it must not rebuild the package forever");
                _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s =>
                    s.IsError && s.DetailKey == "play.merge.gameChangedGaveUp")), Times.AtLeastOnce);
            });
        }

        [Test]
        public async Task RepairOnly_WhenDota2ChangesWhileTheRepairRuns_RepairsAgainOnTheNewItemData()
        {
            int merges = 0;
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(() => new ItemsGameMergeResult
                  {
                      Outcome = ++merges == 1
                          ? ItemsGameMergeOutcome.GameChanged
                          : ItemsGameMergeOutcome.Merged
                  });

            await NewPresenter().RepairOnlyAsync(Target);

            Assert.Multiple(() =>
            {
                Assert.That(merges, Is.EqualTo(2));
                _view.Verify(v => v.ShowShellToast(It.IsAny<string>(), It.IsAny<string>(), "success", It.IsAny<int>()),
                             Times.Once, "the fix only reports success once the package really matches the game");
            });
        }


        private async Task<bool> WaitForPanelAsync(Func<LaunchPanelState, bool> match, int timeoutMs = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (_panels)
                {
                    if (_panels.Any(p => p != null && match(p))) return true;
                }
                await Task.Delay(10);
            }
            return false;
        }

        private static bool IsStalled(LaunchPanelState p) => p.ShowSteamActions && p.HeadingKey == "play.panel.steamStalled";

        [Test]
        public async Task Launch_WhenSteamNeverStartsTheUpdate_ShowsStalledPanelWithSteamActions()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6, BytesToDownload = 5000 });

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForPanelAsync(IsStalled), Is.True, "a manifest that never moves must surface as stalled");

            presenter.Cancel();
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(_launched, Is.Empty);
                _merge.Verify(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
                _view.Verify(v => v.ShowShellToast("play.panel.steamStalled", It.IsAny<string>(), "warning", It.IsAny<int>()),
                             Times.Once, "the user may be away from the window — tell them once, not every poll");
            });
        }

        [Test]
        public async Task Launch_WhenSteamPausedTheUpdate_ShowsStalledOnTheFirstPoll()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1542 });

            var presenter = new LaunchPresenter(_view.Object, _logger, _merge.Object, _modInstaller.Object,
                _steam.Object, url => { _launched.Add(url); return true; }, () => true);
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForPanelAsync(IsStalled, 1500), Is.True);

            presenter.Cancel();
            await run;
        }

        [Test]
        public async Task Launch_WhenTheUpdateFailed_ShowsTheFailurePanelWithItsCode()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 6, UpdateResult = 12 });

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForPanelAsync(p => p.ShowSteamActions
                && p.HeadingKey == "play.panel.steamUpdateFailed"
                && p.DetailKey == "play.panel.steamUpdateFailedDetail"
                && p.DetailVars != null), Is.True);

            presenter.Cancel();
            await run;
        }

        [Test]
        public async Task Launch_WhileTheDownloadMoves_NeverShowsStalled()
        {
            long bytes = 0;
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(() => new SteamAppState
                  {
                      ManifestFound = true, StateFlags = 1026,
                      BytesToDownload = 1_000_000, BytesDownloaded = Interlocked.Increment(ref bytes)
                  });

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);
            await Task.Delay(600);

            presenter.Cancel();
            await run;

            lock (_panels)
                Assert.That(_panels.Any(p => p?.ShowSteamActions == true), Is.False);
        }

        [Test]
        public async Task Launch_WhenTheUpdateStartsAfterAStall_GoesBackToUpdatingThenRepairsAndAsks()
        {
            int reads = 0;
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(() =>
                  {
                      int n = Interlocked.Increment(ref reads);
                      return n <= 40 ? new SteamAppState { ManifestFound = true, StateFlags = 6, BytesToDownload = 100 }
                           : n <= 45 ? new SteamAppState { ManifestFound = true, StateFlags = 1026, BytesToDownload = 100, BytesDownloaded = n }
                           : new SteamAppState { ManifestFound = true, StateFlags = 4 };
                  });

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True);

            lock (_panels)
            {
                int stalled = _panels.FindIndex(p => p != null && IsStalled(p));
                int resumed = _panels.FindLastIndex(p => p?.HeadingKey == "play.panel.steamUpdating");
                Assert.That(stalled, Is.GreaterThanOrEqualTo(0));
                Assert.That(resumed, Is.GreaterThan(stalled), "progress must take the panel back to 'updating'");
            }

            presenter.ConfirmLaunch();
            await run;
            Assert.That(_launched, Is.EqualTo(new[] { "steam://rungameid/570" }));
        }

        [Test]
        public async Task OpenSteamDownloads_WhileStalled_OpensSteamDownloads()
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1542 });

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);
            Assert.That(await WaitForPanelAsync(IsStalled), Is.True);

            presenter.OpenSteamDownloads();
            presenter.Cancel();
            await run;

            Assert.That(_launched, Is.EqualTo(new[] { "steam://open/downloads" }));
        }

        [Test]
        public async Task SteamActions_WhenNotOffered_DoNothing()
        {
            var presenter = NewPresenter();

            presenter.OpenSteamDownloads();
            await presenter.ClearSteamDownloadCacheAsync();

            Assert.That(_launched, Is.Empty);
            _view.Verify(v => v.ShowShellConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [TestCase(true, new[] { "steam://flushconfig" })]
        [TestCase(false, new string[0])]
        public async Task ClearCache_AsksFirstAndOnlyClearsOnYes(bool answer, string[] expected)
        {
            _steam.Setup(s => s.Read(It.IsAny<string>()))
                  .Returns(new SteamAppState { ManifestFound = true, StateFlags = 1542 });
            _view.Setup(v => v.ShowShellConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                 .ReturnsAsync(answer);

            var presenter = NewPresenter();
            var run = presenter.LaunchAsync(Target);
            Assert.That(await WaitForPanelAsync(IsStalled), Is.True);

            await presenter.ClearSteamDownloadCacheAsync();
            presenter.Cancel();
            await run;

            Assert.That(_launched, Is.EqualTo(expected), "the cache clear signs the user out of Steam — never without a yes");
        }

        [Test]
        public async Task Launch_WhenSteamBlinksDuringTheWait_KeepsWaiting()
        {
            int checks = 0;
            _steamRunning = () => Interlocked.Increment(ref checks) is < 3 or > 8;
            _steam.Setup(s => s.Read(It.IsAny<string>())).Returns(NthReadUpdating(12));

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target);

            Assert.That(await WaitForTheQuestionAsync(), Is.True, "a Steam restart must not end the wait");
            presenter.ConfirmLaunch();
            await run;

            _view.Verify(v => v.SetLaunchPanel(It.Is<LaunchPanelState>(s => s != null && s.HeadingKey == "play.panel.steamClosed")), Times.Never);
        }

        [Test]
        public async Task Launch_AfterWaitingOutAnUpdate_RestoresTheConfigBeforeRepairing()
        {
            var order = new List<string>();
            _steam.Setup(s => s.Read(It.IsAny<string>())).Returns(NthReadUpdating(2));
            _modInstaller.Setup(m => m.UpdatePatcherAsync(It.IsAny<string>(), It.IsAny<Action<string>>(),
                                                           It.IsAny<CancellationToken>()))
                         .Callback(() => order.Add("patch"))
                         .ReturnsAsync(PatchResult.Success);
            _merge.Setup(m => m.MergeAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(),
                                           It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                  .Callback(() => order.Add("merge"))
                  .ReturnsAsync(new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.Merged });

            var presenter = NewPresenterThatSeesTheGameStart();
            var run = presenter.LaunchAsync(Target, needsPatch: false);

            Assert.That(await WaitForTheQuestionAsync(), Is.True);
            presenter.ConfirmLaunch();
            await run;

            Assert.That(order, Is.EqualTo(new[] { "patch", "merge" }));
        }
    }
}
