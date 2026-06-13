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
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Cache;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetCacheServiceTests
    {
        #region IsKnownMissing

        [Test]
        public void IsKnownMissing_EmptyUrl_ReturnsFalse()
        {
            Assert.That(AssetCacheService.Instance.IsKnownMissing("", TimeSpan.FromDays(7)), Is.False);
        }

        [Test]
        public void IsKnownMissing_UnmarkedUrl_ReturnsFalse()
        {
            // A URL that was never recorded as missing must not be treated as missing.
            var unique = $"https://cdn.ardysamods.my.id/Assets/misc/never/{Guid.NewGuid():N}.png";
            Assert.That(AssetCacheService.Instance.IsKnownMissing(unique, TimeSpan.FromDays(7)), Is.False);
        }

        #endregion
    }
}
