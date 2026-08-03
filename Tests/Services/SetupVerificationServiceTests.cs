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
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SetupVerificationServiceTests
    {
        private string _root = null!;
        private SetupVerificationService _service = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AMT_VerifyTests_" + Guid.NewGuid().ToString("N"));
            _service = new SetupVerificationService();
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        #region Fabricated tree

        private const string MountingGameInfo =
            "\t\tSearchPaths\r\n\t\t{\r\n\t\t\tGame\t\t_ArdysaMods\r\n\t\t\tGame\t\tmod\r\n\t\t\tGame\t\tdota\r\n\t\t}\r\n";

        private string Write(string relativePath, string content)
        {
            var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        private static string Sha1OfFile(string path)
        {
            using var sha1 = SHA1.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha1.ComputeHash(fs));
        }

        private void BuildTree(string gameInfo = MountingGameInfo, string? recordedSha1 = null)
        {
            Write(DotaPaths.Dota2Exe, "exe");
            var gameInfoPath = Write(DotaPaths.GameInfo, gameInfo);
            string sha1 = recordedSha1 ?? Sha1OfFile(gameInfoPath);
            Write(DotaPaths.Signatures, SignaturesWithBothEntries(sha1));
        }

        private const string VanillaGameInfoSha1 = "224B6F879D00D419B6A10FE07B4DDB3D827FD31F";

        private static string SignaturesWithBothEntries(string appendedSha1) =>
            "SIGNATURES V2\r\n" +
            "...\\..\\..\\dota\\gameinfo_branchspecific.gi~SHA1:" + VanillaGameInfoSha1 + ";CRC:C42BAA0F\r\n" +
            "...\\..\\..\\dota\\pak01_dir.vpk~SHA1:" + new string('B', 40) + ";CRC:00000000\r\n" +
            "DIGEST:ABCDEF0123456789;\r\n" +
            "...\\..\\..\\dota\\gameinfo_branchspecific.gi~SHA1:" + appendedSha1 + ";CRC:41EFBC8A\r\n";

        private static SetupCheck Check(SetupVerificationResult r, SetupCheckId id) =>
            r.Checks.Single(c => c.Id == id);

        #endregion

        #region VerifyAsync — signature ↔ gameinfo

        [Test]
        public async Task VerifyAsync_GameInfoMatchesRecordedHash_Passes()
        {
            BuildTree();

            var result = await _service.VerifyAsync(_root);

            Assert.That(Check(result, SetupCheckId.SignatureMatchesGameInfo).State,
                Is.EqualTo(SetupCheckState.Pass));
        }

        [Test]
        public async Task VerifyAsync_GameInfoDoesNotMatchRecordedHash_Fails()
        {
            BuildTree(recordedSha1: new string('A', 40));

            var result = await _service.VerifyAsync(_root);
            var check = Check(result, SetupCheckId.SignatureMatchesGameInfo);

            Assert.Multiple(() =>
            {
                Assert.That(check.State, Is.EqualTo(SetupCheckState.Fail));
                Assert.That(check.FailStatus, Is.EqualTo(ModStatus.NeedUpdate),
                    "Patch Update rewrites both files, so this is an update state");
                Assert.That(check.Diagnostic, Does.Contain("AAAA"), "the diagnostic must name both hashes");
                Assert.That(result.AllPassed, Is.False);
                Assert.That(result.FirstFailure, Is.EqualTo(check));
            });
        }

        [Test]
        public async Task VerifyAsync_NoSignatureLine_IsUnknownNotFailed()
        {
            Write(DotaPaths.Dota2Exe, "exe");
            Write(DotaPaths.GameInfo, MountingGameInfo);
            Write(DotaPaths.Signatures, "SIGNATURES V2\nDIGEST:ABCDEF;\n");

            var result = await _service.VerifyAsync(_root);

            Assert.That(Check(result, SetupCheckId.SignatureMatchesGameInfo).State,
                Is.EqualTo(SetupCheckState.Unknown));
        }

        [Test]
        public async Task VerifyAsync_MissingFiles_AreUnknownNotFailed()
        {
            Write(DotaPaths.Dota2Exe, "exe");

            var result = await _service.VerifyAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(Check(result, SetupCheckId.SignatureMatchesGameInfo).State,
                    Is.EqualTo(SetupCheckState.Unknown));
                Assert.That(Check(result, SetupCheckId.SearchPathsMounted).State,
                    Is.EqualTo(SetupCheckState.Unknown));
                Assert.That(result.AllPassed, Is.True, "Unknown must never block the status pill");
            });
        }

        [Test]
        public async Task VerifyAsync_NoPath_ReturnsEmptySweep()
        {
            Assert.That((await _service.VerifyAsync(null)).Checks, Is.Empty);
            Assert.That((await _service.VerifyAsync("  ")).Checks, Is.Empty);
        }

        #endregion

        #region VerifyAsync — search paths

        [Test]
        public async Task VerifyAsync_BothSearchPathsMounted_Passes()
        {
            BuildTree();

            var result = await _service.VerifyAsync(_root);

            Assert.That(Check(result, SetupCheckId.SearchPathsMounted).State,
                Is.EqualTo(SetupCheckState.Pass));
        }

        [Test]
        public async Task VerifyAsync_ProtectedPathMissing_FailsAndNamesIt()
        {
            BuildTree(gameInfo: "\t\tSearchPaths\r\n\t\t{\r\n\t\t\tGame\t\t_ArdysaMods\r\n\t\t\tGame\t\tdota\r\n\t\t}\r\n");

            var check = Check(await _service.VerifyAsync(_root), SetupCheckId.SearchPathsMounted);

            Assert.Multiple(() =>
            {
                Assert.That(check.State, Is.EqualTo(SetupCheckState.Fail));
                Assert.That(check.Diagnostic, Does.Contain("mod"));
                Assert.That(check.Diagnostic, Does.Not.Contain("_ArdysaMods"),
                    "only the missing path should be named");
            });
        }

        [Test]
        public async Task VerifyAsync_UnpatchedGameInfo_FailsNamingBothPaths()
        {
            BuildTree(gameInfo: "\t\tSearchPaths\r\n\t\t{\r\n\t\t\tGame\t\tdota\r\n\t\t}\r\n");

            var check = Check(await _service.VerifyAsync(_root), SetupCheckId.SearchPathsMounted);

            Assert.Multiple(() =>
            {
                Assert.That(check.State, Is.EqualTo(SetupCheckState.Fail));
                Assert.That(check.Diagnostic, Does.Contain("_ArdysaMods"));
                Assert.That(check.Diagnostic, Does.Contain("mod"));
            });
        }

        #endregion

        #region Forced elevation

        [Test]
        public async Task VerifyAsync_NoCompatibilityFlag_Passes()
        {
            BuildTree();

            Assert.That(Check(await _service.VerifyAsync(_root), SetupCheckId.NotForcedToRunAsAdmin).State,
                Is.EqualTo(SetupCheckState.Pass));
        }

        [TestCase("~ RUNASADMIN", true)]
        [TestCase("RUNASADMIN", true)]
        [TestCase("~ HIGHDPIAWARE RUNASADMIN", true)]
        [TestCase("~ runasadmin", true)]
        [TestCase("~ HIGHDPIAWARE", false)]
        [TestCase("~", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasRunAsAdmin_RecognisesTheTokenWhateverItTravelsWith(string? data, bool expected)
        {
            Assert.That(SetupVerificationService.HasRunAsAdmin(data), Is.EqualTo(expected));
        }

        [Test]
        public void RemoveRunAsAdmin_KeepsEveryOtherLayer()
        {
            Assert.That(SetupVerificationService.RemoveRunAsAdmin("~ HIGHDPIAWARE RUNASADMIN"),
                Is.EqualTo("~ HIGHDPIAWARE"));
        }

        [Test]
        public void RemoveRunAsAdmin_NothingMeaningfulLeft_SignalsDeleteTheValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SetupVerificationService.RemoveRunAsAdmin("~ RUNASADMIN"), Is.Null);
                Assert.That(SetupVerificationService.RemoveRunAsAdmin("RUNASADMIN"), Is.Null);
                Assert.That(SetupVerificationService.RemoveRunAsAdmin(""), Is.Null);
            });
        }

        [Test]
        public async Task TryClearForcedAdminAsync_NothingFlagged_ReportsNoWorkAndNoError()
        {
            BuildTree();

            var (cleared, error) = await _service.TryClearForcedAdminAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(cleared, Is.Zero);
                Assert.That(error, Is.Null, "a clean machine is not an error");
            });
        }

        [Test]
        public async Task TryClearForcedAdminAsync_NoPath_ReportsAnError()
        {
            var (cleared, error) = await _service.TryClearForcedAdminAsync(null);

            Assert.Multiple(() =>
            {
                Assert.That(cleared, Is.Zero);
                Assert.That(error, Is.Not.Null);
            });
        }

        #endregion

        #region Signature line parsing

        [Test]
        public void ExtractRecordedGameInfoSha1_ReadsTheRealPatchLineShape()
        {
            const string line = "...\\..\\..\\dota\\gameinfo_branchspecific.gi~SHA1:" +
                                "162F5CF09FECCB510A3E13097F8045E5BC0B38F4;CRC:41EFBC8A";

            Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1("DIGEST:x;\n" + line),
                Is.EqualTo("162F5CF09FECCB510A3E13097F8045E5BC0B38F4"));
        }

        [Test]
        public void ExtractRecordedGameInfoSha1_PrefersTheEntryAppendedAfterDigest()
        {
            string text = SignaturesWithBothEntries(ModConstants.ModPatchSHA1);

            Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(text),
                Is.EqualTo(ModConstants.ModPatchSHA1));
        }

        [Test]
        public void ExtractRecordedGameInfoSha1_NothingAfterDigest_FallsBackToValvesOwnEntry()
        {
            string text =
                "SIGNATURES V2\r\n" +
                "...\\..\\..\\dota\\gameinfo_branchspecific.gi~SHA1:" + VanillaGameInfoSha1 + ";CRC:C42BAA0F\r\n" +
                "DIGEST:ABCDEF0123456789;\r\n";

            Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(text),
                Is.EqualTo(VanillaGameInfoSha1));
        }

        [Test]
        public void ExtractRecordedGameInfoSha1_MalformedOrAbsent_ReturnsNull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(null), Is.Null);
                Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1("DIGEST:x;"), Is.Null);
                Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(
                    "gameinfo_branchspecific.gi~SHA1:162F5CF0"), Is.Null);
                Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(
                    "gameinfo_branchspecific.gi~SHA1:" + new string('Z', 40)), Is.Null);
            });
        }

        [Test]
        public void ExtractRecordedGameInfoSha1_MatchesTheShippedConstant()
        {
            Assert.That(SetupVerificationService.ExtractRecordedGameInfoSha1(ModConstants.ModPatchLine),
                Is.EqualTo(ModConstants.ModPatchSHA1));
        }

        #endregion
    }
}
