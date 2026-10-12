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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Tests.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModsPackInstallSplitTests
    {
        private string _root = null!;
        private string _targetPath = null!;
        private string _mainVpk = null!;
        private string _protectedVpk = null!;

        private static byte[] Pack(string sentinel) =>
            TestVpk.Build(TestVpk.ItemsGame($"\"items_game\"\n{{\n\t\"{sentinel}\" \"1\"\n}}\n"));

        private static bool Contains(string vpk, string needle) =>
            System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(vpk)).Contains(needle, StringComparison.Ordinal);

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtInstallSplit_" + Guid.NewGuid().ToString("N"));
            _targetPath = Path.Combine(_root, "dota");
            Directory.CreateDirectory(Path.Combine(_targetPath, "game", "bin", "win64"));
            File.WriteAllText(Path.Combine(_targetPath, "game", "bin", "win64", "dota2.exe"), "stub");
            Directory.CreateDirectory(Path.Combine(_targetPath, "game", "_ArdysaMods"));
            _mainVpk = ProtectedVpkStore.MainVpkPath(_targetPath);
            _protectedVpk = ProtectedVpkStore.VpkPath(_targetPath);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (File.Exists(_protectedVpk)) File.SetAttributes(_protectedVpk, FileAttributes.Normal);
                string dir = ProtectedVpkStore.Dir(_targetPath);
                if (Directory.Exists(dir)) new DirectoryInfo(dir).Attributes = FileAttributes.Normal;
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch {  }
        }

        private void SeedSplitInstall()
        {
            File.WriteAllBytes(_mainVpk, Pack("OLD_MAIN"));
            ProtectedVpkStore.Ensure(_targetPath);
            File.WriteAllBytes(_protectedVpk, Pack("OLD_PROTECTED"));
            File.SetAttributes(_protectedVpk, FileAttributes.Hidden | FileAttributes.System);
        }

        private string Build(string name, byte[] bytes)
        {
            string p = Path.Combine(_root, name);
            File.WriteAllBytes(p, bytes);
            return p;
        }


        [Test]
        public async Task DeployInstallPair_HappyPath_BothLand_ProtectedStaysSuperhidden()
        {
            SeedSplitInstall();
            string newMain = Build("root.vpk", Pack("NEW_MAIN"));
            string newProt = Build("protected.vpk", Pack("NEW_PROTECTED"));

            bool ok = await ProtectedVpkStore.DeployInstallPairAsync(_targetPath, _mainVpk, newMain, newProt, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(Contains(_mainVpk, "NEW_MAIN"), Is.True);
                Assert.That(Contains(_protectedVpk, "NEW_PROTECTED"), Is.True);
                Assert.That(File.GetAttributes(_protectedVpk).HasFlag(FileAttributes.Hidden | FileAttributes.System), Is.True);
                Assert.That(File.Exists(_mainVpk + ".bak") || File.Exists(_protectedVpk + ".bak"), Is.False, "no backups left");
            });
        }

        [Test]
        public async Task DeployInstallPair_WhenTheMainSwapFails_PutsTheOldProtectedPackageBack()
        {
            SeedSplitInstall();
            string newMain = Build("root.vpk", Pack("NEW_MAIN"));
            string newProt = Build("protected.vpk", Pack("NEW_PROTECTED"));

            bool ok;
            using (File.Open(_mainVpk, FileMode.Open, FileAccess.Read, FileShare.None))
                ok = await ProtectedVpkStore.DeployInstallPairAsync(_targetPath, _mainVpk, newMain, newProt, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(Contains(_mainVpk, "OLD_MAIN"), Is.True);
                Assert.That(Contains(_protectedVpk, "OLD_PROTECTED"), Is.True, "protected package rolled back with the main one");
                Assert.That(File.GetAttributes(_protectedVpk).HasFlag(FileAttributes.Hidden), Is.True);
            });
        }

        [Test]
        public async Task DeployInstallPair_WhenTheMainBuildIsUnreadable_ChangesNothing()
        {
            SeedSplitInstall();
            string newMain = Build("root.vpk", new byte[] { 1, 2, 3, 4 });
            string newProt = Build("protected.vpk", Pack("NEW_PROTECTED"));

            bool ok = await ProtectedVpkStore.DeployInstallPairAsync(_targetPath, _mainVpk, newMain, newProt, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(Contains(_mainVpk, "OLD_MAIN"), Is.True);
                Assert.That(Contains(_protectedVpk, "OLD_PROTECTED"), Is.True);
            });
        }


        [Test]
        public void SignatureTagging_KeepsTheProtectedPackageSuperhidden()
        {
            SeedSplitInstall();

            VpkSignatureSection.TryApply(_protectedVpk);

            Assert.Multiple(() =>
            {
                Assert.That(VpkSignatureSection.IsApplied(_protectedVpk), Is.True, "precondition: it was tagged");
                Assert.That(File.GetAttributes(_protectedVpk).HasFlag(FileAttributes.Hidden | FileAttributes.System), Is.True);
            });
        }


        private static void RequireRealTools()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var hl = new FileInfo(Path.Combine(baseDir, "HLExtract.exe"));
            if (!hl.Exists || hl.Length < 4096)
                Assert.Ignore("Real HLExtract.exe not present in the test output.");
        }

        [Test]
        public async Task InstallIsIntact_SplitInstallWithBothPackages_IsIntact()
        {
            SeedSplitInstall();
            Assert.That(await new ModInstallerService().InstallIsIntactAsync(_targetPath), Is.True);
        }

        [Test]
        public async Task InstallIsIntact_NoMainPackage_IsNotIntact()
        {
            ProtectedVpkStore.Ensure(_targetPath);
            File.WriteAllBytes(_protectedVpk, Pack("OLD_PROTECTED"));
            Assert.That(await new ModInstallerService().InstallIsIntactAsync(_targetPath), Is.False);
        }

        [Test]
        public async Task InstallIsIntact_StrippedMainWithoutProtectedPackage_IsNotIntact()
        {
            RequireRealTools();
            File.WriteAllBytes(_mainVpk, TestVpk.Build(TestVpk.Blob("models/heroes/axe", "axe", "vmdl_c", 64)));
            Assert.That(await new ModInstallerService().InstallIsIntactAsync(_targetPath), Is.False);
        }

        [Test]
        public async Task InstallIsIntact_SelfContainedMainWithoutProtectedPackage_IsIntact()
        {
            RequireRealTools();
            File.WriteAllBytes(_mainVpk, Pack("SELF_CONTAINED"));
            Assert.That(await new ModInstallerService().InstallIsIntactAsync(_targetPath), Is.True);
        }

        [Test]
        public async Task ManualInstall_AsIsPackWithoutItemData_FailsAndRestoresThePreviousInstall()
        {
            RequireRealTools();
            SeedSplitInstall();
            string slimPack = Build("slim.vpk", TestVpk.Build(TestVpk.Blob("models/heroes/axe", "axe", "vmdl_c", 64)));

            bool result = await new ModInstallerService().ManualInstallModsAsync(_targetPath, slimPack, rebuild: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(Contains(_mainVpk, "OLD_MAIN"), Is.True);
                Assert.That(Contains(_protectedVpk, "OLD_PROTECTED"), Is.True);
            });
        }


        private sealed class DeployingDataService : ModsPackDataService
        {
            private readonly string _newMain, _newProt;
            public DeployingDataService(string newMain, string newProt) { _newMain = newMain; _newProt = newProt; }

            public override async Task<bool> RebuildVpkAsync(string targetPath, string vpkPath,
                Action<string> status, CancellationToken ct = default, IProgress<int>? percent = null)
            {
                if (!await ProtectedVpkStore.DeployInstallPairAsync(targetPath, vpkPath, _newMain, _newProt, status))
                    return false;
                await ItemsGameBaselineStore.CommitAsync(targetPath, new[] { "999" }, ct);
                return true;
            }
        }

        private sealed class ExtractorWithoutId999 : IGameItemsGameExtractor
        {
            private static Task<bool> Write(string dest)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.WriteAllText(dest, "\"items_game\" { \"items\" { \"101\" { } } }");
                return Task.FromResult(true);
            }
            public Task<bool> ExtractItemsGameAsync(string vpkPath, string destFilePath, Action<string>? log = null, CancellationToken ct = default, bool normalize = true) => Write(destFilePath);
            public Task<bool> ExtractModItemsGameAsync(string targetPath, string destFilePath, Action<string>? log = null, CancellationToken ct = default) => Write(destFilePath);
            public Task<bool> RefreshFromGameAsync(string targetPath, string extractDir, Action<string> status, CancellationToken ct = default) => Task.FromResult(true);
        }

        [Test]
        public async Task ManualInstall_FailingAfterTheProtectedDeploy_RestoresBothOldPackages()
        {
            SeedSplitInstall();
            string userVpk = Build("user_pack.vpk", Pack("USER_PACK"));
            var data = new DeployingDataService(Build("root.vpk", Pack("NEW_MAIN")), Build("protected.vpk", Pack("NEW_PROTECTED")));
            var installer = new ModInstallerService(null, data, new ExtractorWithoutId999());

            bool result = await installer.ManualInstallModsAsync(_targetPath, userVpk, rebuild: true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "precondition: verification rejected the install");
                Assert.That(Contains(_mainVpk, "OLD_MAIN"), Is.True);
                Assert.That(File.Exists(_protectedVpk), Is.True, "the old item data must come back with the old main package");
                Assert.That(Contains(_protectedVpk, "OLD_PROTECTED"), Is.True);
                Assert.That(Directory.GetFiles(ProtectedVpkStore.Dir(_targetPath)), Has.Length.EqualTo(1), "no backup left in game/mod");
            });
        }
    }
}
