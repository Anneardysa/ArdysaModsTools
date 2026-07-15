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
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class SafeTempPathHelperTests
    {
        private string _dir = null!;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AmtHideDirTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_dir))
                {
                    new DirectoryInfo(_dir).Attributes = FileAttributes.Directory;
                    Directory.Delete(_dir, true);
                }
            }
            catch {  }
        }

        [Test]
        public void HideDirectory_SetsHiddenAndSystem()
        {
            SafeTempPathHelper.HideDirectory(_dir);

            var attrs = new DirectoryInfo(_dir).Attributes;
            Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.True, "expected Hidden");
            Assert.That(attrs.HasFlag(FileAttributes.System), Is.True, "expected System");
        }

        [Test]
        public void HideDirectory_IsIdempotent()
        {
            SafeTempPathHelper.HideDirectory(_dir);
            SafeTempPathHelper.HideDirectory(_dir);

            var attrs = new DirectoryInfo(_dir).Attributes;
            Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.True);
            Assert.That(attrs.HasFlag(FileAttributes.System), Is.True);
        }

        [Test]
        public void HideDirectory_NonexistentPath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                SafeTempPathHelper.HideDirectory(Path.Combine(_dir, "does-not-exist")));
        }
    }
}
