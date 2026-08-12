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
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.Services.Update.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class DeltaUpdateServiceTests
    {
        private string _installDir = null!;

        [SetUp]
        public void Setup()
        {
            _installDir = Path.Combine(Path.GetTempPath(), $"AMT_Delta_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_installDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_installDir, true); } catch {  }
        }

        private AssetHashEntry WriteLocal(string relPath, string content)
        {
            string full = Path.Combine(_installDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return Entry(content);
        }

        private static AssetHashEntry Entry(string content) => new()
        {
            Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
            Size = Encoding.UTF8.GetByteCount(content)
        };

        private Task<DeltaPlan> BuildAsync(
            Dictionary<string, AssetHashEntry> manifest,
            Dictionary<string, AssetHashEntry>? oldManifest = null)
            => DeltaUpdateService.BuildPlanAsync(
                manifest, oldManifest, "9.9.9", _installDir, Path.Combine(_installDir, "staging"),
                "https://example.invalid/releases/9.9.9/files/");

        [Test]
        public void CanAutoUpdate_OnlyInstallerBuildsWithAPublishedManifest()
        {
            static UpdateInfo Info(string? manifest) =>
                new() { Version = "9.9.9", FilesManifestUrl = manifest };

            Assert.Multiple(() =>
            {
                Assert.That(DeltaUpdateService.CanAutoUpdate(
                    InstallationType.Installer, Info("https://cdn.invalid/releases/9.9.9/files.json")),
                    Is.True, "installer + manifest is the only auto-update case");

                Assert.That(DeltaUpdateService.CanAutoUpdate(
                    InstallationType.Portable, Info("https://cdn.invalid/releases/9.9.9/files.json")),
                    Is.False, "portable has no applier — it must keep the manual download links");

                Assert.That(DeltaUpdateService.CanAutoUpdate(InstallationType.Installer, Info(null)),
                    Is.False, "no per-file manifest published for this release");
                Assert.That(DeltaUpdateService.CanAutoUpdate(InstallationType.Installer, Info("   ")),
                    Is.False, "blank manifest URL is not a manifest");
                Assert.That(DeltaUpdateService.CanAutoUpdate(InstallationType.Installer, null),
                    Is.False, "no update info at all");
            });
        }

        [Test]
        public void FilesBaseUrl_AndSiblingManifestUrl_AreDerivedFromTheManifestUrl()
        {
            const string manifest = "https://cdn.ardysamods.my.id/releases/2.2.15-beta/files.json";

            Assert.Multiple(() =>
            {
                Assert.That(DeltaUpdateService.FilesBaseUrl(manifest),
                    Is.EqualTo("https://cdn.ardysamods.my.id/releases/2.2.15-beta/files/"));
                Assert.That(DeltaUpdateService.SiblingManifestUrl(manifest, "2.2.14-beta"),
                    Is.EqualTo("https://cdn.ardysamods.my.id/releases/2.2.14-beta/files.json"));
            });
        }

        [Test]
        public async Task BuildPlan_UnchangedFile_IsNotDownloaded()
        {
            var entry = WriteLocal("app.dll", "same-bytes");

            var plan = await BuildAsync(new() { ["app.dll"] = entry });

            Assert.That(plan.Files.Select(f => f.RelPath), Does.Not.Contain("app.dll"));
            Assert.That(plan.TotalDownloadBytes, Is.Zero);
        }

        [Test]
        public async Task BuildPlan_ChangedContent_IsDownloaded()
        {
            WriteLocal("app.dll", "old-bytes");
            var updated = Entry("new-bytes-longer");

            var plan = await BuildAsync(new() { ["app.dll"] = updated });

            Assert.That(plan.Files.Select(f => f.RelPath), Does.Contain("app.dll"));
            Assert.That(plan.Files.Single().Sha256, Is.EqualTo(updated.Sha256));
            Assert.That(plan.TotalDownloadBytes, Is.EqualTo(updated.Size));
        }

        [Test]
        public async Task BuildPlan_SameSizeDifferentContent_IsDownloaded()
        {
            WriteLocal("Assets/Html/x.html", "aaaa");
            var updated = Entry("bbbb");

            var plan = await BuildAsync(new() { ["Assets/Html/x.html"] = updated });

            Assert.That(plan.Files.Select(f => f.RelPath), Does.Contain("Assets/Html/x.html"));
        }

        [Test]
        public async Task BuildPlan_MissingFile_IsDownloaded()
        {
            var plan = await BuildAsync(new() { ["Assets/Locales/new.json"] = Entry("brand new") });

            Assert.That(plan.Files.Single().RelPath, Is.EqualTo("Assets/Locales/new.json"));
        }

        [Test]
        public async Task BuildPlan_Applier_IsAlwaysDownloadedEvenWhenUnchanged()
        {
            var entry = WriteLocal(DeltaUpdateService.UpdaterRelPath, "applier");

            var plan = await BuildAsync(new() { [DeltaUpdateService.UpdaterRelPath] = entry });

            Assert.That(plan.Files.Single().RelPath, Is.EqualTo(DeltaUpdateService.UpdaterRelPath));
        }

        [Test]
        public async Task BuildPlan_DroppedFile_IsDeleted()
        {
            var kept = WriteLocal("app.dll", "same");
            WriteLocal("Assets/Locales/id.json", "removed locale");

            var plan = await BuildAsync(
                manifest: new() { ["app.dll"] = kept },
                oldManifest: new() { ["app.dll"] = kept, ["Assets/Locales/id.json"] = Entry("removed locale") });

            Assert.That(plan.Deletions, Is.EqualTo(new[] { "Assets/Locales/id.json" }));
        }

        [Test]
        public async Task BuildPlan_WithoutOldManifest_DeletesNothing()
        {
            var kept = WriteLocal("app.dll", "same");
            WriteLocal("stale.dll", "left over from an ancient build");

            var plan = await BuildAsync(new() { ["app.dll"] = kept }, oldManifest: null);

            Assert.That(plan.Deletions, Is.Empty);
        }

        [Test]
        public async Task BuildPlan_LocalExtraFileNotInEitherManifest_IsLeftAlone()
        {
            var kept = WriteLocal("app.dll", "same");
            WriteLocal("user_notes.txt", "not ours to touch");

            var plan = await BuildAsync(
                manifest: new() { ["app.dll"] = kept },
                oldManifest: new() { ["app.dll"] = kept });

            Assert.That(plan.Files, Is.Empty);
            Assert.That(plan.Deletions, Is.Empty);
        }

        [Test]
        public void RepairInterruptedUpdate_RestoresBackupWhenTargetIsMissing()
        {
            File.WriteAllText(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension), "original");
            File.WriteAllText(Path.Combine(_installDir, DeltaUpdateService.InProgressMarker), "9.9.9");

            NewService().RepairInterruptedUpdate();

            Assert.That(File.ReadAllText(Path.Combine(_installDir, "app.dll")), Is.EqualTo("original"));
            Assert.That(File.Exists(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension)), Is.False);
            Assert.That(File.Exists(Path.Combine(_installDir, DeltaUpdateService.InProgressMarker)), Is.False);
        }

        [Test]
        public void RepairInterruptedUpdate_DropsBackupWhenTargetSurvived()
        {
            File.WriteAllText(Path.Combine(_installDir, "app.dll"), "new");
            File.WriteAllText(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension), "old");
            File.WriteAllText(Path.Combine(_installDir, "half.dll" + DeltaUpdateService.IncomingExtension), "partial");
            File.WriteAllText(Path.Combine(_installDir, DeltaUpdateService.InProgressMarker), "9.9.9");

            NewService().RepairInterruptedUpdate();

            Assert.That(File.ReadAllText(Path.Combine(_installDir, "app.dll")), Is.EqualTo("new"));
            Assert.That(File.Exists(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension)), Is.False);
            Assert.That(File.Exists(Path.Combine(_installDir, "half.dll" + DeltaUpdateService.IncomingExtension)), Is.False);
            Assert.That(File.Exists(Path.Combine(_installDir, DeltaUpdateService.InProgressMarker)), Is.False);
        }

        [Test]
        public void RepairInterruptedUpdate_WithoutMarker_SweepsLeftoverBackups()
        {
            File.WriteAllText(Path.Combine(_installDir, "app.dll"), "new");
            File.WriteAllText(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension), "old");
            File.WriteAllText(Path.Combine(_installDir, "dropped.dll" + DeltaUpdateService.BackupExtension), "deleted by the update");

            NewService().RepairInterruptedUpdate();

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(Path.Combine(_installDir, "app.dll")), Is.EqualTo("new"));
                Assert.That(File.Exists(Path.Combine(_installDir, "app.dll" + DeltaUpdateService.BackupExtension)), Is.False);
                Assert.That(File.Exists(Path.Combine(_installDir, "dropped.dll" + DeltaUpdateService.BackupExtension)), Is.False);
                Assert.That(File.Exists(Path.Combine(_installDir, "dropped.dll")), Is.False);
            });
        }

        [Test]
        public void RepairInterruptedUpdate_CleanInstall_DoesNothing()
        {
            File.WriteAllText(Path.Combine(_installDir, "app.dll"), "untouched");

            NewService().RepairInterruptedUpdate();

            Assert.That(File.ReadAllText(Path.Combine(_installDir, "app.dll")), Is.EqualTo("untouched"));
        }

        [Test]
        public void ReportLastApplyOutcome_FailedApply_LogsTheReasonOnceAndKeepsTheLog()
        {
            string stagingRoot = Path.Combine(_installDir, "stagingRoot");
            string logPath = Path.Combine(stagingRoot, "9.9.9", "update.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, new[]
            {
                "[09:10:59] Verified 30 staged file(s) for v9.9.9.",
                "[09:11:12] Apply failed: Could not install ArdysaModsTools.dll — rolling back.",
                "[09:11:13] FAILED: Update failed and was rolled back: Could not install ArdysaModsTools.dll",
            });

            var lines = new List<string>();
            var service = NewService(stagingRoot, lines.Add);

            service.ReportLastApplyOutcome();

            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Exactly(1).Contains("Could not install ArdysaModsTools.dll"));
                Assert.That(File.Exists(logPath), Is.False);
                Assert.That(File.Exists(logPath + ".previous"), Is.True);
            });

            lines.Clear();
            service.ReportLastApplyOutcome();
            Assert.That(lines, Is.Empty);

            var info = new UpdateInfo { Version = "9.9.9", FilesManifestUrl = "https://cdn.invalid/releases/9.9.9/files.json" };
            Assert.That(DeltaUpdateService.CanAutoUpdate(InstallationType.Installer, info, service), Is.False);
            Assert.That(service.HasLastApplyFailedForVersion("9.9.9"), Is.True);
        }

        [Test]
        public void CanAutoUpdate_SuppressedWhenPreviousApplyFailed()
        {
            string stagingRoot = Path.Combine(_installDir, "stagingRoot");
            string logPath = Path.Combine(stagingRoot, "2.2.22-beta", "update.log.previous");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, new[]
            {
                "[09:10:59] FAILED: Update failed and was rolled back: Could not install ArdysaModsTools.dll",
            });

            var service = NewService(stagingRoot, _ => { });
            var info = new UpdateInfo { Version = "2.2.22-beta", FilesManifestUrl = "https://cdn.invalid/releases/2.2.22-beta/files.json" };

            Assert.Multiple(() =>
            {
                Assert.That(service.HasLastApplyFailedForVersion("2.2.22-beta"), Is.True);
                Assert.That(DeltaUpdateService.CanAutoUpdate(InstallationType.Installer, info, service), Is.False);
            });
        }

        [Test]
        public void ReportLastApplyOutcome_SuccessfulApply_SaysNothing()
        {
            string stagingRoot = Path.Combine(_installDir, "stagingRoot");
            string logPath = Path.Combine(stagingRoot, "9.9.9", "update.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, new[]
            {
                "[09:10:59] OK: Updated to v9.9.9.",
                "[09:11:00] Relaunched C:\\App\\ArdysaModsTools.exe",
            });

            var lines = new List<string>();
            NewService(stagingRoot, lines.Add).ReportLastApplyOutcome();

            Assert.That(lines, Is.Empty);
        }

        private DeltaUpdateService NewService() =>
            new(new ArdysaModsTools.Core.Services.Logger((_, _) => { }), _installDir);

        private DeltaUpdateService NewService(string stagingRoot, Action<string> log) =>
            new(new ArdysaModsTools.Core.Services.Logger((message, _) => log(message)), _installDir, stagingRoot);
    }
}
