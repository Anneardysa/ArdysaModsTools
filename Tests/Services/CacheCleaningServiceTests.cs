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
using ArdysaModsTools.Core.Services.App;
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
    }
}
