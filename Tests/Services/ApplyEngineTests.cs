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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArdysaModsTools.Updater;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ApplyEngineTests
    {
        private string _root = null!;
        private string _targetDir = null!;
        private string _stagingDir = null!;
        private string _filesDir = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), $"AMT_Apply_{Guid.NewGuid():N}");
            _targetDir = Path.Combine(_root, "install");
            _stagingDir = Path.Combine(_root, "staging");
            _filesDir = Path.Combine(_stagingDir, "files");

            Directory.CreateDirectory(_targetDir);
            Directory.CreateDirectory(_filesDir);

            File.WriteAllText(Path.Combine(_targetDir, ApplyEngine.ExeName), "old exe");
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, true); } catch {  }
        }


        private ApplyFile Stage(string relPath, string content, string? forgedSha = null)
        {
            string full = Path.Combine(_filesDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);

            return new ApplyFile
            {
                RelPath = relPath,
                Sha256 = forgedSha ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                Size = Encoding.UTF8.GetByteCount(content)
            };
        }

        private void Install(string relPath, string content)
        {
            string full = Path.Combine(_targetDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        private void WritePlan(ApplyFile[] files, string[]? deletions = null, bool stagedOk = true, string? targetDir = null)
        {
            var plan = new ApplyPlan
            {
                Version = "9.9.9",
                TargetDir = targetDir ?? _targetDir,
                StagingDir = _stagingDir,
                Files = files,
                Deletions = deletions ?? []
            };

            File.WriteAllText(
                Path.Combine(_stagingDir, ApplyEngine.PlanFileName),
                JsonSerializer.Serialize(plan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            if (stagedOk)
                File.WriteAllText(Path.Combine(_stagingDir, ApplyEngine.StagedOkMarker), "9.9.9");
        }

        private ApplyResult Run(bool dryRun = false) =>
            ApplyEngine.Run(_stagingDir, waitPid: 0, log: _ => { }, dryRun: dryRun);

        private string Target(string relPath) =>
            Path.Combine(_targetDir, relPath.Replace('/', Path.DirectorySeparatorChar));

        private bool AnyStraysLeft() =>
            Directory.EnumerateFiles(_targetDir, "*", SearchOption.AllDirectories)
                .Any(f => f.EndsWith(ApplyEngine.BackupExtension) || f.EndsWith(ApplyEngine.IncomingExtension));


        [Test]
        public void Run_ReplacesAddsAndDeletes_LeavingNoStrayFiles()
        {
            Install("app.dll", "old");
            Install("Assets/Locales/id.json", "dropped in the new release");

            WritePlan(
                files:
                [
                    Stage("app.dll", "new"),
                    Stage("Assets/Html/new_page.html", "brand new file"),
                    Stage(ApplyEngine.ExeName, "new exe")
                ],
                deletions: ["Assets/Locales/id.json"]);

            var result = Run();

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(File.ReadAllText(Target("app.dll")), Is.EqualTo("new"));
                Assert.That(File.ReadAllText(Target(ApplyEngine.ExeName)), Is.EqualTo("new exe"));
                Assert.That(File.ReadAllText(Target("Assets/Html/new_page.html")), Is.EqualTo("brand new file"));
                Assert.That(File.Exists(Target("Assets/Locales/id.json")), Is.False);
                Assert.That(AnyStraysLeft(), Is.False, "no .amtbak/.amtnew may survive a successful apply");
                Assert.That(File.Exists(Path.Combine(_targetDir, ApplyEngine.MarkerName)), Is.False);
            });
        }


        [Test]
        public void Run_StagedFileWithWrongHash_IsRejectedBeforeAnythingIsTouched()
        {
            Install("app.dll", "old");
            WritePlan([Stage("app.dll", "tampered", forgedSha: new string('A', 64))]);

            var result = Run();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("verification"));
            Assert.That(File.ReadAllText(Target("app.dll")), Is.EqualTo("old"), "the install must be untouched");
            Assert.That(File.Exists(Path.Combine(_targetDir, ApplyEngine.MarkerName)), Is.False);
        }

        [Test]
        public void Run_WithoutStagedOkMarker_Refuses()
        {
            Install("app.dll", "old");
            WritePlan([Stage("app.dll", "new")], stagedOk: false);

            var result = Run();

            Assert.That(result.Success, Is.False);
            Assert.That(File.ReadAllText(Target("app.dll")), Is.EqualTo("old"));
        }

        [Test]
        public void Run_TargetDirWithoutTheApp_Refuses()
        {
            string stranger = Path.Combine(_root, "not-an-amt-install");
            Directory.CreateDirectory(stranger);
            File.WriteAllText(Path.Combine(stranger, "important.txt"), "someone else's data");

            WritePlan([Stage("app.dll", "new")], targetDir: stranger);

            var result = Run();

            Assert.That(result.Success, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(stranger, "important.txt")), Is.EqualTo("someone else's data"));
        }

        [TestCase("../../escape.dll")]
        [TestCase("C:/Windows/System32/evil.dll")]
        public void Run_PathEscapingTheInstallFolder_IsRejected(string relPath)
        {
            var forged = Stage("harmless.dll", "payload");
            forged.RelPath = relPath;
            WritePlan([forged]);

            var result = Run();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("unsafe"));
        }


        [Test]
        public void Run_WhenOneFileIsLocked_RollsEverythingBack()
        {
            Install("first.dll", "first-old");
            Install("locked.dll", "locked-old");

            WritePlan([Stage("first.dll", "first-new"), Stage("locked.dll", "locked-new")]);

            using (var _ = new FileStream(Target("locked.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var result = Run();
                Assert.That(result.Success, Is.False, "a locked file must fail the apply, not half-apply it");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(Target("first.dll")), Is.EqualTo("first-old"), "rolled back");
                Assert.That(File.ReadAllText(Target("locked.dll")), Is.EqualTo("locked-old"), "never changed");
                Assert.That(File.Exists(Path.Combine(_targetDir, ApplyEngine.MarkerName)), Is.False);
            });
        }

        [Test]
        public void Run_WhenApplyFails_NewFilesFromThisUpdateAreRemoved()
        {
            Install("locked.dll", "locked-old");
            WritePlan([Stage("Assets/Html/added.html", "added"), Stage("locked.dll", "locked-new")]);

            using (var _ = new FileStream(Target("locked.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Run();
            }

            Assert.That(File.Exists(Target("Assets/Html/added.html")), Is.False);
            Assert.That(AnyStraysLeft(), Is.False);
        }


        [Test]
        public void Run_DryRun_VerifiesButChangesNothing()
        {
            Install("app.dll", "old");
            WritePlan([Stage("app.dll", "new")]);

            var result = Run(dryRun: true);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(File.ReadAllText(Target("app.dll")), Is.EqualTo("old"));
            Assert.That(File.Exists(Path.Combine(_targetDir, ApplyEngine.MarkerName)), Is.False);
        }

        [Test]
        public void Run_WithNoPlanFile_FailsCleanly()
        {
            var result = Run();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Invalid update plan"));
        }


        [Test]
        public void Run_WhenTheAppNeverExits_AbortsWithoutTouchingAnythingAndWithoutRelaunching()
        {
            Install("app.dll", "old");
            WritePlan([Stage("app.dll", "new")]);

            var result = ApplyEngine.Run(
                _stagingDir, waitPid: Environment.ProcessId, log: _ => { }, dryRun: false, waitTimeoutMs: 300);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("still running"));
                Assert.That(File.ReadAllText(Target("app.dll")), Is.EqualTo("old"), "nothing may be swapped");
                Assert.That(result.RelaunchPath, Is.Null);
                Assert.That(File.Exists(Path.Combine(_targetDir, ApplyEngine.MarkerName)), Is.False);
            });
        }
    }
}
