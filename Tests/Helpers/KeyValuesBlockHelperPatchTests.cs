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
    }
}
