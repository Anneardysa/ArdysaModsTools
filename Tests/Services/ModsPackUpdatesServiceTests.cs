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
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModsPackUpdatesServiceTests
    {
        private const string Base = "https://ardysamods.my.id";

        [Test]
        public void Parse_ValidManifest_MapsFieldsInOrder()
        {
            const string json = """
            [
              { "hero": "Anti-Mage", "image": "assets/updates/antimage.jpg", "date": "2026-01-30", "attribute": "Agility" },
              { "hero": "Axe", "image": "assets/updates/axe.jpg", "date": "2026-01-30", "attribute": "Strength" }
            ]
            """;

            var updates = ModsPackUpdatesService.Parse(json, Base);

            Assert.That(updates, Is.Not.Null);
            Assert.That(updates!, Has.Count.EqualTo(2));
            Assert.That(updates[0].Hero, Is.EqualTo("Anti-Mage"));
            Assert.That(updates[0].Attribute, Is.EqualTo("Agility"));
            Assert.That(updates[0].Date, Is.EqualTo("2026-01-30"));
            Assert.That(updates[1].Hero, Is.EqualTo("Axe"));
        }

        [Test]
        public void Parse_RelativeImage_ResolvedAgainstBase()
        {
            const string json = """
            [ { "hero": "Axe", "image": "assets/updates/axe.jpg" } ]
            """;

            var updates = ModsPackUpdatesService.Parse(json, Base);

            Assert.That(updates, Is.Not.Null);
            Assert.That(updates![0].Image, Is.EqualTo("https://ardysamods.my.id/assets/updates/axe.jpg"));
        }

        [Test]
        public void Parse_AbsoluteImage_KeptAsIs()
        {
            const string json = """
            [ { "hero": "Axe", "image": "https://cdn.example.com/axe.jpg" } ]
            """;

            var updates = ModsPackUpdatesService.Parse(json, Base);

            Assert.That(updates, Is.Not.Null);
            Assert.That(updates![0].Image, Is.EqualTo("https://cdn.example.com/axe.jpg"));
        }

        [Test]
        public void Parse_DropsEntriesWithoutHero()
        {
            const string json = """
            [
              { "hero": "Axe", "image": "assets/updates/axe.jpg" },
              { "image": "assets/updates/orphan.jpg" },
              { "hero": "", "image": "assets/updates/empty.jpg" }
            ]
            """;

            var updates = ModsPackUpdatesService.Parse(json, Base);

            Assert.That(updates, Is.Not.Null);
            Assert.That(updates!, Has.Count.EqualTo(1));
            Assert.That(updates[0].Hero, Is.EqualTo("Axe"));
        }

        [Test]
        public void Parse_EmptyOrInvalid_ReturnsNull()
        {
            Assert.That(ModsPackUpdatesService.Parse(null, Base), Is.Null);
            Assert.That(ModsPackUpdatesService.Parse("", Base), Is.Null);
            Assert.That(ModsPackUpdatesService.Parse("   ", Base), Is.Null);
            Assert.That(ModsPackUpdatesService.Parse("[]", Base), Is.Null);
            Assert.That(ModsPackUpdatesService.Parse("not json", Base), Is.Null);
        }
    }
}
