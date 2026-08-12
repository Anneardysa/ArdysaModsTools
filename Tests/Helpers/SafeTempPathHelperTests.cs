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

        [Test]
        [TestCase("subfolder/file.txt")]
        [TestCase("textures/models/hero.vtf")]
        [TestCase("items_game.txt")]
        public void IsSafeExtractionPath_ValidRelativePaths_ReturnsTrueAndResolvedPath(string relativePath)
        {
            bool safe = SafeTempPathHelper.IsSafeExtractionPath(_dir, relativePath, out var safePath);

            Assert.That(safe, Is.True);
            Assert.That(safePath, Does.StartWith(Path.GetFullPath(_dir)));
        }

        [Test]
        [TestCase("../evil.dll")]
        [TestCase("../../Windows/System32/cmd.exe")]
        [TestCase("subfolder/../../outside.txt")]
        [TestCase(@"..\..\..\AppData\Roaming\malicious.exe")]
        public void IsSafeExtractionPath_ZipSlipTraversalPaths_ReturnsFalse(string traversalPath)
        {
            bool safe = SafeTempPathHelper.IsSafeExtractionPath(_dir, traversalPath, out var safePath);

            Assert.That(safe, Is.False, $"Traversal path '{traversalPath}' must be blocked.");
            Assert.That(safePath, Is.Empty);
        }
    }
}
