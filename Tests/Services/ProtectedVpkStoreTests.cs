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
            File.WriteAllBytes(path, TestVpk.Minimal());
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
        [TestCase("kisilev_ind/particles/econ/items/antimage/weapon.vpcf_c")]
        [TestCase("models/heroes/antimage/kisilev_ind/weapon.vmdl_c")]
        [TestCase(@"models\heroesntimage\kisilev_ind\weapon.vmdl_c")]
        [TestCase("materials/kisilev_ind/textures.vmat_c")]
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
        public void CanSplit_NoGameInfoFile_ReturnsFalse()
        {
            Assert.That(ProtectedVpkStore.CanSplit(_targetPath), Is.False);
        }

        [Test]
        public void CanSplit_PatchedGameInfo_ReturnsTrue()
        {
            WriteGameInfo(MountingGameInfo);
            Assert.That(ProtectedVpkStore.CanSplit(_targetPath), Is.True);
        }

        [Test]
        public void CanSplit_VanillaGameInfo_ReturnsTrue()
        {
            WriteGameInfo("\t\t\tGame\t\t\t\tdota\r\n\t\t\tGame\t\t\t\tcore\r\n");
            Assert.That(ProtectedVpkStore.CanSplit(_targetPath), Is.True);
        }

        [Test]
        public void CanSplit_LegacyPatchedGameInfo_ReturnsFalse()
        {
            WriteGameInfo("\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n");
            Assert.That(ProtectedVpkStore.CanSplit(_targetPath), Is.False);
        }

        private void WriteGameInfo(string text)
        {
            string gi = Path.Combine(_targetPath, "game", "dota", "gameinfo_branchspecific.gi");
            Directory.CreateDirectory(Path.GetDirectoryName(gi)!);
            File.WriteAllText(gi, text);
        }

        #endregion

        #region Split

        [Test]
        public void Split_LegacyPatchedGameInfo_LeavesAssetsAndPackageTextInTheMainTree()
        {
            WriteGameInfo("\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n");
            var asset = WriteAsset("models/heroes/x/x.vmdl_c");
            var itemsGame = WriteAsset("scripts/items/items_game.txt");

            int moved = ProtectedVpkStore.Split(
                _targetPath, _extractDir, _protectedDir, new[] { "models/heroes/x/x.vmdl_c" });

            Assert.Multiple(() =>
            {
                Assert.That(moved, Is.Zero);
                Assert.That(File.Exists(asset), Is.True, "the asset must stay in the package the game mounts");
                Assert.That(File.Exists(itemsGame), Is.True);
                Assert.That(Directory.Exists(_protectedDir), Is.False, "nothing is staged for a package that is never mounted");
            });
        }

        [Test]
        public void Split_PatchedGameInfo_MovesBothTheAssetsAndThePackageText()
        {
            WriteGameInfo(MountingGameInfo);
            var asset = WriteAsset("models/heroes/x/x.vmdl_c");
            var plainAsset = WriteAsset("models/heroes/y/y.vmdl_c");
            WriteAsset("scripts/items/items_game.txt");

            int moved = ProtectedVpkStore.Split(
                _targetPath, _extractDir, _protectedDir, new[] { "models/heroes/x/x.vmdl_c" });

            Assert.Multiple(() =>
            {
                Assert.That(moved, Is.EqualTo(2));
                Assert.That(File.Exists(asset), Is.False);
                Assert.That(File.Exists(Path.Combine(_protectedDir, "models", "heroes", "x", "x.vmdl_c")), Is.True);
                Assert.That(File.Exists(Path.Combine(_protectedDir, "scripts", "items", "items_game.txt")), Is.True);
                Assert.That(File.Exists(plainAsset), Is.True, "an unprotected asset stays in the main package");
            });
        }

        [Test]
        public void Split_NoAssetPaths_StillMovesThePackageText()
        {
            WriteGameInfo(MountingGameInfo);
            WriteAsset("scripts/items/items_game.txt");

            Assert.That(ProtectedVpkStore.Split(_targetPath, _extractDir, _protectedDir), Is.EqualTo(1));
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

        #region MovePackageText / BorrowPackageText

        [Test]
        public void MovePackageText_MovesItemDataAndLocalizationOnly()
        {
            var itemsGame = WriteAsset("scripts/items/items_game.txt");
            var locale = WriteAsset("resource/localization/abilities_english.txt");
            var otherScript = WriteAsset("scripts/vscripts/other.lua");
            var model = WriteAsset("models/heroes/x/x.vmdl_c");

            int moved = ProtectedVpkStore.MovePackageText(_extractDir, _protectedDir);

            Assert.Multiple(() =>
            {
                Assert.That(moved, Is.EqualTo(2));
                Assert.That(File.Exists(itemsGame), Is.False, "item data must leave the main tree");
                Assert.That(File.Exists(locale), Is.False, "localization must leave the main tree");
                Assert.That(File.Exists(Path.Combine(_protectedDir, "scripts", "items", "items_game.txt")), Is.True);
                Assert.That(File.Exists(Path.Combine(_protectedDir, "resource", "localization", "abilities_english.txt")), Is.True);
                Assert.That(File.Exists(otherScript), Is.True);
                Assert.That(File.Exists(model), Is.True);
            });
        }

        [Test]
        public void BorrowPackageText_IsTheExactReverseOfMove()
        {
            WriteAsset("scripts/items/items_game.txt");
            WriteAsset("resource/localization/abilities_english.txt");

            ProtectedVpkStore.MovePackageText(_extractDir, _protectedDir);
            int borrowed = ProtectedVpkStore.BorrowPackageText(_protectedDir, _extractDir);

            Assert.Multiple(() =>
            {
                Assert.That(borrowed, Is.EqualTo(2));
                Assert.That(File.ReadAllText(Path.Combine(_extractDir, "scripts", "items", "items_game.txt")),
                    Is.EqualTo("scripts/items/items_game.txt"), "content survives the round trip");
                Assert.That(File.ReadAllText(Path.Combine(_extractDir, "resource", "localization", "abilities_english.txt")),
                    Is.EqualTo("resource/localization/abilities_english.txt"));
                Assert.That(Directory.GetFiles(_protectedDir, "*", SearchOption.AllDirectories), Is.Empty);
            });
        }

        [Test]
        public void MovePackageText_NothingToMove_IsNoOp()
        {
            WriteAsset("models/heroes/x/x.vmdl_c");

            Assert.That(ProtectedVpkStore.MovePackageText(_extractDir, _protectedDir), Is.Zero);
        }

        #endregion

        #region Deploy / Clear

        [Test]
        public async Task GetActiveModVpkStamp_ChangesWhenTheProtectedPackageGoesAway()
        {
            string mainVpk = ProtectedVpkStore.MainVpkPath(_targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(mainVpk)!);
            File.Copy(NewVpk(Path.Combine(_root, "main.vpk")), mainVpk);
            await ProtectedVpkStore.DeployAsync(_targetPath, NewVpk(Path.Combine(_root, "prot.vpk")), _ => { });

            var withProtected = ProtectedVpkStore.GetActiveModVpkStamp(_targetPath);
            ProtectedVpkStore.Clear(_targetPath);
            var withoutProtected = ProtectedVpkStore.GetActiveModVpkStamp(_targetPath);

            Assert.Multiple(() =>
            {
                Assert.That(withProtected, Is.Not.Null);
                Assert.That(withoutProtected, Is.Not.Null, "the main package is still installed");
                Assert.That(withoutProtected, Is.Not.EqualTo(withProtected));
            });
        }

        [Test]
        public async Task DeployAsync_InstallsSuperhiddenPackage()
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
                Assert.That(File.ReadAllBytes(ProtectedVpk), Is.EqualTo(TestVpk.Minimal()));
            });
        }

        #region DeployPairAsync — protected + main as one unit

        private static readonly byte[] PreviousProtected = TestVpk.Build(TestVpk.Blob("models/old", "prev", "vmdl_c", 32));
        private const FileAttributes Superhidden = FileAttributes.Hidden | FileAttributes.System;

        private void InstallPreviousProtected()
        {
            Directory.CreateDirectory(ProtectedVpkStore.Dir(_targetPath));
            File.WriteAllBytes(ProtectedVpk, PreviousProtected);
            File.SetAttributes(ProtectedVpk, Superhidden);
        }

        private void AssertPreviousProtectedIsBack()
        {
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(ProtectedVpk), Is.EqualTo(PreviousProtected), "previous protected package restored");
                Assert.That(File.GetAttributes(ProtectedVpk) & Superhidden, Is.EqualTo(Superhidden), "still superhidden");
                Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False, ".bak settled");
            });
        }

        [Test]
        public async Task DeployPair_MainSucceeds_CommitsTheNewProtectedPackage()
        {
            InstallPreviousProtected();
            int mainCalls = 0;

            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, NewVpk(Path.Combine(_root, "n.vpk")),
                () => { mainCalls++; return Task.FromResult(true); }, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(mainCalls, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(ProtectedVpk), Is.EqualTo(TestVpk.Minimal()));
                Assert.That(File.GetAttributes(ProtectedVpk) & Superhidden, Is.EqualTo(Superhidden));
                Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False);
            });
        }

        [Test]
        public async Task DeployPair_MainFails_PutsThePreviousProtectedPackageBack()
        {
            InstallPreviousProtected();

            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, NewVpk(Path.Combine(_root, "n.vpk")),
                () => Task.FromResult(false), _ => { });

            Assert.That(ok, Is.False);
            AssertPreviousProtectedIsBack();
        }

        [Test]
        public void DeployPair_MainThrows_RethrowsAndPutsThePreviousProtectedPackageBack()
        {
            InstallPreviousProtected();

            Assert.That(() => ProtectedVpkStore.DeployPairAsync(_targetPath, NewVpk(Path.Combine(_root, "n.vpk")),
                    () => throw new IOException("disk full"), _ => { }),
                Throws.InstanceOf<IOException>());

            AssertPreviousProtectedIsBack();
        }

        [Test]
        public void DeployPair_MainCanceled_RethrowsAndPutsThePreviousProtectedPackageBack()
        {
            InstallPreviousProtected();

            Assert.That(() => ProtectedVpkStore.DeployPairAsync(_targetPath, NewVpk(Path.Combine(_root, "n.vpk")),
                    () => Task.FromCanceled<bool>(new CancellationToken(true)), _ => { }),
                Throws.InstanceOf<OperationCanceledException>());

            AssertPreviousProtectedIsBack();
        }

        [Test]
        public async Task DeployPair_MainFails_WithNoPreviousProtected_LeavesNone()
        {
            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, NewVpk(Path.Combine(_root, "n.vpk")),
                () => Task.FromResult(false), _ => { });

            Assert.That(ok, Is.False);
            Assert.That(File.Exists(ProtectedVpk), Is.False, "nothing was installed before, so nothing stays");
            Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False);
        }

        [Test]
        public async Task DeployPair_NullSourceAndMainFails_RestoresTheRemovedPackage()
        {
            InstallPreviousProtected();

            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, null, () => Task.FromResult(false), _ => { });

            Assert.That(ok, Is.False);
            AssertPreviousProtectedIsBack();
        }

        [Test]
        public async Task DeployPair_NullSourceAndMainSucceeds_RemovesThePackage()
        {
            InstallPreviousProtected();

            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, null, () => Task.FromResult(true), _ => { });

            Assert.That(ok, Is.True);
            Assert.That(File.Exists(ProtectedVpk), Is.False);
            Assert.That(File.Exists(ProtectedVpk + ".bak"), Is.False);
        }

        [Test]
        public async Task DeployPair_ProtectedRefused_NeverTouchesTheMainPackage()
        {
            InstallPreviousProtected();
            var corrupt = Path.Combine(_root, "corrupt.vpk");
            File.WriteAllBytes(corrupt, new byte[] { 1, 2, 3 });
            bool mainCalled = false;

            bool ok = await ProtectedVpkStore.DeployPairAsync(_targetPath, corrupt,
                () => { mainCalled = true; return Task.FromResult(true); }, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(mainCalled, Is.False, "main swap must not run after the protected one failed");
            });
            AssertPreviousProtectedIsBack();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("relative_dota")]
        public async Task DeployPair_EmptyOrRelativeTarget_ReturnsFalse_WithoutCallingMain(string? target)
        {
            bool mainCalled = false;

            bool ok = await ProtectedVpkStore.DeployPairAsync(target!, NewVpk(Path.Combine(_root, "n.vpk")),
                () => { mainCalled = true; return Task.FromResult(true); }, _ => { });

            Assert.That(ok, Is.False);
            Assert.That(mainCalled, Is.False);
        }

        #endregion

        [TestCase(null)]
        [TestCase("")]
        [TestCase("relative_dota")]
        public async Task DeployAsync_EmptyOrRelativeTarget_ReturnsFalseAndCreatesNothing(string? target)
        {
            var cwdGame = Path.Combine(Environment.CurrentDirectory, "relative_dota");
            var cwdMod = Path.Combine(Environment.CurrentDirectory, "game", "mod");
            foreach (var stale in new[] { cwdGame, cwdMod })
                if (Directory.Exists(stale)) { SafeDelete(stale); }

            bool ok = await ProtectedVpkStore.DeployAsync(target!, NewVpk(Path.Combine(_root, "rel.vpk")), _ => { });

            Assert.That(ok, Is.False);
            Assert.That(Directory.Exists(cwdGame) || Directory.Exists(cwdMod), Is.False,
                "Ensure must not run against the working directory");
        }

        private static void SafeDelete(string dir)
        {
            foreach (var p in Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories).Append(dir))
                try { File.SetAttributes(p, FileAttributes.Normal); } catch { }
            Directory.Delete(dir, true);
        }

        [Test]
        public async Task DeployAsync_NullSource_ClearsPackage()
        {
            await ProtectedVpkStore.DeployAsync(_targetPath, NewVpk(Path.Combine(_root, "p1.vpk")), _ => { });
            Assert.That(File.Exists(ProtectedVpk), Is.True, "precondition");

            bool ok = await ProtectedVpkStore.DeployAsync(_targetPath, null, _ => { });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(File.Exists(ProtectedVpk), Is.False, "mod package removed when no protected assets");
            });
        }

        [Test]
        public async Task Clear_RemovesSuperhiddenPackage_ButKeepsTheFolderSuperhidden()
        {
            string sampleProtVpk = NewVpk(Path.Combine(_root, "sample_prot.vpk"));
            await ProtectedVpkStore.DeployAsync(_targetPath, sampleProtVpk, _ => { });

            string tempDir = Path.Combine(_targetPath, "game", "_ArdysaMods", "_temp");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "hero_extraction_log.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "hero_selections.json"), "{}");

            string strayFile = Path.Combine(ProtectedVpkStore.Dir(_targetPath), "models", "stray.vmdl_c");
            Directory.CreateDirectory(Path.GetDirectoryName(strayFile)!);
            File.WriteAllText(strayFile, "stray");

            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.True);
            Assert.That(Directory.Exists(ProtectedVpkStore.Dir(_targetPath)), Is.True);

            ProtectedVpkStore.Clear(_targetPath);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.False, "game/mod/pak01_dir.vpk removed");
                Assert.That(Directory.Exists(ProtectedVpkStore.Dir(_targetPath)), Is.True, "game/mod directory kept");
                Assert.That(File.Exists(Path.Combine(tempDir, "hero_extraction_log.json")), Is.False, "extraction log removed");
                Assert.That(File.Exists(Path.Combine(tempDir, "hero_selections.json")), Is.False, "selections log removed");

                string modsDir = ProtectedVpkStore.Dir(_targetPath);
                var dirAttrs = new DirectoryInfo(modsDir).Attributes;
                Assert.That(dirAttrs.HasFlag(FileAttributes.Hidden), Is.True, "game/mod folder must stay superhidden");
                Assert.That(dirAttrs.HasFlag(FileAttributes.System), Is.True, "game/mod folder must stay superhidden");

                Assert.That(File.Exists(strayFile), Is.True, "a leftover file is not deleted by Clear()");
                var fileAttrs = File.GetAttributes(strayFile);
                Assert.That(fileAttrs.HasFlag(FileAttributes.Hidden), Is.True, "a leftover file must not sit exposed either");
                Assert.That(fileAttrs.HasFlag(FileAttributes.System), Is.True, "a leftover file must not sit exposed either");
            });
        }

        [Test]
        public void Clear_NoPackage_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ProtectedVpkStore.Clear(_targetPath));
        }

        #endregion

        [Test]
        public void DeletePermanently_WhenProtectedFolderAndContentsExist_RemovesDirectoryCompletely()
        {
            ProtectedVpkStore.Ensure(_targetPath);
            string modDir = ProtectedVpkStore.Dir(_targetPath);
            Assert.That(Directory.Exists(modDir), Is.True);

            string vpkPath = ProtectedVpkStore.VpkPath(_targetPath);
            File.WriteAllBytes(vpkPath, new byte[] { 1, 2, 3 });
            File.SetAttributes(vpkPath, FileAttributes.Hidden | FileAttributes.System);

            string subDir = Path.Combine(modDir, "sub");
            Directory.CreateDirectory(subDir);
            string subFile = Path.Combine(subDir, "test.txt");
            File.WriteAllText(subFile, "test");
            File.SetAttributes(subFile, FileAttributes.ReadOnly);

            bool deleted = ProtectedVpkStore.DeletePermanently(_targetPath);

            Assert.That(deleted, Is.True);
            Assert.That(Directory.Exists(modDir), Is.False);
        }

        [Test]
        public void DeletePermanently_NoFolder_ReturnsTrue()
        {
            Assert.That(ProtectedVpkStore.DeletePermanently(_targetPath), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("dota")]
        public void DeletePermanently_EmptyOrRelativePath_ReturnsFalseAndTouchesNothing(string? path)
        {
            string original = Environment.CurrentDirectory;
            string cwdModDir = Path.Combine(_root, "game", "mod");
            string rootedModDir = ProtectedVpkStore.Dir(_targetPath);
            Directory.CreateDirectory(cwdModDir);
            Directory.CreateDirectory(rootedModDir);
            try
            {
                Environment.CurrentDirectory = _root;

                Assert.That(ProtectedVpkStore.DeletePermanently(path!), Is.False);
                Assert.That(Directory.Exists(cwdModDir), Is.True, "must never resolve against the working directory");
                Assert.That(Directory.Exists(rootedModDir), Is.True);
            }
            finally
            {
                Environment.CurrentDirectory = original;
            }
        }

        [Test]
        public void DeletePermanently_WhenLocked_ReturnsFalseAndReHidesSurvivors()
        {
            ProtectedVpkStore.Ensure(_targetPath);
            string modDir = ProtectedVpkStore.Dir(_targetPath);
            string survivor = Path.Combine(modDir, "sub", "keep.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(survivor)!);
            File.WriteAllText(survivor, "x");
            File.WriteAllBytes(ProtectedVpk, new byte[] { 1 });

            bool deleted;
            using (new FileStream(ProtectedVpk, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                deleted = ProtectedVpkStore.DeletePermanently(_targetPath);

            var dirAttrs = File.GetAttributes(modDir);
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.False);
                Assert.That(File.Exists(ProtectedVpk), Is.True);
                Assert.That(dirAttrs.HasFlag(FileAttributes.Hidden), Is.True, "Normalize stripped it; a failed delete must restore it");
                Assert.That(dirAttrs.HasFlag(FileAttributes.System), Is.True, "Normalize stripped it; a failed delete must restore it");
                Assert.That(File.GetAttributes(ProtectedVpk).HasFlag(FileAttributes.Hidden), Is.True, "the surviving package must be re-hidden");
            });
        }
    }
}