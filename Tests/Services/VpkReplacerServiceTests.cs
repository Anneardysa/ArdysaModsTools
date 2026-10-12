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
    public class VpkReplacerServiceTests
    {
        private string _root = null!;
        private string _targetPath = null!;
        private string _sourceVpk = null!;
        private string DeployedVpk => Path.Combine(_targetPath, "game", "_ArdysaMods", "pak01_dir.vpk");

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtVpkReplacerTests_" + Guid.NewGuid().ToString("N"));
            _targetPath = Path.Combine(_root, "dota");
            Directory.CreateDirectory(_targetPath);

            _sourceVpk = Path.Combine(_root, "new_pak01_dir.vpk");
            File.WriteAllBytes(_sourceVpk, TestVpk.Minimal());
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    foreach (var f in new[] { DeployedVpk, DeployedVpk + ".bak", DeployedVpk + ".tmp" })
                        if (File.Exists(f)) File.SetAttributes(f, FileAttributes.Normal);
                    Directory.Delete(_root, true);
                }
            }
            catch {  }
        }

        private void DeployLegacyHiddenVpk()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk, new byte[] { 9, 9, 9 });
            File.SetAttributes(DeployedVpk, FileAttributes.Hidden | FileAttributes.System);
        }

        [Test]
        public async Task ReplaceAsync_OverLegacyHiddenVpk_Succeeds()
        {
            var service = new VpkReplacerService();
            DeployLegacyHiddenVpk();

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { });

            Assert.That(ok, Is.True, "replace over a hidden VPK must not throw");
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
        }

        [Test]
        public async Task ReplaceAsync_Success_DeploysRealVpk_WithNormalAttributes()
        {
            var service = new VpkReplacerService();
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk, new byte[] { 7 });

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default);

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
            Assert.That(File.GetAttributes(DeployedVpk).HasFlag(FileAttributes.Hidden), Is.False);
        }

        [Test]
        public async Task ReplaceAsync_StrayBakFromPriorRun_IsCleanedUp()
        {
            var service = new VpkReplacerService();
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk + ".bak", new byte[] { 0 });

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default);

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
        }

        #region DeployVpkAsync — fixtures

        private static readonly byte[] PreviousPackage = TestVpk.Build(TestVpk.Blob("models/old", "previous", "vmdl_c", 64));
        private static readonly byte[] Partial = { 0x34, 0x12, 0xAA };

        private string Bak => DeployedVpk + ".bak";
        private string Tmp => DeployedVpk + ".tmp";
        private const FileAttributes Superhidden = FileAttributes.Hidden | FileAttributes.System;

        private void Install(byte[] bytes, FileAttributes attributes = FileAttributes.Normal)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk, bytes);
            File.SetAttributes(DeployedVpk, attributes);
        }

        private void LeaveBak(byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(Bak, bytes);
        }

        private string Source(byte[] bytes)
        {
            var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".vpk");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private Task<bool> Deploy(string? source, bool hide = false, List<string>? log = null)
            => VpkReplacerService.DeployVpkAsync(DeployedVpk, source, hide, s => log?.Add(s), default);

        private void AssertNoLeftovers()
        {
            Assert.That(File.Exists(Bak), Is.False, ".bak must not survive");
            Assert.That(File.Exists(Tmp), Is.False, ".tmp must not survive");
        }

        #endregion

        #region DeployVpkAsync — guards (nothing in the game folder is touched)

        [Test]
        public async Task Deploy_MissingSource_FailsFast_AndKeepsTheInstalledPackage()
        {
            Install(PreviousPackage);
            var log = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool ok = await Deploy(Path.Combine(_root, "never_built.vpk"), log: log);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)), "used to retry a missing file for 15s");
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(log, Has.Some.Contains("missing"));
                AssertNoLeftovers();
            });
        }

        [Test]
        public async Task Deploy_CorruptSource_IsRefused_AndKeepsTheInstalledPackage()
        {
            Install(PreviousPackage, Superhidden);

            bool ok = await Deploy(Source(Partial), hide: true);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
                AssertNoLeftovers();
            });
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(@"game\_ArdysaMods\pak01_dir.vpk")]
        public async Task Deploy_NonRootedDestination_IsRefused(string dest)
        {
            bool ok = await VpkReplacerService.DeployVpkAsync(dest, Source(TestVpk.Minimal()), false, _ => { }, default);

            Assert.That(ok, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(@"relative\dota")]
        public async Task ReplaceAsync_NonRootedTarget_IsRefused_AndCreatesNothing(string? target)
        {
            var created = new[] { "relative", "game" }.Select(d => Path.Combine(Environment.CurrentDirectory, d)).ToArray();
            foreach (var d in created.Where(Directory.Exists))
                Directory.Delete(d, true);

            bool ok = await new VpkReplacerService().ReplaceAsync(target!, _sourceVpk, _ => { });

            Assert.That(ok, Is.False);
            Assert.That(created.Any(Directory.Exists), Is.False, "no folder created under the working directory");
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task ReplaceAsync_NoSource_IsRefused_AndKeepsTheMainPackage(string? source)
        {
            Install(PreviousPackage);

            bool ok = await new VpkReplacerService().ReplaceAsync(_targetPath, source!, _ => { });

            Assert.That(ok, Is.False);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
        }

        #endregion

        #region DeployVpkAsync — locked package (Dota running / antivirus)

        [Test]
        public async Task Deploy_LockedPackage_Fails_KeepsBytesAndSuperhiddenAttributes()
        {
            Install(PreviousPackage, Superhidden);
            var log = new List<string>();

            bool ok;
            using (new FileStream(DeployedVpk, FileMode.Open, FileAccess.Read, FileShare.Read))
                ok = await Deploy(Source(TestVpk.Minimal()), hide: true, log: log);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
                Assert.That(log, Has.Some.Contains("close Dota 2"));
                AssertNoLeftovers();
            });
        }

        [Test]
        public void Deploy_CanceledWhileWaitingOnLock_KeepsBytesAndAttributes()
        {
            Install(PreviousPackage, Superhidden);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));

            using (new FileStream(DeployedVpk, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.That(async () => await VpkReplacerService.DeployVpkAsync(
                        DeployedVpk, Source(TestVpk.Minimal()), true, _ => { }, cts.Token),
                    Throws.InstanceOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
            });
        }

        #endregion

        #region DeployVpkAsync — failure after the rename-aside rolls back

        [Test]
        public async Task Deploy_CopyFails_RestoresPreviousPackageWithItsAttributes()
        {
            Install(PreviousPackage, Superhidden);
            Directory.CreateDirectory(Tmp);
            var log = new List<string>();

            bool ok = await Deploy(Source(TestVpk.Minimal()), hide: true, log: log);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
                Assert.That(File.Exists(Bak), Is.False);
                Assert.That(log, Has.Some.StartsWith("Failed to replace VPK"));
            });
            Directory.Delete(Tmp);
        }

        #endregion

        #region DeployVpkAsync — recovery from an interrupted earlier deploy

        [Test]
        public async Task Deploy_BakWithoutPackage_IsRestored_WhenThisRunFails()
        {
            LeaveBak(PreviousPackage);

            bool ok = await Deploy(Source(Partial));

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                AssertNoLeftovers();
            });
        }

        [Test]
        public async Task Deploy_PartialPackageWithGoodBak_RestoresTheBak()
        {
            Install(Partial);
            LeaveBak(PreviousPackage);

            bool ok = await Deploy(Source(Partial));

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                AssertNoLeftovers();
            });
        }

        [Test]
        public void Recover_GoodPackageAndBak_KeepsThePackage_DropsTheBak()
        {
            Install(TestVpk.Minimal());
            LeaveBak(PreviousPackage);

            VpkReplacerService.RecoverInterruptedDeploy(DeployedVpk, Bak, Tmp);

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
                AssertNoLeftovers();
            });
        }

        [Test]
        public void Recover_BothUnreadable_KeepsThePackage()
        {
            Install(Partial);
            LeaveBak(new byte[] { 1 });

            VpkReplacerService.RecoverInterruptedDeploy(DeployedVpk, Bak, Tmp);

            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(Partial));
            AssertNoLeftovers();
        }

        [Test]
        public async Task Deploy_StrayTmp_IsDropped_AndTheDeploySucceeds()
        {
            Install(PreviousPackage);
            File.WriteAllBytes(Tmp, Partial);

            bool ok = await Deploy(Source(TestVpk.Minimal()));

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
                AssertNoLeftovers();
            });
        }

        #endregion

        #region DeployVpkAsync — happy paths

        [Test]
        public async Task Deploy_Hidden_InstallsSuperhidden_NoLeftovers()
        {
            Install(PreviousPackage);

            bool ok = await Deploy(Source(TestVpk.Minimal()), hide: true);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
                AssertNoLeftovers();
            });
        }

        [Test]
        public async Task Deploy_NullSource_RemovesThePackage_NoLeftovers()
        {
            Install(PreviousPackage, Superhidden);

            bool ok = await Deploy(null, hide: true);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.Exists(DeployedVpk), Is.False);
                AssertNoLeftovers();
            });
        }

        [Test]
        public async Task Deploy_KeepBackup_LeavesTheBak_ThenRollbackRestoresIt()
        {
            Install(PreviousPackage, Superhidden);

            bool ok = await VpkReplacerService.DeployVpkAsync(DeployedVpk, Source(TestVpk.Minimal()), true, _ => { },
                default, keepBackup: true);

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(Bak), Is.EqualTo(PreviousPackage), "kept for the caller to settle");

            Assert.That(VpkReplacerService.RollbackToBackup(DeployedVpk, hideOutput: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(PreviousPackage));
                Assert.That(File.GetAttributes(DeployedVpk) & Superhidden, Is.EqualTo(Superhidden));
                AssertNoLeftovers();
            });
        }

        [Test]
        public async Task Deploy_KeepBackup_ThenCommit_DropsTheBak()
        {
            Install(PreviousPackage);

            await VpkReplacerService.DeployVpkAsync(DeployedVpk, Source(TestVpk.Minimal()), false, _ => { },
                default, keepBackup: true);
            VpkReplacerService.CommitBackup(DeployedVpk);

            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
            AssertNoLeftovers();
        }

        [Test]
        public async Task Deploy_NothingInstalledYet_CreatesFolderAndInstalls()
        {
            bool ok = await Deploy(Source(TestVpk.Minimal()));

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(TestVpk.Minimal()));
            AssertNoLeftovers();
        }

        #endregion
    }
}
