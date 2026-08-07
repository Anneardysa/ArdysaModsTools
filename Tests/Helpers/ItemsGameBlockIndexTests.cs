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
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class ItemsGameBlockIndexTests
    {
        private const string TwoItems = @"
""items_game""
{
    ""items""
    {
        ""101""
        {
            ""name""            ""weapon_a""
            ""prefab""          ""wearable""
            ""used_by_heroes""
            {
                ""npc_dota_hero_axe""    ""1""
            }
        }
        ""102""
        {
            ""name""            ""weapon_b""
            ""prefab""          ""wearable""
        }
    }
}";

        [Test]
        public void Build_IndexesNumericIdBlocks()
        {
            var index = ItemsGameBlockIndex.Build(TwoItems);

            Assert.That(index.Keys, Is.EquivalentTo(new[] { "101", "102" }));
            Assert.That(index["101"], Is.Not.EqualTo(index["102"]));
        }

        [Test]
        public void Build_EmptyOrGarbageInput_ReturnsEmpty()
        {
            Assert.That(ItemsGameBlockIndex.Build(null), Is.Empty);
            Assert.That(ItemsGameBlockIndex.Build(""), Is.Empty);
            Assert.That(ItemsGameBlockIndex.Build("   \n\t "), Is.Empty);
            Assert.That(ItemsGameBlockIndex.Build("not key values at all"), Is.Empty);
        }

        [Test]
        public void Build_SameContentDifferentFormatting_ProducesSameHash()
        {
            const string pretty = "\"101\"\n{\n\t\"name\"\t\t\"weapon_a\"\n\t\"prefab\"\t\"wearable\"\n}\n";
            const string oneLiner = "\"101\" { \"name\" \"weapon_a\" \"prefab\" \"wearable\" }";
            const string crlfDeepIndent = "\"101\"\r\n        {\r\n                \"name\"   \"weapon_a\"\r\n                \"prefab\" \"wearable\"\r\n        }\r\n";

            var a = ItemsGameBlockIndex.Build(pretty);
            var b = ItemsGameBlockIndex.Build(oneLiner);
            var c = ItemsGameBlockIndex.Build(crlfDeepIndent);

            Assert.That(b["101"], Is.EqualTo(a["101"]));
            Assert.That(c["101"], Is.EqualTo(a["101"]));
        }

        [Test]
        public void Build_CrlfAndLfInput_ProduceIdenticalHashes()
        {
            var lf = ItemsGameBlockIndex.Build(TwoItems.Replace("\r\n", "\n"));
            var crlf = ItemsGameBlockIndex.Build(TwoItems.Replace("\r\n", "\n").Replace("\n", "\r\n"));

            Assert.That(crlf, Is.EquivalentTo(lf));
        }

        [Test]
        public void Compare_AcrossLineEndingStyles_ReportsNoDifference()
        {
            var lf = ItemsGameBlockIndex.Build(TwoItems.Replace("\r\n", "\n"));
            var crlf = ItemsGameBlockIndex.Build(TwoItems.Replace("\r\n", "\n").Replace("\n", "\r\n"));

            var diff = ItemsGameBlockIndex.Compare(lf, crlf);

            Assert.Multiple(() =>
            {
                Assert.That(diff.HasIdDelta, Is.False);
                Assert.That(diff.Changed, Is.Zero, "line endings must never read as a content change");
            });
        }

        [Test]
        public void Build_CommentsDoNotAffectHash()
        {
            const string plain = "\"101\" { \"name\" \"weapon_a\" }";
            const string commented = "\"101\"\n{\n\t// added in 7.39\n\t\"name\"\t\"weapon_a\"\n}\n";

            Assert.That(ItemsGameBlockIndex.Build(commented)["101"],
                        Is.EqualTo(ItemsGameBlockIndex.Build(plain)["101"]));
        }

        [Test]
        public void Build_DifferentValue_ProducesDifferentHash()
        {
            var a = ItemsGameBlockIndex.Build("\"101\" { \"model_player\" \"models/vanilla.vmdl\" }");
            var b = ItemsGameBlockIndex.Build("\"101\" { \"model_player\" \"models/modded.vmdl\" }");

            Assert.That(b["101"], Is.Not.EqualTo(a["101"]));
        }

        [Test]
        public void Compare_IdenticalInputs_ReportsNoDiff()
        {
            var index = ItemsGameBlockIndex.Build(TwoItems);
            var diff = ItemsGameBlockIndex.Compare(index, ItemsGameBlockIndex.Build(TwoItems));

            Assert.Multiple(() =>
            {
                Assert.That(diff.Added, Is.Zero);
                Assert.That(diff.Removed, Is.Zero);
                Assert.That(diff.Changed, Is.Zero);
                Assert.That(diff.HasIdDelta, Is.False);
            });
        }

        [Test]
        public void Compare_DetectsAddedRemovedAndChanged()
        {
            var vanilla = new Dictionary<string, string>
            {
                ["101"] = "HASH_A",
                ["102"] = "HASH_B",
                ["103"] = "HASH_C"
            };
            var modded = new Dictionary<string, string>
            {
                ["101"] = "HASH_A",
                ["102"] = "HASH_MODDED",
                ["999"] = "HASH_X"
            };

            var diff = ItemsGameBlockIndex.Compare(vanilla, modded);

            Assert.Multiple(() =>
            {
                Assert.That(diff.Added, Is.EqualTo(1));
                Assert.That(diff.Removed, Is.EqualTo(1));
                Assert.That(diff.Changed, Is.EqualTo(1));
                Assert.That(diff.AddedIds, Does.Contain("103"));
                Assert.That(diff.HasIdDelta, Is.True);
            });
        }

        [Test]
        public void Compare_SameIdsDifferentValues_ReportsNoIdDelta()
        {
            var vanilla = ItemsGameBlockIndex.Build("\"101\" { \"model_player\" \"models/vanilla.vmdl\" }");
            var modded = ItemsGameBlockIndex.Build("\"101\" { \"model_player\" \"models/modded.vmdl\" }");

            var diff = ItemsGameBlockIndex.Compare(vanilla, modded);

            Assert.Multiple(() =>
            {
                Assert.That(diff.HasIdDelta, Is.False);
                Assert.That(diff.Changed, Is.EqualTo(1));
            });
        }
    }
}
