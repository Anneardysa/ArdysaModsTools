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
using System.Collections.Generic;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SetUpdateResolverTests
    {
        private static readonly DateTime Now = new(2026, 7, 15);

        private static HeroModel Hero(string heroId, string name, params (string SetName, string[] Files)[] sets)
        {
            var hero = new HeroModel { HeroId = heroId, Name = name };
            foreach (var (setName, files) in sets)
                hero.Sets[setName] = new List<string>(files);
            return hero;
        }

        private static List<HeroModel> Heroes() => new()
        {
            Hero("npc_dota_hero_juggernaut", "juggernaut",
                ("Bladeform Legacy", new[] { "readme.txt", "https://cdn/jugg/bladeform/thumb.jpg" }),
                ("Arms of the Onyx Crucible", new[] { "https://cdn/jugg/onyx/thumb.webp" })),
            Hero("npc_dota_hero_axe", "axe",
                ("Red Mist Reaper", new[] { "https://cdn/axe/redmist/thumb.png" }),
                ("No Image Set", new[] { "model.vmdl", "notes.txt" }))
        };

        [Test]
        public void Resolve_MatchesHeroIdInAllFormats()
        {
            var updates = new List<(string, string, DateTime)>
            {
                ("npc_dota_hero_juggernaut", "Bladeform Legacy", Now),
                ("axe", "Red Mist Reaper", Now),
                ("NPC_DOTA_HERO_JUGGERNAUT", "arms of the onyx crucible", Now)
            };

            var cards = SetUpdateResolver.Resolve(Heroes(), updates, Now);

            Assert.That(cards, Has.Count.EqualTo(3));
            Assert.That(cards[0].HeroName, Is.EqualTo("juggernaut"));
            Assert.That(cards[1].HeroName, Is.EqualTo("axe"));
            Assert.That(cards[2].SetIndex, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_PicksFirstImageFile_SkippingNonImages()
        {
            var updates = new List<(string, string, DateTime)>
            {
                ("npc_dota_hero_juggernaut", "Bladeform Legacy", Now)
            };

            var cards = SetUpdateResolver.Resolve(Heroes(), updates, Now);

            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].SetThumbnail, Is.EqualTo("https://cdn/jugg/bladeform/thumb.jpg"));
        }

        [Test]
        public void Resolve_DropsUnresolvedEntries()
        {
            var updates = new List<(string, string, DateTime)>
            {
                ("npc_dota_hero_unknown", "Some Set", Now),
                ("npc_dota_hero_axe", "Not A Real Set", Now),
                ("npc_dota_hero_axe", "No Image Set", Now),
                ("npc_dota_hero_axe", "Red Mist Reaper", Now)
            };

            var cards = SetUpdateResolver.Resolve(Heroes(), updates, Now);

            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].SetName, Is.EqualTo("Red Mist Reaper"));
        }

        [Test]
        public void Resolve_ComputesDaysAgoFromInjectedClock_AndFormatsDate()
        {
            var updates = new List<(string, string, DateTime)>
            {
                ("npc_dota_hero_axe", "Red Mist Reaper", Now.AddDays(-3))
            };

            var cards = SetUpdateResolver.Resolve(Heroes(), updates, Now);

            Assert.That(cards[0].DaysAgo, Is.EqualTo(3));
            Assert.That(cards[0].AddedDate, Is.EqualTo("2026-07-12"));
        }

        [Test]
        public void Resolve_PreservesInputOrder()
        {
            var updates = new List<(string, string, DateTime)>
            {
                ("npc_dota_hero_axe", "Red Mist Reaper", Now),
                ("npc_dota_hero_juggernaut", "Bladeform Legacy", Now.AddDays(-1)),
                ("npc_dota_hero_juggernaut", "Arms of the Onyx Crucible", Now.AddDays(-2))
            };

            var cards = SetUpdateResolver.Resolve(Heroes(), updates, Now);

            Assert.That(cards, Has.Count.EqualTo(3));
            Assert.That(cards[0].SetName, Is.EqualTo("Red Mist Reaper"));
            Assert.That(cards[1].SetName, Is.EqualTo("Bladeform Legacy"));
            Assert.That(cards[2].SetName, Is.EqualTo("Arms of the Onyx Crucible"));
        }

        [Test]
        public void Resolve_NullInputs_ReturnEmpty()
        {
            Assert.That(SetUpdateResolver.Resolve(null!, new List<(string, string, DateTime)>(), Now), Is.Empty);
            Assert.That(SetUpdateResolver.Resolve(Heroes(), null!, Now), Is.Empty);
        }
    }
}
