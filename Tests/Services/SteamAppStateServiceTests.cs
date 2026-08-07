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

        private void WriteManifest(long stateFlags, long toDownload = 0, long downloaded = 0) =>
            File.WriteAllText(Path.Combine(_steamapps, "appmanifest_570.acf"), $@"""AppState""
{{
	""appid""		""570""
	""name""		""Dota 2""
	""StateFlags""		""{stateFlags}""
	""installdir""		""dota 2 beta""
	""BytesToDownload""		""{toDownload}""
	""BytesDownloaded""		""{downloaded}""
}}");

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
                Assert.That(state.DownloadPercent, Is.EqualTo(25));
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
    }
}
