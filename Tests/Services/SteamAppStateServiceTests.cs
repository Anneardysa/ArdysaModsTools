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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SteamAppStateServiceTests
    {
        private string _steamapps = null!;
        private string _dotaRoot = null!;
        private SteamAppStateService _service = null!;

        [SetUp]
        public void Setup()
        {
            _steamapps = Path.Combine(Path.GetTempPath(), "AMT_SteamTests_" + Guid.NewGuid().ToString("N"), "steamapps");
            _dotaRoot = Path.Combine(_steamapps, "common", "dota 2 beta");
            Directory.CreateDirectory(_dotaRoot);
            _service = new SteamAppStateService();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                var root = Directory.GetParent(_steamapps)?.FullName;
                if (root != null && Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch { }
        }

        private void WriteManifest(long stateFlags, long toDownload = 0, long downloaded = 0, string extra = "") =>
            File.WriteAllText(Path.Combine(_steamapps, "appmanifest_570.acf"), $@"""AppState""
{{
	""appid""		""570""
	""name""		""Dota 2""
	""StateFlags""		""{stateFlags}""
	""installdir""		""dota 2 beta""
	""BytesToDownload""		""{toDownload}""
	""BytesDownloaded""		""{downloaded}""
{extra}
	""InstalledDepots""
	{{
		""373301""
		{{
			""manifest""		""1234""
			""size""		""999""
		}}
	}}
}}");

        [Test]
        public void Read_OnTargetBuildWithStaleBytes_IsNotPending()
        {
            WriteManifest(4, toDownload: 5000, downloaded: 0,
                extra: "\t\"buildid\"\t\t\"777\"\n\t\"TargetBuildID\"\t\t\"777\"");

            var state = _service.Read(_dotaRoot);

            Assert.Multiple(() =>
            {
                Assert.That(state.IsOnTargetBuild, Is.True);
                Assert.That(state.IsUpdatePending, Is.False);
                Assert.That(state.IsSettled, Is.True);
            });
        }

        [Test]
        public void Read_StaleBytesTowardsANewBuild_StillPending()
        {
            WriteManifest(4, toDownload: 5000, downloaded: 0,
                extra: "\t\"buildid\"\t\t\"777\"\n\t\"TargetBuildID\"\t\t\"778\"");

            Assert.That(_service.Read(_dotaRoot).IsUpdatePending, Is.True);
        }

        [Test]
        public void Read_ParsesBuildIdsResultStagingAndSchedule()
        {
            WriteManifest(1030, toDownload: 10, downloaded: 10, extra:
                "\t\"buildid\"\t\t\"111\"\n\t\"TargetBuildID\"\t\t\"222\"\n\t\"UpdateResult\"\t\t\"12\"\n" +
                "\t\"BytesToStage\"\t\t\"400\"\n\t\"BytesStaged\"\t\t\"100\"\n" +
                "\t\"ScheduledAutoUpdate\"\t\t\"1700000000\"\n\t\"AutoUpdateBehavior\"\t\t\"1\"");

            var s = _service.Read(_dotaRoot);

            Assert.Multiple(() =>
            {
                Assert.That(s.BuildId, Is.EqualTo(111), "buildid must not pick up TargetBuildID");
                Assert.That(s.TargetBuildId, Is.EqualTo(222));
                Assert.That(s.UpdateResult, Is.EqualTo(12));
                Assert.That(s.BytesToStage, Is.EqualTo(400));
                Assert.That(s.BytesStaged, Is.EqualTo(100));
                Assert.That(s.ScheduledAutoUpdate, Is.EqualTo(1700000000));
                Assert.That(s.AutoUpdateBehavior, Is.EqualTo(1));
                Assert.That(s.IsUpdatePending, Is.True);
            });
        }

        [Test]
        public void Read_PausedUpdate_IsPausedAndPending()
        {
            WriteManifest(1542, toDownload: 1000, downloaded: 300);

            var s = _service.Read(_dotaRoot);

            Assert.That(s.IsPaused, Is.True);
            Assert.That(s.IsUpdatePending, Is.True);
        }

        [Test]
        public void UpdatePercent_SwitchesToStagingOnceDownloadCompletes()
        {
            var downloading = new SteamAppState { ManifestFound = true, StateFlags = 1030,
                BytesToDownload = 1000, BytesDownloaded = 500, BytesToStage = 2000 };
            var staging = downloading with { BytesDownloaded = 1000, BytesStaged = 500 };
            var done = staging with { BytesStaged = 2000 };

            Assert.Multiple(() =>
            {
                Assert.That(downloading.UpdatePercent, Is.EqualTo(50));
                Assert.That(staging.UpdatePercent, Is.EqualTo(25));
                Assert.That(done.UpdatePercent, Is.EqualTo(100));
                Assert.That(new SteamAppState { ManifestFound = true, StateFlags = 6 }.UpdatePercent, Is.Null);
            });
        }

        [Test]
        public void Read_FullyInstalled_IsSettledAndNotPending()
        {
            WriteManifest(4);

            var state = _service.Read(_dotaRoot);

            Assert.Multiple(() =>
            {
                Assert.That(state.ManifestFound, Is.True);
                Assert.That(state.IsSettled, Is.True);
                Assert.That(state.IsUpdatePending, Is.False);
            });
        }

        [Test]
        public void Read_UpdateQueued_IsPending()
        {
            WriteManifest(6);

            var state = _service.Read(_dotaRoot);

            Assert.That(state.IsUpdatePending, Is.True);
            Assert.That(state.IsSettled, Is.False);
        }

        [Test]
        public void Read_UpdateRunning_IsPendingAndReportsProgress()
        {
            WriteManifest(1042, toDownload: 1000, downloaded: 250);

            var state = _service.Read(_dotaRoot);

            Assert.Multiple(() =>
            {
                Assert.That(state.IsUpdatePending, Is.True);
                Assert.That(state.UpdatePercent, Is.EqualTo(25));
            });
        }

        [Test]
        public void Read_FullyInstalledButBytesOutstanding_IsPending()
        {
            WriteManifest(4, toDownload: 5000, downloaded: 100);

            Assert.That(_service.Read(_dotaRoot).IsUpdatePending, Is.True);
        }

        [Test]
        public void Read_MissingManifest_IsNeitherSettledNorPending()
        {
            var state = _service.Read(_dotaRoot);

            Assert.Multiple(() =>
            {
                Assert.That(state.ManifestFound, Is.False);
                Assert.That(state.IsSettled, Is.False);
                Assert.That(state.IsUpdatePending, Is.False, "an unreadable manifest must never block a launch");
            });
        }

        [Test]
        public void Read_MalformedManifest_DoesNotThrowAndDoesNotBlock()
        {
            File.WriteAllText(Path.Combine(_steamapps, "appmanifest_570.acf"), "this is not a manifest {{{");

            SteamAppState state = null!;
            Assert.DoesNotThrow(() => state = _service.Read(_dotaRoot));
            Assert.That(state.IsUpdatePending, Is.False);
        }

        [Test]
        public void Read_NoPath_IsUnknown()
        {
            Assert.That(_service.Read(null).ManifestFound, Is.False);
            Assert.That(_service.Read("   ").ManifestFound, Is.False);
        }

        [Test]
        public void ResolveManifestPath_ForACopiedFolder_ReturnsNull()
        {
            var loose = Path.Combine(Path.GetTempPath(), "AMT_Loose_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(loose);
            try
            {
                Assert.That(SteamAppStateService.ResolveManifestPath(loose), Is.Null);
            }
            finally
            {
                try { Directory.Delete(loose, true); } catch { }
            }
        }

        [Test]
        public void Read_LocksNothing_SoSteamCanKeepWriting()
        {
            WriteManifest(4);
            string manifest = Path.Combine(_steamapps, "appmanifest_570.acf");

            _service.Read(_dotaRoot);

            Assert.DoesNotThrow(() =>
            {
                using var fs = new FileStream(manifest, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            });
        }

        [Test]
        public void Read_UserConfigLanguage_IsTheGameLanguage()
        {
            WriteManifest(4, extra: "\t\"UserConfig\"\n\t{\n\t\t\"language\"\t\t\"russian\"\n\t}\n\t\"MountedConfig\"\n\t{\n\t\t\"language\"\t\t\"english\"\n\t}");

            Assert.That(_service.Read(_dotaRoot).Language, Is.EqualTo("russian"));
        }

        [Test]
        public void Read_NoLanguage_IsNull()
        {
            WriteManifest(4);

            Assert.That(_service.Read(_dotaRoot).Language, Is.Null);
        }

        [TestCase("../../x")]
        [TestCase("english\\\" -e \\\"x")]
        [TestCase("")]
        public void Read_LanguageThatIsNotAPlainWord_IsNull(string value)
        {
            WriteManifest(4, extra: $"\t\"UserConfig\"\n\t{{\n\t\t\"language\"\t\t\"{value}\"\n\t}}");

            Assert.That(_service.Read(_dotaRoot).Language, Is.Null);
        }
    }
}
