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
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class StatusServiceTests
    {
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;
        private StatusService _service = null!;
        private string _root = null!;

        [SetUp]
        public void Setup()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new StatusService(_logger);
            _root = Path.Combine(Path.GetTempPath(), "AMT_StatusTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            _testConsole?.Dispose();
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        #region Fabricated Dota tree helpers

        private static string PatchedSignatures =>
            "SIGNATURES V2\nDIGEST:ABCDEF0123456789;\n" + ModConstants.ModPatchLine + "\n";

        private void BuildDotaTree(
            bool withVpk = true,
            string? gameInfoContent = "Game _ArdysaMods\nGame dota",
            string? signaturesContent = null,
            string? version = "3.4")
        {
            WriteFile(DotaPaths.Dota2Exe, "exe");
            if (signaturesContent != null)
                WriteFile(DotaPaths.Signatures, signaturesContent);
            if (withVpk)
                WriteFile(DotaPaths.ModsVpk, "vpk");
            if (version != null && withVpk)
                WriteFile(DotaPaths.ModsVersion, version);
            if (gameInfoContent != null)
                WriteFile(DotaPaths.GameInfo, gameInfoContent);
        }

        private void WriteFile(string relativePath, string content)
        {
            var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        #endregion

        #region Constructor & guard tests

        [Test]
        public void Constructor_WithNullLogger_CreatesInstance()
        {
            Assert.That(new StatusService(null), Is.Not.Null);
        }

        [Test]
        public async Task GetDetailedStatusAsync_WithNullPath_ReturnsNotChecked()
        {
            var result = await _service.GetDetailedStatusAsync(null);

            Assert.That(result.Status, Is.EqualTo(ModStatus.NotChecked));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.pathNotSet.text"));
        }

        [Test]
        public async Task GetDetailedStatusAsync_WithEmptyPath_ReturnsNotChecked()
        {
            var result = await _service.GetDetailedStatusAsync("");

            Assert.That(result.Status, Is.EqualTo(ModStatus.NotChecked));
        }

        [Test]
        public async Task GetDetailedStatusAsync_WithNonExistentPath_ReturnsInvalidPathError()
        {
            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.invalidPath.text"));
        }

        #endregion

        #region Status-determination branch tests

        [Test]
        public async Task GetDetailedStatusAsync_SignaturesMissing_ReturnsCoreMissingError()
        {
            BuildDotaTree(signaturesContent: null);

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.coreMissing.text"));
        }

        [Test]
        public async Task GetDetailedStatusAsync_VpkMissing_ReturnsNotInstalledWithInstallAction()
        {
            BuildDotaTree(withVpk: false, signaturesContent: PatchedSignatures);

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.NotInstalled));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Install));
        }

        [Test]
        public async Task GetDetailedStatusAsync_GameInfoMissing_ReturnsDisabledWithEnableAction()
        {
            BuildDotaTree(gameInfoContent: null, signaturesContent: PatchedSignatures);

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Disabled));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Enable));
        }

        [Test]
        public async Task GetDetailedStatusAsync_GameInfoWithoutMarker_ReturnsDisabled()
        {
            BuildDotaTree(gameInfoContent: "Game dota\nGame core", signaturesContent: PatchedSignatures);

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Disabled));
        }

        [Test]
        public async Task GetDetailedStatusAsync_SignaturesWithoutDigest_ReturnsInvalidCoreError()
        {
            BuildDotaTree(signaturesContent: "SIGNATURES V2\nno digest line here\n");

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.invalidCore.text"));
        }

        [Test]
        public async Task GetDetailedStatusAsync_ExactPatchLinePresent_ReturnsReadyWithVersion()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Ready));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.None));
            Assert.That(result.Version, Is.EqualTo("3.4"));
        }

        [Test]
        public async Task GetDetailedStatusAsync_PatchLineAbsent_ReturnsNeedUpdateWithUpdateAction()
        {
            BuildDotaTree(signaturesContent: "SIGNATURES V2\nDIGEST:ABCDEF0123456789;\n");

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.NeedUpdate));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Update));
        }

        [Test]
        public async Task GetDetailedStatusAsync_Sha1WithWrongPathFormat_ReturnsInvalidPatchError()
        {
            BuildDotaTree(signaturesContent:
                "SIGNATURES V2\nDIGEST:ABCDEF0123456789;\n" +
                $"gameinfo_branchspecific.gi~SHA1:{ModConstants.ModPatchSHA1};CRC:043F604A\n");

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Fix));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.invalidPatch.text"));
        }

        #endregion
    }
}
