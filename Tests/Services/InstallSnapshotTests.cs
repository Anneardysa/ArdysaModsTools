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
using NUnit.Framework;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class InstallSnapshotTests
    {
        private string _dir = null!;
        private string _vpk = null!;
        private string _hash = null!;
        private string _protectedVpk = null!;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), $"AMT_SnapshotTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dir);
            _vpk = Path.Combine(_dir, "pak01_dir.vpk");
            _hash = Path.Combine(_dir, "ModsPack.hash");
            string protDir = Path.Combine(_dir, "mod");
            Directory.CreateDirectory(protDir);
            _protectedVpk = Path.Combine(protDir, "pak01_dir.vpk");
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch {  }
        }

        [Test]
        public void Dispose_WithoutCommit_RestoresPreviousInstall()
        {
            File.WriteAllText(_vpk, "old-vpk");
            File.WriteAllText(_hash, "old-hash");

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk))
            {
                File.WriteAllText(_vpk, "new-vpk");
                File.WriteAllText(_hash, "new-hash");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(_vpk), Is.EqualTo("old-vpk"));
                Assert.That(File.ReadAllText(_hash), Is.EqualTo("old-hash"));
                Assert.That(File.Exists(_vpk + ".bak"), Is.False);
                Assert.That(File.Exists(_hash + ".bak"), Is.False);
            });
        }

        [Test]
        public void Commit_KeepsNewInstall_AndDropsBackups()
        {
            File.WriteAllText(_vpk, "old-vpk");
            File.WriteAllText(_hash, "old-hash");
            File.WriteAllText(_protectedVpk, "old-protected");

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk, _protectedVpk))
            {
                File.WriteAllText(_vpk, "new-vpk");
                File.WriteAllText(_hash, "new-hash");
                File.WriteAllText(_protectedVpk, "new-protected");
                snapshot.Commit();
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(_vpk), Is.EqualTo("new-vpk"));
                Assert.That(File.ReadAllText(_hash), Is.EqualTo("new-hash"));
                Assert.That(File.ReadAllText(_protectedVpk), Is.EqualTo("new-protected"));
                Assert.That(File.Exists(_vpk + ".bak"), Is.False);
                Assert.That(File.Exists(_hash + ".bak"), Is.False);
                Assert.That(File.Exists(_protectedVpk + ".bak"), Is.False);
            });
        }

        [Test]
        public void Dispose_WithoutCommit_RestoresProtectedPackage()
        {
            File.WriteAllText(_vpk, "old-vpk");
            File.WriteAllText(_protectedVpk, "old-protected");

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk, _protectedVpk))
            {
                File.WriteAllText(_vpk, "new-vpk");
                File.WriteAllText(_protectedVpk, "half-written-protected");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(_vpk), Is.EqualTo("old-vpk"));
                Assert.That(File.ReadAllText(_protectedVpk), Is.EqualTo("old-protected"));
                Assert.That(File.Exists(_protectedVpk + ".bak"), Is.False);
            });
        }

        [Test]
        public void Dispose_WithoutCommit_OnFreshInstall_ClearsTheSlot()
        {
            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk))
            {
                File.WriteAllText(_vpk, "half-written-vpk");
                File.WriteAllText(_hash, "new-hash");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(_vpk), Is.False);
                Assert.That(File.Exists(_hash), Is.False);
            });
        }

        [Test]
        public async Task Dispose_AfterARealProtectedDeploy_StillRestoresTheOldProtectedPackage()
        {
            byte[] oldProtected = TestVpk.Build(TestVpk.ItemsGame("\"items_game\" { \"OLD_PROTECTED\" \"1\" }"));
            byte[] newProtected = TestVpk.Build(TestVpk.ItemsGame("\"items_game\" { \"NEW_PROTECTED\" \"1\" }"));
            string newBuild = Path.Combine(_dir, "built_protected.vpk");
            File.WriteAllBytes(_vpk, TestVpk.Minimal());
            File.WriteAllBytes(_protectedVpk, oldProtected);
            File.WriteAllBytes(newBuild, newProtected);

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk, _protectedVpk))
            {
                bool deployed = await VpkReplacerService.DeployVpkAsync(
                    _protectedVpk, newBuild, hideOutput: true, _ => { }, CancellationToken.None);
                Assert.That(deployed, Is.True, "precondition: the rebuild's protected deploy landed");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(_protectedVpk), Is.True, "the previous protected package must come back");
                Assert.That(File.ReadAllBytes(_protectedVpk), Is.EqualTo(oldProtected));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(_protectedVpk)!), Has.Length.EqualTo(1),
                    "no backup left behind in game/mod");
            });
        }

        [Test]
        public void Capture_WhenACrashLeftOnlyTheBackup_TakesItBackAsThePreviousInstall()
        {
            File.WriteAllText(_protectedVpk, "old-protected");
            ModInstallerService.InstallSnapshot.Capture(_vpk, _protectedVpk);
            Assert.That(File.Exists(_protectedVpk), Is.False, "precondition: only the backup survived");

            using (ModInstallerService.InstallSnapshot.Capture(_vpk, _protectedVpk))
            {
                File.WriteAllText(_protectedVpk, "half-written");
            }

            Assert.That(File.ReadAllText(_protectedVpk), Is.EqualTo("old-protected"));
        }
    }
}
