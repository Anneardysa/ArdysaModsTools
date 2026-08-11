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
using System.Diagnostics;
using System.Text;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class ItemsGameMergerTests
    {
        private const string ModdedModel = "models/MODDED_axe_weapon.vmdl";

        private const string OldVanilla = @"
""items_game""
{
    ""items""
    {
        ""101""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_axe"" ""1"" }
            ""model_player""    ""models/vanilla_axe_weapon.vmdl""
        }
        ""102""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_lina"" ""1"" }
            ""model_player""    ""models/vanilla_lina_head.vmdl""
        }
    }
}";

        private static string Modded => OldVanilla.Replace("models/vanilla_axe_weapon.vmdl", ModdedModel);

        private const string NewVanilla = @"
""items_game""
{
    ""items""
    {
        ""101""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_axe"" ""1"" }
            ""model_player""    ""models/vanilla_axe_weapon.vmdl""
        }
        ""102""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_lina"" ""1"" }
            ""model_player""    ""models/vanilla_lina_head_REWORKED.vmdl""
        }
        ""103""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_sniper"" ""1"" }
            ""model_player""    ""models/vanilla_brand_new_item.vmdl""
        }
    }
}";

        [Test]
        public void Merge_KeepsItemsTheUpdateAdded()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded);

            Assert.That(result.Text, Does.Contain("\"103\""));
            Assert.That(result.Text, Does.Contain("models/vanilla_brand_new_item.vmdl"));
        }

        [Test]
        public void Merge_KeepsTheModsCustomisations()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded, new[] { "101" });

            Assert.That(result.Text, Does.Contain(ModdedModel));
            Assert.That(result.Applied, Is.EqualTo(1));
        }

        [Test]
        public void Merge_WithBuildRecord_TakesValvesVersionOfItemsTheModNeverTouched()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded, new[] { "101" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Does.Contain("models/vanilla_lina_head_REWORKED.vmdl"));
                Assert.That(result.Text, Does.Not.Contain("models/vanilla_lina_head.vmdl\""));
                Assert.That(result.Text, Does.Contain(ModdedModel), "the real customisation still lands");
                Assert.That(result.Applied, Is.EqualTo(1));
            });
        }

        [Test]
        public void Merge_WithoutBuildRecord_AlsoKeepsThePackagesCopyOfItemsValveRespecced()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Does.Contain(ModdedModel), "the real customisation still lands");
                Assert.That(result.Text, Does.Contain("models/vanilla_lina_head.vmdl"),
                    "and so does the package's stale copy — indistinguishable without a record");
                Assert.That(result.Applied, Is.EqualTo(2));
            });
        }

        [Test]
        public void Merge_WithEmptyBuildRecord_FallsBackToComparisonInsteadOfMergingNothing()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded, Array.Empty<string>());

            Assert.That(result.Text, Does.Contain(ModdedModel));
            Assert.That(result.Applied, Is.GreaterThan(0));
        }

        [Test]
        public void Merge_AgainstUnchangedVanilla_StillProducesTheModdedResult()
        {
            var result = ItemsGameMerger.Merge(OldVanilla, Modded, new[] { "101" });

            Assert.That(result.Text, Does.Contain(ModdedModel));
            Assert.That(result.Applied, Is.EqualTo(1));
        }

        [Test]
        public void Merge_DropsCustomisationsForItemsTheGameRemoved()
        {
            const string vanillaWithout101 = @"
""items_game""
{
    ""items""
    {
        ""102""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_lina"" ""1"" }
            ""model_player""    ""models/vanilla_lina_head.vmdl""
        }
    }
}";

            var result = ItemsGameMerger.Merge(vanillaWithout101, Modded);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Does.Not.Contain(ModdedModel));
                Assert.That(result.Text, Does.Not.Contain("\"101\""));
                Assert.That(result.Dropped, Is.EqualTo(1));
            });
        }

        [Test]
        public void Merge_LeavesNonItemNumericSectionsAlone()
        {
            const string vanilla = @"
""items_game""
{
    ""item_levels""
    {
        ""1"" { ""name"" ""badge_level_NEW"" ""level"" ""1"" }
    }
    ""items""
    {
        ""101""
        {
            ""prefab""          ""wearable""
            ""used_by_heroes""  { ""npc_dota_hero_axe"" ""1"" }
            ""model_player""    ""models/vanilla_axe_weapon.vmdl""
        }
    }
}";
            string modded = vanilla
                .Replace("badge_level_NEW", "badge_level_OLD")
                .Replace("models/vanilla_axe_weapon.vmdl", ModdedModel);

            var result = ItemsGameMerger.Merge(vanilla, modded);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Does.Contain("badge_level_NEW"), "schema must stay the game's");
                Assert.That(result.Text, Does.Contain(ModdedModel), "the real item must still be merged");
            });
        }

        [Test]
        public void Merge_KeepsTheGamesStructure_BlockOrderAndEveryVanillaKey()
        {
            const string packageBlock =
                "\t\t\"101\"\n\t\t{\n" +
                "\t\t\t\"model_player\"\t\t\"models/MODDED.vmdl\"\n" +
                "\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier0\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"particle\"\n\t\t\t\t}\n\t\t\t}\n" +
                "\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t}\n";

            const string currentGame =
                "\"items_game\"\n{\n\t\"items\"\n\t{\n" +
                "\t\t\"100\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_lina\" \"1\" }\n\t\t\t\"model_player\"\t\"models/a.vmdl\"\n\t\t}\n" +
                "\t\t\"101\"\n\t\t{\n" +
                "\t\t\t\"name\"\t\t\"Axe Weapon\"\n" +
                "\t\t\t\"prefab\"\t\t\"default_item\"\n" +
                "\t\t\t\"item_rarity\"\t\t\"common\"\n" +
                "\t\t\t\"model_player\"\t\t\"models/vanilla.vmdl\"\n" +
                "\t\t\t\"portraits\"\n\t\t\t{\n\t\t\t\t\"icon\"\t{ \"PortraitLightFOV\" \"56\" }\n\t\t\t}\n" +
                "\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t}\n" +
                "\t\t\"102\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_sniper\" \"1\" }\n\t\t\t\"model_player\"\t\"models/c.vmdl\"\n\t\t}\n" +
                "\t}\n}\n";

            string package = currentGame
                .Replace("\t\t\t\"item_rarity\"\t\t\"common\"\n", "")
                .Replace(
                    "\t\t\"101\"\n\t\t{\n" +
                    "\t\t\t\"name\"\t\t\"Axe Weapon\"\n" +
                    "\t\t\t\"prefab\"\t\t\"default_item\"\n" +
                    "\t\t\t\"model_player\"\t\t\"models/vanilla.vmdl\"\n" +
                    "\t\t\t\"portraits\"\n\t\t\t{\n\t\t\t\t\"icon\"\t{ \"PortraitLightFOV\" \"56\" }\n\t\t\t}\n" +
                    "\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t}\n",
                    packageBlock);

            var result = ItemsGameMerger.Merge(currentGame, package);

            var gameOrder = OrderedIds(currentGame);
            var mergedOrder = OrderedIds(result.Text);
            string merged101 = KeyValuesBlockHelper.ExtractBlockById(result.Text, "101")!;

            Assert.Multiple(() =>
            {
                Assert.That(mergedOrder, Is.EqualTo(gameOrder), "block order must match the game's file");

                Assert.That(merged101, Does.Contain("\"name\""));
                Assert.That(merged101, Does.Contain("\"item_rarity\""), "the update's new key must survive");
                Assert.That(merged101, Does.Contain("\"portraits\""));
                Assert.That(merged101, Does.Contain("PortraitLightFOV"));

                Assert.That(merged101, Does.Contain("models/MODDED.vmdl"));
                Assert.That(merged101, Does.Contain("\"visuals\""));
                Assert.That(merged101, Does.Not.Contain("models/vanilla.vmdl"));

                Assert.That(result.Text, Does.Not.Contain("\r"), "Source 2 crashes on CRLF in item data");
            });
        }

        private static System.Collections.Generic.List<string> OrderedIds(string itemsGame)
        {
            string norm = KeyValuesBlockHelper.NormalizeKvText(itemsGame);
            var spans = ItemsGameBlockIndex.IndexSpans(norm);
            var ids = new System.Collections.Generic.List<string>(spans.Keys);
            ids.Sort((a, b) => spans[a].Start.CompareTo(spans[b].Start));
            return ids;
        }

        [Test]
        public void Merge_ProducesBalancedOutputWithEveryItemStillPresent()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, Modded);

            int open = 0, close = 0;
            foreach (char c in result.Text)
            {
                if (c == '{') open++;
                else if (c == '}') close++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(open, Is.EqualTo(close), "braces must balance");
                Assert.That(ItemsGameBlockIndex.Build(result.Text).Keys,
                    Is.EquivalentTo(ItemsGameBlockIndex.Build(NewVanilla).Keys),
                    "the merge must not add or lose item ids relative to the game's data");
            });
        }

        [Test]
        public void Merge_WithNoModdedContent_ReturnsVanillaUntouched()
        {
            var result = ItemsGameMerger.Merge(NewVanilla, "");

            Assert.Multiple(() =>
            {
                Assert.That(result.Applied, Is.Zero);
                Assert.That(ItemsGameBlockIndex.Build(result.Text),
                    Is.EquivalentTo(ItemsGameBlockIndex.Build(NewVanilla)));
            });
        }

        [Test]
        public void IsEquivalent_IgnoresFormattingButNotContent()
        {
            var noOp = ItemsGameMerger.Merge(OldVanilla, Modded, new[] { "101" });
            Assert.That(ItemsGameMerger.IsEquivalent(noOp.Text, Modded), Is.True,
                "a merge that changed nothing must not trigger a rebuild");

            var real = ItemsGameMerger.Merge(NewVanilla, Modded, new[] { "101" });
            Assert.That(ItemsGameMerger.IsEquivalent(real.Text, Modded), Is.False,
                "the game added an item — that has to count as changed");
        }

        [Test]
        public void IsEquivalent_CountsChangesOutsideItemBlocks()
        {
            const string withOldSchema = "\"items_game\"\n{\n\t\"game_info\" { \"first_valid_class\" \"1\" }\n}\n";
            const string withNewSchema = "\"items_game\"\n{\n\t\"game_info\" { \"first_valid_class\" \"2\" }\n}\n";

            Assert.That(ItemsGameMerger.IsEquivalent(withOldSchema, withNewSchema), Is.False);
        }

        [Test]
        public void Merge_WithoutVanillaBase_Throws()
        {
            Assert.Throws<ArgumentException>(() => ItemsGameMerger.Merge(null, Modded));
            Assert.Throws<ArgumentException>(() => ItemsGameMerger.Merge("  ", Modded));
        }

        [Test]
        public void Merge_ScalesToARealisticPackage()
        {
            const int items = 1200;
            var vanilla = new StringBuilder("\"items_game\"\n{\n\t\"items\"\n\t{\n");
            var modded = new StringBuilder("\"items_game\"\n{\n\t\"items\"\n\t{\n");

            for (int i = 1; i <= items; i++)
            {
                vanilla.Append($"\t\t\"{i}\"\n\t\t{{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{{ \"npc_dota_hero_axe\" \"1\" }}\n\t\t\t\"model_player\"\t\"models/vanilla_{i}.vmdl\"\n\t\t}}\n");
                modded.Append($"\t\t\"{i}\"\n\t\t{{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{{ \"npc_dota_hero_axe\" \"1\" }}\n\t\t\t\"model_player\"\t\"models/MOD_{i}.vmdl\"\n\t\t}}\n");
            }

            vanilla.Append("\t}\n}\n");
            modded.Append("\t}\n}\n");

            string vanillaText = vanilla.ToString();
            string moddedText = modded.ToString();
            long sizeBytes = (long)vanillaText.Length * 2;

            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var result = ItemsGameMerger.Merge(vanillaText, moddedText);
            sw.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Multiple(() =>
            {
                Assert.That(result.Applied, Is.EqualTo(items));
                Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(20)),
                    "a quadratic implementation blows through this by orders of magnitude");

                Assert.That(allocated, Is.LessThan(sizeBytes * 30),
                    $"allocated {allocated / 1048576.0:F1} MB for a {sizeBytes / 1048576.0:F1} MB input");
            });
        }

        [Test]
        public void Merge_PreservesCustomCosmeticItemProperties_SuchAsModdedEmberSpiritHead()
        {
            const string vanilla = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            ""name""                ""Ember Spirit's Head Item""
            ""prefab""              ""default_item""
            ""creation_date""        ""2013-10-11""
            ""image_inventory""      ""econ/heroes/ember_spirit/back""
            ""item_description""    ""#DOTA_Item_Desc_Ember_Spirits_Head_Item""
            ""item_name""           ""#DOTA_Item_Ember_Spirits_Head_Item""
            ""item_slot""           ""head""
            ""item_type_name""      ""#DOTA_WearableType_Head""
            ""model_player""        ""models/heroes/ember_spirit/back.vmdl""
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
            ""visuals""
            {
                ""asset_modifier0""
                {
                    ""type""        ""particle_create""
                    ""modifier""    ""particles/units/heroes/hero_ember_spirit/ember_spirit_ambient_head.vpcf""
                }
            }
        }
    }
}";

            const string modded = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            ""name""                ""Master of the Searing Path Head""
            ""prefab""              ""default_item""
            ""creation_date""        ""2020-08-14""
            ""event_id""            ""EVENT_ID_INTERNATIONAL_2020""
            ""image_inventory""      ""econ/items/ember_spirit/kungfu_master_head/kungfu_master_head""
            ""item_description""    ""This item has been modded to enhance your in-game experience.""
            ""item_name""           ""#DOTA_Item_Master_of_the_Searing_Path_Head""
            ""item_rarity""         ""mythical""
            ""item_slot""           ""head""
            ""item_type_name""      ""#DOTA_WearableType_head""
            ""model_player""        ""models/heroes/ember_spirit/back.vmdl""
            ""portraits""
            {
                ""icon""
                {
                    ""PortraitLightPosition""   ""20.145739 -94.024666 122.325706""
                }
            }
            ""static_attributes""
            {
                ""cannot trade""     { ""attribute_class"" ""cannot_trade"" ""value"" ""1"" }
            }
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
            ""visuals""
            {
                ""asset_modifier0""
                {
                    ""type""        ""particle_create""
                    ""modifier""    ""particles/econ/items/ember_spirit/ember_ti10_cache/ember_ti10_cache_head.vpcf""
                }
            }
        }
    }
}";

            var result = ItemsGameMerger.Merge(vanilla, modded, new[] { "462" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Applied, Is.EqualTo(1));
                Assert.That(result.Text, Does.Contain("Master of the Searing Path Head"));
                Assert.That(result.Text, Does.Contain("econ/items/ember_spirit/kungfu_master_head/kungfu_master_head"));
                Assert.That(result.Text, Does.Contain("This item has been modded to enhance your in-game experience."));
                Assert.That(result.Text, Does.Contain("#DOTA_Item_Master_of_the_Searing_Path_Head"));
                Assert.That(result.Text, Does.Contain("static_attributes"));
                Assert.That(result.Text, Does.Contain("particles/econ/items/ember_spirit/ember_ti10_cache/ember_ti10_cache_head.vpcf"));
                Assert.That(result.Text, Does.Not.Contain("Ember Spirit's Head Item"));
            });
        }

        [Test]
        public void Merge_WhenSameNumericIdExistsInNonItemSection_PreservesCosmeticItemModifications()
        {
            const string vanilla = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            ""name""                ""Ember Spirit's Head Item""
            ""prefab""              ""default_item""
            ""image_inventory""      ""econ/heroes/ember_spirit/back""
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
        }
    }
    ""kill_eater_score_types""
    {
        ""462""
        {
            ""type_name""           ""#DOTA_StatTracking_Headshots""
        }
    }
}";

            const string modded = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            ""name""                ""Master of the Searing Path Head""
            ""prefab""              ""default_item""
            ""image_inventory""      ""econ/items/ember_spirit/kungfu_master_head/kungfu_master_head""
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
        }
    }
    ""kill_eater_score_types""
    {
        ""462""
        {
            ""type_name""           ""#DOTA_StatTracking_Headshots""
        }
    }
}";

            var result = ItemsGameMerger.Merge(vanilla, modded, new[] { "462" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Applied, Is.EqualTo(1));
                Assert.That(result.Text, Does.Contain("Master of the Searing Path Head"));
                Assert.That(result.Text, Does.Contain("econ/items/ember_spirit/kungfu_master_head/kungfu_master_head"));
                Assert.That(result.Text, Does.Not.Contain("Ember Spirit's Head Item"));
                Assert.That(result.Text, Does.Contain("#DOTA_StatTracking_Headshots"));
            });
        }

        [Test]
        public void Merge_WhenBlockContainsLineCommentsWithQuotes_ParsesAndMergesSuccessfully()
        {
            const string vanilla = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            // ""Comment with quotes""
            ""name""                ""Ember Spirit's Head Item""
            ""prefab""              ""default_item""
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
        }
    }
}";

            const string modded = @"
""items_game""
{
    ""items""
    {
        ""462""
        {
            // ""Another comment with quotes""
            ""name""                ""Master of the Searing Path Head""
            ""prefab""              ""default_item""
            ""used_by_heroes""      { ""npc_dota_hero_ember_spirit"" ""1"" }
        }
    }
}";

            var result = ItemsGameMerger.Merge(vanilla, modded, new[] { "462" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Applied, Is.EqualTo(1));
                Assert.That(result.Text, Does.Contain("Master of the Searing Path Head"));
                Assert.That(result.Text, Does.Not.Contain("Ember Spirit's Head Item"));
            });
        }
    }
}
