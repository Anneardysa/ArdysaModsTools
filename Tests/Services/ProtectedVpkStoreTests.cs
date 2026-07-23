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
    public class ProtectedVpkStoreTests
    {
        private string _root = null!;
        private string _targetPath = null!;
        private string _extractDir = null!;
        private string _protectedDir = null!;
        private string ProtectedVpk => ProtectedVpkStore.VpkPath(_targetPath);

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtProtectedVpkTests_" + Guid.NewGuid().ToString("N"));
            _targetPath = Path.Combine(_root, "dota");
            _extractDir = Path.Combine(_root, "base");
            _protectedDir = Path.Combine(_root, "protected");
            Directory.CreateDirectory(_targetPath);
            Directory.CreateDirectory(_extractDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (File.Exists(ProtectedVpk)) File.SetAttributes(ProtectedVpk, FileAttributes.Normal);
                var dir = ProtectedVpkStore.Dir(_targetPath);
                if (Directory.Exists(dir)) File.SetAttributes(dir, FileAttributes.Normal);
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch {  }
        }

        private string WriteAsset(string relativePath)
        {
            var full = Path.Combine(_extractDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, relativePath);
            return full;
        }

        private static string NewVpk(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            return path;
        }

        #region IsProtectable

        [TestCase("models/heroes/abaddon/abaddon.vmdl_c")]
        [TestCase("particles/econ/items/x.vpcf_c")]
        [TestCase("materials/models/x.vmat_c")]
        [TestCase("sound/weapons/x.vsnd_c")]
        [TestCase("panorama/images/x.vtex_c")]
        [TestCase(@"models\heroes\x.vmdl_c")]
        public void IsProtectable_AssetPaths_ReturnsTrue(string path)
        {
            Assert.That(ProtectedVpkStore.IsProtectable(path), Is.True);
        }

        [TestCase("scripts/items/items_game.txt")]
        [TestCase("resource/localization/dota_english.txt")]
        [TestCase(@"scripts\items\items_game.txt")]
        [TestCase("")]
        public void IsProtectable_PackageOwnedPaths_ReturnsFalse(string path)
        {
            Assert.That(ProtectedVpkStore.IsProtectable(path), Is.False);
        }

        #endregion

        #region IsMountedBy

        private const string MountingGameInfo =
            "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tmod\r\n\t\t\tGame\t\t\t\tdota\r\n" +
            "\r\n\t\t\tMod\t\t\t\t\t_ArdysaMods\r\n\t\t\tMod\t\t\t\t\tmod\r\n\t\t\tMod\t\t\t\t\tdota\r\n";

        [Test]
        public void IsMountedBy_GameInfoWithModEntry_ReturnsTrue()
        {
            Assert.That(ProtectedVpkStore.IsMountedBy(MountingGameInfo), Is.True);
        }

        [Test]
        public void IsMountedBy_QuotedEntry_ReturnsTrue()
        {
            Assert.That(ProtectedVpkStore.IsMountedBy("\t\t\tGame\t\t\"mod\"\n"), Is.True);
        }

        [TestCase("\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n")]
        [TestCase("\t\t\tGame\t\t\t\tmods\r\n")]
        [TestCase("\t\t\tGame\t\t\t\tmod extra\r\n")]
        [TestCase("")]
        [TestCase(null)]
        public void IsMountedBy_WithoutModEntry_ReturnsFalse(string? gameInfo)
        {
            Assert.That(ProtectedVpkStore.IsMountedBy(gameInfo), Is.False);
        }

        [Test]
        public void IsMounted_NoGameInfoFile_ReturnsFalse()
        {
            Assert.That(ProtectedVpkStore.IsMounted(_targetPath), Is.False);
        }

        #endregion

        #region MoveProtected

        [Test]
        public void MoveProtected_MovesListedFiles_AndLeavesTheRest()
        {
            var protectedAsset = WriteAsset("models/heroes/x/x.vmdl_c");
            var plainAsset = WriteAsset("models/heroes/y/y.vmdl_c");
            var itemsGame = WriteAsset("scripts/items/items_game.txt");

            int moved = ProtectedVpkStore.MoveProtected(
                _extractDir, _protectedDir, new[] { "models/heroes/x/x.vmdl_c" });

            Assert.Multiple(() =>
            {
                Assert.That(moved, Is.EqualTo(1));
                Assert.That(File.Exists(protectedAsset), Is.False, "source must be gone");
                Assert.That(File.Exists(Path.Combine(_protectedDir, "models", "heroes", "x", "x.vmdl_c")),
                    Is.True, "structure must be preserved");
                Assert.That(File.Exists(plainAsset), Is.True);
                Assert.That(File.Exists(itemsGame), Is.True);
            });
        }

        [Test]
        public void MoveProtected_MissingPath_IsSkipped()
        {
            int moved = ProtectedVpkStore.MoveProtected(
                _extractDir, _protectedDir, new[] { "models/gone.vmdl_c" });

            Assert.That(moved, Is.Zero);
        }

        #endregion

        #region Deploy / Clear

        [Test]
        public async Task DeployAsync_InstallsPackage_Superhidden()
        {
            var source = NewVpk(Path.Combine(_root, "protected.vpk"));

            bool ok = await ProtectedVpkStore.DeployAsync(_targetPath, source, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.Exists(ProtectedVpk), Is.True);
                var fileAttrs = File.GetAttributes(ProtectedVpk);
                Assert.That(fileAttrs.HasFlag(FileAttributes.Hidden), Is.True, "file Hidden");
                Assert.That(fileAttrs.HasFlag(FileAttributes.System), Is.True, "file System");
                var dirAttrs = File.GetAttributes(ProtectedVpkStore.Dir(_targetPath));
                Assert.That(dirAttrs.HasFlag(FileAttributes.Hidden), Is.True, "folder Hidden");
                Assert.That(dirAttrs.HasFlag(FileAttributes.System), Is.True, "folder System");
                Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False, "no backup left behind");
            });
        }

        [Test]
        public async Task DeployAsync_NullSource_RemovesPreviousPackage()
        {
            await ProtectedVpkStore.DeployAsync(_targetPath, NewVpk(Path.Combine(_root, "p1.vpk")), _ => { });
            Assert.That(File.Exists(ProtectedVpk), Is.True, "precondition");

            bool ok = await ProtectedVpkStore.DeployAsync(_targetPath, null, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.Exists(ProtectedVpk), Is.False);
                Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False);
            });
        }

        [Test]
        public async Task Clear_RemovesSuperhiddenPackage()
        {
            await ProtectedVpkStore.DeployAsync(_targetPath, NewVpk(Path.Combine(_root, "p1.vpk")), _ => { });

            ProtectedVpkStore.Clear(_targetPath);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(ProtectedVpk), Is.False);
                Assert.That(Directory.Exists(ProtectedVpkStore.Dir(_targetPath)), Is.True, "folder stays");
            });
        }

        [Test]
        public void Clear_NoPackage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ProtectedVpkStore.Clear(_targetPath));
        }

        #endregion
    }
}
