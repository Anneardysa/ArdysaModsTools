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
using Moq;
using NUnit.Framework;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ModInstallerServiceTests
    {
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;
        private ModInstallerService _service = null!;

        [SetUp]
        public void Setup()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new ModInstallerService(_logger);
        }

        [TearDown]
        public void TearDown()
        {
            _testConsole?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithLogger_CreatesInstance()
        {
            var service = new ModInstallerService(_logger);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_CreatesInstanceWithNullLogger()
        {
            var service = new ModInstallerService(null);
            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region ValidateVpkAsync Tests

        [Test]
        public async Task ValidateVpkAsync_WithNullPath_ReturnsInvalid()
        {
            var (isValid, errorMessage) = await _service.ValidateVpkAsync(null!);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateVpkAsync_WithEmptyPath_ReturnsInvalid()
        {
            var (isValid, errorMessage) = await _service.ValidateVpkAsync("");

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateVpkAsync_WithNonExistentFile_ReturnsInvalid()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.vpk");

            var (isValid, errorMessage) = await _service.ValidateVpkAsync(nonExistentPath);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Contains.Substring("not found"));
        }

        #endregion

        #region UpdatePatcherAsync Tests

        [Test]
        public async Task UpdatePatcherAsync_WithEmptyPath_ReturnsFailed()
        {
            var result = await _service.UpdatePatcherAsync("");

            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        [Test]
        public async Task UpdatePatcherAsync_WithNullPath_ReturnsFailed()
        {
            var result = await _service.UpdatePatcherAsync(null!);

            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        [Test]
        public async Task UpdatePatcherAsync_WithInvalidPath_ReturnsFailed()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var result = await _service.UpdatePatcherAsync(invalidPath);

            Assert.That(result, Is.EqualTo(PatchResult.Failed));
        }

        [Test]
        public async Task UpdatePatcherAsync_WithMalformedSignatures_FailsAndLeavesNoTempFiles()
        {
            var gameRoot = Path.Combine(Path.GetTempPath(), "amt_patch_" + Guid.NewGuid().ToString("N"));
            var win64 = Path.Combine(gameRoot, "game", "bin", "win64");
            Directory.CreateDirectory(win64);
            var signatures = Path.Combine(win64, "dota.signatures");
            await File.WriteAllTextAsync(signatures, "nothing useful here\n");

            try
            {
                var result = await _service.UpdatePatcherAsync(gameRoot);

                Assert.That(result, Is.EqualTo(PatchResult.Failed));
                Assert.That(Directory.GetFiles(gameRoot, "*.tmp", SearchOption.AllDirectories),
                    Is.Empty, "patch must not leave orphaned .tmp files when it fails");
                Assert.That(await File.ReadAllTextAsync(signatures), Is.EqualTo("nothing useful here\n"),
                    "a failed patch must not modify the original signatures file");
            }
            finally
            {
                try { Directory.Delete(gameRoot, true); } catch {  }
            }
        }

        #endregion

        #region GameInfo integrity

        [Test]
        public void ComputeSHA1Hex_ReturnsUppercaseHex_AndDetectsMismatch()
        {
            var actual = ModInstallerService.ComputeSHA1Hex(System.Text.Encoding.ASCII.GetBytes("abc"));

            Assert.That(actual, Is.EqualTo("A9993E364706816ABA3E25717850C26C9CD0D89D"));
            Assert.That(actual, Is.Not.EqualTo(ModInstallerService.ComputeSHA1Hex(
                System.Text.Encoding.ASCII.GetBytes("abd"))), "a changed payload must produce a different hash");
        }

        [Test]
        public void ModPatchSHA1_IsWellFormed_AndEmbeddedInThePatchLine()
        {
            Assert.That(ModConstants.ModPatchSHA1, Does.Match("^[0-9A-F]{40}$"));
            Assert.That(ModConstants.ModPatchLine, Does.Contain(ModConstants.ModPatchSHA1),
                "the signature line must pin the same hash the download is verified against");
        }

        #endregion

        #region DisableModsAsync Tests

        [Test]
        public async Task DisableModsAsync_WithEmptyPath_ReturnsTrue()
        {
            var result = await _service.DisableModsAsync("");

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task DisableModsAsync_WithInvalidPath_ReturnsTrue()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var result = await _service.DisableModsAsync(invalidPath);

            Assert.That(result, Is.True);
        }

        [Test]
        public void TrimAfterDigest_DropsThePatchLine_AndKeepsEverythingUpToDigest()
        {
            var lines = new[] { "HEADER", "DIGEST:abc123", ModConstants.ModPatchLine };

            var trimmed = ModInstallerService.TrimAfterDigest(lines);

            Assert.That(trimmed, Is.EqualTo(new[] { "HEADER", "DIGEST:abc123" }));
        }

        [Test]
        public void TrimAfterDigest_WithoutDigestLine_ReturnsNull()
        {
            Assert.That(ModInstallerService.TrimAfterDigest(new[] { "HEADER", "OTHER" }), Is.Null);
        }

        #endregion

        #region ReplaceAtomicAsync Tests

        [Test]
        public async Task ReplaceAtomicAsync_OverwritesExistingFile_AndLeavesNoTempBehind()
        {
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            try
            {
                var path = Path.Combine(dir, "dota.signatures");
                await File.WriteAllTextAsync(path, "old");

                await ModInstallerService.ReplaceAtomicAsync(path,
                    (tmp, ct) => File.WriteAllTextAsync(tmp, "new", ct), CancellationToken.None);

                Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("new"));
                Assert.That(File.Exists(path + ".tmp"), Is.False, "the temp file must not survive the swap");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task ReplaceAtomicAsync_CreatesFileWhenMissing()
        {
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            try
            {
                var path = Path.Combine(dir, "gameinfo_branchspecific.gi");

                await ModInstallerService.ReplaceAtomicAsync(path,
                    (tmp, ct) => File.WriteAllTextAsync(tmp, "clean", ct), CancellationToken.None);

                Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("clean"));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ReplaceAtomicAsync_WhenWriteFails_LeavesNoTempAndTheTargetUntouched()
        {
            var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            try
            {
                var path = Path.Combine(dir, "dota.signatures");
                File.WriteAllText(path, "original");

                Assert.ThrowsAsync<IOException>(async () =>
                    await ModInstallerService.ReplaceAtomicAsync(path, async (tmp, ct) =>
                    {
                        await File.WriteAllTextAsync(tmp, "partial", ct);
                        throw new IOException("disk full");
                    }, CancellationToken.None));

                Assert.That(File.Exists(path + ".tmp"), Is.False, "the temp file must be cleaned up on failure");
                Assert.That(File.ReadAllText(path), Is.EqualTo("original"), "a failed write must not touch the target");
            }
            finally { Directory.Delete(dir, true); }
        }

        #endregion
    }
}

