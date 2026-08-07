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
using System.Linq;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class VanillaLayoutOverlayTests
    {
        private const string Vanilla =
            "\t\t\"1\"\n" +
            "\t\t{\n" +
            "\t\t\t\"name\"\t\t\"Anti-Mage's Glaive\"\n" +
            "\t\t\t\"prefab\"\t\t\"default_item\"\n" +
            "\t\t\t\"image_inventory\"\t\t\"econ/heroes/antimage/antimage_weapon\"\n" +
            "\t\t\t\"item_rarity\"\t\t\"common\"\n" +
            "\t\t\t\"model_player\"\t\t\"models/heroes/antimage/antimage_weapon.vmdl\"\n" +
            "\t\t\t\"portraits\"\n" +
            "\t\t\t{\n" +
            "\t\t\t\t\"icon\"\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\t\"PortraitLightFOV\"\t\t\"56\"\n" +
            "\t\t\t\t}\n" +
            "\t\t\t}\n" +
            "\t\t\t\"used_by_heroes\"\n" +
            "\t\t\t{\n" +
            "\t\t\t\t\"npc_dota_hero_antimage\"\t\t\"1\"\n" +
            "\t\t\t}\n" +
            "\t\t}";

        private const string Package =
            "\t\t\"1\"\n" +
            "\t\t{\n" +
            "\t\t\t\"model_player\"\t\t\"models/MODDED_glaive.vmdl\"\n" +
            "\t\t\t\"visuals\"\n" +
            "\t\t\t{\n" +
            "\t\t\t\t\"asset_modifier0\"\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"particle\"\n" +
            "\t\t\t\t}\n" +
            "\t\t\t}\n" +
            "\t\t\t\"used_by_heroes\"\n" +
            "\t\t\t{\n" +
            "\t\t\t\t\"npc_dota_hero_antimage\"\t\t\"1\"\n" +
            "\t\t\t}\n" +
            "\t\t}";

        private static string Merge() =>
            KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(Vanilla, Package);

        private static List<string> TopKeys(string block)
        {
            var keys = new List<string>();
            int brace = block.IndexOf('{');
            int end = KeyValuesBlockHelper.ExtractBalancedBlockEnd(block, brace);
            int pos = brace + 1, bodyEnd = end - 1;

            while (pos < bodyEnd)
            {
                int q1 = block.IndexOf('"', pos);
                if (q1 < 0 || q1 >= bodyEnd) break;
                int q2 = block.IndexOf('"', q1 + 1);
                if (q2 < 0 || q2 >= bodyEnd) break;

                keys.Add(block.Substring(q1 + 1, q2 - q1 - 1));

                int after = KeyValuesBlockHelper.SkipWhitespace(block, q2 + 1);
                if (after >= bodyEnd) break;
                if (block[after] == '{')
                {
                    int sub = KeyValuesBlockHelper.ExtractBalancedBlockEnd(block, after);
                    if (sub < 0) break;
                    pos = sub;
                }
                else if (block[after] == '"')
                {
                    int v2 = block.IndexOf('"', after + 1);
                    if (v2 < 0) break;
                    pos = v2 + 1;
                }
                else break;
            }
            return keys;
        }

        [Test]
        public void KeysOnlyTheGameDefines_AreKept()
        {
            var keys = TopKeys(Merge());

            Assert.Multiple(() =>
            {
                Assert.That(keys, Does.Contain("name"));
                Assert.That(keys, Does.Contain("prefab"));
                Assert.That(keys, Does.Contain("image_inventory"));
                Assert.That(keys, Does.Contain("item_rarity"), "an update adding this key must not be undone");
                Assert.That(keys, Does.Contain("portraits"));
            });
        }

        [Test]
        public void TheGamesKeyOrder_IsPreserved()
        {
            var merged = TopKeys(Merge());
            var vanilla = TopKeys(Vanilla);

            var shared = merged.Where(vanilla.Contains).ToList();
            Assert.That(shared, Is.EqualTo(vanilla));
        }

        [Test]
        public void SharedKeys_TakeThePackagesValue()
        {
            var merged = Merge();

            Assert.That(merged, Does.Contain("models/MODDED_glaive.vmdl"));
            Assert.That(merged, Does.Not.Contain("models/heroes/antimage/antimage_weapon.vmdl"));
        }

        [Test]
        public void KeysOnlyThePackageDefines_AreAppended()
        {
            var keys = TopKeys(Merge());

            Assert.That(keys, Does.Contain("visuals"));
            Assert.That(keys.Last(), Is.EqualTo("visuals"), "mod additions go after the game's own keys");
        }

        [Test]
        public void TheResultIsStillAWellFormedBlock()
        {
            var merged = Merge();

            Assert.Multiple(() =>
            {
                Assert.That(merged.Count(c => c == '{'), Is.EqualTo(merged.Count(c => c == '}')));
                Assert.That(KeyValuesBlockHelper.ExtractBlockById(merged, "1"), Is.Not.Null);
            });
        }

        [Test]
        public void CarriageReturns_AreNeverEmitted()
        {
            var merged = KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(
                Vanilla.Replace("\n", "\r\n"), Package.Replace("\n", "\r\n"));

            Assert.That(merged, Does.Not.Contain("\r"));
        }

        [Test]
        public void TheGamesIndentationIsUsed()
        {
            var lines = Merge().Split('\n');

            Assert.That(lines[0], Does.StartWith("\t\t\"1\""));
            Assert.That(lines[1].TrimEnd(), Is.EqualTo("\t\t{"));
            Assert.That(lines.Last(c => !string.IsNullOrWhiteSpace(c)).TrimEnd(), Is.EqualTo("\t\t}"));
        }

        [Test]
        public void UnparseableInput_FallsBackToTheGamesBlock()
        {
            Assert.That(KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(Vanilla, "not a block"),
                        Does.Contain("models/heroes/antimage/antimage_weapon.vmdl"));
            Assert.That(KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(Vanilla, ""), Is.EqualTo(Vanilla));
        }

        [Test]
        public void RepairIsIdempotent()
        {
            var once = Merge();
            var twice = KeyValuesBlockHelper.OverlayBlockKeepingVanillaLayout(Vanilla, once);

            Assert.That(TopKeys(twice), Is.EqualTo(TopKeys(once)));
            Assert.That(twice, Does.Contain("models/MODDED_glaive.vmdl"));
        }

        [Test]
        public void TheInstallTimeOverlay_DropsWhatThisOneKeeps()
        {
            var installStyle = KeyValuesBlockHelper.OverlayBlockPreservingStructure(Vanilla, Package);

            Assert.That(TopKeys(installStyle), Does.Not.Contain("item_rarity"),
                "the install overlay is verbatim-the-package by design");
            Assert.That(TopKeys(Merge()), Does.Contain("item_rarity"),
                "the repair overlay must keep it");
        }
    }
}
