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
            File.WriteAllBytes(_sourceVpk, new byte[] { 1, 2, 3, 4 });
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    if (File.Exists(DeployedVpk)) File.SetAttributes(DeployedVpk, FileAttributes.Normal);
                    Directory.Delete(_root, true);
                }
            }
            catch {  }
        }

        [Test]
        public async Task ReplaceAsync_HideOutputTrue_MarksDeployedVpkHiddenAndSystem()
        {
            var service = new VpkReplacerService();

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default, hideOutput: true);

            Assert.That(ok, Is.True);
            Assert.That(File.Exists(DeployedVpk), Is.True);
            var attrs = File.GetAttributes(DeployedVpk);
            Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.True, "expected Hidden");
            Assert.That(attrs.HasFlag(FileAttributes.System), Is.True, "expected System");
        }

        [Test]
        public async Task ReplaceAsync_OverExistingHiddenVpk_Succeeds()
        {
            var service = new VpkReplacerService();

            var first = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default, hideOutput: true);
            Assert.That(first, Is.True);
            Assert.That(File.GetAttributes(DeployedVpk).HasFlag(FileAttributes.Hidden), Is.True);

            var source2 = Path.Combine(_root, "new_pak01_dir_2.vpk");
            File.WriteAllBytes(source2, new byte[] { 9, 9, 9 });

            var second = await service.ReplaceAsync(_targetPath, source2, _ => { }, default, hideOutput: true);
            Assert.That(second, Is.True, "second replace over a hidden VPK must not throw");
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(new byte[] { 9, 9, 9 }));
        }

        [Test]
        public async Task ReplaceAsync_HideOutputFalse_LeavesVpkVisible()
        {
            var service = new VpkReplacerService();

            await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default, hideOutput: true);

            var source2 = Path.Combine(_root, "new_pak01_dir_2.vpk");
            File.WriteAllBytes(source2, new byte[] { 5, 6 });

            var ok = await service.ReplaceAsync(_targetPath, source2, _ => { }, default, hideOutput: false);

            Assert.That(ok, Is.True);
            var attrs = File.GetAttributes(DeployedVpk);
            Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.False, "expected not Hidden");
            Assert.That(attrs.HasFlag(FileAttributes.System), Is.False, "expected not System");
        }

        [Test]
        public async Task ReplaceAsync_DestinationLocked_ReturnsFalse_OriginalIntact()
        {
            var service = new VpkReplacerService();
            var original = new byte[] { 7, 7, 7 };
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk, original);

            var logged = new List<string>();
            using (new FileStream(DeployedVpk, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, logged.Add, default);
                Assert.That(ok, Is.False, "locked destination must fail");
            }

            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(original), "original must be untouched");
            Assert.That(File.Exists(DeployedVpk + ".bak"), Is.False, "no backup may be left behind");
            Assert.That(logged, Has.Some.Contains("close Dota 2"), "user must get an actionable message");
        }

        [Test]
        public async Task ReplaceAsync_Success_LeavesNoBakFile()
        {
            var service = new VpkReplacerService();
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk, new byte[] { 7 });

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default);

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(File.Exists(DeployedVpk + ".bak"), Is.False);
        }

        [Test]
        public async Task ReplaceAsync_StrayBakFromPriorRun_IsCleanedUp()
        {
            var service = new VpkReplacerService();
            Directory.CreateDirectory(Path.GetDirectoryName(DeployedVpk)!);
            File.WriteAllBytes(DeployedVpk + ".bak", new byte[] { 0 });

            var ok = await service.ReplaceAsync(_targetPath, _sourceVpk, _ => { }, default);

            Assert.That(ok, Is.True);
            Assert.That(File.ReadAllBytes(DeployedVpk), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(File.Exists(DeployedVpk + ".bak"), Is.False);
        }
    }
}
