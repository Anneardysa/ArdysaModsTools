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
        public void BuildMergedCourierBlock_ReplacesDefaultModelsWithSelected()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            // Selected courier models should replace default donkey models
            Assert.That(merged, Does.Contain("drodo.vmdl"));
            Assert.That(merged, Does.Contain("drodo_flying.vmdl"));
        }

        [Test]
        public void BuildMergedCourierBlock_WithStyle_UsesStyleSpecificModels()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, StyledCourier, styleIndex: 5);

            // Style 5 models should be used
            Assert.That(merged, Does.Contain("styled_lvl5.vmdl"));
            Assert.That(merged, Does.Contain("styled_lvl5_flying.vmdl"));
            // Style 0 models should NOT be present
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

        #endregion
    }
}
