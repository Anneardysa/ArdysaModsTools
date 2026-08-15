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
using System.Linq;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroDefaultItemRegistryTests
    {
        [Test]
        public void Count_MatchesExtractedDataset()
        {
            Assert.That(HeroDefaultItemRegistry.Count, Is.GreaterThanOrEqualTo(850));
        }

        [TestCase("1", "npc_dota_hero_antimage", "Anti-Mage", "Weapon", "Anti-Mage's Glaive")]
        [TestCase("2", "npc_dota_hero_axe", "Axe", "Weapon", "Axe's Axe")]
        [TestCase("5", "npc_dota_hero_axe", "Axe", "Belt", "Axe's Belt")]
        [TestCase("6", "npc_dota_hero_juggernaut", "Juggernaut", "Head", "Juggernaut's Mask")]
        [TestCase("7", "npc_dota_hero_juggernaut", "Juggernaut", "Weapon", "Juggernaut's Weapon")]
        [TestCase("15", "npc_dota_hero_faceless_void", "Faceless Void", "Weapon", "Faceless Void's Weapon")]
        [TestCase("47", "npc_dota_hero_pudge", "Pudge", "Weapon", "Pudge's Rusty Meathook")]
        [TestCase("48", "npc_dota_hero_invoker", "Invoker", "Back", "Invoker's Cape")]
        public void TryGetItem_WithKnownDefaultItems_ReturnsAccurateMetadata(
            string itemId, string expectedHeroId, string expectedHeroName, string expectedSlot, string expectedName)
        {
            bool found = HeroDefaultItemRegistry.TryGetItem(itemId, out var info);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(info, Is.Not.Null);
                Assert.That(info.HeroId, Is.EqualTo(expectedHeroId));
                Assert.That(info.HeroName, Is.EqualTo(expectedHeroName));
                Assert.That(info.SlotDisplayName, Is.EqualTo(expectedSlot));
                Assert.That(info.TechnicalName, Is.EqualTo(expectedName));
            });
        }

        [Test]
        public void TryGetItem_WithIntOverload_WorksEqually()
        {
            bool foundStr = HeroDefaultItemRegistry.TryGetItem("1", out var infoStr);
            bool foundInt = HeroDefaultItemRegistry.TryGetItem(1, out var infoInt);

            Assert.Multiple(() =>
            {
                Assert.That(foundStr, Is.True);
                Assert.That(foundInt, Is.True);
                Assert.That(infoInt, Is.EqualTo(infoStr));
            });
        }

        [Test]
        public void TryGetItem_WithNonExistentId_ReturnsFalse()
        {
            bool found = HeroDefaultItemRegistry.TryGetItem("9999999", out var info);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.False);
                Assert.That(info, Is.Null);
            });
        }

        [TestCase("1", true)]
        [TestCase("2", true)]
        [TestCase("5", true)]
        [TestCase("47", true)]
        [TestCase("999999", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsHeroDefaultItem_ValidatesCorrectly(string? itemId, bool expected)
        {
            Assert.That(HeroDefaultItemRegistry.IsHeroDefaultItem(itemId), Is.EqualTo(expected));
        }

        [TestCase("npc_dota_hero_antimage", "Anti-Mage")]
        [TestCase("npc_dota_hero_nevermore", "Shadow Fiend")]
        [TestCase("npc_dota_hero_zuus", "Zeus")]
        [TestCase("npc_dota_hero_furion", "Nature's Prophet")]
        [TestCase("npc_dota_hero_shredder", "Timbersaw")]
        [TestCase("npc_dota_hero_windrunner", "Windranger")]
        [TestCase("npc_dota_hero_crystal_maiden", "Crystal Maiden")]
        [TestCase("all", "All Heroes")]
        [TestCase("", "Universal")]
        [TestCase(null, "Universal")]
        public void FormatHeroName_FormatsCorrectly(string? heroId, string expected)
        {
            Assert.That(HeroDefaultItemRegistry.FormatHeroName(heroId), Is.EqualTo(expected));
        }

        [TestCase("ability_effects_1", "Ability Effects 1")]
        [TestCase("body_head", "Body / Head")]
        [TestCase("hero_base", "Base Model")]
        [TestCase("base", "Base Model")]
        [TestCase("offhand_weapon", "Off-Hand Weapon")]
        [TestCase("ambient_effects", "Ambient Effects")]
        [TestCase("weapon", "Weapon")]
        [TestCase("armor", "Armor")]
        [TestCase("", "Default")]
        [TestCase(null, "Default")]
        public void FormatSlotDisplayName_FormatsCorrectly(string? slot, string expected)
        {
            Assert.That(HeroDefaultItemRegistry.FormatSlotDisplayName(slot), Is.EqualTo(expected));
        }

        [TestCase("855", "npc_dota_hero_earthshaker", "Earthshaker", "Earthshaker's Base")]
        [TestCase("811", "npc_dota_hero_juggernaut", "Juggernaut", "Juggernaut's Base")]
        [TestCase("825", "npc_dota_hero_monkey_king", "Monkey King", "Monkey King's Base")]
        public void TryGetItem_HeroBaseItems_RecognizedCorrectly(
            string itemId, string expectedHeroId, string expectedHeroName, string expectedName)
        {
            bool found = HeroDefaultItemRegistry.TryGetItem(itemId, out var info);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(info.HeroId, Is.EqualTo(expectedHeroId));
                Assert.That(info.HeroName, Is.EqualTo(expectedHeroName));
                Assert.That(info.Slot, Is.EqualTo("hero_base"));
                Assert.That(info.TechnicalName, Is.EqualTo(expectedName));
            });
        }
    }
}
