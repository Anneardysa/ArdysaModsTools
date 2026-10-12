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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Utilities;
using ArdysaModsTools.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroBadgeServiceTests
    {
        private Mock<IConfigService> _config = null!;
        private Mock<IVpkExtractor> _extractor = null!;
        private Mock<IVpkRecompiler> _recompiler = null!;
        private Mock<IVpkReplacer> _replacer = null!;
        private string _root = null!;

        private static readonly HeroBadgeRequest Grandmaster30 =
            new(HeroBadgeShow.Tier, 5, HeroBadgeLevel.Custom, 30, HeroBadgeProgress.Max);

        [SetUp]
        public void SetUp()
        {
            _config = new Mock<IConfigService>();
            _extractor = new Mock<IVpkExtractor>();
            _recompiler = new Mock<IVpkRecompiler>();
            _replacer = new Mock<IVpkReplacer>();
            _root = Path.Combine(Path.GetTempPath(), $"FakeDotaBadge_{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private HeroBadgeService NewService(bool gameRunning = false) =>
            new(_config.Object, _extractor.Object, _recompiler.Object, _replacer.Object, null, () => gameRunning);

        private string Install(byte[]? mainVpk = null, bool withMain = true, bool withProtected = false)
        {
            string gameVpk = Path.Combine(_root, "game", "dota", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(gameVpk)!);
            File.WriteAllBytes(gameVpk, TestVpk.Minimal());
            if (withMain)
            {
                string main = Path.Combine(_root, "game", "_ArdysaMods", "pak01_dir.vpk");
                Directory.CreateDirectory(Path.GetDirectoryName(main)!);
                File.WriteAllBytes(main, mainVpk ?? TestVpk.Minimal());
            }
            if (withProtected)
            {
                string prot = Path.Combine(_root, "game", "mod", "pak01_dir.vpk");
                Directory.CreateDirectory(Path.GetDirectoryName(prot)!);
                File.WriteAllBytes(prot, TestVpk.Minimal());
            }
            return _root;
        }

        private void VerifyPackageNeverTouched()
        {
            _extractor.Verify(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()), Times.Never);
            _replacer.Verify(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private void UnpackAs(params string[] files) =>
            _extractor.Setup(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
                .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((_, _, dir, _, _, _, _) =>
                {
                    foreach (var f in files)
                    {
                        string p = Path.Combine(dir, f.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                        File.WriteAllBytes(p, new byte[2048]);
                    }
                    return Task.FromResult(true);
                });

        #region Guards

        [Test]
        public void Constructor_NullDependency_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new HeroBadgeService(null!, _extractor.Object, _recompiler.Object, _replacer.Object));
            Assert.Throws<ArgumentNullException>(() => new HeroBadgeService(_config.Object, null!, _recompiler.Object, _replacer.Object));
            Assert.Throws<ArgumentNullException>(() => new HeroBadgeService(_config.Object, _extractor.Object, null!, _replacer.Object));
            Assert.Throws<ArgumentNullException>(() => new HeroBadgeService(_config.Object, _extractor.Object, _recompiler.Object, null!));
        }

        [Test]
        public async Task Apply_OutOfRange_RejectedBeforeTheFilesystem()
        {
            var result = await NewService().ApplyAsync(@"Z:\does-not-exist",
                new HeroBadgeRequest(Level: HeroBadgeLevel.Custom, CustomLevel: 31));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(Loc.T("badge.error.invalidSelection")));
            VerifyPackageNeverTouched();
        }

        [Test]
        public async Task Apply_NothingToChange_Rejected()
        {
            var result = await NewService().ApplyAsync(Install(), new HeroBadgeRequest());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(Loc.T("badge.error.nothingSelected")));
            VerifyPackageNeverTouched();
        }

        [Test]
        public async Task ApplyAndReset_DotaRunning_FailFast()
        {
            string root = Install();
            var service = NewService(gameRunning: true);

            Assert.That((await service.ApplyAsync(root, Grandmaster30)).Success, Is.False);
            Assert.That((await service.ResetAsync(root)).Success, Is.False);
            VerifyPackageNeverTouched();
        }

        [Test]
        public async Task Apply_OnlyProtectedPackage_IsNotInstalled_NeverFallsBackToIt()
        {
            var result = await NewService().ApplyAsync(Install(withMain: false, withProtected: true), Grandmaster30);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(HeroBadgeService.FailureMessage(reset: false, "notInstalled")));
            VerifyPackageNeverTouched();
        }

        [Test]
        public async Task Failures_SayWhatFailed_ThenOnePlainReason()
        {
            string root = Install();
            var service = NewService(gameRunning: true);

            var apply = await service.ApplyAsync(root, Grandmaster30);
            var reset = await service.ResetAsync(root);

            Assert.That(apply.Message, Is.EqualTo($"{Loc.T("badge.fail.apply")} {Loc.T("badge.reason.dotaRunning")}"));
            Assert.That(reset.Message, Is.EqualTo($"{Loc.T("badge.fail.reset")} {Loc.T("badge.reason.dotaRunning")}"));
        }

        [Test]
        public void EveryFailureReason_HasItsTextInEnglish()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            string source = File.ReadAllText(Path.Combine(dir!.FullName, "Core", "Services", "Utilities", "HeroBadgeService.cs"));
            var en = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(dir.FullName, "Assets", "Locales", "en.json")))!;

            var reasons = System.Text.RegularExpressions.Regex.Matches(source, @"Failed\(""(\w+)""")
                .Select(m => m.Groups[1].Value).Distinct().ToList();

            Assert.That(reasons, Has.Count.GreaterThan(8), "the pattern should find the service's failures");
            Assert.That(en, Contains.Key("badge.fail.apply").And.ContainKey("badge.fail.reset"));
            foreach (var reason in reasons)
                Assert.That(en, Contains.Key($"badge.reason.{reason}"), reason);
        }

        #endregion

        #region Reset through the real pipeline (up to the mocked repack)

        [Test]
        public async Task Reset_PackageHoldsNothingOfOurs_ForgetsStateWithoutRebuilding()
        {
            UnpackAs("scripts/items/items_game.txt");
            var result = await NewService().ResetAsync(Install());

            Assert.That(result.Success, Is.True, result.Message);
            _config.Verify(c => c.SetValue(HeroBadgeService.ConfigKey, string.Empty), Times.Once);
            _recompiler.Verify(x => x.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()), Times.Never);
            _replacer.Verify(x => x.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Reset_OurPackage_ClearsOnlyOurFilesBeforeRepacking()
        {
            const string rankIcon = "panorama/images/rank_tier_icons/rank8_psd.vtex_c";
            const string econ = "panorama/styles/ui_econ_item.vcss_c";
            UnpackAs(HeroBadgePlan.MarkerEntry, HeroBadgePlan.LargeLevelTexture,
                "panorama/styles/dotastyles.vcss_c", "models/particle/mastery_icons/mastery_rank1.vmdl_c",
                rankIcon, econ);

            List<string>? leftAtRepack = null;
            _recompiler.Setup(x => x.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
                .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((_, dir, _, _, _, _, _) =>
                {
                    leftAtRepack = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).ToList();
                    return Task.FromResult<string?>(null);
                });

            var result = await NewService().ResetAsync(Install());

            Assert.That(result.Success, Is.False, "the mocked repack returns nothing");
            Assert.That(leftAtRepack, Is.EquivalentTo(new[] { rankIcon, econ }));
            _config.Verify(c => c.SetValue(HeroBadgeService.ConfigKey, It.IsAny<string>()), Times.Never,
                "a failed run must not change the saved state");
        }

        [Test]
        public async Task FailedRun_NeverReportsFinish_AndCleansItsWorkDir()
        {
            UnpackAs(HeroBadgePlan.MarkerEntry);
            string temp = ArdysaModsTools.Core.Helpers.SafeTempPathHelper.GetSafeTempPath();
            var before = Directory.GetDirectories(temp, "ArdysaBadge_*");
            var sink = new List<RankChangerProgress>();

            var result = await NewService().ResetAsync(Install(), new SyncProgress(sink));

            Assert.That(result.Success, Is.False);
            Assert.That(sink.Select(p => p.Stage), Has.No.Member(RankChangerStage.Finish));
            Assert.That(sink.Select(p => p.Percent), Is.Ordered);
            Assert.That(Directory.GetDirectories(temp, "ArdysaBadge_*"), Is.EquivalentTo(before));
        }

        private sealed class SyncProgress(List<RankChangerProgress> items) : IProgress<RankChangerProgress>
        {
            public void Report(RankChangerProgress value) => items.Add(value);
        }

        #endregion

        #region Ownership and staging

        [Test]
        public void ForeignFiles_FindsHeroBadgeFilesAnotherModShipped()
        {
            string dir = Directory.CreateDirectory(Path.Combine(_root, "x")).FullName;
            string css = Path.Combine(dir, "panorama", "styles", "dotastyles.vcss_c");
            Directory.CreateDirectory(Path.GetDirectoryName(css)!);
            File.WriteAllText(css, "theirs");

            Assert.That(HeroBadgeService.ForeignFiles(dir), Is.EqualTo(new[] { "panorama/styles/dotastyles.vcss_c" }));
        }

        private static Dictionary<string, string> FakeSources(string dir, int tier, long size = 4096)
        {
            var map = new Dictionary<string, string>();
            foreach (var (source, _) in HeroBadgePlan.Copies(tier))
            {
                string local = Path.Combine(dir, Path.GetFileName(source));
                File.WriteAllBytes(local, new byte[size]);
                map[source] = local;
            }
            return map;
        }

        [Test]
        public async Task Stage_WritesExactlyWhatItReports_AndAMarker()
        {
            string src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;
            var fx = HeroBadgePlan.Resolve(Grandmaster30);
            var sheets = new Dictionary<string, byte[]> { ["panorama/styles/hero_badge.vcss_c"] = new byte[] { 1, 2, 3 } };

            var written = await HeroBadgeService.StageAsync(extract, FakeSources(src, 5), fx, sheets, null, null, CancellationToken.None);

            var onDisk = Directory.GetFiles(extract, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(extract, f).Replace('\\', '/'));
            Assert.That(written, Is.Not.Null);
            Assert.That(onDisk, Is.EquivalentTo(written!));
            Assert.That(written, Does.Contain(HeroBadgePlan.MarkerEntry));
            Assert.That(written, Does.Contain(HeroBadgePlan.LargeLevelTexture));
            Assert.That(written, Does.Contain(HeroBadgePlan.ProgressTexture));
            Assert.That(written, Is.Unique);
        }

        [Test]
        public async Task Stage_SecondApply_CarriesNothingOverFromTheFirst()
        {
            string src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;
            var none = new Dictionary<string, byte[]>();

            await HeroBadgeService.StageAsync(extract, FakeSources(src, 5), HeroBadgePlan.Resolve(Grandmaster30), none, null, null, CancellationToken.None);
            var second = await HeroBadgeService.StageAsync(extract, new Dictionary<string, string>(),
                HeroBadgePlan.Resolve(new HeroBadgeRequest(Level: HeroBadgeLevel.Hide)), none, null, null, CancellationToken.None);

            var onDisk = Directory.GetFiles(extract, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(extract, f).Replace('\\', '/'));
            Assert.That(onDisk, Is.EquivalentTo(second!), "tier copies and level textures from the first run must be gone");
        }

        [Test]
        public async Task Stage_KnownLevelWithProgress_WritesTheXpLine()
        {
            string src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;
            var fx = HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 4, HeroBadgeLevel.Custom, 26, HeroBadgeProgress.Custom, 50));

            var written = await HeroBadgeService.StageAsync(extract, FakeSources(src, 4), fx, new Dictionary<string, byte[]>(), null, null, CancellationToken.None);
            var withoutLevel = await HeroBadgeService.StageAsync(extract, new Dictionary<string, string>(),
                HeroBadgePlan.Resolve(new HeroBadgeRequest(Progress: HeroBadgeProgress.Max)), new Dictionary<string, byte[]>(), null, null, CancellationToken.None);

            Assert.That(written, Does.Contain(HeroBadgePlan.XpTexture));
            Assert.That(withoutLevel, Does.Not.Contain(HeroBadgePlan.XpTexture), "no level, no numbers to draw");
            Assert.That(File.Exists(Path.Combine(extract, "panorama", "images", "amtbx.vtex_c")), Is.False,
                "the second stage cleared the first stage's XP line");
        }

        [Test]
        public async Task Stage_Scoped_TouchesOnlyTheScopedTiersFiles()
        {
            string src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;
            var fx = HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 5, OnlyTier: 2));

            var written = await HeroBadgeService.StageAsync(extract, FakeSources(src, 5), fx, new Dictionary<string, byte[]>(), null, null, CancellationToken.None);

            Assert.That(written!.Where(w => w != HeroBadgePlan.MarkerEntry),
                Has.All.Matches<string>(p => p.Contains("rank_2_") || p.Contains("_3_psd") || p.Contains("level_3_") || p.EndsWith("mastery_rank3.vmdl_c")));
            Assert.That(written, Has.None.Contains("empty"), "heroes with no badge are never in a tier scope");
        }

        [Test]
        public async Task Stage_SourceTooSmall_RefusesTheWholeSet()
        {
            string src = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;

            var written = await HeroBadgeService.StageAsync(extract, FakeSources(src, 0, size: 10),
                HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Tier, 0)), new Dictionary<string, byte[]>(), null, null, CancellationToken.None);

            Assert.That(written, Is.Null);
        }

        [Test]
        public async Task Stage_HiddenBadge_BlanksEveryImage()
        {
            string extract = Directory.CreateDirectory(Path.Combine(_root, "extract")).FullName;

            var written = await HeroBadgeService.StageAsync(extract, new Dictionary<string, string>(),
                HeroBadgePlan.Resolve(new HeroBadgeRequest(HeroBadgeShow.Hide)), new Dictionary<string, byte[]>(), null, null, CancellationToken.None);

            Assert.That(HeroBadgePlan.Blanked(), Is.SubsetOf(written!));
            Assert.That(written, Has.None.Contains("mastery_rank"), "models are never blanked; the scene is collapsed by CSS");
        }

        [Test]
        public void LocateExtracted_MissingOrAmbiguous_IsNull()
        {
            string dir = Directory.CreateDirectory(Path.Combine(_root, "hl")).FullName;
            Directory.CreateDirectory(Path.Combine(dir, "a"));
            Directory.CreateDirectory(Path.Combine(dir, "b"));
            File.WriteAllText(Path.Combine(dir, "a", "hero_badge.vcss_c"), "1");

            var one = new[] { "panorama/styles/hero_badge.vcss_c" };
            Assert.That(HeroBadgeService.LocateExtracted(dir, one), Is.Not.Null);
            Assert.That(HeroBadgeService.LocateExtracted(dir, new[] { "panorama/styles/dotastyles.vcss_c" }), Is.Null);

            File.WriteAllText(Path.Combine(dir, "b", "hero_badge.vcss_c"), "2");
            Assert.That(HeroBadgeService.LocateExtracted(dir, one), Is.Null);
        }

        #endregion

        #region State

        private void Saved(HeroBadgeRequest request) =>
            _config.Setup(c => c.GetValue<string>(HeroBadgeService.ConfigKey, string.Empty))
                .Returns(JsonSerializer.Serialize(request));

        [Test]
        public void LoadState_NothingSaved_IsClean()
        {
            _config.Setup(c => c.GetValue<string>(HeroBadgeService.ConfigKey, string.Empty)).Returns(string.Empty);
            Assert.That(NewService().LoadState(), Is.EqualTo(new HeroBadgeState(false, null)));
        }

        [Test]
        public void LoadState_CorruptJson_IsCleanNotAThrow()
        {
            _config.Setup(c => c.GetValue<string>(HeroBadgeService.ConfigKey, string.Empty)).Returns("{ nope");
            Assert.That(NewService().LoadState(), Is.EqualTo(new HeroBadgeState(false, null)));
        }

        [Test]
        public void LoadState_PackageCarriesTheMarker_IsApplied()
        {
            Saved(Grandmaster30);
            string root = Install(TestVpk.Build(TestVpk.Blob("panorama/images", "amtbm", "vtex_c", 64)));

            Assert.That(NewService().LoadState(root), Is.EqualTo(new HeroBadgeState(true, Grandmaster30)));
        }

        [Test]
        public void LoadState_PackageRebuiltWithoutUs_IsNotAppliedButKeepsThePick()
        {
            Saved(Grandmaster30);
            string root = Install(TestVpk.Build(TestVpk.Blob("panorama/images", "amtm", "vtex_c", 64)));

            Assert.That(NewService().LoadState(root), Is.EqualTo(new HeroBadgeState(false, Grandmaster30)));
        }

        #endregion
    }
}
