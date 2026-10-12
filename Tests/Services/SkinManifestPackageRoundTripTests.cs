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
using System.Diagnostics;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Hero;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SkinManifestPackageRoundTripTests
    {
        private string _tempRoot = null!;

        [SetUp]
        public void Setup()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"SkinManifestRT_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempRoot, true); } catch {  }
        }

        [Test]
        public async Task ManifestSurvivesPackAndIsReadBackFromTheInstalledPackage()
        {
            string vpkTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            string hlExtract = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(vpkTool) || !File.Exists(hlExtract))
                Assert.Ignore("vpk.exe / HLExtract.exe not present in test output.");

            string packDir = Path.Combine(_tempRoot, "pak01_dir");
            Directory.CreateDirectory(packDir);
            File.WriteAllText(Path.Combine(packDir, "readme.txt"), "filler");

            var manifest = new SkinManifest
            {
                GeneratedAtUtc = "2026-08-28T10:11:12Z",
                Build = "2.4.4.2380",
                Entries =
                {
                    new SkinManifestEntry { HeroId = "juggernaut", SetName = "Bladeform Legacy", Layer = "set", Sha256 = "AABBCC" },
                    new SkinManifestEntry { HeroId = "pudge", SetName = "item_hook", Layer = "item", Sha256 = "DDEEFF" },
                }
            };
            await SkinManifestStore.WriteToTreeAsync(packDir, manifest);

            using (var proc = Process.Start(new ProcessStartInfo
            {
                FileName = vpkTool,
                Arguments = $"\"{packDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })!)
            {
                proc.WaitForExit(30000);
            }

            string built = Path.Combine(_tempRoot, "pak01_dir.vpk");
            Assert.That(File.Exists(built), "vpk.exe did not produce a package");

            string installed = Path.Combine(_tempRoot, "game", "_ArdysaMods", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.Move(built, installed);

            var reader = new SkinManifestReader(cachePath: Path.Combine(_tempRoot, "skinstate.amtx"));
            var read = await reader.ReadInstalledAsync(_tempRoot);

            Assert.That(read, Is.Not.Null, "manifest was not found inside the packed package");
            Assert.That(read!.Build, Is.EqualTo("2.4.4.2380"));
            Assert.That(read.Entries, Has.Count.EqualTo(2));
            Assert.That(read.Entries[0].HeroId, Is.EqualTo("juggernaut"));
            Assert.That(read.Entries[0].SetName, Is.EqualTo("Bladeform Legacy"));
            Assert.That(read.Entries[1].Layer, Is.EqualTo("item"));
            Assert.That(read.Entries[1].Sha256, Is.EqualTo("DDEEFF"));
        }

        [Test]
        public async Task PackageWithoutAManifestReadsBackAsNothingInstalled()
        {
            string vpkTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            string hlExtract = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(vpkTool) || !File.Exists(hlExtract))
                Assert.Ignore("vpk.exe / HLExtract.exe not present in test output.");

            string packDir = Path.Combine(_tempRoot, "pak01_dir");
            Directory.CreateDirectory(packDir);
            File.WriteAllText(Path.Combine(packDir, "readme.txt"), "filler");

            using (var proc = Process.Start(new ProcessStartInfo
            {
                FileName = vpkTool,
                Arguments = $"\"{packDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })!)
            {
                proc.WaitForExit(30000);
            }

            string installed = Path.Combine(_tempRoot, "game", "_ArdysaMods", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.Move(Path.Combine(_tempRoot, "pak01_dir.vpk"), installed);

            var reader = new SkinManifestReader(cachePath: Path.Combine(_tempRoot, "skinstate.amtx"));

            Assert.That(await reader.ReadInstalledAsync(_tempRoot), Is.Null);
        }

        [Test]
        public async Task MissingPackageReadsBackAsNothingInstalled()
        {
            var reader = new SkinManifestReader(cachePath: Path.Combine(_tempRoot, "skinstate.amtx"));

            Assert.That(await reader.ReadInstalledAsync(_tempRoot), Is.Null);
        }
    }
}
