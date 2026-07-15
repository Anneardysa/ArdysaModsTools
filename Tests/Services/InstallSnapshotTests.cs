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
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class InstallSnapshotTests
    {
        private string _dir = null!;
        private string _vpk = null!;
        private string _hash = null!;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), $"AMT_SnapshotTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dir);
            _vpk = Path.Combine(_dir, "pak01_dir.vpk");
            _hash = Path.Combine(_dir, "ModsPack.hash");
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch {  }
        }

        [Test]
        public void Dispose_WithoutCommit_RestoresPreviousInstall()
        {
            File.WriteAllText(_vpk, "old-vpk");
            File.WriteAllText(_hash, "old-hash");

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk))
            {
                File.WriteAllText(_vpk, "new-vpk");
                File.WriteAllText(_hash, "new-hash");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(_vpk), Is.EqualTo("old-vpk"));
                Assert.That(File.ReadAllText(_hash), Is.EqualTo("old-hash"));
                Assert.That(File.Exists(_vpk + ".bak"), Is.False);
                Assert.That(File.Exists(_hash + ".bak"), Is.False);
            });
        }

        [Test]
        public void Commit_KeepsNewInstall_AndDropsBackups()
        {
            File.WriteAllText(_vpk, "old-vpk");
            File.WriteAllText(_hash, "old-hash");

            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk))
            {
                File.WriteAllText(_vpk, "new-vpk");
                File.WriteAllText(_hash, "new-hash");
                snapshot.Commit();
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(_vpk), Is.EqualTo("new-vpk"));
                Assert.That(File.ReadAllText(_hash), Is.EqualTo("new-hash"));
                Assert.That(File.Exists(_vpk + ".bak"), Is.False);
                Assert.That(File.Exists(_hash + ".bak"), Is.False);
            });
        }

        [Test]
        public void Dispose_WithoutCommit_OnFreshInstall_ClearsTheSlot()
        {
            using (var snapshot = ModInstallerService.InstallSnapshot.Capture(_vpk))
            {
                File.WriteAllText(_vpk, "half-written-vpk");
                File.WriteAllText(_hash, "new-hash");
            }

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(_vpk), Is.False);
                Assert.That(File.Exists(_hash), Is.False);
            });
        }
    }
}
