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
            _service = new StatusService(_logger, new StubVerification(SetupVerificationResult.Empty));
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

        private sealed class StubVerification : ArdysaModsTools.Core.Interfaces.ISetupVerificationService
        {
            private readonly SetupVerificationResult _result;
            public StubVerification(SetupVerificationResult result) => _result = result;

            public Task<SetupVerificationResult> VerifyAsync(string? targetPath, CancellationToken ct = default)
                => Task.FromResult(_result);

            public Task<(int cleared, string? error)> TryClearForcedAdminAsync(string? targetPath, CancellationToken ct = default)
                => Task.FromResult<(int, string?)>((0, null));
        }

        private static SetupVerificationResult SweepWith(SetupCheckId id, ModStatus failStatus) =>
            new()
            {
                Checks = new[]
                {
                    new SetupCheck
                    {
                        Id = id,
                        State = SetupCheckState.Fail,
                        DetailKey = "verify.signature.fail",
                        Diagnostic = "test failure",
                        FailStatus = failStatus
                    }
                }
            };

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
        public async Task GetDetailedStatusAsync_SetupCheckFails_DoesNotReportReady()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var service = new StatusService(_logger,
                new StubVerification(SweepWith(SetupCheckId.SignatureMatchesGameInfo, ModStatus.NeedUpdate)));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ModStatus.NeedUpdate));
                Assert.That(result.Action, Is.EqualTo(RecommendedAction.Update));
                Assert.That(result.SetupFailure, Is.EqualTo(SetupCheckId.SignatureMatchesGameInfo));
                Assert.That(result.DescriptionKey, Is.EqualTo("verify.signature.fail"));
                Assert.That(result.ErrorMessage, Is.EqualTo("test failure"));
            });
        }

        [Test]
        public async Task GetDetailedStatusAsync_ForcedAdminFails_ReportsErrorTaggedAsSetupFailure()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var service = new StatusService(_logger,
                new StubVerification(SweepWith(SetupCheckId.NotForcedToRunAsAdmin, ModStatus.Error)));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
                Assert.That(result.Action, Is.EqualTo(RecommendedAction.Fix));
                Assert.That(result.SetupFailure, Is.EqualTo(SetupCheckId.NotForcedToRunAsAdmin));
            });
        }

        [Test]
        public async Task GetDetailedStatusAsync_ElevationDetected_NamesItselfInsteadOfReportingReady()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var service = new StatusService(_logger, new StubVerification(ElevationDetectedSweep()));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ModStatus.NeedUpdate), "amber, not the red of a broken install");
                Assert.That(result.StatusTextKey, Is.EqualTo("verify.chip.admin"));
                Assert.That(result.DescriptionKey, Is.EqualTo("status.elevation.desc"));
                Assert.That(result.Action, Is.EqualTo(RecommendedAction.Fix),
                    "Patch Update rewrites correct files and changes nothing here");
                Assert.That(result.SetupFailure, Is.Null, "an advisory is not a failed check");
            });
        }

        [Test]
        public async Task GetDetailedStatusAsync_ElevationClean_StillReportsReady()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var sweep = new SetupVerificationResult
            {
                Checks = new[]
                {
                    new SetupCheck
                    {
                        Id = SetupCheckId.NotForcedToRunAsAdmin,
                        State = SetupCheckState.Pass,
                        DetailKey = "verify.admin.clean",
                        HasOwnDialog = true
                    }
                }
            };
            var service = new StatusService(_logger, new StubVerification(sweep));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Ready));
        }

        [Test]
        public async Task GetDetailedStatusAsync_RealFailureOutranksTheElevationAdvisory()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var sweep = new SetupVerificationResult
            {
                Checks = ElevationDetectedSweep().Checks
                    .Concat(SweepWith(SetupCheckId.SearchPathsMounted, ModStatus.NeedUpdate).Checks)
                    .ToArray()
            };
            var service = new StatusService(_logger, new StubVerification(sweep));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.That(result.SetupFailure, Is.EqualTo(SetupCheckId.SearchPathsMounted));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Update));
        }

        [Test]
        public async Task GetDetailedStatusAsync_StalePackage_BlocksReady_AndRecommendsPlayNotPatchUpdate()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var sweep = new SetupVerificationResult
            {
                Checks = new[]
                {
                    new SetupCheck
                    {
                        Id = SetupCheckId.ItemsGameInSync,
                        State = SetupCheckState.Fail,
                        DetailKey = "verify.sync.fail",
                        Diagnostic = "hash mismatch",
                        FailStatus = ModStatus.NeedUpdate,
                        FailAction = RecommendedAction.Play
                    }
                }
            };
            var service = new StatusService(_logger, new StubVerification(sweep));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ModStatus.NeedUpdate), "amber — the install is intact, the package is old");
                Assert.That(result.Action, Is.EqualTo(RecommendedAction.Play));
                Assert.That(result.SetupFailure, Is.EqualTo(SetupCheckId.ItemsGameInSync));
                Assert.That(result.DescriptionKey, Is.EqualTo("verify.sync.fail"));
            });
        }

        [Test]
        public async Task GetDetailedStatusAsync_UnknownPackageSync_DoesNotBlockReady()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var sweep = new SetupVerificationResult
            {
                Checks = new[]
                {
                    new SetupCheck
                    {
                        Id = SetupCheckId.ItemsGameInSync,
                        State = SetupCheckState.Unknown,
                        DetailKey = "verify.sync.unknown"
                    }
                }
            };
            var service = new StatusService(_logger, new StubVerification(sweep));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.That(result.SetupFailure, Is.Null);
            Assert.That(result.Status, Is.Not.EqualTo(ModStatus.Error));
        }

        [Test]
        public async Task GetDetailedStatusAsync_NullFailAction_PreservesTheOriginalActionMapping()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);

            var needUpdate = await new StatusService(_logger,
                new StubVerification(SweepWith(SetupCheckId.SignatureMatchesGameInfo, ModStatus.NeedUpdate)))
                .GetDetailedStatusAsync(_root);

            var error = await new StatusService(_logger,
                new StubVerification(SweepWith(SetupCheckId.NotForcedToRunAsAdmin, ModStatus.Error)))
                .GetDetailedStatusAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(needUpdate.Action, Is.EqualTo(RecommendedAction.Update));
                Assert.That(error.Action, Is.EqualTo(RecommendedAction.Fix));
            });
        }

        private static SetupVerificationResult ElevationDetectedSweep() =>
            new()
            {
                Checks = new[]
                {
                    new SetupCheck
                    {
                        Id = SetupCheckId.NotForcedToRunAsAdmin,
                        State = SetupCheckState.Advisory,
                        DetailKey = "verify.admin.detected",
                        DetailVars = new { apps = "steam.exe" },
                        Diagnostic = "steam.exe (pid 1) is running elevated",
                        HasOwnDialog = true
                    }
                }
            };

        [Test]
        public async Task GetDetailedStatusAsync_SweepIsExposedForTheUi()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var sweep = SweepWith(SetupCheckId.SearchPathsMounted, ModStatus.NeedUpdate);
            var service = new StatusService(_logger, new StubVerification(sweep));

            var result = await service.GetDetailedStatusAsync(_root);

            Assert.That(result.Verification.Checks, Has.Count.EqualTo(1),
                "the shell chips read the sweep off the status result rather than re-running the probes");
        }

        [Test]
        public async Task GetDetailedStatusAsync_NoPathAttached_ClearsThePreviousSweep()
        {
            BuildDotaTree(signaturesContent: PatchedSignatures);
            var service = new StatusService(_logger,
                new StubVerification(SweepWith(SetupCheckId.SearchPathsMounted, ModStatus.NeedUpdate)));
            await service.GetDetailedStatusAsync(_root);

            var result = await service.GetDetailedStatusAsync(null);

            Assert.That(result.Verification.Checks, Is.Empty,
                "stale chips must not linger after the folder is detached");
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
                ModConstants.ModPatchLine.Substring(ModConstants.ModPatchLine.IndexOf("gameinfo_", StringComparison.Ordinal)) + "\n");

            var result = await _service.GetDetailedStatusAsync(_root);

            Assert.That(result.Status, Is.EqualTo(ModStatus.Error));
            Assert.That(result.Action, Is.EqualTo(RecommendedAction.Fix));
            Assert.That(result.StatusTextKey, Is.EqualTo("status.invalidPatch.text"));
        }

        #endregion
    }
}
