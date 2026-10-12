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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Services.App;
using ArdysaModsTools.Core.Services.Cache;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CacheCleaningServiceTests
    {
        [Test]
        public void ShouldClearForVersion_ClearsWhenVersionChanged()
        {
            Assert.That(CacheCleaningService.ShouldClearForVersion("2.2.19-beta (Build 2283)", "2.2.20-beta (Build 2286)"), Is.True);
        }

        [Test]
        public void ShouldClearForVersion_ClearsOnBuildBumpOfSameVersion()
        {
            Assert.That(CacheCleaningService.ShouldClearForVersion("2.2.20-beta (Build 2285)", "2.2.20-beta (Build 2286)"), Is.True);
        }

        [Test]
        public void ShouldClearForVersion_SkipsWhenUnchanged()
        {
            Assert.That(CacheCleaningService.ShouldClearForVersion("2.2.20-beta (Build 2286)", "2.2.20-beta (Build 2286)"), Is.False);
        }

        [Test]
        public void ShouldClearForVersion_SkipsOnFirstEverLaunch()
        {
            Assert.That(CacheCleaningService.ShouldClearForVersion(null, "2.2.20-beta (Build 2286)"), Is.False);
            Assert.That(CacheCleaningService.ShouldClearForVersion("", "2.2.20-beta (Build 2286)"), Is.False);
            Assert.That(CacheCleaningService.ShouldClearForVersion("   ", "2.2.20-beta (Build 2286)"), Is.False);
        }


        private static bool ListsAssetCache(CacheClearScope scope)
        {
            string assetCache = Path.GetFullPath(AssetCacheService.Instance.CacheDirectory);
            return new CacheCleaningService().GetCacheFolders(scope).Any(f =>
                string.Equals(Path.GetFullPath(f.FolderPath).TrimEnd('\\'), assetCache.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetCacheFolders_AppUpdate_KeepsAssetCache()
        {
            Assert.That(ListsAssetCache(CacheClearScope.AppUpdate), Is.False);
        }

        [Test]
        public void GetCacheFolders_Everything_IncludesAssetCache()
        {
            Assert.That(ListsAssetCache(CacheClearScope.Everything), Is.True);
        }

        [Test]
        public void PostUpdateClearInProgramCs_UsesAppUpdateScope()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repo root (no build.txt in any parent)");

            string program = File.ReadAllText(Path.Combine(dir!.FullName, "Program.cs"));
            var calls = Regex.Matches(program, @"ClearAllCacheAsync\(([^)]*)\)");

            Assert.That(calls, Is.Not.Empty, "Program.cs no longer clears caches on update — update this guard");
            foreach (Match call in calls)
                Assert.That(call.Groups[1].Value.Trim(), Is.EqualTo("CacheClearScope.AppUpdate"),
                    "launch-time clears must keep the thumbnail AssetCache");
        }
    }
}
