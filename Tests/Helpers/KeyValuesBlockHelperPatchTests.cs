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
    /// <summary>
    /// Tests for KeyValuesBlockHelper block extraction and replacement,
    /// focusing on short numeric IDs, hero filtering, and text normalization.
    /// </summary>
    [TestFixture]
    public class KeyValuesBlockHelperPatchTests
    {
        #region ExtractBlockById Tests

        /// <summary>
        /// Without hero filtering, ExtractBlockById returns the first matching block,
        /// which may be from a non-item section (demonstrating the false-match problem).
        /// </summary>
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
            // Without hero filtering, finds the first "99" block (score types)
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "99");
            Assert.That(block, Is.Not.Null);
        }

        /// <summary>
        /// With hero filtering, ExtractBlockById skips false matches
        /// and returns the correct item block for the specified hero.
        /// </summary>
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
            // With hero filtering, skips the score_types block and finds the item block
            var block = KeyValuesBlockHelper.ExtractBlockById(content, "99", "npc_dota_hero_invoker");
            Assert.That(block, Is.Not.Null);
            Assert.That(block, Does.Contain("\"Invoker's Hair\""));
            Assert.That(block, Does.Contain("\"used_by_heroes\""));
            Assert.That(block, Does.Not.Contain("\"Score_Type_99\""));
        }

        /// <summary>
        /// With wrong heroId, ExtractBlockById returns null.
        /// </summary>
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

        /// <summary>
        /// Verify that when the ID is unique, extraction works correctly.
        /// </summary>
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

        /// <summary>
        /// Verify ReplaceIdBlock correctly replaces a unique item block.
        /// </summary>
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

        /// <summary>
        /// ReplaceIdBlock with heroId only replaces the correct hero's block.
        /// </summary>
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

        #endregion

        #region ParseKvBlocks Tests

        /// <summary>
        /// Verify ParseKvBlocks correctly parses blocks from index.txt content.
        /// </summary>
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

        /// <summary>
        /// Verify ParseKvBlocks skips non-numeric block IDs.
        /// </summary>
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

        /// <summary>
        /// Verify smart quotes are normalized to standard ASCII quotes.
        /// </summary>
        [Test]
        public void NormalizeKvText_HandlesSmartQuotes()
        {
            // Smart left/right double quotes: \u201C and \u201D
            var input = "\u201Cname\u201D\t\t\u201Cvalue\u201D";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("\"name\"\t\t\"value\""));
        }

        /// <summary>
        /// Verify smart apostrophes are normalized to standard ASCII single quotes.
        /// </summary>
        [Test]
        public void NormalizeKvText_HandlesSmartApostrophes()
        {
            var input = "Invoker\u2019s Hair";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("Invoker's Hair"));
        }

        /// <summary>
        /// Verify zero-width characters are stripped.
        /// </summary>
        [Test]
        public void NormalizeKvText_StripsZeroWidthChars()
        {
            // \u200B = zero-width space, \u200D = zero-width joiner
            var input = "\"na\u200Bme\"\t\t\"\u200Dvalue\"";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("\"name\"\t\t\"value\""));
        }

        /// <summary>
        /// Verify non-breaking spaces are replaced with regular spaces.
        /// </summary>
        [Test]
        public void NormalizeKvText_ReplacesNonBreakingSpaces()
        {
            var input = "key\u00A0value";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            Assert.That(result, Is.EqualTo("key value"));
        }

        /// <summary>
        /// Verify BOM is stripped and line endings are normalized.
        /// </summary>
        [Test]
        public void NormalizeKvText_StripsBomAndNormalizesLineEndings()
        {
            var input = "\uFEFF\"key\"\t\t\"value\"\r\n\"key2\"\t\t\"value2\"";
            var result = KeyValuesBlockHelper.NormalizeKvText(input);

            // BOM should be stripped — result must start with the first real character
            Assert.That(result, Does.StartWith("\""));
            Assert.That(result, Does.Not.Contain("\r\n"));
            Assert.That(result, Does.Contain("\n"));
        }

        /// <summary>
        /// ParseKvBlocks works correctly on content with smart quotes after normalization.
        /// </summary>
        [Test]
        public void ParseKvBlocks_WithSmartQuotes_ParsesCorrectly()
        {
            // Smart quotes around the ID and values
            var indexContent = "\u201C99\u201D\n{\n\t\u201Cname\u201D\t\t\u201CInvoker's Hair\u201D\n\t\u201Cprefab\u201D\t\t\u201Cdefault_item\u201D\n}";

            var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks.ContainsKey("99"), Is.True);
        }

        #endregion

        #region Deeply Nested Block Tests

        /// <summary>
        /// Verify ParseKvBlocks correctly handles deeply nested structures like
        /// the Shadow Fiend arcana block (portrait_game with cameras sub-block).
        /// </summary>
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

        /// <summary>
        /// Verify ReplaceIdBlock correctly replaces a small default block
        /// with a much larger rich block (arcana-style).
        /// </summary>
        [Test]
        public void ReplaceIdBlock_SmallToLargeBlock_ReplacesCorrectly()
        {
            // Default block in items_game.txt (small)
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

            // Rich replacement block (much larger)
            var replacement = "\t\"826\"\n\t{\n\t\t\"name\"\t\t\"Shadow Fiend's Base\"\n\t\t\"prefab\"\t\t\"default_item\"\n\t\t\"item_rarity\"\t\t\"arcana\"\n\t\t\"item_slot\"\t\t\"hero_base\"\n\t\t\"used_by_heroes\"\n\t\t{\n\t\t\t\"npc_dota_hero_nevermore\"\t\t\"1\"\n\t\t}\n\t\t\"visuals\"\n\t\t{\n\t\t\t\"skip_model_combine\"\t\t\"1\"\n\t\t\t\"asset_modifier\"\n\t\t\t{\n\t\t\t\t\"type\"\t\t\"activity\"\n\t\t\t\t\"asset\"\t\t\"ALL\"\n\t\t\t\t\"modifier\"\t\t\"arcana\"\n\t\t\t}\n\t\t}\n\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "826", replacement, out bool didReplace, "npc_dota_hero_nevermore");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"item_rarity\""));
            Assert.That(result, Does.Contain("\"arcana\""));
            Assert.That(result, Does.Contain("\"activity\""));
            // Default values should be gone
            Assert.That(result, Does.Not.Contain("\"skip_model_combine\"\t\t\"0\""));
        }

        /// <summary>
        /// Verify extraction and replacement works with double-tab format
        /// (as produced by vkv_formatter.py).
        /// </summary>
        [Test]
        public void ReplaceIdBlock_DoubleTabFormat_ReplacesCorrectly()
        {
            // Double-tab format (vkv_formatter.py output)
            var content = "\"items\"\n{\n\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"item_slot\" \"hero_base\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t\t\t\"visuals\"\n\t\t\t\t{\n\t\t\t\t\t\t\"skip_model_combine\" \"0\"\n\t\t\t\t}\n\t\t}\n}";

            // Replacement block with rich content
            var replacement = "\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"item_rarity\" \"arcana\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t}";

            var result = KeyValuesBlockHelper.ReplaceIdBlock(content, "826", replacement, out bool didReplace, "npc_dota_hero_nevermore");

            Assert.That(didReplace, Is.True);
            Assert.That(result, Does.Contain("\"item_rarity\""));
            Assert.That(result, Does.Contain("\"arcana\""));
            Assert.That(result, Does.Not.Contain("\"skip_model_combine\""));
        }

        /// <summary>
        /// Verify ExtractBlockById works with double-tab format.
        /// </summary>
        [Test]
        public void ExtractBlockById_DoubleTabFormat_FindsBlock()
        {
            var content = "\"items\"\n{\n\t\t\"826\"\n\t\t{\n\t\t\t\t\"name\" \"Shadow Fiend's Base\"\n\t\t\t\t\"prefab\" \"default_item\"\n\t\t\t\t\"used_by_heroes\"\n\t\t\t\t{\n\t\t\t\t\t\t\"npc_dota_hero_nevermore\" \"1\"\n\t\t\t\t}\n\t\t}\n}";

            var block = KeyValuesBlockHelper.ExtractBlockById(content, "826", "npc_dota_hero_nevermore");

            Assert.That(block, Is.Not.Null);
            Assert.That(block, Does.Contain("\"Shadow Fiend's Base\""));
            Assert.That(block, Does.Contain("\"npc_dota_hero_nevermore\""));
        }

        #endregion
    }
}
