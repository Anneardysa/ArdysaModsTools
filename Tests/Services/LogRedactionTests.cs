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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [NonParallelizable]
    public class LogRedactionTests
    {
        private string _root = null!;
        private string _targetPath = null!;
        private string _logFile = null!;
        private string _originalLogFile = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtLogRedactionTests_" + Guid.NewGuid().ToString("N"));
            _targetPath = Path.Combine(_root, "dota 2 beta");
            Directory.CreateDirectory(_targetPath);

            _originalLogFile = FallbackLogger.LogFilePath;
            _logFile = Path.Combine(_root, "ardysa_fallback.log");
            FallbackLogger.SetLogFile(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            FallbackLogger.SetLogFile(_originalLogFile);
            try
            {
                var dir = ProtectedVpkStore.Dir(_targetPath);
                if (Directory.Exists(dir)) File.SetAttributes(dir, FileAttributes.Normal);
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch {  }
        }

        private string ReadLog() => File.Exists(_logFile) ? File.ReadAllText(_logFile) : string.Empty;

        private static void AssertClean(string where, string text)
        {
            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Not.Contain(@"game\mod").IgnoreCase, $"{where}: leaks the protected path");
                Assert.That(text, Does.Not.Contain("game/mod").IgnoreCase, $"{where}: leaks the protected path");
                Assert.That(text, Does.Not.Contain(@"game\\mod").IgnoreCase, $"{where}: leaks the protected path (escaped)");
                Assert.That(text, Does.Not.Contain("ProtectedVpkStore").IgnoreCase, $"{where}: names the mechanism");
                Assert.That(text, Does.Not.Contain("protected package").IgnoreCase, $"{where}: names the mechanism");
            });
        }


        [Test]
        public void DeletingTheProtectedFolder_NamesItNeitherOnTheConsoleNorInTheLogFile()
        {
            var console = new List<string>();
            var logger = new Logger((msg, _) => console.Add(msg));

            var dir = ProtectedVpkStore.Dir(_targetPath);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "pak01_dir.vpk"), "payload");

            ProtectedVpkStore.DeletePermanently(_targetPath, logger);

            Assert.That(Directory.Exists(dir), Is.False, "precondition: the folder really was deleted");
            AssertClean("console", string.Join("\n", console));
            AssertClean("fallback log", ReadLog());
        }


        [TestCase(@"ProtectedVpkStore: permanently deleted C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta\game\mod",
                  "permanently deleted", TestName = "Redact_TheReportedConsoleLine")]
        [TestCase(@"VpkReplacerService: rename-aside failed, C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta\game\mod\pak01_dir.vpk is locked.",
                  "is locked", TestName = "Redact_AbsolutePathWithTrailingFile")]
        [TestCase("Deploying to C:/Steam/steamapps/common/dota 2 beta/game/mod/pak01_dir.vpk now",
                  "Deploying to", TestName = "Redact_ForwardSlashes")]
        [TestCase(@"Access to the path 'D:\Games\dota 2 beta\game\mod\pak01_dir.vpk' is denied.",
                  "is denied", TestName = "Redact_InsideAnIoExceptionMessage")]
        [TestCase(@"moved 12 file(s) into game\mod\models",
                  "moved 12 file(s)", TestName = "Redact_RelativePath")]
        [TestCase(@"{""error"":""could not write C:\\Steam\\dota 2 beta\\game\\mod\\pak01_dir.vpk""}",
                  "could not write", TestName = "Redact_JsonEscapedPath")]
        [TestCase("ProtectedVpkStore.DeletePermanently failed: access denied",
                  "failed: access denied", TestName = "Redact_TheClassName")]
        [TestCase(@"Protected split: 3 file(s) moved out of the main package into game/mod.",
                  "3 file(s) moved", TestName = "Redact_TheMechanismWording")]
        public void Apply_RemovesTheProtectedPackage_ButKeepsTheSentence(string leaky, string survives)
        {
            var redacted = LogRedaction.Apply(leaky);

            AssertClean("redacted line", redacted);
            Assert.That(redacted, Does.Contain(survives),
                $"redaction ate the rest of the message: '{redacted}'");
        }

        [Test]
        public void Apply_IsIdempotent_BecauseTheFallbackHopLogsTheSameLineTwice()
        {
            const string leaky = @"ProtectedVpkStore: permanently deleted C:\Steam\dota 2 beta\game\mod";

            var once = LogRedaction.Apply(leaky);
            Assert.That(LogRedaction.Apply(once), Is.EqualTo(once));
        }

        [Test]
        public void Apply_OverRedactsTwoAdjacentPaths_OnPurpose()
        {
            var redacted = LogRedaction.Apply(@"copy C:\temp\a.txt to D:\dota 2 beta\game\mod");

            AssertClean("redacted line", redacted);
            Assert.That(redacted, Does.Not.Contain("a.txt"),
                "the over-redaction this test documents has changed — re-check the regex comment");
        }


        [Test]
        public void EveryLoggerConsolePath_IsRedacted()
        {
            const string leaky = @"ProtectedVpkStore: cannot write C:\Steam\dota 2 beta\game\mod\pak01_dir.vpk";

            var console = new List<string>();
            var i18n = new List<string>();
            var logger = new Logger((msg, _) => console.Add(msg), (json, _) => i18n.Add(json));

            logger.Log(leaky);
            logger.Log(leaky, LogLevel.Info);
            logger.Log(leaky, LogLevel.Warning);
            logger.Log(leaky, LogLevel.Error);
            logger.LogError("deploy failed", new IOException(leaky));
            logger.LogWarning(leaky);
            logger.LogCategorized(leaky, "error");
            logger.LogLocalized("error", LogSegment.Text("[GEN] "), LogSegment.T("log.gen.failed", new { error = leaky }));

            Assert.That(console, Is.Not.Empty, "precondition: the console sink was actually used");
            Assert.That(i18n, Is.Not.Empty, "precondition: the i18n sink was actually used");
            AssertClean("console sink", string.Join("\n", console));
            AssertClean("i18n sink", string.Join("\n", i18n));
        }


        [Test]
        public void EveryDiskLoggerPath_IsRedacted()
        {
            const string leaky = @"ProtectedVpkStore: cannot write C:\Steam\dota 2 beta\game\mod\pak01_dir.vpk";

            FallbackLogger.Log(leaky);
            FallbackLogger.LogFileOnly(leaky);
            FileAppLogger.Instance.Log(leaky);
            FileAppLogger.Instance.LogDebug(leaky);
            FileAppLogger.Instance.LogError("deploy failed", new IOException(leaky));
            new Logger((_, _) => { }).Log(leaky, LogLevel.Debug);

            var log = ReadLog();
            Assert.That(log, Does.Contain("cannot write"), "precondition: the lines really were written");
            AssertClean("fallback log", log);
        }


        [Test]
        public void TheInstallFailureCard_DoesNotNameTheMechanism()
        {
            InstallReport.Begin();
            InstallReport.Fail("Could not build the protected package.");
            InstallReport.Step(@"Writing C:\Steam\dota 2 beta\game\mod\pak01_dir.vpk");

            var text = string.Join("\n", InstallReport.Snapshot().Select(l => l.Text));

            Assert.That(text, Does.Contain("Could not build"), "precondition: the lines were recorded");
            AssertClean("install report", text);
        }

        [Test]
        public void TheCopyableGenerationLog_DoesNotNameTheMechanism()
        {
            var report = new GenerationReport(_ => { });

            report.Log(@"ProtectedVpkStore.Split: moved 12 file(s) into game\mod");
            report.Skip("Abaddon", @"could not write D:\dota 2 beta\game\mod\pak01_dir.vpk");

            var text = string.Join("\n", report.Lines);

            Assert.That(text, Does.Contain("moved 12 file(s)"), "precondition: the lines were recorded");
            AssertClean("generation report", text);
        }

        [Test]
        public void AnExceptionInterpolatedIntoALocalizedString_IsRedacted()
        {
            var loc = new LocalizationService();

            var text = loc.T("mods.error.generic",
                new { error = @"Access to the path 'D:\dota 2 beta\game\mod\pak01_dir.vpk' is denied." });

            Assert.That(text, Does.Contain("is denied"), "precondition: the value really was spliced in");
            AssertClean("localized error", text);
        }
    }
}
