/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class KeyValuesBlockHelperPatchTests
    {
        #region ExtractBlockById Tests

        [Test]
        public void ExtractBlockById_ShortId_WithoutHeroId_ReturnsFirstMatch()
        {
            var content = @"
""DOTAEconomyItems""
{
	""kill_eater_score_types""
	{
		""99""
		{
			""type_name""		""#Score_Type_99""
			""level_data""		""score_level_data""
		}
	}
	""items""
	{
		""99""
		{
			""name""		""Invoker's Hair""
			""prefab""		""default_item""
			""item_slot""		""head""
			""model_player""		""models/heroes/invoker/invoker_hair.vmdl""
			""used_by_heroes""
			{
				""npc_dota_hero_invoker""		""1""
			}
		}
	}
}";
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "99");
            Assert.That(block, Is.Not.Null);
        }

        [Test]
        public void ExtractBlockById_ShortId_WithHeroId_SkipsFalseMatches()
        {
            var content = @"
""DOTAEconomyItems""
{
	""kill_eater_score_types""
	{
		""99""
		{
			""type_name""		""#Score_Type_99""
			""level_data""		""score_level_data""
		}
	}
	""items""
	{
		""99""
		{
			""name""		""Invoker's Hair""
			""prefab""		""default_item""
			""item_slot""		""head""
			""model_player""		""models/heroes/invoker/invoker_hair.vmdl""
			""used_by_heroes""
			{
				""npc_dota_hero_invoker""		""1""
			}
		}
	}
}";
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "99", "npc_dota_hero_invoker");
            Assert.That(block, Is.Not.Null);
            Assert.That(block, Does.Contain("\"Invoker's Hair\""));
            Assert.That(block, Does.Contain("\"used_by_heroes\""));
            Assert.That(block, Does.Not.Contain("\"Score_Type_99\""));
        }

        [Test]
        public void ExtractBlockById_WithWrongHeroId_ReturnsNull()
        {
            var content = @"
""items""
{
	""99""
	{
		""name""		""Invoker's Hair""
		""prefab""		""default_item""
		""used_by_heroes""
		{
			""npc_dota_hero_invoker""		""1""
		}
	}
}";
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "99", "npc_dota_hero_antimage");
            Assert.That(block, Is.Null);
        }

        [Test]
        public void ExtractBlockById_UniqueId_FindsCorrectBlock()
        {
            var content = @"
""DOTAEconomyItems""
{
	""items""
	{
		""12345""
		{
			""name""		""Test Item""
			""prefab""		""default_item""
			""item_slot""		""head""
			""used_by_heroes""
			{
				""npc_dota_hero_invoker""		""1""
			}
		}
	}
}";
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "12345");
            Assert.That(block, Is.Not.Null);
            Assert.That(block, Does.Contain("\"Test Item\""));
            Assert.That(block, Does.Contain("\"used_by_heroes\""));
        }

        #endregion

        #region ReplaceIdBlock Tests

        [Test]
        public void ReplaceIdBlock_UniqueId_ReplacesCorrectly()
        {
            var content = @"
""items""
{
	""12345""
	{
		""name""		""Original Item""
		""prefab""		""default_item""
		""used_by_heroes""
		{
			""npc_dota_hero_invoker""		""1""
		}
	}
}";
            var replacement = "\t\"12345\"\n\t{\n\t\t\"name\"\t\t\"Modified Item\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"item_rarity\"\t\t\"mythical\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_invoker\"\t\t\"1\"\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "12345", replacement);

            Assert.That(result, Does.Contain("\"Modified Item\""));
            Assert.That(result, Does.Contain("\"item_rarity\""));
            Assert.That(result, Does.Not.Contain("\"Original Item\""));
        }

        [Test]
        public void ReplaceIdBlock_WithHeroId_ReplacesCorrectBlock()
        {
            var content = @"
""kill_eater_score_types""
{
	""99""
	{
		""type_name""		""#Score_Type_99""
	}
}
""items""
{
	""99""
	{
		""name""		""Invoker's Hair""
		""prefab""		""default_item""
		""used_by_heroes""
		{
			""npc_dota_hero_invoker""		""1""
		}
	}
}";
            var replacement = "\t\"99\"\n\t{\n\t\t\"name\"\t\t\"Custom Hair\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_invoker\"\t\t\"1\"\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "99", replacement, out bool didReplace, "npc_dota_hero_invoker");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"Custom Hair\""));
            Assert.That(result, Does.Contain("Score_Type_99"), "Score types block should be preserved");
        }

        [Test]
        public void ReplaceIdBlock_WithRequirePrefab_SkipsNonItemCollision()
        {
            var content = "\"recipes\"\n{\n\t\"202\"\n\t{\n\t\t\"name\"\t\t\"some recipe entry that is long enough to look like an item block\"\n\t\t\"item_class\"\t\t\"recipe\"\n\t}\n}\n\"items\"\n{\n\t\"202\"\n\t{\n\t\t\"name\"\t\t\"Default Cursor Pack\"\n\t\t\"prefab\"\t\t\"cursor_pack\"\n\t\t\"item_slot\"\t\t\"weapon\"\n\t}\n}";
            var replacement = "\t\"202\"\n\t{\n\t\t\"name\"\t\t\"Emerald Sea Cursor Pack\"\n\t\t\"prefab\"\t\t\"cursor_pack\"\n\t\t\"item_rarity\"\t\t\"rare\"\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "202", replacement, out bool didReplace, requireItemMarkers: true, requirePrefab: "cursor_pack");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"Emerald Sea Cursor Pack\""), "Real cursor item must be replaced");
            Assert.That(result, Does.Not.Contain("\"Default Cursor Pack\""), "Vanilla cursor must be gone");
            Assert.That(result, Does.Contain("\"some recipe entry"), "Decoy non-item 202 block must be untouched");
        }

        [Test]
        public void ReplaceIdBlock_NoPrefab_ReplacesFirstCollision()
        {
            var content = "\"recipes\"\n{\n\t\"202\"\n\t{\n\t\t\"name\"\t\t\"some recipe entry that is long enough to look like an item block\"\n\t\t\"item_class\"\t\t\"recipe\"\n\t}\n}\n\"items\"\n{\n\t\"202\"\n\t{\n\t\t\"name\"\t\t\"Default Cursor Pack\"\n\t\t\"prefab\"\t\t\"cursor_pack\"\n\t}\n}";
            var replacement = "\t\"202\"\n\t{\n\t\t\"name\"\t\t\"Emerald Sea Cursor Pack\"\n\t\t\"prefab\"\t\t\"cursor_pack\"\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "202", replacement, out bool didReplace);

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"Default Cursor Pack\""));
        }

        #endregion

        #region ParseKvBlocks Tests

        [Test]
        public void ParseKvBlocks_ParsesNumericIdBlocks()
        {
            var indexContent = @"
        ""99""
        {
            ""name""		""Invoker's Hair""
            ""prefab""		""default_item""
            ""used_by_heroes""
            {
                ""npc_dota_hero_invoker""		""1""
            }
        }
        ""100""
        {
            ""name""		""Another Item""
            ""prefab""		""default_item""
        }";

            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);

            Assert.That(blocks.Count, Is.EqualTo(2));
            Assert.That(blocks.ContainsKey("99"), Is.True);
            Assert.That(blocks.ContainsKey("100"), Is.True);
            Assert.That(blocks["99"], Does.Contain("\"Invoker's Hair\""));
        }

        [Test]
        public void ParseKvBlocks_SkipsNonNumericIds()
        {
            var indexContent = @"
        ""items""
        {
            ""name""		""Container""
        }
        ""99""
        {
            ""name""		""Item 99""
            ""prefab""		""default_item""
        }";

            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks.ContainsKey("99"), Is.True);
            Assert.That(blocks.ContainsKey("items"), Is.False);
        }

        #endregion

        #region NormalizeKvText Tests

        [Test]
        public void NormalizeKvText_HandlesSmartQuotes()
        {
            var input = "\u201Cname\u201D\t\t\u201Cvalue\u201D";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("\"name\"\t\t\"value\""));
        }

        [Test]
        public void NormalizeKvText_HandlesSmartApostrophes()
        {
            var input = "Invoker\u2019s Hair";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("Invoker's Hair"));
        }

        [Test]
        public void NormalizeKvText_StripsZeroWidthChars()
        {
            var input = "\"na\u200Bme\"\t\t\"\u200Dvalue\"";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("\"name\"\t\t\"value\""));
        }

        [Test]
        public void NormalizeKvText_ReplacesNonBreakingSpaces()
        {
            var input = "key\u00A0value";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("key value"));
        }

        [Test]
        public void NormalizeKvText_StripsBomAndNormalizesLineEndings()
        {
            var input = "\uFEFF\"key\"\t\t\"value\"\r\n\"key2\"\t\t\"value2\"";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Does.StartWith("\""));
            Assert.That(result, Does.Not.Contain("\r\n"));
            Assert.That(result, Does.Contain("\n"));
        }

        [Test]
        public void ParseKvBlocks_WithSmartQuotes_ParsesCorrectly()
        {
            var indexContent = "\u201C99\u201D\n{\n\t\u201Cname\u201D\t\t\u201CInvoker's Hair\u201D\n\t\u201Cprefab\u201D\t\t\u201Cdefault_item\u201D\n}";

            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks.ContainsKey("99"), Is.True);
        }

        #endregion

        #region Deeply Nested Block Tests

        [Test]
        public void ParseKvBlocks_DeeplyNestedBlock_ExtractsCorrectly()
        {
            var indexContent = @"
""826""
{
	""name""	""Shadow Fiend's Base""
	""prefab""	""default_item""
	""item_rarity""	""arcana""
	""item_slot""	""hero_base""
	""used_by_heroes""
	{
		""npc_dota_hero_nevermore""	""1""
	}
	""visuals""
	{
		""skip_model_combine""	""1""
		""asset_modifier""
		{
			""type""	""activity""
			""asset""	""ALL""
			""modifier""	""arcana""
		}
		""asset_modifier""
		{
			""type""	""particle""
			""asset""	""particles/units/heroes/hero_nevermore/nevermore_shadowraze.vpcf""
			""modifier""	""particles/econ/items/shadow_fiend/sf_fire_arcana/sf_fire_arcana_shadowraze.vpcf""
		}
		""asset_modifier""
		{
			""type""	""portrait_game""
			""asset""	""models/heroes/shadow_fiend/shadow_fiend.vmdl""
			""modifier""
			{
				""PortraitLightPosition""	""85.00 9.68 296.06""
				""cameras""
				{
					""default""
					{
						""PortraitPosition""	""126.07 -14.40 175.18""
						""PortraitFOV""	""33""
					}
				}
			}
		}
		""asset_modifier0""
		{
			""type""	""particle_create""
			""modifier""	""""
		}
	}
}";
            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks.ContainsKey("826"), Is.True);
            Assert.That(blocks["826"], Does.Contain("\"item_rarity\""));
            Assert.That(blocks["826"], Does.Contain("\"arcana\""));
            Assert.That(blocks["826"], Does.Contain("\"portrait_game\""));
            Assert.That(blocks["826"], Does.Contain("\"cameras\""));
            Assert.That(blocks["826"], Does.Contain("\"PortraitFOV\""));
            Assert.That(blocks["826"], Does.Contain("\"particle_create\""));
        }

        [Test]
        public void ReplaceIdBlock_SmallToLargeBlock_ReplacesCorrectly()
        {
            var content = @"
""items""
{
	""826""
	{
		""name""		""Shadow Fiend's Base""
		""prefab""		""default_item""
		""item_slot""		""hero_base""
		""used_by_heroes""
		{
			""npc_dota_hero_nevermore""		""1""
		}
		""visuals""
		{
			""skip_model_combine""		""0""
		}
	}
}";

            var replacement = "\t\"826\"\n\t{\n\t\t\"name\"\t\t\"Shadow Fiend's Base\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"item_slot\"\t\t\"hero_base\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_nevermore\"\t\t\"1\"\n\t\t}\n\t\t\"visuals\"\n\t\t{\n\t\t\t\"skip_model_combine\"\t\t\"1\"\n\t\t\t\"asset_modifier\"\n\t\t\t{\n\t\t\t\t\"type\"\t\t\"activity\"\n\t\t\t\t\"asset\"\t\t\"ALL\"\n\t\t\t\t\"modifier\"\t\t\"arcana\"\n\t\t\t}\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "826", replacement, out bool didReplace, "npc_dota_hero_nevermore");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"item_rarity\""));
            Assert.That(result, Does.Contain("\"arcana\""));
            Assert.That(result, Does.Contain("\"activity\""));
            Assert.That(result, Does.Not.Contain("\"skip_model_combine\"\t\t\"0\""));
        }

        [Test]
        public void ReplaceIdBlock_DoubleTabFormat_ReplacesCorrectly()
        {
            var content = "\"items\"\n{\n\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"item_slot\" \"hero_base\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t\t\t\"visuals\"\n\t\t\t\t{\n\t\t\t\t\t\t\"skip_model_combine\" \"0\"\n\t\t\t\t}\n\t\t}\n}";

            var replacement = "\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"item_rarity\" \"arcana\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "826", replacement, out bool didReplace, "npc_dota_hero_nevermore");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"item_rarity\""));
            Assert.That(result, Does.Contain("\"arcana\""));
            Assert.That(result, Does.Not.Contain("\"skip_model_combine\""));
        }

        [Test]
        public void ExtractBlockById_DoubleTabFormat_FindsBlock()
        {
            var content = "\"items\"\n{\n\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t}\n}";

            var block = KeyValuesBlockHelper.ExtractBlockById(content, "826", "npc_dota_hero_nevermore");

            Assert.That(block, Is.Not.Null);
            Assert.That(block, Does.Contain("\"Shadow Fiend's Base\""));
            Assert.That(block, Does.Contain("\"npc_dota_hero_nevermore\""));
        }

        [Test]
        public void ReplaceIdBlock_NeverProducesCRLF()
        {
            var content = "\"items\"\n{\n\t\"12345\"\n\t{\n\t\t\"name\"\t\t\"Original\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_invoker\"\t\t\"1\"\n\t\t}\n\t}\n}";
            var replacement = "\t\"12345\"\n\t{\n\t\t\"name\"\t\t\"Modified\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_invoker\"\t\t\"1\"\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "12345", replacement, out bool didReplace, "npc_dota_hero_invoker");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Not.Contain("\r"),
                "ReplaceIdBlock must use LF-only line endings for Source 2 compatibility");
        }

        [Test]
        public void PrettifyKvText_NeverProducesCRLF()
        {
            var oneliner = "\"items\" { \"12345\" { \"name\" \"Test\" \"prefab\" \"default_item\" \"used_by_heroes\" { \"npc_dota_hero_invoker\" \"1\" } } }";

            var result = KeyValuesBlockHelper.PrettifyKvText(oneliner);

            Assert.That(result, Does.Not.Contain("\r"),
                "PrettifyKvText must use LF-only line endings for Source 2 compatibility");
            Assert.That(result, Does.Contain("\n"),
                "Output should contain LF newlines");
        }

        [Test]
        public void PrettifyKvText_OneLiner_ProducesDoubleTabSeparation()
        {
            var oneliner = "\"555\" { \"name\" \"Weather Snow\" \"prefab\" \"default_item\" }";

            var result = KeyValuesBlockHelper.PrettifyKvText(oneliner);

            Assert.That(result, Does.Contain("\"name\"\t\t\"Weather Snow\""),
                "Key-value pairs must be separated by double-tab (\\t\\t)");
            Assert.That(result, Does.Contain("\"prefab\"\t\t\"default_item\""),
                "Key-value pairs must be separated by double-tab (\\t\\t)");
        }

        [Test]
        public void PrettifyKvText_OneLiner_ProducesCorrectIndentation()
        {
            var oneliner = "\"items\" { \"99\" { \"name\" \"Hair\" \"used_by_heroes\" { \"npc_dota_hero_invoker\" \"1\" } } }";

            var result = KeyValuesBlockHelper.PrettifyKvText(oneliner);
            var lines = result.Split('\n');

            Assert.That(result, Does.Contain("\"items\""));
            Assert.That(result, Does.Contain("\t\"99\""), "ID should be at indent level 1");
            Assert.That(result, Does.Contain("\t\t\"name\"\t\t\"Hair\""), "Properties at indent level 2");
            Assert.That(result, Does.Contain("\t\t\"used_by_heroes\""), "Sub-block key at indent level 2");
            Assert.That(result, Does.Contain("\t\t\t\"npc_dota_hero_invoker\"\t\t\"1\""), "Inner property at indent level 3");
        }

        [Test]
        public void PrettifyKvText_RoundTrip_ExtractBlockStillWorks()
        {
            var oneliner = "\"DOTAEconomyItems\" { \"items\" { \"555\" { \"name\" \"Weather Snow\" \"prefab\" \"default_item\" \"item_slot\" \"weather\" \"used_by_heroes\" { \"npc_dota_hero_base\" \"1\" } } \"590\" { \"name\" \"Map Terrain\" \"prefab\" \"default_item\" \"item_slot\" \"map\" } } }";

            var prettified = KeyValuesBlockHelper.PrettifyKvText(oneliner);

            var block555 = KeyValuesBlockHelper.ExtractBlockById(prettified, "555");
            var block590 = KeyValuesBlockHelper.ExtractBlockById(prettified, "590");

            Assert.That(block555, Is.Not.Null, "Block 555 must be extractable from prettified output");
            Assert.That(block555, Does.Contain("\"Weather Snow\""));
            Assert.That(block555, Does.Contain("\"npc_dota_hero_base\""));

            Assert.That(block590, Is.Not.Null, "Block 590 must be extractable from prettified output");
            Assert.That(block590, Does.Contain("\"Map Terrain\""));
        }

        [Test]
        public void PrettifyKvText_ThenReplaceIdBlock_ProducesValidOutput()
        {
            var oneliner = "\"DOTAEconomyItems\" { \"items\" { \"555\" { \"name\" \"Weather Snow\" \"prefab\" \"default_item\" } \"590\" { \"name\" \"Default Map\" \"prefab\" \"default_item\" } } }";

            var prettified = KeyValuesBlockHelper.PrettifyKvText(oneliner);

            var newBlock = "\t\t\"555\"\n\t\t{\n\t\t\t\"name\"\t\t\"Weather Aurora\"\n\t\t\t\"prefab\"\t\t\"default_item\"\n\t\t\t\"item_rarity\"\t\t\"mythical\"\n\t\t}";
            var result = KeyValuesBlockHelper.ReplaceIdBlock(prettified, "555", newBlock, out bool didReplace);

            Assert.That(didReplace, Is.True, "ReplaceIdBlock must find block in prettified content");
            Assert.That(result, Does.Contain("\"Weather Aurora\""), "New block content must be present");
            Assert.That(result, Does.Not.Contain("\"Weather Snow\""), "Old block content must be replaced");
            Assert.That(result, Does.Contain("\"Default Map\""), "Other blocks must be preserved");
            Assert.That(result, Does.Not.Contain("\r"), "No CRLF in final output");
        }

        #region MergeBlocks Tests

        [Test]
        public void MergeBlocks_DeepMergesDuplicateIDs_PreferringSecondBlock()
        {
            var arcanaBlock = @"""855""
{
	""name""		""Earthshaker's Base""
	""prefab""		""default_item""
	""creation_date""		""2019-05-24""
	""image_inventory""		""econ/items/earthshaker/arcana/earthshaker_arcana_style1""
	""item_name""		""#DOTA_Item_Planetfall""
	""item_rarity""		""arcana""
	""item_slot""		""hero_base""
	""portraits""
	{
		""game""
		{
			""cameras""
			{
				""default""
				{
					""PortraitPosition""		""215.323151 -70.693619 233.642761""
					""PortraitFOV""		""20""
				}
			}
		}
	}
	""visuals""
	{
		""skip_model_combine""		""1""
		""asset_modifier""
		{
			""type""		""arcana_level""
			""level""		""2""
		}
		""asset_modifier""
		{
			""type""		""particle""
			""asset""		""particles/units/heroes/hero_earthshaker/earthshaker_echoslam_start.vpcf""
			""modifier""		""particles/econ/items/earthshaker/earthshaker_arcana/earthshaker_arcana_echoslam_start_v2.vpcf""
		}
	}
}";

            var vanillaBlock = @"""855""
{
	""name""		""Earthshaker's Base""
	""prefab""		""default_item""
	""creation_date""		""2025-07-20""
	""image_inventory""		""econ/heroes/earthshaker/base""
	""item_name""		""#DOTA_Item_Earthshakers_Base""
	""item_slot""		""hero_base""
	""item_type_name""		""#DOTA_WearableType_Base""
	""used_by_heroes""
	{
		""npc_dota_hero_earthshaker""		""1""
	}
	""visuals""
	{
		""skip_model_combine""		""0""
	}
}";

            var merged = KeyValuesBlockHelper.MergeBlocks(vanillaBlock, arcanaBlock, preferRightSideStrongly: true);
            
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"arcana\""));
            Assert.That(merged, Does.Contain("\"image_inventory\"\t\t\"econ/items/earthshaker/arcana/earthshaker_arcana_style1\""));
            Assert.That(merged, Does.Contain("\"item_name\"\t\t\"#DOTA_Item_Planetfall\""));
            Assert.That(merged, Does.Contain("\"skip_model_combine\"\t\t\"1\""));
            Assert.That(merged, Does.Contain("\"portraits\""));
            Assert.That(merged, Does.Contain("\"arcana_level\""));
            Assert.That(merged, Does.Contain("\"used_by_heroes\""));
            Assert.That(merged, Does.Contain("\"npc_dota_hero_earthshaker\""));
        }

        [Test]
        public void MergeBlocks_EarthshakersBracers_PrefersModOverVanilla()
        {
            var vanillaBlock = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""prefab""		""default_item""
	""creation_date""		""2013-06-26""
	""image_inventory""		""econ/heroes/earthshaker/bracers""
	""item_name""		""#DOTA_Item_Earthshakers_Bracers""
	""item_slot""		""arms""
	""item_type_name""		""#DOTA_WearableType_Bracer""
	""used_by_heroes""
	{
		""npc_dota_hero_earthshaker""		""1""
	}
	""visuals""
	{
		""skip_model_combine""		""0""
        ""asset_modifier""
        {
            ""type""      ""particle_create""
            ""modifier""  ""particles/foo.vpcf""
        }
	}
}";

            var modBlock = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""prefab""		""default_item""
	""creation_date""		""2013-06-26""
	""event_id""		""EVENT_ID_INTERNATIONAL_2016""
	""image_inventory""		""econ/items/earthshaker/ti6_immortal_bracer/mesh/ti6_immortal_bracer_style1""
	""item_description""		""This item has been modded...""
	""item_name""		""#DOTA_Item_Bracers_of_the_Cavern_Luminar""
	""item_rarity""		""immortal""
	""item_slot""		""arms""
	""item_type_name""		""#DOTA_WearableType_Bracer""
	""model_player""		""models/heroes/earthshaker/bracers.vmdl""
    ""visuals""
    {
        ""skip_model_combine""  ""1""
        ""asset_modifier""
        {
            ""type""      ""particle_create""
            ""modifier""  ""particles/bar.vpcf""
        }
    }
}";

            var merged = KeyValuesBlockHelper.MergeBlocks(vanillaBlock, modBlock, preferRightSideStrongly: true);

            Assert.That(merged, Does.Contain("\"image_inventory\"\t\t\"econ/items/earthshaker/ti6_immortal_bracer/mesh/ti6_immortal_bracer_style1\""));
            Assert.That(merged, Does.Contain("\"item_name\"\t\t\"#DOTA_Item_Bracers_of_the_Cavern_Luminar\""));
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"immortal\""));
            Assert.That(merged, Does.Contain("\"event_id\"\t\t\"EVENT_ID_INTERNATIONAL_2016\""));
            Assert.That(merged, Does.Contain("\"model_player\"\t\t\"models/heroes/earthshaker/bracers.vmdl\""));
            
            Assert.That(merged, Does.Contain("\"particles/bar.vpcf\""));
            Assert.That(merged, Does.Not.Contain("\"particles/foo.vpcf\""));
            
            Assert.That(merged, Does.Contain("\"skip_model_combine\"\t\t\"1\""));
            
            Assert.That(merged, Does.Contain("\"used_by_heroes\""));
            Assert.That(merged, Does.Contain("\"npc_dota_hero_earthshaker\""));
        }

        [Test]
        public void MergeBlocks_WithNullOrEmpty_ReturnsOther()
        {
            var blockA = @"""855"" { ""name"" ""A"" }";
            Assert.That(KeyValuesBlockHelper.MergeBlocks(blockA, null), Is.EqualTo(blockA));
            Assert.That(KeyValuesBlockHelper.MergeBlocks(null, blockA), Is.EqualTo(blockA));
        }

        [Test]
        public void MergeBlocks_FullReal460Blocks_MergesCorrectly()
        {
            var vanillaBlock = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""prefab""		""default_item""
	""creation_date""		""2013-06-26""
	""image_inventory""		""econ/heroes/earthshaker/bracers""
	""item_name""		""#DOTA_Item_Earthshakers_Bracers""
	""item_slot""		""arms""
	""item_type_name""		""#DOTA_WearableType_Bracer""
	""hero_presets""
	{
		""npc_dota_hero_earthshaker""
		{
			""1""
			{
				""asset_modifier""
				{
					""type""		""particle_create""
					""modifier""		""particles/units/heroes/hero_earthshaker/earthshaker_totem_cast.vpcf""
				}
			}
		}
	}
	""used_by_heroes""
	{
		""npc_dota_hero_earthshaker""		""1""
	}
	""visuals""
	{
		""skip_model_combine""		""0""
		""asset_modifier0""
		{
			""type""		""particle_create""
			""modifier""		""particles/units/heroes/hero_earthshaker/earthshaker_totem_cast.vpcf""
		}
	}
}";

            var modBlock = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""prefab""		""default_item""
	""creation_date""		""2013-06-26""
	""event_id""		""EVENT_ID_INTERNATIONAL_2016""
	""image_inventory""		""econ/items/earthshaker/ti6_immortal_bracer/mesh/ti6_immortal_bracer_style1""
	""item_description""		""This item has been modded...""
	""item_name""		""#DOTA_Item_Bracers_of_the_Cavern_Luminar""
	""item_rarity""		""immortal""
	""item_slot""		""arms""
	""item_type_name""		""#DOTA_WearableType_Bracer""
	""model_player""		""models/heroes/earthshaker/bracers.vmdl""
	""visuals""
	{
		""skip_model_combine""		""1""
		""asset_modifier""
		{
			""type""		""arcana_level""
			""level""		""2""
		}
		""asset_modifier""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/items/earthshaker/earthshaker_arcana/earthshaker_arcana_totem_cast_v2.vpcf""
		}
	}
}";

            var merged = KeyValuesBlockHelper.MergeBlocks(vanillaBlock, modBlock, preferRightSideStrongly: true);

            Assert.That(merged, Does.Contain("\"image_inventory\"\t\t\"econ/items/earthshaker/ti6_immortal_bracer/mesh/ti6_immortal_bracer_style1\""));
            Assert.That(merged, Does.Contain("\"item_name\"\t\t\"#DOTA_Item_Bracers_of_the_Cavern_Luminar\""));
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"immortal\""));
            Assert.That(merged, Does.Contain("\"item_type_name\""));
            
            Assert.That(merged, Does.Contain("\"hero_presets\""));
            
            Assert.That(merged, Does.Contain("\"arcana_level\""));
            Assert.That(merged, Does.Contain("\"asset_modifier\""));
            
            Assert.That(merged, Does.Contain("\"asset_modifier0\""));
            
            Assert.That(merged, Does.Not.Contain("\r"));
        }

        #endregion

        #region OverlayBlockPreservingStructure Tests

        [Test]
        public void Overlay_NoVanillaOnlyKeys_ReturnsIndexBlockVerbatim()
        {
            var indexBlock = "\t\"140\"\n\t{\n\t\t\"name\"\t\t\"PA Weapon\"\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t}";
            var vanillaBlock = "\t\"140\"\n\t{\n\t\t\"name\"\t\t\"Vanilla\"\n\t\t\"item_rarity\"\t\t\"mythical\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.OverlayBlockPreservingStructure(vanillaBlock, indexBlock);

            Assert.That(result, Is.EqualTo(indexBlock), "Index block must be used verbatim when no vanilla-only keys exist");
        }

        [Test]
        public void Overlay_CarriesEssentialKeys_DropsCosmetic()
        {
            var indexBlock = "\t\"140\"\n\t{\n\t\t\"name\"\t\t\"PA Weapon\"\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t}";
            var vanillaBlock = "\t\"140\"\n\t{\n\t\t\"name\"\t\t\"Vanilla\"\n\t\t\"hero_presets\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\n\t\t\t{\n\t\t\t\t\"1\"\t\t\"x\"\n\t\t\t}\n\t\t}\n\t\t\"creation_date\"\t\t\"2020-09-28\"\n\t}";

            var result = KeyValuesBlockHelper.OverlayBlockPreservingStructure(vanillaBlock, indexBlock);

            Assert.That(result, Does.Contain("\"item_rarity\"\t\t\"arcana\""));
            Assert.That(result, Does.Contain("\"hero_presets\""));
            Assert.That(result, Does.Not.Contain("\"creation_date\""));
            Assert.That(result.TrimEnd().EndsWith("}"), Is.True);
        }

        [Test]
        public void Overlay_IndexHasEssentials_IgnoresVanillaCosmetic_ReturnsVerbatim()
        {
            var indexBlock = "\t\"140\"\n\t{\n\t\t\"image_inventory\"\t\t\"econ/items/phantom_assassin/manifold_paradox/arcana_pa_style2\"\n\t\t\"item_name\"\t\t\"#DOTA_Item_Manifold_Paradox\"\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t}";
            var vanillaBlock = "\t\"140\"\n\t{\n\t\t\"image_inventory\"\t\t\"econ/items/phantom_assassin/assassination_of_dark_feather_weapon/assassination_of_dark_feather_weapon\"\n\t\t\"item_name\"\t\t\"#DOTA_Item_Darkfeather_Factioneer__Weapon\"\n\t\t\"item_rarity\"\t\t\"mythical\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t\t\"creation_date\"\t\t\"2020-09-28\"\n\t}";

            var result = KeyValuesBlockHelper.OverlayBlockPreservingStructure(vanillaBlock, indexBlock);

            Assert.That(result, Is.EqualTo(indexBlock), "Index block must be used verbatim, ignoring vanilla cosmetic keys");
            Assert.That(result, Does.Not.Contain("dark_feather"));
            Assert.That(result, Does.Not.Contain("\"creation_date\""));
        }

        [Test]
        public void Overlay_PreservesIndexKeyOrder()
        {
            var indexBlock = "\t\"140\"\n\t{\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"item_type_name\"\t\t\"#DOTA_WearableType_Parallel_Blades\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t}";
            var vanillaBlock = "\t\"140\"\n\t{\n\t\t\"item_type_name\"\t\t\"#DOTA_WearableType_weapon\"\n\t\t\"item_rarity\"\t\t\"mythical\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_phantom_assassin\"\t\t\"1\"\n\t\t}\n\t\t\"creation_date\"\t\t\"2020-09-28\"\n\t}";

            var result = KeyValuesBlockHelper.OverlayBlockPreservingStructure(vanillaBlock, indexBlock);

            int rarityPos = result.IndexOf("\"item_rarity\"", StringComparison.Ordinal);
            int typePos = result.IndexOf("\"item_type_name\"", StringComparison.Ordinal);
            Assert.That(rarityPos, Is.GreaterThan(-1));
            Assert.That(typePos, Is.GreaterThan(rarityPos), "item_rarity must stay before item_type_name (index order)");
            Assert.That(result, Does.Contain("\"item_rarity\"\t\t\"arcana\""));
            Assert.That(result, Does.Not.Contain("\"item_rarity\"\t\t\"mythical\""));
        }

        [Test]
        public void Overlay_NeverProducesCRLF()
        {
            var indexBlock = "\t\"140\"\r\n\t{\r\n\t\t\"name\"\t\t\"PA\"\r\n\t}";
            var vanillaBlock = "\t\"140\"\r\n\t{\r\n\t\t\"name\"\t\t\"Vanilla\"\r\n\t\t\"item_slot\"\t\t\"weapon\"\r\n\t}";

            var result = KeyValuesBlockHelper.OverlayBlockPreservingStructure(vanillaBlock, indexBlock);

            Assert.That(result, Does.Not.Contain("\r"), "Overlay must emit LF-only line endings");
            Assert.That(result, Does.Contain("\"item_slot\""), "Essential key carried over");
        }

        [Test]
        public void Overlay_NullOrEmpty_ReturnsOther()
        {
            var block = "\t\"140\"\n\t{\n\t\t\"name\"\t\t\"X\"\n\t}";
            Assert.That(KeyValuesBlockHelper.OverlayBlockPreservingStructure(block, null), Is.EqualTo(block));
            Assert.That(KeyValuesBlockHelper.OverlayBlockPreservingStructure(null, block), Is.EqualTo(block));
        }

        #endregion

        #endregion

        #region TryGetTopLevelValue / AnyBlockHasItemSlot Tests

        [Test]
        public void TryGetTopLevelValue_TopLevelKey_ReturnsValue()
        {
            var block = "\"855\"\n{\n\t\"name\"\t\t\"Earthshaker's Base\"\n\t\"item_slot\"\t\t\"hero_base\"\n}";

            bool ok = KeyValuesBlockHelper.TryGetTopLevelValue(block, "item_slot", out var value);

            Assert.That(ok, Is.True);
            Assert.That(value, Is.EqualTo("hero_base"));
        }

        [Test]
        public void TryGetTopLevelValue_NestedKey_NotReturned()
        {
            var block = "\"855\"\n{\n\t\"item_slot\"\t\t\"weapon\"\n\t\"visuals\"\n\t{\n\t\t\"item_slot\"\t\t\"hero_base\"\n\t}\n}";

            bool ok = KeyValuesBlockHelper.TryGetTopLevelValue(block, "item_slot", out var value);

            Assert.That(ok, Is.True);
            Assert.That(value, Is.EqualTo("weapon"), "Top-level item_slot wins over the nested one");
        }

        [Test]
        public void TryGetTopLevelValue_EmptyOrMalformed_ReturnsFalse()
        {
            Assert.That(KeyValuesBlockHelper.TryGetTopLevelValue("", "item_slot", out _), Is.False);
            Assert.That(KeyValuesBlockHelper.TryGetTopLevelValue("\"855\"\n{\n\t\"item_slot\"", "item_slot", out _), Is.False);
        }

        [Test]
        public void AnyBlockHasItemSlot_TopLevelMatch_ReturnsTrue()
        {
            var index = "\"460\"\n{\n\t\"item_slot\"\t\t\"arms\"\n}\n\"855\"\n{\n\t\"item_slot\"\t\t\"hero_base\"\n}";

            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(index, "hero_base"), Is.True);
        }

        [Test]
        public void AnyBlockHasItemSlot_NestedOnly_ReturnsFalse()
        {
            var index = "\"460\"\n{\n\t\"item_slot\"\t\t\"arms\"\n\t\"visuals\"\n\t{\n\t\t\"asset\"\t\t\"hero_base\"\n\t}\n}";

            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(index, "hero_base"), Is.False);
        }

        [Test]
        public void AnyBlockHasItemSlot_NoMatch_ReturnsFalse()
        {
            var index = "\"460\"\n{\n\t\"item_slot\"\t\t\"weapon\"\n}";

            Assert.That(KeyValuesBlockHelper.AnyBlockHasItemSlot(index, "hero_base"), Is.False);
        }

        #endregion
    }
}
