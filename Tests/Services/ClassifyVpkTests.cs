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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ClassifyVpkTests
    {
        private const string HiddenMarker = "materials/dev/deferred_light_cache.vtex_c";
        private const string LegacyMarker = "version/_ardysamods";
        private const string ItemsGame = "scripts/items/items_game.txt";

        private string _tempRoot = null!;

        [SetUp]
        public void Setup()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"ClassifyVpkTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempRoot, true); } catch {  }
        }

        [Test]
        public async Task HiddenMarker_Slim_ClassifiesOfficialNeedsRebuild()
        {
            string vpk = ForgeVpk(HiddenMarker);
            var svc = new ModInstallerService();
            Assert.That(await svc.ClassifyVpkAsync(vpk), Is.EqualTo((VpkOrigin.Official, true)));
        }

        [Test]
        public async Task HiddenMarker_SelfContained_ClassifiesOfficialNoRebuild()
        {
            string vpk = ForgeVpk(HiddenMarker, ItemsGame);
            var svc = new ModInstallerService();
            Assert.That(await svc.ClassifyVpkAsync(vpk), Is.EqualTo((VpkOrigin.Official, false)));
        }

        [Test]
        public async Task LegacyMarkerOnly_ClassifiesUnofficial()
        {
            string vpk = ForgeVpk(LegacyMarker);
            var svc = new ModInstallerService();
            Assert.That((await svc.ClassifyVpkAsync(vpk)).Origin, Is.EqualTo(VpkOrigin.Unofficial));
        }

        [Test]
        public async Task NoMarker_ClassifiesUnofficial()
        {
            string vpk = ForgeVpk((string?)null);
            var svc = new ModInstallerService();
            Assert.That((await svc.ClassifyVpkAsync(vpk)).Origin, Is.EqualTo(VpkOrigin.Unofficial));
        }

        [Test]
        public async Task MarkerLookalikeName_ClassifiesUnofficial()
        {
            string vpk = ForgeVpk(HiddenMarker + ".txt");
            var svc = new ModInstallerService();
            Assert.That((await svc.ClassifyVpkAsync(vpk)).Origin, Is.EqualTo(VpkOrigin.Unofficial));
        }

        [Test]
        public async Task GarbageFile_ClassifiesUnreadable()
        {
            string p = Path.Combine(_tempRoot, "garbage_pak01_dir.vpk");
            await File.WriteAllTextAsync(p, "not a vpk");
            var svc = new ModInstallerService();
            Assert.That((await svc.ClassifyVpkAsync(p)).Origin, Is.EqualTo(VpkOrigin.Unreadable));
        }

        private string ForgeVpk(params string?[] relPaths)
        {
            string vpkTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            if (!File.Exists(vpkTool))
                Assert.Ignore("vpk.exe not present in test output.");

            string packDir = Path.Combine(_tempRoot, "pak01_dir");
            Directory.CreateDirectory(packDir);
            File.WriteAllText(Path.Combine(packDir, "readme.txt"), "filler");
            foreach (var rel in relPaths)
            {
                if (rel == null)
                    continue;
                string full = Path.Combine(packDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, "x");
            }

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = vpkTool,
                Arguments = $"\"{packDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            proc.WaitForExit(30000);

            string vpk = Path.Combine(_tempRoot, "pak01_dir.vpk");
            Assert.That(File.Exists(vpk), "vpk.exe did not produce a package");
            return vpk;
        }
    }
}
