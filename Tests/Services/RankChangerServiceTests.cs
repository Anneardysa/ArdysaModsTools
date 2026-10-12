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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Utilities;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class RankChangerServiceTests
    {
        private Mock<IConfigService> _configMock = null!;
        private Mock<IVpkExtractor> _extractorMock = null!;
        private Mock<IVpkRecompiler> _recompilerMock = null!;
        private Mock<IVpkReplacer> _replacerMock = null!;
        private Mock<IAppLogger> _loggerMock = null!;

        private RankChangerService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _configMock = new Mock<IConfigService>();
            _extractorMock = new Mock<IVpkExtractor>();
            _recompilerMock = new Mock<IVpkRecompiler>();
            _replacerMock = new Mock<IVpkReplacer>();
            _loggerMock = new Mock<IAppLogger>();

            _service = NewService();
        }

        private RankChangerService NewService(Func<bool>? isGameRunning = null) => new(
            _configMock.Object,
            _extractorMock.Object,
            _recompilerMock.Object,
            _replacerMock.Object,
            _loggerMock.Object,
            isGameRunning ?? (() => false));

        private static string CreateFakeDotaInstall(bool withMainMod = true, bool withProtectedMod = false)
        {
            string root = Path.Combine(Path.GetTempPath(), $"FakeDota_{Guid.NewGuid():N}");
            string gameVpk = Path.Combine(root, "game", "dota", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(gameVpk)!);
            File.WriteAllBytes(gameVpk, TestVpk.Minimal());

            if (withMainMod)
            {
                string modVpk = Path.Combine(root, "game", "_ArdysaMods", "pak01_dir.vpk");
                Directory.CreateDirectory(Path.GetDirectoryName(modVpk)!);
                File.WriteAllBytes(modVpk, TestVpk.Minimal());
            }

            if (withProtectedMod)
            {
                string protVpk = Path.Combine(root, "game", "mod", "pak01_dir.vpk");
                Directory.CreateDirectory(Path.GetDirectoryName(protVpk)!);
                File.WriteAllBytes(protVpk, TestVpk.Minimal());
            }

            return root;
        }

        #region Guards

        [Test]
        public void Constructor_NullDependencies_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RankChangerService(null!, _extractorMock.Object, _recompilerMock.Object, _replacerMock.Object));
            Assert.Throws<ArgumentNullException>(() => new RankChangerService(_configMock.Object, null!, _recompilerMock.Object, _replacerMock.Object));
            Assert.Throws<ArgumentNullException>(() => new RankChangerService(_configMock.Object, _extractorMock.Object, null!, _replacerMock.Object));
            Assert.Throws<ArgumentNullException>(() => new RankChangerService(_configMock.Object, _extractorMock.Object, _recompilerMock.Object, null!));
        }

        [Test]
        public async Task ApplyAsync_NullRequest_ThrowsArgumentNullException()
        {
            await Assert.ThatAsync(
                async () => await _service.ApplyAsync(@"C:\Games\Dota 2", null!),
                Throws.TypeOf<ArgumentNullException>());
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public async Task ApplyAsync_EmptyTargetPath_ReturnsFail(string? targetPath)
        {
            var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true);
            var result = await _service.ApplyAsync(targetPath!, req);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task ApplyAsync_NothingSelected_ReturnsFail()
        {
            var req = new RankChangerRequest(true, 4, 1, 7, 5, false, false, false);
            var result = await _service.ApplyAsync(@"C:\Games\Dota 2", req);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task ApplyAsync_NonExistentGameVpk_ReturnsFail()
        {
            string dummyPath = Path.Combine(Path.GetTempPath(), $"NonExistentDota_{Guid.NewGuid():N}");
            var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true);

            var result = await _service.ApplyAsync(dummyPath, req);

            Assert.That(result.Success, Is.False);
        }

        [TestCase(-1, 100, 6, TestName = "ApplyAsync_NegativeMmr_ReturnsFailBeforeTouchingAnyFile")]
        [TestCase(100_000, 100, 6, TestName = "ApplyAsync_MmrAboveCeiling_ReturnsFailBeforeTouchingAnyFile")]
        [TestCase(5000, 101, 6, TestName = "ApplyAsync_ConfidenceAboveCeiling_ReturnsFailBeforeTouchingAnyFile")]
        public async Task ApplyAsync_PickerValueOutOfRange_RejectedByThePlanNotTheFilesystem(
            int profileMmr, int rankConfidence, int leaderboardRank)
        {
            var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true,
                TargetLeaderboard: 0, ProfileMmr: profileMmr, RankConfidence: rankConfidence, LeaderboardRank: leaderboardRank);

            var result = await _service.ApplyAsync(@"Z:\does-not-exist", req);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(ArdysaModsTools.Core.Services.Localization.Loc.T("rank.error.invalidSelection")));
            _extractorMock.Verify(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>?>(), It.IsAny<bool>()),
                Times.Never);
            _recompilerMock.Verify(x => x.RecompileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>?>()),
                Times.Never);
            _replacerMock.Verify(x => x.ReplaceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ApplyAsync_OnlyProtectedModPackagePresent_ReturnsNotInstalled_NeverFallsBackToIt()
        {
            string dotaRoot = CreateFakeDotaInstall(withMainMod: false, withProtectedMod: true);
            try
            {
                var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true);
                var result = await _service.ApplyAsync(dotaRoot, req);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Is.EqualTo(ArdysaModsTools.Core.Services.Localization.Loc.T("rank.error.notInstalled")));
                _replacerMock.Verify(x => x.ReplaceAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
            }
        }

        [Test]
        public async Task ApplyAsync_DotaIsRunning_FailsFastWithoutTouchingAnyDependency()
        {
            string dotaRoot = CreateFakeDotaInstall();
            try
            {
                var service = NewService(isGameRunning: () => true);
                var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true);

                var result = await service.ApplyAsync(dotaRoot, req);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Is.EqualTo(ArdysaModsTools.Core.Services.Localization.Loc.T("shell.warning.closeDota")));
                _extractorMock.Verify(x => x.ExtractAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>?>(), It.IsAny<bool>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
            }
        }

        [Test]
        public async Task ResetAsync_DotaIsRunning_FailsFastWithoutTouchingAnyDependency()
        {
            string dotaRoot = CreateFakeDotaInstall();
            try
            {
                var service = NewService(isGameRunning: () => true);

                var result = await service.ResetAsync(dotaRoot);

                Assert.That(result.Success, Is.False);
                _replacerMock.Verify(x => x.ReplaceAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
            }
        }

        #endregion

        #region State Management Tests

        [Test]
        public void LoadState_EmptyConfig_ReturnsUnappliedState()
        {
            _configMock.Setup(c => c.GetValue<string>(RankChangerService.ConfigKey, string.Empty))
                .Returns(string.Empty);

            var state = _service.LoadState();

            Assert.That(state.Applied, Is.False);
            Assert.That(state.Request, Is.Null);
        }

        [Test]
        public void LoadState_ValidSavedJson_ReturnsAppliedState()
        {
            var savedReq = new RankChangerRequest(
                ReplaceEveryRank: true,
                CurrentTier: 4,
                CurrentStars: 2,
                TargetTier: 8,
                TargetStars: 1,
                ApplyStars: false,
                ApplyProfile: true,
                ApplyScoreboard: true,
                TargetLeaderboard: 3,
                ProfileMmr: 12620,
                RankConfidence: 100,
                LeaderboardRank: 6);

            _configMock.Setup(c => c.GetValue<string>(RankChangerService.ConfigKey, string.Empty))
                .Returns(JsonSerializer.Serialize(savedReq));

            var state = _service.LoadState();

            Assert.That(state.Applied, Is.True);
            Assert.That(state.Request, Is.Not.Null);
            Assert.That(state.Request!.TargetTier, Is.EqualTo(8));
            Assert.That(state.Request.ProfileMmr, Is.EqualTo(12620));
            Assert.That(state.Request.LeaderboardRank, Is.EqualTo(6));
        }

        [Test]
        public void LoadState_CorruptJson_ReturnsEmptyGracefullyWithoutThrowing()
        {
            _configMock.Setup(c => c.GetValue<string>(RankChangerService.ConfigKey, string.Empty))
                .Returns("{ invalid-json ");

            var state = _service.LoadState();

            Assert.That(state.Applied, Is.False);
            Assert.That(state.Request, Is.Null);
        }

        private static readonly RankChangerRequest SavedDivine = new(false, 4, 1, 7, 5);

        private string CreateInstallWithMainPackage(byte[] mainVpk)
        {
            string root = CreateFakeDotaInstall();
            File.WriteAllBytes(Path.Combine(root, "game", "_ArdysaMods", "pak01_dir.vpk"), mainVpk);
            _configMock.Setup(c => c.GetValue<string>(RankChangerService.ConfigKey, string.Empty))
                .Returns(JsonSerializer.Serialize(SavedDivine));
            return root;
        }

        [Test]
        public void LoadState_PackageStillCarriesTheOverride_ReportsApplied()
        {
            string root = CreateInstallWithMainPackage(TestVpk.Build(
                TestVpk.Blob("panorama/images", "amtm", "vtex_c", 64)));
            try
            {
                var state = _service.LoadState(root);

                Assert.That(state.Applied, Is.True);
                Assert.That(state.Request, Is.EqualTo(SavedDivine));
            }
            finally { Directory.Delete(root, recursive: true); }
        }

        [Test]
        public void LoadState_PackageRebuiltWithoutTheOverride_ReportsNotAppliedButKeepsTheRequest()
        {
            string root = CreateInstallWithMainPackage(TestVpk.Build(
                TestVpk.Blob("materials/dev", "something", "vtex_c", 64)));
            try
            {
                var state = _service.LoadState(root);

                Assert.That(state.Applied, Is.False);
                Assert.That(state.Request, Is.EqualTo(SavedDivine),
                    "The request must survive so the page reopens on the user's last pick.");
            }
            finally { Directory.Delete(root, recursive: true); }
        }

        [Test]
        public void LoadState_PackageUnreadable_TrustsConfig()
        {
            string root = CreateInstallWithMainPackage(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 });
            try
            {
                Assert.That(_service.LoadState(root).Applied, Is.True);
                Assert.That(_service.LoadState(null).Applied, Is.True);
            }
            finally { Directory.Delete(root, recursive: true); }
        }

        #endregion

        #region Vpk verification

        [Test]
        public void EnsurePanoramaProtectedInVpk_NonExistentFile_ReturnsFalse()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.vpk");
            bool ok = RankChangerService.EnsurePanoramaProtectedInVpk(nonExistent, Array.Empty<string>(), out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("does not exist"));
        }

        [Test]
        public void EnsurePanoramaProtectedInVpk_InvalidVpkFile_ReturnsFalse()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.vpk");
            try
            {
                File.WriteAllText(tempFile, "This is not a valid VPK file header");
                bool ok = RankChangerService.EnsurePanoramaProtectedInVpk(tempFile, Array.Empty<string>(), out var error);

                Assert.That(ok, Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Test]
        public void EnsurePanoramaProtectedInVpk_AllExpectedEntriesPresent_AppliesSignatureAndReturnsTrue()
        {
            string vpkPath = Path.Combine(Path.GetTempPath(), $"protected_{Guid.NewGuid():N}.vpk");
            byte[] vpk = TestVpk.Build(
                TestVpk.Blob("panorama/images/rank_tier_icons", "rank7_psd", "vtex_c", 64),
                TestVpk.Blob("panorama/styles", "ui_rank_badge", "vcss_c", 32));
            File.WriteAllBytes(vpkPath, vpk);

            try
            {
                var expected = new[]
                {
                    "panorama/images/rank_tier_icons/rank7_psd.vtex_c",
                    "panorama/styles/ui_rank_badge.vcss_c"
                };

                bool ok = RankChangerService.EnsurePanoramaProtectedInVpk(vpkPath, expected, out var error);

                Assert.That(ok, Is.True);
                Assert.That(error, Is.Empty);
                Assert.That(VpkSignatureSection.IsApplied(vpkPath), Is.True,
                    "EnsurePanoramaProtectedInVpk must apply the signature section itself when missing.");
            }
            finally
            {
                if (File.Exists(vpkPath)) File.Delete(vpkPath);
            }
        }

        [Test]
        public void EnsurePanoramaProtectedInVpk_MissingExpectedEntry_ReturnsFalseNamingIt()
        {
            string vpkPath = Path.Combine(Path.GetTempPath(), $"protected_{Guid.NewGuid():N}.vpk");
            File.WriteAllBytes(vpkPath, TestVpk.Build(
                TestVpk.Blob("panorama/images/rank_tier_icons", "rank7_psd", "vtex_c", 64)));

            try
            {
                bool ok = RankChangerService.EnsurePanoramaProtectedInVpk(vpkPath,
                    new[] { "panorama/images/rank_tier_icons/rank8_psd.vtex_c" }, out var error);

                Assert.That(ok, Is.False);
                Assert.That(error, Does.Contain("rank8_psd.vtex_c"));
            }
            finally
            {
                if (File.Exists(vpkPath)) File.Delete(vpkPath);
            }
        }

        #endregion

        #region Temp lifecycle

        [Test]
        public async Task ApplyAsync_FailedRun_CleansOnlyItsOwnTempDirectory_LeavesConcurrentRunsUntouched()
        {
            string safeTempPath = SafeTempPathHelper.GetSafeTempPath();
            string dotaRoot = CreateFakeDotaInstall();
            string concurrentRunDir = SafeTempPathHelper.CreateSafeTempDirectory("ArdysaRank");
            try
            {
                var before = Directory.GetDirectories(safeTempPath, "ArdysaRank_*", SearchOption.TopDirectoryOnly);

                var req = new RankChangerRequest(true, 4, 1, 7, 5, true, true, true);
                var result = await _service.ApplyAsync(dotaRoot, req);

                Assert.That(result.Success, Is.False);
                Assert.That(Directory.Exists(concurrentRunDir), Is.True,
                    "A directory that belongs to a different, still-running Rank Changer run must survive.");

                var after = Directory.GetDirectories(safeTempPath, "ArdysaRank_*", SearchOption.TopDirectoryOnly);
                Assert.That(after, Is.EquivalentTo(before),
                    "The failed run's own temp directory must be cleaned up, and nothing else touched.");
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
                if (Directory.Exists(concurrentRunDir)) Directory.Delete(concurrentRunDir, recursive: true);
            }
        }

        [Test]
        public void SweepLoosePanoramaFiles_ExistingLoosePanoramaDir_DeletesDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"DotaRoot_{Guid.NewGuid():N}");
            string loosePanorama = Path.Combine(tempRoot, "game", "_ArdysaMods", "panorama", "styles");
            Directory.CreateDirectory(loosePanorama);
            string looseFile = Path.Combine(loosePanorama, "test.vcss_c");
            File.WriteAllText(looseFile, "dummy");

            try
            {
                Assert.That(Directory.Exists(loosePanorama), Is.True);

                _service.SweepLoosePanoramaFiles(tempRoot);

                string looseDirRoot = Path.Combine(tempRoot, "game", "_ArdysaMods", "panorama");
                Assert.That(Directory.Exists(looseDirRoot), Is.False, "Loose panorama folder must be purged!");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void SweepLoosePanoramaFiles_NonExistentDir_DoesNotThrow()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"NonExistentRoot_{Guid.NewGuid():N}");
            Assert.DoesNotThrow(() => _service.SweepLoosePanoramaFiles(tempRoot));
        }

        [Test]
        public void SafeTempPathHelper_SweepStaleWorkDirs_CleansArdysaRankFolders()
        {
            string sandboxRoot = Path.Combine(Path.GetTempPath(), $"Sandbox_{Guid.NewGuid():N}");
            string rankFolder = Path.Combine(sandboxRoot, $"ArdysaRank_{Guid.NewGuid():N}");
            Directory.CreateDirectory(rankFolder);
            File.WriteAllText(Path.Combine(rankFolder, "test.dat"), "content");

            try
            {
                Assert.That(Directory.Exists(rankFolder), Is.True);
                int removed = SafeTempPathHelper.SweepStaleWorkDirs(sandboxRoot);
                Assert.That(removed, Is.GreaterThanOrEqualTo(1));
                Assert.That(Directory.Exists(rankFolder), Is.False);
            }
            finally
            {
                if (Directory.Exists(sandboxRoot))
                    try { Directory.Delete(sandboxRoot, true); } catch { }
            }
        }

        #endregion

        #region Progress

        private sealed class ProgressSink : IProgress<RankChangerProgress>
        {
            public System.Collections.Generic.List<RankChangerProgress> Items { get; } = new();
            public void Report(RankChangerProgress value) => Items.Add(value);
        }

        [Test]
        public async Task ApplyAsync_FailedRun_NeverReportsFinishAndNeverGoesBackwards()
        {
            string dotaRoot = CreateFakeDotaInstall();
            try
            {
                var sink = new ProgressSink();
                var result = await _service.ApplyAsync(dotaRoot, new RankChangerRequest(true, 4, 1, 7, 5), sink);

                Assert.That(result.Success, Is.False);
                Assert.That(sink.Items, Is.Not.Empty, "Start must be reported once the run is underway.");
                Assert.That(sink.Items[0].Stage, Is.EqualTo(RankChangerStage.Start));
                Assert.That(sink.Items.Select(p => p.Stage), Has.No.Member(RankChangerStage.Finish));
                Assert.That(sink.Items.Select(p => p.Percent), Is.Ordered);
                Assert.That(sink.Items.Select(p => (int)p.Stage), Is.Ordered);
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
            }
        }

        [Test]
        public async Task ApplyAsync_RejectedBeforeStarting_ReportsNothing()
        {
            string dotaRoot = CreateFakeDotaInstall();
            try
            {
                var sink = new ProgressSink();
                var result = await NewService(isGameRunning: () => true)
                    .ApplyAsync(dotaRoot, new RankChangerRequest(true, 4, 1, 7, 5), sink);

                Assert.That(result.Success, Is.False);
                Assert.That(sink.Items, Is.Empty, "A refused run must not flash the panel at Start.");
            }
            finally
            {
                Directory.Delete(dotaRoot, recursive: true);
            }
        }

        #endregion

        #region Tool runner

        [Test]
        public async Task RunToolAsync_ToolWritesMoreThanAPipeBuffer_StillCompletes()
        {
            var run = RankChangerService.RunToolAsync("cmd.exe",
                "/c for /L %i in (1,1,5000) do @echo xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                TimeSpan.FromMinutes(1), null, CancellationToken.None);

            var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(60)));

            Assert.That(finished, Is.SameAs(run), "~400 KB of output must not deadlock the runner.");
            Assert.That(await run, Is.True);
        }

        [Test]
        public async Task RunToolAsync_ToolHangs_TimesOutAndReturnsFalse()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool ok = await RankChangerService.RunToolAsync("cmd.exe", "/c ping -n 30 127.0.0.1",
                TimeSpan.FromSeconds(1), null, CancellationToken.None);

            Assert.That(ok, Is.False);
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void RunToolAsync_CallerCancels_Throws()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            Assert.That(async () => await RankChangerService.RunToolAsync("cmd.exe", "/c ping -n 30 127.0.0.1",
                TimeSpan.FromMinutes(1), null, cts.Token), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task RunToolAsync_NonZeroExitOrMissingTool_ReturnsFalse()
        {
            Assert.That(await RankChangerService.RunToolAsync("cmd.exe", "/c exit 3",
                TimeSpan.FromMinutes(1), null, CancellationToken.None), Is.False);
            Assert.That(await RankChangerService.RunToolAsync(Path.Combine(Path.GetTempPath(), "no_such_tool.exe"), "",
                TimeSpan.FromMinutes(1), null, CancellationToken.None), Is.False);
        }

        #endregion

        #region Staging (step 3 of a run)

        private static readonly string[] OwnedFiles =
        {
            "panorama/images/amtm.vtex_c", "panorama/images/amtc.vtex_c", "panorama/images/amtr.vtex_c",
            "panorama/images/amt_m.vtex_c", "panorama/images/hb_r.vtex_c",
            "panorama/images/rank_tier_icons/rank7_psd.vtex_c",
            "panorama/styles/ui_rank_badge.vcss_c",
            "panorama/styles/dashboard_page_profile_stats_shared.vcss_c",
            "panorama/images/amtp.vtex_c",
            "panorama/styles/hud/dota_hud_hero_picking_player.vcss_c",
        };

        private static readonly string[] ForeignFiles =
        {
            "panorama/images/other.vtex_c",
            "panorama/styles/dotastyles.vcss_c",
            "panorama/styles/hud/dota_hud_top_bar.vcss_c",
            "scripts/items/items_game.txt",
        };

        private sealed class StageSandbox : IDisposable
        {
            public string Root { get; } = Path.Combine(Path.GetTempPath(), $"RankStage_{Guid.NewGuid():N}");
            public string Extract => Path.Combine(Root, "extract");
            public string Icons => Path.Combine(Root, "icons");

            public StageSandbox()
            {
                foreach (var f in OwnedFiles) Put(Extract, f, 16);
                foreach (var f in ForeignFiles) Put(Extract, f, 16);
            }

            public static void Put(string root, string rel, int size)
            {
                string p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllBytes(p, new byte[size]);
            }

            public void SeedIcons(int size = 2048)
            {
                for (int t = 0; t <= 8; t++)
                {
                    Put(Icons, RankOverridePlan.Profile(t), size);
                    Put(Icons, RankOverridePlan.Scoreboard(t), size);
                }
                for (int lb = 1; lb <= 3; lb++)
                {
                    Put(Icons, RankOverridePlan.Profile(8, lb), size);
                    Put(Icons, RankOverridePlan.Scoreboard(8, lb), size);
                }
                for (int s = 1; s <= 7; s++) Put(Icons, RankOverridePlan.Stars(s), size);
            }

            public bool Has(string rel) => File.Exists(Path.Combine(Extract, rel.Replace('/', Path.DirectorySeparatorChar)));

            public string[] AllFiles() => Directory.GetFiles(Extract, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Extract, f).Replace('\\', '/')).ToArray();

            public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
        }

        private static Task<System.Collections.Generic.IReadOnlyList<string>?> Stage(StageSandbox box, RankChangerRequest? request,
            bool withPick = true)
        {
            bool pick = request != null && withPick;
            var entries = request is null
                ? (System.Collections.Generic.IReadOnlyList<RankOverrideEntry>)Array.Empty<RankOverrideEntry>()
                : RankOverridePlan.Build(request);
            return RankChangerService.StageOverridesAsync(box.Extract, request is null ? null : box.Icons, entries, request,
                request is null ? null : new byte[] { 1 }, request is null ? null : new byte[] { 2 },
                pick ? new byte[] { 3 } : null, pick ? "Divine V" : null,
                box.Root, null, CancellationToken.None);
        }

        [Test]
        public async Task StageOverrides_Reset_RemovesEveryRankFileAndNothingElse()
        {
            using var box = new StageSandbox();

            var expected = await Stage(box, null);

            Assert.That(expected, Is.Empty);
            foreach (var f in OwnedFiles) Assert.That(box.Has(f), Is.False, $"{f} must be cleared by Reset");
            foreach (var f in ForeignFiles) Assert.That(box.Has(f), Is.True, $"{f} is not ours and must survive");
        }

        [Test]
        public async Task StageOverrides_Apply_WritesExactlyTheFilesItReports()
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            var expected = await Stage(box, new RankChangerRequest(true, 4, 1, 7, 5));

            Assert.That(expected, Is.Not.Null);
            var written = box.AllFiles().Except(ForeignFiles);
            Assert.That(written, Is.EquivalentTo(expected!.Distinct()));
            Assert.That(expected, Does.Contain("panorama/images/amtm.vtex_c"),
                "LoadState's marker must be part of every Apply.");
        }

        [TestCase(8)]
        [TestCase(0)]
        public async Task StageOverrides_StarlessTarget_BlanksEveryPip(int targetTier)
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            var expected = await Stage(box, new RankChangerRequest(false, 4, 1, targetTier, 5));

            byte[] blank = VtexGenerator.GenerateEmptyPipTexture();
            for (int s = RankOverridePlan.MinStars; s <= RankOverridePlan.MaxStars; s++)
            {
                string rel = $"{RankOverridePlan.IconFolder}/{RankOverridePlan.Stars(s)}";
                Assert.That(expected, Does.Contain(rel));
                Assert.That(File.ReadAllBytes(Path.Combine(box.Extract, rel.Replace('/', '\\'))), Is.EqualTo(blank));
            }
        }

        [Test]
        public async Task StageOverrides_StarredTargetInOnlyMyRank_LeavesOtherPipsAlone()
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            var expected = await Stage(box, new RankChangerRequest(false, 4, 1, 7, 5));

            var pips = expected!.Where(e => e.Contains("/pip")).ToArray();
            Assert.That(pips, Is.EqualTo(new[] { $"{RankOverridePlan.IconFolder}/pip1_psd.vtex_c" }),
                "Only the current star count is redrawn; the other six stay Valve's.");
        }

        [Test]
        public async Task StageOverrides_SourceIconTooSmall_RefusesWithNull()
        {
            using var box = new StageSandbox();
            box.SeedIcons(size: 100);

            var expected = await Stage(box, new RankChangerRequest(false, 4, 1, 7, 5));

            Assert.That(expected, Is.Null);
        }

        [Test]
        public async Task StageOverrides_SecondApply_LeavesOnlyTheSecondSet()
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            await Stage(box, new RankChangerRequest(true, 4, 1, 8, 5));
            var second = await Stage(box, new RankChangerRequest(false, 4, 1, 7, 5));

            var written = box.AllFiles().Except(ForeignFiles);
            Assert.That(written, Is.EquivalentTo(second!.Distinct()),
                "Nothing from the first Apply may survive into the second package.");
        }

        [Test]
        public async Task StageOverrides_Apply_WritesThePickingLabelAndReportsIt()
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            var expected = await Stage(box, new RankChangerRequest(false, 4, 1, 7, 5));

            Assert.That(expected, Does.Contain(PanoramaCssPatcher.PickPhaseSheet));
            Assert.That(expected, Does.Contain(PanoramaCssPatcher.PickLabelTexture));
            Assert.That(File.ReadAllBytes(Path.Combine(box.Extract, PanoramaCssPatcher.PickPhaseSheet.Replace('/', '\\'))),
                Is.EqualTo(new byte[] { 3 }), "the patched stylesheet is written as given");
            Assert.That(File.ReadAllBytes(Path.Combine(box.Extract, PanoramaCssPatcher.PickLabelTexture.Replace('/', '\\'))),
                Is.EqualTo(VtexGenerator.GeneratePickRankTexture("Divine V", box.Root)));
        }

        [Test]
        public async Task StageOverrides_PickPatchUnavailable_SkipsBothHalvesAndKeepsTheRest()
        {
            using var box = new StageSandbox();
            box.SeedIcons();

            var expected = await Stage(box, new RankChangerRequest(false, 4, 1, 7, 5), withPick: false);

            Assert.That(expected, Does.Not.Contain(PanoramaCssPatcher.PickPhaseSheet));
            Assert.That(expected, Does.Not.Contain(PanoramaCssPatcher.PickLabelTexture));
            Assert.That(box.Has(PanoramaCssPatcher.PickPhaseSheet), Is.False, "a stale label from an earlier Apply must go too");
            Assert.That(box.Has(PanoramaCssPatcher.PickLabelTexture), Is.False);
            Assert.That(expected, Does.Contain("panorama/images/amtm.vtex_c"));
            Assert.That(box.AllFiles().Except(ForeignFiles), Is.EquivalentTo(expected!.Distinct()));
        }

        [Test]
        public void TryPatchPickPhase_StylesheetNoLongerMatches_ReturnsNullAndTellsTheUser()
        {
            var logger = new Mock<IAppLogger>();

            var patched = RankChangerService.TryPatchPickPhase(System.Text.Encoding.UTF8.GetBytes("}#X{a: b;}\0"),
                new RankChangerRequest(false, 4, 1, 7, 5), logger.Object);

            Assert.That(patched, Is.Null);
            logger.Verify(l => l.Log(It.Is<string>(s => s.Contains("picking")), It.IsAny<LogLevel>()), Times.Once);
        }

        [TestCase(true, 0, 0)]
        [TestCase(false, 0, 0)]
        [TestCase(false, 8, 0)]
        [TestCase(false, 8, 3)]
        [TestCase(true, 7, 0)]
        public void TryPatchPickPhase_ValvesStylesheet_PatchesForEveryTarget(bool everyRank, int targetTier, int bracket)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            byte[] valve = File.ReadAllBytes(Path.Combine(dir!.FullName, "Tests", "Data", "RankChanger", "dota_hud_hero_picking_player.vcss_c"));

            var patched = RankChangerService.TryPatchPickPhase(valve,
                new RankChangerRequest(everyRank, 4, 1, targetTier, 5, TargetLeaderboard: bracket, LeaderboardRank: 1), null);

            Assert.That(patched, Is.Not.Null);
            Assert.That(patched!.Length, Is.EqualTo(valve.Length));
            Assert.That(System.Text.Encoding.UTF8.GetString(patched), Does.Contain("panorama/images/amtp.vtex"));
        }

        [Test]
        public void TryPatchPickPhase_NotExtracted_ReturnsNullWithoutThrowing() =>
            Assert.That(RankChangerService.TryPatchPickPhase(null, new RankChangerRequest(true, 4, 1, 7, 5), null), Is.Null);

        [Test]
        public void OwnedPaths_NeverOverlapTheHeroBadgeChanger()
        {
            var heroBadge = HeroBadgeCssPatcher.Stylesheets.Concat(new[]
            {
                HeroBadgePlan.MarkerEntry, HeroBadgePlan.LargeLevelTexture, HeroBadgePlan.SmallLevelTexture,
                HeroBadgePlan.ProgressTexture, HeroBadgePlan.XpTexture,
            });

            Assert.That(RankChangerService.OwnedPaths().Intersect(heroBadge, StringComparer.OrdinalIgnoreCase), Is.Empty);
        }

        #endregion
    }
}
