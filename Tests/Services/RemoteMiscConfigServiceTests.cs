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
using System.IO;
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class RemoteMiscConfigServiceTests
    {
        private string? _backupPath;

        // The service writes to a fixed %LocalAppData% path. Preserve any real cache the
        // developer machine may have so the suite never destroys live state, then restore it.
        [SetUp]
        public void SetUp()
        {
            var path = RemoteMiscConfigService.CacheFilePath;
            if (File.Exists(path))
            {
                _backupPath = path + ".testbak";
                File.Copy(path, _backupPath, overwrite: true);
                File.Delete(path);
            }
            else
            {
                _backupPath = null;
            }

            RemoteMiscConfigService.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            var path = RemoteMiscConfigService.CacheFilePath;
            if (File.Exists(path))
                File.Delete(path);

            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Move(_backupPath, path, overwrite: true);
                _backupPath = null;
            }

            RemoteMiscConfigService.InvalidateCache();
        }

        #region DeleteCache

        [Test]
        public void DeleteCache_FileExists_DeletesFileAndReturnsBytesFreed()
        {
            // Arrange: write a known dummy cache file at the service's path.
            var path = RemoteMiscConfigService.CacheFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = "{\"version\":\"test\"}";
            File.WriteAllText(path, payload);
            long expected = new FileInfo(path).Length;

            // Act
            long freed = RemoteMiscConfigService.DeleteCache();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(freed, Is.EqualTo(expected));
                Assert.That(File.Exists(path), Is.False);
                Assert.That(RemoteMiscConfigService.CurrentConfig, Is.Null);
            });
        }

        [Test]
        public void DeleteCache_NoFile_ReturnsZeroAndDoesNotThrow()
        {
            // Arrange: ensure no cache file is present (SetUp already cleared it).
            Assert.That(File.Exists(RemoteMiscConfigService.CacheFilePath), Is.False);

            // Act
            long freed = RemoteMiscConfigService.DeleteCache();

            // Assert
            Assert.That(freed, Is.EqualTo(0));
            Assert.That(RemoteMiscConfigService.CurrentConfig, Is.Null);
        }

        #endregion
    }
}
