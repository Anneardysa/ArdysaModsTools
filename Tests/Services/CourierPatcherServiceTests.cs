/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for CourierPatcherService: KV block parsing, model mapping, and block merging.
    /// </summary>
    [TestFixture]
    public class CourierPatcherServiceTests
    {
        #region Test Data

        /// <summary>
        /// Drodo courier block — 2 model courier (ground + flying), same model for radiant/dire.
        /// </summary>
        private const string DrodoBlock = @"
""10003""
{
	""name""		""Drodo the Druffin""
	""prefab""		""courier""
	""image_inventory""		""econ/courier/drodo/drodo""
	""item_description""		""#DOTA_Item_Desc_Drodo_the_Druffin""
	""item_name""		""#DOTA_Item_Drodo_the_Druffin""
	""item_quality""		""unusual""
	""item_rarity""		""immortal""
	""item_type_name""		""#DOTA_WearableType_International_Courier""
	""particle_folder""		""particles/econ/courier/courier_drodo""
	""portraits""
	{
		""icon""
		{
			""PortraitLightPosition""		""77.508858 -40.010113 177.399033""
			""PortraitAnimationActivity""		""ACT_DOTA_IDLE""
			""cameras""
			{
				""default""
				{
					""PortraitPosition""		""264.737152 -227.063721 147.023560""
					""PortraitFOV""		""37.000000""
				}
			}
		}
	}
	""visuals""
	{
		""asset_modifier2""
		{
			""type""		""courier""
			""modifier""		""models/courier/drodo/drodo.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/courier/drodo/drodo.vmdl""
			""asset""		""dire""
		}
		""asset_modifier1""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/drodo/drodo_flying.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier3""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/drodo/drodo_flying.vmdl""
			""asset""		""dire""
		}
		""asset_modifier4""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/courier_drodo/courier_drodo_ambient.vpcf""
		}
	}
}";

        /// <summary>
        /// Default Courier block (ID 595).
        /// </summary>
        private const string DefaultCourierBlock = @"
""595""
{
	""name""		""Default Courier""
	""prefab""		""courier""
	""baseitem""		""1""
	""creation_date""		""2015-07-24""
	""image_inventory""		""econ/courier/donkey_radiant_default""
	""item_name""		""#DOTA_Item_Default_Courier""
	""item_quality""		""base""
	""portraits""
	{
		""icon""
		{
			""PortraitLightPosition""		""301.568268 -151.132080 297.993896""
			""PortraitAnimationActivity""		""ACT_DOTA_IDLE""
			""cameras""
			{
				""default""
				{
					""PortraitPosition""		""254.894318 -275.662659 264.834534""
					""PortraitFOV""		""30""
				}
			}
		}
	}
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/props_gameplay/donkey.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier1""
		{
			""type""		""courier""
			""modifier""		""models/props_gameplay/donkey_dire.vmdl""
			""asset""		""dire""
		}
		""asset_modifier2""
		{
			""type""		""courier_flying""
			""modifier""		""models/props_gameplay/donkey_wings.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier3""
		{
			""type""		""courier_flying""
			""modifier""		""models/props_gameplay/donkey_dire_wings.vmdl""
			""asset""		""dire""
		}
	}
}";

        /// <summary>
        /// Simple single-model courier for edge case testing.
        /// </summary>
        private const string SingleModelCourier = @"
""20000""
{
	""name""		""Single Model Courier""
	""prefab""		""courier""
	""item_quality""		""rare""
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/courier/single/single.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier1""
		{
			""type""		""courier""
			""modifier""		""models/courier/single/single.vmdl""
			""asset""		""dire""
		}
	}
}";

        /// <summary>
        /// Styled courier with different models per style (like Onibi).
        /// </summary>
        private const string StyledCourier = @"
""30000""
{
	""name""		""Styled Courier""
	""prefab""		""courier""
	""item_quality""		""rare""
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/courier/styled/styled_lvl0.vmdl""
			""asset""		""radiant""
			""style""		""0""
		}
		""asset_modifier1""
		{
			""type""		""courier""
			""modifier""		""models/courier/styled/styled_lvl0.vmdl""
			""asset""		""dire""
			""style""		""0""
		}
		""asset_modifier2""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/styled/styled_lvl0_flying.vmdl""
			""asset""		""radiant""
			""style""		""0""
		}
		""asset_modifier3""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/styled/styled_lvl0_flying.vmdl""
			""asset""		""dire""
			""style""		""0""
		}
		""asset_modifier4""
		{
			""type""		""courier""
			""modifier""		""models/courier/styled/styled_lvl5.vmdl""
			""asset""		""radiant""
			""style""		""5""
		}
		""asset_modifier5""
		{
			""type""		""courier""
			""modifier""		""models/courier/styled/styled_lvl5.vmdl""
			""asset""		""dire""
			""style""		""5""
		}
		""asset_modifier6""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/styled/styled_lvl5_flying.vmdl""
			""asset""		""radiant""
			""style""		""5""
		}
		""asset_modifier7""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/styled/styled_lvl5_flying.vmdl""
			""asset""		""dire""
			""style""		""5""
		}
		""styles""
		{
			""0""
			{
				""name""		""Default""
			}
			""5""
			{
				""name""		""Level 5""
			}
		}
	}
}";

        /// <summary>
        /// Courier with "skin" field in visuals (like Golden Baby Roshan).
        /// Uses same model as base Baby Roshan but with skin index for different appearance.
        /// </summary>
        private const string GoldenBabyRoshanBlock = @"
""10096""
{
	""name""		""Golden Baby Roshan""
	""prefab""		""courier""
	""image_inventory""		""econ/courier/baby_rosh/babyroshan1""
	""item_name""		""#DOTA_Item_Golden_Baby_Roshan""
	""item_quality""		""unusual""
	""item_rarity""		""immortal""
	""visuals""
	{
		""skin""		""1""
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/courier/baby_rosh/babyroshan.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier1""
		{
			""type""		""courier""
			""modifier""		""models/courier/baby_rosh/babyroshan.vmdl""
			""asset""		""dire""
		}
		""asset_modifier2""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/baby_rosh/babyroshan_flying.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier3""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/baby_rosh/babyroshan_flying.vmdl""
			""asset""		""dire""
		}
	}
}";

        #endregion

        #region ParseCourierVisuals Tests

        [Test]
        public void ParseCourierVisuals_2Models_ExtractsCorrectly()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);

            Assert.That(models, Has.Count.EqualTo(4)); // 4 entries total (2 unique models)
            Assert.That(models.Where(m => m.Type == "courier").Count(), Is.EqualTo(2));
            Assert.That(models.Where(m => m.Type == "courier_flying").Count(), Is.EqualTo(2));

            // Verify model paths
            Assert.That(models.Any(m => m.ModelPath.Contains("drodo.vmdl")), Is.True);
            Assert.That(models.Any(m => m.ModelPath.Contains("drodo_flying.vmdl")), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_SingleModel_ExtractsCorrectly()
        {
            var models = CourierPatcherService.ParseCourierVisuals(SingleModelCourier);

            Assert.That(models, Has.Count.EqualTo(2)); // 2 entries, 1 unique model
            Assert.That(models.All(m => m.Type == "courier"), Is.True);
            Assert.That(models.All(m => m.ModelPath.Contains("single.vmdl")), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_EmptyBlock_ReturnsEmpty()
        {
            var models = CourierPatcherService.ParseCourierVisuals("");
            Assert.That(models, Is.Empty);
        }

        [Test]
        public void ParseCourierVisuals_NoVisuals_ReturnsEmpty()
        {
            const string block = @"""999"" { ""name"" ""Test"" ""prefab"" ""courier"" }";
            var models = CourierPatcherService.ParseCourierVisuals(block);
            Assert.That(models, Is.Empty);
        }

        [Test]
        public void ParseCourierVisuals_ExtractsSideCorrectly()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);

            var radiantGround = models.FirstOrDefault(m => m.Type == "courier" && m.Side == "radiant");
            var direGround = models.FirstOrDefault(m => m.Type == "courier" && m.Side == "dire");
            
            Assert.That(radiantGround, Is.Not.Null);
            Assert.That(direGround, Is.Not.Null);
        }

        #endregion

        #region ParseCourierVisuals Style Tests

        [Test]
        public void ParseCourierVisuals_WithStyleFilter_ReturnsOnlyMatchingStyle()
        {
            // Style 5 should return only lvl5 models
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier, styleIndex: 5);

            Assert.That(models, Has.Count.EqualTo(4)); // 2 ground + 2 flying for style 5
            Assert.That(models.All(m => m.ModelPath.Contains("lvl5")), Is.True);
            Assert.That(models.All(m => m.StyleIndex == 5), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_WithStyleFilter0_ReturnsOnlyStyle0()
        {
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier, styleIndex: 0);

            Assert.That(models, Has.Count.EqualTo(4)); // 2 ground + 2 flying for style 0
            Assert.That(models.All(m => m.ModelPath.Contains("lvl0")), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_NoStyleFilter_ReturnsAll()
        {
            // No style filter: should return all 8 entries
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier);

            Assert.That(models, Has.Count.EqualTo(8));
        }

        [Test]
        public void ParseCourierVisuals_StyleIndex_IsPopulated()
        {
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier);

            // All entries should have a style index
            Assert.That(models.All(m => m.StyleIndex.HasValue), Is.True);
            Assert.That(models.Where(m => m.StyleIndex == 0).Count(), Is.EqualTo(4));
            Assert.That(models.Where(m => m.StyleIndex == 5).Count(), Is.EqualTo(4));
        }

        [Test]
        public void ParseCourierVisuals_UnstyledCourier_StyleIndexIsNull()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);

            // Drodo has no style fields
            Assert.That(models.All(m => m.StyleIndex == null), Is.True);
        }

        #endregion

        #region ExtractParticleCreateEntries Tests

        [Test]
        public void ExtractParticleCreateEntries_DrodoHasParticle()
        {
            var particles = CourierPatcherService.ExtractParticleCreateEntries(DrodoBlock);

            Assert.That(particles, Has.Count.EqualTo(1));
            Assert.That(particles[0], Does.Contain("particle_create"));
            Assert.That(particles[0], Does.Contain("courier_drodo_ambient.vpcf"));
        }

        [Test]
        public void ExtractParticleCreateEntries_NoParticles_ReturnsEmpty()
        {
            // Default courier has no particle_create entries
            var particles = CourierPatcherService.ExtractParticleCreateEntries(DefaultCourierBlock);
            Assert.That(particles, Is.Empty);
        }

        #endregion

        #region GetModelMapping Tests

        [Test]
        public void GetModelMapping_2Models_MapsTo4BaseFiles()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);
            var mappings = CourierPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(4));

            // Verify all 4 base files are targets
            var targetFiles = mappings.Select(m => m.TargetFileName).ToList();
            Assert.That(targetFiles, Does.Contain("donkey.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_dire.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_wings.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_dire_wings.vmdl_c"));

            // Ground model maps to donkey and donkey_dire
            var donkeyMapping = mappings.First(m => m.TargetFileName == "donkey.vmdl_c");
            var donkeyDireMapping = mappings.First(m => m.TargetFileName == "donkey_dire.vmdl_c");
            Assert.That(donkeyMapping.SourcePath, Does.Contain("drodo.vmdl"));
            Assert.That(donkeyDireMapping.SourcePath, Does.Contain("drodo.vmdl"));

            // Flying model maps to donkey_wings and donkey_dire_wings
            var wingsMapping = mappings.First(m => m.TargetFileName == "donkey_wings.vmdl_c");
            var direWingsMapping = mappings.First(m => m.TargetFileName == "donkey_dire_wings.vmdl_c");
            Assert.That(wingsMapping.SourcePath, Does.Contain("drodo_flying.vmdl"));
            Assert.That(direWingsMapping.SourcePath, Does.Contain("drodo_flying.vmdl"));
        }

        [Test]
        public void GetModelMapping_1Model_DuplicatesTo4()
        {
            var models = CourierPatcherService.ParseCourierVisuals(SingleModelCourier);
            var mappings = CourierPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(4));

            // All 4 should map to the same source
            var sourceFiles = mappings.Select(m => m.SourcePath).Distinct().ToList();
            Assert.That(sourceFiles, Has.Count.EqualTo(1));
            Assert.That(sourceFiles[0], Does.Contain("single.vmdl"));
        }

        [Test]
        public void GetModelMapping_EmptyModels_ReturnsEmpty()
        {
            var mappings = CourierPatcherService.GetModelMapping(new List<CourierModelInfo>());
            Assert.That(mappings, Is.Empty);
        }

        #endregion

        #region GetVpkExtractionPaths Tests

        [Test]
        public void GetVpkExtractionPaths_AppendsSuffix()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);
            var paths = CourierPatcherService.GetVpkExtractionPaths(models);

            // Should deduplicate: drodo.vmdl_c and drodo_flying.vmdl_c
            Assert.That(paths, Has.Count.EqualTo(2));
            Assert.That(paths.All(p => p.EndsWith("_c")), Is.True);
            Assert.That(paths.Any(p => p.Contains("drodo.vmdl_c")), Is.True);
            Assert.That(paths.Any(p => p.Contains("drodo_flying.vmdl_c")), Is.True);
        }

        #endregion

        #region BuildMergedCourierBlock Tests

        [Test]
        public void BuildMergedCourierBlock_PreservesImmutableFields()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Immutable fields from default must be preserved
            Assert.That(merged, Does.Contain("\"name\"\t\t\"Default Courier\""));
            Assert.That(merged, Does.Contain("\"prefab\"\t\t\"courier\""));
            Assert.That(merged, Does.Contain("\"baseitem\"\t\t\"1\""));
        }

        [Test]
        public void BuildMergedCourierBlock_MergesMutableFields()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Mutable fields should come from selected courier (Drodo)
            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"unusual\""));
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"immortal\""));
            Assert.That(merged, Does.Contain("drodo"));
        }

        [Test]
        public void BuildMergedCourierBlock_PreservesDefaultDonkeyModels()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Default donkey model references must stay in visuals
            // (actual model swap happens at file level, not in items_game.txt)
            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_dire.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
            Assert.That(merged, Does.Contain("donkey_dire_wings.vmdl"));
        }

        [Test]
        public void BuildMergedCourierBlock_WithStyle_StillKeepsDonkeyModels()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, StyledCourier, styleIndex: 5);

            // Even with style selection, visuals must keep default donkey models
            // (style-specific model swap is file-level only)
            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
            // Selected courier models should NOT appear in visuals
            Assert.That(merged, Does.Not.Contain("styled_lvl5.vmdl"));
            Assert.That(merged, Does.Not.Contain("styled_lvl0.vmdl"));
        }

        [Test]
        public void BuildMergedCourierBlock_AppendsParticleCreate()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Particle_create from Drodo should be appended
            Assert.That(merged, Does.Contain("particle_create"));
            Assert.That(merged, Does.Contain("courier_drodo_ambient.vpcf"));
        }

        [Test]
        public void BuildMergedCourierBlock_UsesCorrectItemId()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Must use default courier ID 595, NOT the selected courier's ID
            Assert.That(merged, Does.Contain("\"595\""));
            Assert.That(merged, Does.Not.Contain("\"10003\""));
        }

        [Test]
        public void BuildMergedCourierBlock_EmptyInputs_ReturnsDefault()
        {
            var result = CourierPatcherService.BuildMergedCourierBlock("", DrodoBlock);
            Assert.That(result, Is.EqualTo(""));

            var result2 = CourierPatcherService.BuildMergedCourierBlock(DefaultCourierBlock, "");
            Assert.That(result2, Is.EqualTo(DefaultCourierBlock));
        }
        [Test]
        public void BuildMergedCourierBlock_InjectsSkinField()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, GoldenBabyRoshanBlock);

            // Skin field from Golden Baby Roshan should be injected into default visuals
            Assert.That(merged, Does.Contain("\"skin\"\t\t\"1\""));
            // Donkey models must still be present
            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
            // Mutable fields from selected
            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"unusual\""));
        }

        [Test]
        public void BuildMergedCourierBlock_NoSkinField_WhenNotPresent()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Drodo has no skin field — should not appear in merged block
            Assert.That(merged, Does.Not.Contain("\"skin\""));
        }

        #endregion

        #region Ethereal Tests

        [Test]
        public void CountExistingParticles_ReturnsZero_WhenNoParticles()
        {
            // Default courier has no particle_create entries
            Assert.That(CourierPatcherService.CountExistingParticles(DefaultCourierBlock), Is.EqualTo(0));
        }

        [Test]
        public void CountExistingParticles_CountsParticleCreateEntries()
        {
            // Drodo has particle_create entries
            var count = CourierPatcherService.CountExistingParticles(DrodoBlock);
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void AppendEtherealEffects_AddsParticleCreate()
        {
            var visuals = @"""visuals""
{
	""asset_modifier0""
	{
		""type""		""courier""
		""modifier""		""models/courier/test.vmdl""
		""asset""		""radiant""
	}
}";
            var effects = new List<string>
            {
                "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 0);
            Assert.That(result, Does.Contain("particle_create"));
            Assert.That(result, Does.Contain("golden_roshan_ambient.vpcf"));
        }

        [Test]
        public void AppendEtherealEffects_RespectsSlotLimit()
        {
            var visuals = @"""visuals""
{
	""asset_modifier0""
	{
		""type""		""courier""
		""modifier""		""models/courier/test.vmdl""
		""asset""		""radiant""
	}
}";
            var effects = new List<string>
            {
                "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf",
                "particles/econ/courier/courier_roshan_lava/courier_roshan_lava.vpcf",
                "particles/econ/courier/courier_roshan_frost/courier_roshan_frost_ambient.vpcf"
            };

            // Already have 1 existing particle, so only 1 slot available
            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 1);
            Assert.That(result, Does.Contain("golden_roshan_ambient.vpcf"));
            Assert.That(result, Does.Not.Contain("courier_roshan_lava.vpcf"));
            Assert.That(result, Does.Not.Contain("courier_roshan_frost_ambient.vpcf"));
        }
        [Test]
        public void AppendEtherealEffects_DynamicIndex_StartsAfterMaxExisting()
        {
            // Visuals block with asset_modifier up to index 5
            var visuals = @"""visuals""
{
	""asset_modifier3""
	{
		""type""		""courier""
		""modifier""		""models/courier/test.vmdl""
		""asset""		""radiant""
	}
	""asset_modifier5""
	{
		""type""		""courier_flying""
		""modifier""		""models/courier/test_fly.vmdl""
		""asset""		""radiant""
	}
}";
            var effects = new List<string>
            {
                "particles/econ/courier/test/test_ambient.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 0);

            // New entry should be asset_modifier6 (max existing is 5)
            Assert.That(result, Does.Contain("\"asset_modifier6\""));
            Assert.That(result, Does.Not.Contain("\"asset_modifier10\""));
            Assert.That(result, Does.Contain("test_ambient.vpcf"));
        }

        [Test]
        public void AppendEtherealEffects_NoExistingModifiers_StartsAtZero()
        {
            // Visuals block with no asset_modifier entries
            var visuals = @"""visuals""
{
}";
            var effects = new List<string>
            {
                "particles/econ/courier/test/test_ambient.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 0);

            // Should start at index 0 when no existing modifiers
            Assert.That(result, Does.Contain("\"asset_modifier0\""));
            Assert.That(result, Does.Contain("test_ambient.vpcf"));
        }

        [Test]
        public void BuildMergedCourierBlock_PreservesRelativeIndentation()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            var lines = merged.Split('\n');

            // Find the portraits block region (between "portraits" header and "visuals" header)
            int portraitsIdx = -1, visualsIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart('\t', ' ').TrimEnd('\r', '\n');
                if (trimmed.StartsWith("\"portraits\"") && portraitsIdx < 0) portraitsIdx = i;
                if (trimmed.StartsWith("\"visuals\"") && portraitsIdx >= 0) { visualsIdx = i; break; }
            }

            Assert.That(portraitsIdx, Is.GreaterThanOrEqualTo(0), "portraits block should exist");
            Assert.That(visualsIdx, Is.GreaterThan(portraitsIdx), "visuals should come after portraits");

            // Within the portraits region, verify relative depth is preserved
            var portraitsRegion = lines.Skip(portraitsIdx).Take(visualsIdx - portraitsIdx).ToArray();

            var headerLine = portraitsRegion.First(); // "portraits" line
            var iconLine = portraitsRegion.FirstOrDefault(l => l.TrimStart().StartsWith("\"icon\""));
            var fovLine = portraitsRegion.FirstOrDefault(l => l.Contains("PortraitFOV"));

            Assert.That(iconLine, Is.Not.Null, "icon block should exist in portraits");
            Assert.That(fovLine, Is.Not.Null, "PortraitFOV should exist in portraits");

            int headerTabs = headerLine.TakeWhile(c => c == '\t').Count();
            int iconTabs = iconLine!.TakeWhile(c => c == '\t').Count();
            int fovTabs = fovLine!.TakeWhile(c => c == '\t').Count();

            // Each nested level should have progressively deeper indentation
            Assert.That(iconTabs, Is.GreaterThan(headerTabs),
                $"icon ({iconTabs} tabs) should be deeper than portraits ({headerTabs} tabs)");
            Assert.That(fovTabs, Is.GreaterThan(iconTabs),
                $"PortraitFOV ({fovTabs} tabs) should be deeper than icon ({iconTabs} tabs)");
        }

        #endregion
    }
}
