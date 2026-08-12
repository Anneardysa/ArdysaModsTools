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
using System.Collections.Generic;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetModifierServiceTests
    {
        [Test]
        public void ResolveItemIdsForSelections_NullOrEmpty_ReturnsEmptyList()
        {
            var resultNull = AssetModifierService.ResolveItemIdsForSelections(null);
            Assert.That(resultNull, Is.Empty);

            var resultEmpty = AssetModifierService.ResolveItemIdsForSelections(new Dictionary<string, string>());
            Assert.That(resultEmpty, Is.Empty);
        }

        [Test]
        public void ResolveItemIdsForSelections_CategorySelections_ReturnsExpectedItemIds()
        {
            var selections = new Dictionary<string, string>
            {
                { "Weather", "Rain" },
                { "Map", "Low Poly" },
                { "HUD", "default" }
            };

            var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

            Assert.That(resolved, Contains.Item("555"));
            Assert.That(resolved, Contains.Item("590"));
            Assert.That(resolved, Does.Not.Contain("587"));
        }

        [Test]
        public void ResolveItemIdsForSelections_CourierAndWard_IncludesDefaultAndSelectedIds()
        {
            var selections = new Dictionary<string, string>
            {
                { "Courier", "Donkey" },
                { "Ward", "Eyeball" }
            };

            var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

            Assert.That(resolved, Contains.Item("595"));
            Assert.That(resolved, Contains.Item("596"));
        }
    }
}
