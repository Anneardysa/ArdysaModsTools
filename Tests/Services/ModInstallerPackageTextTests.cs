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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Mods;
using ArdysaModsTools.Tests.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModInstallerPackageTextTests
    {
        private string _root = null!;
        private string _targetPath = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtInstallerSplit_" + Guid.NewGuid().ToString("N"));
            _targetPath = Path.Combine(_root, "dota");
            Directory.CreateDirectory(Path.Combine(_targetPath, "game", "bin", "win64"));
            File.WriteAllText(Path.Combine(_targetPath, "game", "bin", "win64", "dota2.exe"), "stub");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                string prot = ProtectedVpkStore.VpkPath(_targetPath);
                if (File.Exists(prot)) File.SetAttributes(prot, FileAttributes.Normal);
                string dir = ProtectedVpkStore.Dir(_targetPath);
                if (Directory.Exists(dir)) new DirectoryInfo(dir).Attributes = FileAttributes.Normal;
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch { }
        }

        private sealed class RecordingDataService : ModsPackDataService
        {
            public bool WasCalled;
            public bool StaleRecordStillExisted;
            public string? RecordWrittenByRebuild;

            public override Task<bool> RebuildVpkAsync(string targetPath, string vpkPath,
                Action<string> status, CancellationToken ct = default, IProgress<int>? percent = null)
            {
                WasCalled = true;
                string temp = Path.Combine(targetPath, "game", "_ArdysaMods", "_temp");
                StaleRecordStillExisted = File.Exists(Path.Combine(temp, "hero_selections.json"));

                Directory.CreateDirectory(temp);
                RecordWrittenByRebuild = Path.Combine(temp, "hero_selections.json");
                File.WriteAllText(RecordWrittenByRebuild, "written by the rebuild");
                return Task.FromResult(false);
            }
        }

        [Test]
        public async Task ManualInstall_ClearsStaleStateBeforeTheRebuild_AndNothingClearsAfterIt()
        {
            ProtectedVpkStore.Ensure(_targetPath);
            File.WriteAllBytes(ProtectedVpkStore.VpkPath(_targetPath), TestVpk.Minimal());
            string temp = Path.Combine(_targetPath, "game", "_ArdysaMods", "_temp");
            Directory.CreateDirectory(temp);
            File.WriteAllText(Path.Combine(temp, "hero_selections.json"), "stale");

            string userVpk = Path.Combine(_root, "user_pack.vpk");
            File.WriteAllBytes(userVpk, TestVpk.Minimal());

            var dataService = new RecordingDataService();
            var installer = new ModInstallerService(null, dataService);

            await installer.ManualInstallModsAsync(_targetPath, userVpk, rebuild: true);

            Assert.Multiple(() =>
            {
                Assert.That(dataService.WasCalled, Is.True, "the rebuild is what writes the new protected package");
                Assert.That(dataService.StaleRecordStillExisted, Is.False,
                    "stale state must already be cleared when the rebuild starts");
                Assert.That(File.Exists(dataService.RecordWrittenByRebuild!), Is.True,
                    "nothing may clear after the rebuild — a Clear() there would delete the item " +
                    "definitions the rebuild had just packed into game/mod");
            });
        }

        [Test]
        public void RemoveLoosePackageText_DeletesLooseScriptsAndResource()
        {
            string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(Path.Combine(modsDir, "scripts", "items"));
            Directory.CreateDirectory(Path.Combine(modsDir, "resource", "localization"));
            File.WriteAllText(Path.Combine(modsDir, "scripts", "items", "items_game.txt"), "loose item data");
            File.WriteAllText(Path.Combine(modsDir, "resource", "localization", "dota_english.txt"), "loose names");
            File.WriteAllBytes(Path.Combine(modsDir, "pak01_dir.vpk"), TestVpk.Minimal());

            bool clean = ModInstallerService.RemoveLoosePackageText(modsDir);

            Assert.Multiple(() =>
            {
                Assert.That(clean, Is.True);
                Assert.That(Directory.Exists(Path.Combine(modsDir, "scripts")), Is.False);
                Assert.That(Directory.Exists(Path.Combine(modsDir, "resource")), Is.False);
                Assert.That(File.Exists(Path.Combine(modsDir, "pak01_dir.vpk")), Is.True,
                    "only the loose trees go — the package itself stays");
            });
        }

        [Test]
        public void RemoveLoosePackageText_WhenALeftoverSurvives_ReportsFailure()
        {
            string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
            string looseItemData = Path.Combine(modsDir, "scripts", "items", "items_game.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(looseItemData)!);
            File.WriteAllText(looseItemData, "loose item data");

            using (File.Open(looseItemData, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ModInstallerService.RemoveLoosePackageText(modsDir), Is.False);
                    Assert.That(File.Exists(looseItemData), Is.True, "precondition: the leftover really did survive");
                });
            }
        }

        [Test]
        public async Task ManualInstall_WhenLoosePackageTextSurvives_FailsAndRollsBack()
        {
            string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);
            string initialVpk = Path.Combine(modsDir, "pak01_dir.vpk");
            File.WriteAllBytes(initialVpk, TestVpk.Minimal());

            string looseItemData = Path.Combine(modsDir, "scripts", "items", "items_game.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(looseItemData)!);
            File.WriteAllText(looseItemData, "loose item data");

            string userVpk = Path.Combine(_root, "user_pack.vpk");
            File.WriteAllBytes(userVpk, TestVpk.Minimal());

            var installer = new ModInstallerService();

            using (File.Open(looseItemData, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool result = await installer.ManualInstallModsAsync(_targetPath, userVpk, rebuild: false);
                Assert.That(result, Is.False, "install must fail when loose item definitions cannot be cleaned");
                Assert.That(File.Exists(initialVpk), Is.True, "snapshot must roll back to original VPK");
            }
        }

        private sealed class SuccessfulRebuildDataService : ModsPackDataService
        {
            private readonly string[] _patchedIds;
            public SuccessfulRebuildDataService(string[] patchedIds) => _patchedIds = patchedIds;

            public override async Task<bool> RebuildVpkAsync(string targetPath, string vpkPath,
                Action<string> status, CancellationToken ct = default, IProgress<int>? percent = null)
            {
                await ItemsGameBaselineStore.CommitAsync(targetPath, _patchedIds, ct).ConfigureAwait(false);
                return true;
            }
        }

        private sealed class FakeExtractorWithCustomContent : IGameItemsGameExtractor
        {
            private readonly string _content;
            public FakeExtractorWithCustomContent(string content) => _content = content;

            public Task<bool> ExtractItemsGameAsync(string vpkPath, string destFilePath, Action<string>? log = null, CancellationToken ct = default, bool normalize = true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                File.WriteAllText(destFilePath, _content);
                return Task.FromResult(true);
            }

            public Task<bool> ExtractModItemsGameAsync(string targetPath, string destFilePath, Action<string>? log = null, CancellationToken ct = default)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                File.WriteAllText(destFilePath, _content);
                return Task.FromResult(true);
            }

            public Task<bool> RefreshFromGameAsync(string targetPath, string extractDir, Action<string> status, CancellationToken ct = default)
            {
                return Task.FromResult(true);
            }
        }

        [Test]
        public async Task ManualInstall_RebuildVerification_MissingPatchedId_FailsAndRollsBack()
        {
            string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);
            string initialVpk = Path.Combine(modsDir, "pak01_dir.vpk");
            File.WriteAllBytes(initialVpk, TestVpk.Minimal());

            string userVpk = Path.Combine(_root, "user_pack.vpk");
            File.WriteAllBytes(userVpk, TestVpk.Minimal());

            var dataService = new SuccessfulRebuildDataService(new[] { "999" });
            string extractedKv = "\"items_game\" { \"items\" { \"101\" { \"prefab\" \"wearable\" } } }";
            var fakeExtractor = new FakeExtractorWithCustomContent(extractedKv);

            var installer = new ModInstallerService(null, dataService, fakeExtractor);

            bool result = await installer.ManualInstallModsAsync(_targetPath, userVpk, rebuild: true);

            Assert.That(result, Is.False, "verification must reject rebuild missing required patched IDs");
            Assert.That(File.Exists(initialVpk), Is.True, "snapshot must roll back to original VPK");
        }

        [Test]
        public async Task ManualInstall_RebuildVerification_MissingLocalization_FailsAndRollsBack()
        {
            string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);
            string initialVpk = Path.Combine(modsDir, "pak01_dir.vpk");
            File.WriteAllBytes(initialVpk, TestVpk.Minimal());

            string userVpk = Path.Combine(_root, "user_pack.vpk");
            File.WriteAllBytes(userVpk, TestVpk.Minimal());

            var dataService = new SuccessfulRebuildDataService(new[] { "101" });
            string extractedKv = "\"items_game\" { \"items\" { \"101\" { \"prefab\" \"wearable\" } } }";
            var fakeExtractor = new FakeExtractorWithCustomContent(extractedKv);

            var installer = new ModInstallerService(null, dataService, fakeExtractor);

            bool result = await installer.ManualInstallModsAsync(_targetPath, userVpk, rebuild: true);

            Assert.That(result, Is.False, "verification must reject rebuild missing localization");
            Assert.That(File.Exists(initialVpk), Is.True, "snapshot must roll back to original VPK");
        }

        [Test]
        public void ManualInstall_RawUserVpk_CanBeGuardedWithVpkSignatureSection()
        {
            string userVpk = Path.Combine(_root, "raw_user_pack.vpk");
            File.WriteAllBytes(userVpk, TestVpk.Minimal());

            Assert.That(VpkSignatureSection.IsApplied(userVpk), Is.False, "precondition: raw user pack is not signed");
            VpkSignatureSection.TryApply(userVpk);
            Assert.That(VpkSignatureSection.IsApplied(userVpk), Is.True, "TryApply must upgrade and secure the user pack");
        }
    }
}
