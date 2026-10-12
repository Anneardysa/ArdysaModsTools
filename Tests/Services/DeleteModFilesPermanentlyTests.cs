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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [NonParallelizable]
    public class DeleteModFilesPermanentlyTests
    {
        private const string VanillaGameInfo = "\"GameInfo\"\r\n{\r\n\tFileSystem\r\n\t{\r\n\t\tSearchPaths\r\n\t\t{\r\n\t\t\tGame\t\tdota\r\n\t\t\tGame\t\tcore\r\n\t\t}\r\n\t}\r\n}\r\n";

        private string _root = null!;
        private string _dota = null!;
        private string _originalCwd = null!;
        private ModInstallerService _service = null!;

        private string ModsDir => Path.Combine(_dota, "game", "_ArdysaMods");
        private string ProtectedDir => ProtectedVpkStore.Dir(_dota);
        private string ProtectedVpk => ProtectedVpkStore.VpkPath(_dota);

        [SetUp]
        public void Setup()
        {
            _originalCwd = Environment.CurrentDirectory;
            _root = Path.Combine(Path.GetTempPath(), "AmtDeletePermanentlyTests_" + Guid.NewGuid().ToString("N"));
            _dota = Path.Combine(_root, "dota 2 beta");
            _service = new ModInstallerService(null);

            Write(DotaPaths.Signatures, "signatures");
            Write(DotaPaths.GameInfo, VanillaGameInfo);

            Write(DotaPaths.ModsVpk, "main");
            string baseline = Write(DotaPaths.ItemsGameBaseline, "{}");
            File.SetAttributes(baseline, FileAttributes.ReadOnly);

            ProtectedVpkStore.Ensure(_dota);
            File.WriteAllText(ProtectedVpk, "protected");
            File.SetAttributes(ProtectedVpk, FileAttributes.Hidden | FileAttributes.System);
            string sub = Path.Combine(ProtectedDir, "models");
            Directory.CreateDirectory(sub);
            string subFile = Path.Combine(sub, "asset.vmdl_c");
            File.WriteAllText(subFile, "asset");
            File.SetAttributes(subFile, FileAttributes.ReadOnly | FileAttributes.Hidden);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.CurrentDirectory = _originalCwd;
            try
            {
                if (Directory.Exists(_root))
                {
                    ProtectedVpkStore.NormalizeAttributesRecursively(_root);
                    Directory.Delete(_root, true);
                }
            }
            catch {  }
        }

        private string Write(string relative, string content)
        {
            string full = Path.Combine(_dota, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        private void AssertBothFoldersIntact()
        {
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(_dota, DotaPaths.ModsVpk)), Is.True, "main package must be untouched");
                Assert.That(File.Exists(ProtectedVpk), Is.True, "protected package must be untouched");
            });
        }


        [Test]
        public async Task DisabledInstall_DeletesBothFolders_IncludingHiddenAndReadOnlyFiles()
        {
            bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(Directory.Exists(ModsDir), Is.False, "game/_ArdysaMods must be gone");
                Assert.That(Directory.Exists(ProtectedDir), Is.False, "game/mod must be gone");
                Assert.That(File.Exists(Path.Combine(_dota, DotaPaths.Signatures)), Is.True, "game files must survive");
                Assert.That(File.Exists(Path.Combine(_dota, DotaPaths.GameInfo)), Is.True, "game files must survive");
            });
        }

        [Test]
        public async Task NothingLeftToDelete_ReturnsTrue()
        {
            ProtectedVpkStore.NormalizeAttributesRecursively(ModsDir);
            ProtectedVpkStore.NormalizeAttributesRecursively(ProtectedDir);
            Directory.Delete(ModsDir, true);
            Directory.Delete(ProtectedDir, true);

            Assert.That(await _service.DeleteModFilesPermanentlyAsync(_dota), Is.True);
        }

        [Test]
        public async Task OnlyProtectedFolderLeft_DeletesIt()
        {
            ProtectedVpkStore.NormalizeAttributesRecursively(ModsDir);
            Directory.Delete(ModsDir, true);

            bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);

            Assert.That(ok, Is.True);
            Assert.That(Directory.Exists(ProtectedDir), Is.False);
        }


        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("dota 2 beta")]
        [TestCase(@"dota 2 beta\game")]
        public async Task EmptyOrRelativePath_ReturnsFalse_AndNeverResolvesAgainstTheWorkingDirectory(string? path)
        {
            Environment.CurrentDirectory = _root;

            bool ok = await _service.DeleteModFilesPermanentlyAsync(path!);

            Assert.That(ok, Is.False);
            AssertBothFoldersIntact();
        }


        [Test]
        public async Task NoDotaSignatures_ReturnsFalse_AndDeletesNothing()
        {
            File.Delete(Path.Combine(_dota, DotaPaths.Signatures));

            bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);

            Assert.That(ok, Is.False);
            AssertBothFoldersIntact();
        }

        [TestCase("\t\t\tGame\t\t_ArdysaMods\r\n", TestName = "StillMounted_ArdysaMods_Crlf")]
        [TestCase("\t\t\tGame\t\tmod\n", TestName = "StillMounted_ProtectedMod_Lf")]
        [TestCase("\t\t\tMod\t\t\"_ArdysaMods\"\r\n", TestName = "StillMounted_QuotedModEntry")]
        public async Task GameInfoStillMountsModFolders_ReturnsFalse_AndDeletesNothing(string mountLine)
        {
            Write(DotaPaths.GameInfo, VanillaGameInfo.Replace("\t\t\tGame\t\tdota\r\n", mountLine + "\t\t\tGame\t\tdota\r\n"));

            bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);

            Assert.That(ok, Is.False, "mods are still live — deleting their folders is not a disable");
            AssertBothFoldersIntact();
        }

        [Test]
        public async Task GameInfoMissing_CountsAsDisabled_AndDeletes()
        {
            File.Delete(Path.Combine(_dota, DotaPaths.GameInfo));

            Assert.That(await _service.DeleteModFilesPermanentlyAsync(_dota), Is.True);
            Assert.That(Directory.Exists(ProtectedDir), Is.False);
        }


        [Test]
        public async Task ProtectedPackageLockedThroughout_ReturnsFalse_AndReHidesTheFolder()
        {
            File.SetAttributes(ProtectedVpk, FileAttributes.Normal);
            using (new FileStream(ProtectedVpk, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);

                Assert.That(ok, Is.False, "the protected package is still on disk");
            }

            var attrs = File.GetAttributes(ProtectedDir);
            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(ProtectedDir), Is.True);
                Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.True, "a surviving game/mod must not sit exposed");
                Assert.That(attrs.HasFlag(FileAttributes.System), Is.True, "a surviving game/mod must not sit exposed");
                Assert.That(Directory.Exists(ModsDir), Is.False, "the unlocked folder is still deleted");
            });
        }

        [Test]
        public async Task MainPackageLockedThroughout_ReturnsFalse_ButStillDeletesProtectedFolder()
        {
            string mainVpk = Path.Combine(_dota, DotaPaths.ModsVpk);
            using (new FileStream(mainVpk, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(await _service.DeleteModFilesPermanentlyAsync(_dota), Is.False);
            }

            Assert.That(Directory.Exists(ProtectedDir), Is.False);
        }

        [Test]
        public async Task TransientLock_ReleasedBeforeRetry_Succeeds()
        {
            File.SetAttributes(ProtectedVpk, FileAttributes.Normal);
            var handle = new FileStream(ProtectedVpk, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var release = Task.Run(async () => { await Task.Delay(100); handle.Dispose(); });

            bool ok = await _service.DeleteModFilesPermanentlyAsync(_dota);
            await release;

            Assert.That(ok, Is.True, "an antivirus-style momentary lock must not fail the delete");
            Assert.That(Directory.Exists(ProtectedDir), Is.False);
        }


        [Test]
        public void AlreadyCancelled_Throws_AndDeletesNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                () => _service.DeleteModFilesPermanentlyAsync(_dota, cts.Token));
            AssertBothFoldersIntact();
        }
    }
}
