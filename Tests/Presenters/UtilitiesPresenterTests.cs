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
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    public class UtilitiesPresenterTests
    {
        private string _tempDir = null!;
        private Mock<IUtilitiesView> _viewMock = null!;
        private Mock<IRankChangerService> _rankChangerMock = null!;
        private Mock<IHeroBadgeService> _badgeMock = null!;
        private Mock<IAppLogger> _loggerMock = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"amt_util_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            _viewMock = new Mock<IUtilitiesView>();
            _rankChangerMock = new Mock<IRankChangerService>();
            _loggerMock = new Mock<IAppLogger>();

            _rankChangerMock.Setup(r => r.LoadState(It.IsAny<string?>()))
                .Returns(new RankChangerState(false, null));

            _badgeMock = new Mock<IHeroBadgeService>();
            _badgeMock.Setup(b => b.LoadState(It.IsAny<string?>()))
                .Returns(new HeroBadgeState(false, null));
        }

        private UtilitiesPresenter NewPresenter() =>
            new(_viewMock.Object, _rankChangerMock.Object, _badgeMock.Object, _loggerMock.Object, _tempDir);

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { }
            }
        }

        [Test]
        public void IsModInstalled_WhenNoModsInstalled_ReturnsFalse()
        {
            Assert.That(PathUtility.IsModInstalled(_tempDir), Is.False);
        }

        [Test]
        public void IsModInstalled_WhenMainModVpkExists_ReturnsTrue()
        {
            string mainVpk = Path.Combine(_tempDir, "game", "_ArdysaMods", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(mainVpk)!);
            File.WriteAllBytes(mainVpk, new byte[] { 0x34, 0x12, 0xaa, 0x55 });

            Assert.That(PathUtility.IsModInstalled(_tempDir), Is.True);
        }

        [Test]
        public void IsModInstalled_WhenProtectedModVpkExists_ReturnsTrue()
        {
            string protVpk = Path.Combine(_tempDir, "game", "mod", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(protVpk)!);
            File.WriteAllBytes(protVpk, new byte[] { 0x34, 0x12, 0xaa, 0x55 });

            Assert.That(PathUtility.IsModInstalled(_tempDir), Is.True);
        }

        [Test]
        public async Task PushState_WhenModsNotInstalled_PushesIsModInstalledFalse()
        {
            string? pushedJson = null;
            _viewMock.Setup(v => v.LoadStateAsync(It.IsAny<string>()))
                .Callback<string>(json => pushedJson = json)
                .Returns(Task.CompletedTask);

            var presenter = NewPresenter();
            _viewMock.Raise(v => v.OnViewShown += null, EventArgs.Empty);

            await Task.Delay(50);

            Assert.That(pushedJson, Is.Not.Null);
            using var doc = JsonDocument.Parse(pushedJson!);
            Assert.That(doc.RootElement.TryGetProperty("isModInstalled", out var prop), Is.True);
            Assert.That(prop.GetBoolean(), Is.False);
        }

        [Test]
        public async Task PushState_WhenModsInstalled_PushesIsModInstalledTrue()
        {
            string protVpk = Path.Combine(_tempDir, "game", "mod", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(protVpk)!);
            File.WriteAllBytes(protVpk, new byte[] { 0x34, 0x12, 0xaa, 0x55 });

            string? pushedJson = null;
            _viewMock.Setup(v => v.LoadStateAsync(It.IsAny<string>()))
                .Callback<string>(json => pushedJson = json)
                .Returns(Task.CompletedTask);

            var presenter = NewPresenter();
            _viewMock.Raise(v => v.OnViewShown += null, EventArgs.Empty);

            await Task.Delay(50);

            Assert.That(pushedJson, Is.Not.Null);
            using var doc = JsonDocument.Parse(pushedJson!);
            Assert.That(doc.RootElement.TryGetProperty("isModInstalled", out var prop), Is.True);
            Assert.That(prop.GetBoolean(), Is.True);
        }

        [Test]
        public async Task Apply_WhenModsNotInstalled_ShowsToastAndDoesNotCallService()
        {
            string requestJson = JsonSerializer.Serialize(new RankChangerRequest(false, 4, 1, 7, 5));

            var presenter = NewPresenter();
            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, requestJson);

            await Task.Delay(50);

            _viewMock.Verify(v => v.ShowToastAsync(It.IsAny<string>(), "error"), Times.Once);
            _rankChangerMock.Verify(r => r.ApplyAsync(It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Apply_SecondCallLandingBeforeTheFirstFinishes_IsIgnored()
        {
            string modVpk = Path.Combine(_tempDir, "game", "mod", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(modVpk)!);
            File.WriteAllBytes(modVpk, new byte[] { 0x34, 0x12, 0xaa, 0x55 });

            var firstRunGate = new TaskCompletionSource<OperationResult>();
            _rankChangerMock.Setup(r => r.ApplyAsync(
                    It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Returns(firstRunGate.Task);

            string requestJson = JsonSerializer.Serialize(new RankChangerRequest(false, 4, 1, 7, 5));
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, requestJson);
            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, requestJson);

            firstRunGate.SetResult(OperationResult.Ok());
            await Task.Delay(50);

            _rankChangerMock.Verify(r => r.ApplyAsync(
                It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void InstallMainPackage()
        {
            string modVpk = Path.Combine(_tempDir, "game", "_ArdysaMods", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(modVpk)!);
            File.WriteAllBytes(modVpk, new byte[] { 0x34, 0x12, 0xaa, 0x55 });
        }

        private void SetupApply(Func<Task<OperationResult>> result) =>
            _rankChangerMock.Setup(r => r.ApplyAsync(
                    It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Returns(result);

        private static readonly string ValidRequestJson = JsonSerializer.Serialize(new RankChangerRequest(false, 4, 1, 7, 5));

        [Test]
        public async Task Apply_NullPayload_ShowsInvalidSelectionAndDoesNotCallService()
        {
            InstallMainPackage();
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, "null");
            await Task.Delay(50);

            _viewMock.Verify(v => v.ShowToastAsync(Loc.T("rank.error.invalidSelection"), "error"), Times.Once);
            _rankChangerMock.Verify(r => r.ApplyAsync(It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Apply_ServiceFails_ShowsItsMessageAndRepushesState()
        {
            InstallMainPackage();
            SetupApply(() => Task.FromResult(OperationResult.Fail("boom")));
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, ValidRequestJson);
            await Task.Delay(50);

            _viewMock.Verify(v => v.ShowToastAsync("boom", "error"), Times.Once);
            _viewMock.Verify(v => v.LoadStateAsync(It.IsAny<string>()), Times.Once);
            _viewMock.Verify(v => v.SetBusyAsync(false), Times.Once);
            _rankChangerMock.Verify(r => r.LoadState(_tempDir), Times.Once,
                "State must be re-read against this install so a stale 'applied' is caught.");
        }

        [Test]
        public async Task ApplyAndReset_ServiceStages_ReachTheProgressPanel()
        {
            InstallMainPackage();
            _rankChangerMock.Setup(r => r.ApplyAsync(
                    It.IsAny<string>(), It.IsAny<RankChangerRequest>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Returns<string, RankChangerRequest, IProgress<RankChangerProgress>?, CancellationToken>((_, _, progress, _) =>
                {
                    progress!.Report(new RankChangerProgress(RankChangerStage.Processing, 35));
                    progress.Report(new RankChangerProgress(RankChangerStage.Finish, 100));
                    return Task.FromResult(OperationResult.Ok());
                });
            _rankChangerMock.Setup(r => r.ResetAsync(It.IsAny<string>(), It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Returns<string, IProgress<RankChangerProgress>?, CancellationToken>((_, progress, _) =>
                {
                    progress!.Report(new RankChangerProgress(RankChangerStage.Compiling, 70));
                    return Task.FromResult(OperationResult.Ok());
                });
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, ValidRequestJson);
            await Task.Delay(100);
            _viewMock.Raise(v => v.OnRankResetRequested += null, EventArgs.Empty);
            await Task.Delay(100);

            _viewMock.Verify(v => v.ShowProgressAsync("processing", 35), Times.Once);
            _viewMock.Verify(v => v.ShowProgressAsync("finish", 100), Times.Once);
            _viewMock.Verify(v => v.ShowProgressAsync("compiling", 70), Times.Once);
        }

        [Test]
        public async Task Apply_ServiceThrows_ShowsErrorAndReleasesTheLock()
        {
            InstallMainPackage();
            int calls = 0;
            SetupApply(() => ++calls == 1 ? throw new InvalidOperationException("kaput") : Task.FromResult(OperationResult.Ok()));
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, ValidRequestJson);
            await Task.Delay(50);
            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, ValidRequestJson);
            await Task.Delay(50);

            Assert.That(calls, Is.EqualTo(2), "A run that threw must not leave the run lock held.");
            _viewMock.Verify(v => v.ShowToastAsync(It.IsAny<string>(), "error"), Times.Once);
            _viewMock.Verify(v => v.ShowToastAsync(Loc.T("rank.toast.applied"), "success"), Times.Once);
        }

        #region Hero badge

        private const string BadgeJson =
            "{\"show\":\"tier\",\"tier\":4,\"level\":\"custom\",\"customLevel\":25,\"progress\":\"custom\",\"customProgress\":40}";

        [Test]
        public async Task BadgeApply_PagePayload_ReachesTheServiceAsTheRecord()
        {
            InstallMainPackage();
            HeroBadgeRequest? seen = null;
            _badgeMock.Setup(b => b.ApplyAsync(It.IsAny<string>(), It.IsAny<HeroBadgeRequest>(),
                    It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Callback<string, HeroBadgeRequest, IProgress<RankChangerProgress>?, CancellationToken>((_, r, _, _) => seen = r)
                .ReturnsAsync(OperationResult.Ok());
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnBadgeApplyRequested += null, _viewMock.Object, BadgeJson);
            await Task.Delay(50);

            Assert.That(seen, Is.EqualTo(new HeroBadgeRequest(HeroBadgeShow.Tier, 4, HeroBadgeLevel.Custom, 25, HeroBadgeProgress.Custom, 40)));
            _viewMock.Verify(v => v.ShowToastAsync(Loc.T("badge.toast.applied"), "success"), Times.Once);
        }

        [Test]
        public async Task BadgeApply_OnlyTier_ReachesTheService()
        {
            InstallMainPackage();
            HeroBadgeRequest? seen = null;
            _badgeMock.Setup(b => b.ApplyAsync(It.IsAny<string>(), It.IsAny<HeroBadgeRequest>(),
                    It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()))
                .Callback<string, HeroBadgeRequest, IProgress<RankChangerProgress>?, CancellationToken>((_, r, _, _) => seen = r)
                .ReturnsAsync(OperationResult.Ok());
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnBadgeApplyRequested += null, _viewMock.Object,
                "{\"show\":\"tier\",\"tier\":5,\"level\":\"keep\",\"customLevel\":30,\"progress\":\"keep\",\"customProgress\":50,\"onlyTier\":3}");
            await Task.Delay(50);

            Assert.That(seen?.OnlyTier, Is.EqualTo(3));
        }

        [Test]
        public async Task BadgeApply_EnumAsNumber_IsRejectedNotGuessed()
        {
            InstallMainPackage();
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnBadgeApplyRequested += null, _viewMock.Object,
                "{\"show\":1,\"tier\":4,\"level\":0,\"customLevel\":25,\"progress\":0,\"customProgress\":40}");
            await Task.Delay(50);

            _viewMock.Verify(v => v.ShowToastAsync(Loc.T("badge.error.invalidSelection"), "error"), Times.Once);
            _badgeMock.Verify(b => b.ApplyAsync(It.IsAny<string>(), It.IsAny<HeroBadgeRequest>(),
                It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task BadgeApply_WhileARankRunIsInFlight_IsIgnored()
        {
            InstallMainPackage();
            var rankGate = new TaskCompletionSource<OperationResult>();
            SetupApply(() => rankGate.Task);
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnRankApplyRequested += null, _viewMock.Object, ValidRequestJson);
            _viewMock.Raise(v => v.OnBadgeApplyRequested += null, _viewMock.Object, BadgeJson);
            _viewMock.Raise(v => v.OnBadgeResetRequested += null, EventArgs.Empty);

            rankGate.SetResult(OperationResult.Ok());
            await Task.Delay(50);

            _badgeMock.Verify(b => b.ApplyAsync(It.IsAny<string>(), It.IsAny<HeroBadgeRequest>(),
                It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
            _badgeMock.Verify(b => b.ResetAsync(It.IsAny<string>(),
                It.IsAny<IProgress<RankChangerProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task PushState_CarriesTheBadgeStateWithEnumNamesThePageReads()
        {
            _badgeMock.Setup(b => b.LoadState(It.IsAny<string?>()))
                .Returns(new HeroBadgeState(true, new HeroBadgeRequest(HeroBadgeShow.Hide, Progress: HeroBadgeProgress.Max)));
            string? pushed = null;
            _viewMock.Setup(v => v.LoadStateAsync(It.IsAny<string>()))
                .Callback<string>(json => pushed = json)
                .Returns(Task.CompletedTask);
            var presenter = NewPresenter();

            _viewMock.Raise(v => v.OnViewShown += null, EventArgs.Empty);
            await Task.Delay(50);

            using var doc = JsonDocument.Parse(pushed!);
            var badge = doc.RootElement.GetProperty("badge");
            Assert.That(badge.GetProperty("applied").GetBoolean(), Is.True);
            var request = badge.GetProperty("request");
            Assert.That(request.GetProperty("show").GetString(), Is.EqualTo("hide"));
            Assert.That(request.GetProperty("progress").GetString(), Is.EqualTo("max"));
            Assert.That(request.GetProperty("level").GetString(), Is.EqualTo("keep"));
        }

        #endregion
    }
}
