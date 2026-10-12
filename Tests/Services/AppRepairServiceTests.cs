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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.App;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AppRepairServiceTests
    {
        private string _root = null!;
        private AppRepairPaths _paths = null!;
        private AppRepairService _service = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AMT_RepairTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            _paths = new AppRepairPaths
            {
                WebView2UserDataFolder = Path.Combine(_root, "WebView2"),
                LocalAppDataDir = Path.Combine(_root, "LocalAppData"),
                RoamingAppDataDir = Path.Combine(_root, "RoamingAppData"),
                InstallDir = Path.Combine(_root, "Install"),
            };
            Directory.CreateDirectory(_paths.WebView2UserDataFolder);
            Directory.CreateDirectory(_paths.InstallDir);

            _service = new AppRepairService(_paths, probeWebView2Environment: () => Task.CompletedTask);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        private AppCheck Get(AppRepairReport report, AppCheckId id) =>
            report.Checks.Single(c => c.Id == id.ToString());

        [Test]
        public async Task Scan_NoLeftovers_LeftoversCheckPasses()
        {
            var report = await _service.ScanAsync();

            var check = Get(report, AppCheckId.WebView2Leftovers);
            Assert.That(check.State, Is.EqualTo("pass"));
            Assert.That(check.CanAutoFix, Is.False);
        }

        [Test]
        public async Task Scan_TwoCorruptFolders_LeftoversAdvisory_AndAutoFixable()
        {
            Directory.CreateDirectory(_paths.WebView2UserDataFolder + ".corrupt-1");
            Directory.CreateDirectory(_paths.WebView2UserDataFolder + ".corrupt-2");

            var report = await _service.ScanAsync();

            var check = Get(report, AppCheckId.WebView2Leftovers);
            Assert.That(check.State, Is.EqualTo("advisory"));
            Assert.That(check.CanAutoFix, Is.True);
        }

        [Test]
        public async Task Repair_LeftoverIds_DeletesFolders_AndRescanPasses()
        {
            Directory.CreateDirectory(_paths.WebView2UserDataFolder + ".corrupt-1");
            Directory.CreateDirectory(_paths.WebView2UserDataFolder + ".corrupt-2");

            var result = await _service.RepairAsync(new[] { nameof(AppCheckId.WebView2Leftovers) });

            Assert.That(Directory.GetDirectories(_root, "WebView2.corrupt-*"), Is.Empty);
            Assert.That(Get(result, AppCheckId.WebView2Leftovers).State, Is.EqualTo("pass"));
        }

        [Test]
        public void Repair_UnknownId_IsIgnored_AndDoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () => await _service.RepairAsync(new[] { "NotARealCheckId" }));
        }

        [Test]
        public async Task Scan_UnwritableAppDataPath_ReportsFail_NotUnknown()
        {
            string blockedByFile = Path.Combine(_root, "blocked-local-appdata");
            File.WriteAllText(blockedByFile, "not a directory");
            var paths = _paths with { LocalAppDataDir = blockedByFile };
            var service = new AppRepairService(paths, probeWebView2Environment: () => Task.CompletedTask);

            var report = await service.ScanAsync();

            var check = Get(report, AppCheckId.AppDataWritable);
            Assert.That(check.State, Is.EqualTo("fail"));
        }

        [Test]
        public void ApplyPendingRepairs_WithoutSentinel_ReturnsFalse()
        {
            bool result = AppRepairService.ApplyPendingRepairs(_paths, () => true);

            Assert.That(result, Is.False);
        }

        [Test]
        public void ApplyPendingRepairs_ResetFails_SentinelKept_ForRetryNextLaunch()
        {
            Directory.CreateDirectory(_paths.LocalAppDataDir);
            string sentinel = Path.Combine(_paths.LocalAppDataDir, AppRepairService.SentinelFileName);
            File.WriteAllText(sentinel, "x");

            bool result = AppRepairService.ApplyPendingRepairs(_paths, () => false);

            Assert.That(result, Is.False, "a failed reset must not be reported as applied");
            Assert.That(File.Exists(sentinel), Is.True, "the sentinel must survive so the next launch retries");
        }

        [Test]
        public void ApplyPendingRepairs_WithSentinel_ConsumesItOnce()
        {
            Directory.CreateDirectory(_paths.LocalAppDataDir);
            File.WriteAllText(Path.Combine(_paths.LocalAppDataDir, AppRepairService.SentinelFileName), "x");

            int resetCalls = 0;
            bool first = AppRepairService.ApplyPendingRepairs(_paths, () => { resetCalls++; return true; });
            bool second = AppRepairService.ApplyPendingRepairs(_paths, () => { resetCalls++; return true; });

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(resetCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task Scan_WebView2ProfileWritable_EnvironmentProbeSucceeds_ReportsPass()
        {
            var service = new AppRepairService(_paths, probeWebView2Environment: () => Task.CompletedTask);

            var report = await service.ScanAsync();

            var check = Get(report, AppCheckId.WebView2Profile);
            Assert.That(check.State, Is.EqualTo("pass"));
            Assert.That(check.CanAutoFix, Is.False);
        }

        [Test]
        public async Task Scan_WebView2ProfileWritable_EnvironmentProbeThrows_ReportsFail_WithDiagnostic()
        {
            var service = new AppRepairService(_paths,
                probeWebView2Environment: () => throw new InvalidOperationException("profile corrupted"));

            var report = await service.ScanAsync();

            var check = Get(report, AppCheckId.WebView2Profile);
            Assert.That(check.State, Is.EqualTo("fail"));
            Assert.That(check.Diagnostic, Does.Contain("profile corrupted"));
            Assert.That(check.CanAutoFix, Is.True);
            Assert.That(check.AppliesOnRestart, Is.True);
        }

        [Test]
        public async Task Scan_WebView2RuntimeCheck_AlwaysProducesARow_AndNeverThrows()
        {
            AppRepairReport? report = null;
            Assert.DoesNotThrowAsync(async () => report = await _service.ScanAsync());

            Assert.That(report, Is.Not.Null);
            var check = Get(report!, AppCheckId.WebView2Runtime);
            Assert.That(check.State, Is.AnyOf("pass", "advisory", "fail"));
        }
    }
}
