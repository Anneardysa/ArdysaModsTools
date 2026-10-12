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
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SetNoticeResolverTests
    {
        private const string Cdn = "https://cdn.ardysamods.my.id/Assets/models/Abaddon/";

        private static KeyValuePair<string, List<string>> Set(string key, string archive) =>
            new(key, new List<string> { $"{Cdn}{archive}", $"{Cdn}thumb.png" });

        private static readonly SetNotice Demo = new(SetNotice.AnimDemoOnly);
        private static readonly SetNotice Idle = new(SetNotice.AnimBug, Anim: "Idle");

        private static Dictionary<string, List<SetNotice>> Notices(params (string stem, SetNotice n)[] items)
        {
            var d = new Dictionary<string, List<SetNotice>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (stem, n) in items)
            {
                if (!d.TryGetValue(stem, out var l)) d[stem] = l = new List<SetNotice>();
                l.Add(n);
            }
            return d;
        }

        [Test]
        public void Resolve_PutsEachNoticeOnTheSetCarryingItsArchive()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                new("Default Set", new List<string> { "default_skin.vpk" }),
                Set("Set 1", "blightfall.zip"),
                Set("Set 2", "item_weapon_mace.zip")
            };

            var map = SetNoticeResolver.Resolve(sets, Notices(("item_weapon_mace", Idle), ("blightfall", Demo)));

            Assert.Multiple(() =>
            {
                Assert.That(map, Has.Length.EqualTo(3));
                Assert.That(map[0], Is.Null, "Default Set has no archive stem");
                Assert.That(map[1], Is.EqualTo(new[] { Demo }));
                Assert.That(map[2], Is.EqualTo(new[] { Idle }));
            });
        }

        [Test]
        public void Resolve_StyleVariantsEachGetTheirOwnNotices()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                Set("Set 11 (item_offhand_basher)", "item_offhand_basher.zip"),
                Set("Set 11 (item_offhand_basher_golden)", "item_offhand_basher_golden.zip")
            };

            var map = SetNoticeResolver.Resolve(sets, Notices(("item_offhand_basher_golden", Demo)));

            Assert.Multiple(() =>
            {
                Assert.That(map[0], Is.Null);
                Assert.That(map[1], Is.EqualTo(new[] { Demo }));
            });
        }

        [Test]
        public void Resolve_SplitArchive_MatchesByStem()
        {
            var sets = new List<KeyValuePair<string, List<string>>> { Set("Set 1", "huge_set.zip.001") };

            var map = SetNoticeResolver.Resolve(sets, Notices(("huge_set", Demo)));

            Assert.That(map[0], Is.EqualTo(new[] { Demo }));
        }

        [Test]
        public void Resolve_UnknownStem_IsReportedAndIgnored()
        {
            var warnings = new List<string>();
            var sets = new List<KeyValuePair<string, List<string>>> { Set("Set 1", "blightfall.zip") };

            var map = SetNoticeResolver.Resolve(sets, Notices(("blightfall", Demo), ("blightfal", Idle)), warnings.Add);

            Assert.Multiple(() =>
            {
                Assert.That(map[0], Is.EqualTo(new[] { Demo }));
                Assert.That(warnings, Has.Count.EqualTo(1));
                Assert.That(warnings[0], Does.Contain("blightfal"));
            });
        }

        [Test]
        public void Resolve_NoNoticesOrNoSets_ReturnsAllNullWithoutThrowing()
        {
            var sets = new List<KeyValuePair<string, List<string>>> { Set("Set 1", "blightfall.zip") };

            Assert.Multiple(() =>
            {
                Assert.That(SetNoticeResolver.Resolve(sets, null), Is.EqualTo(new SetNotice[]?[] { null }));
                Assert.That(SetNoticeResolver.Resolve(new List<KeyValuePair<string, List<string>>>(), Notices(("a", Demo))), Is.Empty);
            });
        }
    }
}
