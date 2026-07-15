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
    [TestFixture]
    public class CourierPatcherServiceTests
    {
        #region Test Data

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

        private const string MultiParticleCourier = @"
""40000""
{
	""name""		""Multi Particle Courier""
	""prefab""		""courier""
	""item_quality""		""legendary""
	""visuals""
	{
		""asset_modifier4""
		{
			""type""		""courier""
			""modifier""		""models/courier/multi/multi.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier5""
		{
			""type""		""courier""
			""modifier""		""models/courier/multi/multi_dire.vmdl""
			""asset""		""dire""
		}
		""asset_modifier0""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/multi/multi_flying.vmdl""
			""asset""		""radiant""
		}
		""asset_modifier2""
		{
			""type""		""courier_flying""
			""modifier""		""models/courier/multi/multi_dire_flying.vmdl""
			""asset""		""dire""
		}
		""asset_modifier""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/multi/multi_ambient.vpcf""
			""ground_courier_only""		""1""
			""radiant_only""		""1""
		}
		""asset_modifier""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/multi/multi_ambient_flying.vpcf""
			""flying_courier_only""		""1""
			""radiant_only""		""1""
		}
		""asset_modifier""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/multi/multi_dire_ambient.vpcf""
			""ground_courier_only""		""1""
			""dire_only""		""1""
		}
		""asset_modifier""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/multi/multi_dire_ambient_flying.vpcf""
			""flying_courier_only""		""1""
			""dire_only""		""1""
		}
	}
}";

        private const string StyledParticleCourier = @"
""50000""
{
	""name""		""Onibi""
	""prefab""		""courier""
	""item_rarity""		""immortal""
	""image_inventory""		""econ/items/courier/onibi_lvl_00/onibi_lvl_00""
	""item_name""		""#DOTA_Item_Onibi""
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""courier""
			""modifier""		""models/items/courier/onibi_lvl_21/onibi_lvl_21.vmdl""
			""asset""		""radiant""
			""style""		""21""
		}
		""asset_modifier1""
		{
			""type""		""courier""
			""modifier""		""models/items/courier/onibi_lvl_21/onibi_lvl_21.vmdl""
			""asset""		""dire""
			""style""		""21""
		}
		""asset_modifier2""
		{
			""type""		""courier_flying""
			""modifier""		""models/items/courier/onibi_lvl_21/onibi_lvl_21_flying.vmdl""
			""asset""		""radiant""
			""style""		""21""
		}
		""asset_modifier3""
		{
			""type""		""courier_flying""
			""modifier""		""models/items/courier/onibi_lvl_21/onibi_lvl_21_flying.vmdl""
			""asset""		""dire""
			""style""		""21""
		}
		""asset_modifier4""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/courier_onibi/courier_onibi_green_lvl0_ambient.vpcf""
			""style""		""0""
		}
		""asset_modifier5""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/courier_onibi/courier_onibi_blue_lvl5_ambient.vpcf""
			""style""		""5""
		}
		""asset_modifier6""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/courier/courier_onibi/courier_onibi_black_lvl21_ambient.vpcf""
			""style""		""21""
		}
		""styles""
		{
			""0""
			{
				""name""		""#DOTA_Style_Onibi_0""
				""skin""		""0""
			}
			""5""
			{
				""name""		""#DOTA_Style_Onibi_5""
				""skin""		""0""
				""alternate_icon""		""5""
			}
			""21""
			{
				""name""		""#DOTA_Style_Onibi_21""
				""skin""		""0""
				""alternate_icon""		""21""
			}
		}
		""alternate_icons""
		{
			""0""
			{
				""icon_path""		""econ/items/courier/onibi_lvl_00/onibi_lvl_00""
			}
			""5""
			{
				""icon_path""		""econ/items/courier/onibi_lvl_05/onibi_lvl_05""
			}
			""21""
			{
				""icon_path""		""econ/items/courier/onibi_lvl_21/onibi_lvl_21""
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

            Assert.That(models, Has.Count.EqualTo(4));
            Assert.That(models.Where(m => m.Type == "courier").Count(), Is.EqualTo(2));
            Assert.That(models.Where(m => m.Type == "courier_flying").Count(), Is.EqualTo(2));

            Assert.That(models.Any(m => m.ModelPath.Contains("drodo.vmdl")), Is.True);
            Assert.That(models.Any(m => m.ModelPath.Contains("drodo_flying.vmdl")), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_SingleModel_ExtractsCorrectly()
        {
            var models = CourierPatcherService.ParseCourierVisuals(SingleModelCourier);

            Assert.That(models, Has.Count.EqualTo(2));
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
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier, styleIndex: 5);

            Assert.That(models, Has.Count.EqualTo(4));
            Assert.That(models.All(m => m.ModelPath.Contains("lvl5")), Is.True);
            Assert.That(models.All(m => m.StyleIndex == 5), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_WithStyleFilter0_ReturnsOnlyStyle0()
        {
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier, styleIndex: 0);

            Assert.That(models, Has.Count.EqualTo(4));
            Assert.That(models.All(m => m.ModelPath.Contains("lvl0")), Is.True);
        }

        [Test]
        public void ParseCourierVisuals_NoStyleFilter_ReturnsAll()
        {
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier);

            Assert.That(models, Has.Count.EqualTo(8));
        }

        [Test]
        public void ParseCourierVisuals_StyleIndex_IsPopulated()
        {
            var models = CourierPatcherService.ParseCourierVisuals(StyledCourier);

            Assert.That(models.All(m => m.StyleIndex.HasValue), Is.True);
            Assert.That(models.Where(m => m.StyleIndex == 0).Count(), Is.EqualTo(4));
            Assert.That(models.Where(m => m.StyleIndex == 5).Count(), Is.EqualTo(4));
        }

        [Test]
        public void ParseCourierVisuals_UnstyledCourier_StyleIndexIsNull()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);

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
            var particles = CourierPatcherService.ExtractParticleCreateEntries(DefaultCourierBlock);
            Assert.That(particles, Is.Empty);
        }

        #endregion

        #region ExtractParticleCreateEntries Style Tests

        [Test]
        public void ExtractParticleCreateEntries_WithStyleFilter_ReturnsOnlyMatchingStyle()
        {
            var particles = CourierPatcherService.ExtractParticleCreateEntries(StyledParticleCourier, styleIndex: 21);

            Assert.That(particles, Has.Count.EqualTo(1));
            Assert.That(particles[0], Does.Contain("courier_onibi_black_lvl21_ambient.vpcf"));
            Assert.That(particles[0], Does.Not.Contain("lvl0_ambient"));
            Assert.That(particles[0], Does.Not.Contain("lvl5_ambient"));

            Assert.That(particles[0], Does.Not.Contain("\"style\""),
                "Style field should be stripped after filtering");
        }

        [Test]
        public void ExtractParticleCreateEntries_WithStyleFilter0_ReturnsOnlyStyle0()
        {
            var particles = CourierPatcherService.ExtractParticleCreateEntries(StyledParticleCourier, styleIndex: 0);

            Assert.That(particles, Has.Count.EqualTo(1));
            Assert.That(particles[0], Does.Contain("courier_onibi_green_lvl0_ambient.vpcf"));
        }

        [Test]
        public void ExtractParticleCreateEntries_NoStyleFilter_ReturnsAll()
        {
            var particles = CourierPatcherService.ExtractParticleCreateEntries(StyledParticleCourier);

            Assert.That(particles, Has.Count.EqualTo(3));
            Assert.That(particles.Any(p => p.Contains("lvl0_ambient")), Is.True);
            Assert.That(particles.Any(p => p.Contains("lvl5_ambient")), Is.True);
            Assert.That(particles.Any(p => p.Contains("lvl21_ambient")), Is.True);
        }

        [Test]
        public void ExtractParticleCreateEntries_UnstyledParticles_AlwaysIncluded()
        {
            var particlesNoFilter = CourierPatcherService.ExtractParticleCreateEntries(DrodoBlock);
            var particlesWithFilter = CourierPatcherService.ExtractParticleCreateEntries(DrodoBlock, styleIndex: 5);

            Assert.That(particlesNoFilter, Has.Count.EqualTo(1));
            Assert.That(particlesWithFilter, Has.Count.EqualTo(1));
            Assert.That(particlesWithFilter[0], Does.Contain("courier_drodo_ambient.vpcf"));
        }

        [Test]
        public void ExtractParticleCreateEntries_NonexistentStyle_ReturnsEmpty()
        {
            var particles = CourierPatcherService.ExtractParticleCreateEntries(StyledParticleCourier, styleIndex: 99);
            Assert.That(particles, Is.Empty);
        }

        [Test]
        public void BuildMergedCourierBlock_StyledParticles_OnlyInjectsMatchingStyle()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, StyledParticleCourier, styleIndex: 21);

            Assert.That(merged, Does.Contain("courier_onibi_black_lvl21_ambient.vpcf"),
                "Style 21 particle should be injected");
            Assert.That(merged, Does.Not.Contain("courier_onibi_green_lvl0_ambient.vpcf"),
                "Style 0 particle should NOT be injected");
            Assert.That(merged, Does.Not.Contain("courier_onibi_blue_lvl5_ambient.vpcf"),
                "Style 5 particle should NOT be injected");

            Assert.That(merged, Does.Not.Contain("\"style\""),
                "Style field should be stripped from particle entries");

            Assert.That(merged, Does.Contain("#DOTA_Style_Onibi_21"),
                "item_name should be overridden with style-specific name");
            Assert.That(merged, Does.Not.Contain("#DOTA_Item_Onibi"),
                "Original item_name should be replaced");

            Assert.That(merged, Does.Contain("econ/items/courier/onibi_lvl_21/onibi_lvl_21"),
                "image_inventory should be overridden with style-specific icon");
            Assert.That(merged, Does.Not.Contain("econ/items/courier/onibi_lvl_00/onibi_lvl_00"),
                "Original image_inventory should be replaced");

            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
        }

        #endregion

        #region GetModelMapping Tests

        [Test]
        public void GetModelMapping_2Models_MapsTo4BaseFiles()
        {
            var models = CourierPatcherService.ParseCourierVisuals(DrodoBlock);
            var mappings = CourierPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(4));

            var targetFiles = mappings.Select(m => m.TargetFileName).ToList();
            Assert.That(targetFiles, Does.Contain("donkey.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_dire.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_wings.vmdl_c"));
            Assert.That(targetFiles, Does.Contain("donkey_dire_wings.vmdl_c"));

            var donkeyMapping = mappings.First(m => m.TargetFileName == "donkey.vmdl_c");
            var donkeyDireMapping = mappings.First(m => m.TargetFileName == "donkey_dire.vmdl_c");
            Assert.That(donkeyMapping.SourcePath, Does.Contain("drodo.vmdl"));
            Assert.That(donkeyDireMapping.SourcePath, Does.Contain("drodo.vmdl"));

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

            Assert.That(merged, Does.Contain("\"name\"\t\t\"Default Courier\""));
            Assert.That(merged, Does.Contain("\"prefab\"\t\t\"courier\""));
            Assert.That(merged, Does.Contain("\"baseitem\"\t\t\"1\""));
        }

        [Test]
        public void BuildMergedCourierBlock_MergesMutableFields()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"unusual\""));
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"immortal\""));
            Assert.That(merged, Does.Contain("drodo"));
        }

        [Test]
        public void BuildMergedCourierBlock_PreservesDefaultDonkeyModels()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

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

            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
            Assert.That(merged, Does.Not.Contain("styled_lvl5.vmdl"));
            Assert.That(merged, Does.Not.Contain("styled_lvl0.vmdl"));
        }

        [Test]
        public void BuildMergedCourierBlock_AppendsParticleCreate()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            Assert.That(merged, Does.Contain("particle_create"));
            Assert.That(merged, Does.Contain("courier_drodo_ambient.vpcf"));
        }

        [Test]
        public void BuildMergedCourierBlock_UsesCorrectItemId()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

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

            Assert.That(merged, Does.Contain("\"skin\"\t\t\"1\""));
            Assert.That(merged, Does.Contain("donkey.vmdl"));
            Assert.That(merged, Does.Contain("donkey_wings.vmdl"));
            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"unusual\""));
        }

        [Test]
        public void BuildMergedCourierBlock_NoSkinField_WhenNotPresent()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            Assert.That(merged, Does.Not.Contain("\"skin\""));
        }

        #endregion

        #region Ethereal Tests

        [Test]
        public void CountExistingParticles_ReturnsZero_WhenNoParticles()
        {
            Assert.That(CourierPatcherService.CountExistingParticles(DefaultCourierBlock), Is.EqualTo(0));
        }

        [Test]
        public void CountExistingParticles_CountsParticleCreateEntries()
        {
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

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 1);
            Assert.That(result, Does.Contain("golden_roshan_ambient.vpcf"));
            Assert.That(result, Does.Not.Contain("courier_roshan_lava.vpcf"));
            Assert.That(result, Does.Not.Contain("courier_roshan_frost_ambient.vpcf"));
        }
        [Test]
        public void AppendEtherealEffects_DynamicIndex_StartsAfterMaxExisting()
        {
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

            Assert.That(result, Does.Contain("\"asset_modifier6\""));
            Assert.That(result, Does.Not.Contain("\"asset_modifier10\""));
            Assert.That(result, Does.Contain("test_ambient.vpcf"));
        }

        [Test]
        public void AppendEtherealEffects_NoExistingModifiers_StartsAtZero()
        {
            var visuals = @"""visuals""
{
}";
            var effects = new List<string>
            {
                "particles/econ/courier/test/test_ambient.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 0);

            Assert.That(result, Does.Contain("\"asset_modifier0\""));
            Assert.That(result, Does.Contain("test_ambient.vpcf"));
        }

        [Test]
        public void BuildMergedCourierBlock_PreservesRelativeIndentation()
        {
            var merged = CourierPatcherService.BuildMergedCourierBlock(
                DefaultCourierBlock, DrodoBlock);

            var lines = merged.Split('\n');

            int portraitsIdx = -1, visualsIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart('\t', ' ').TrimEnd('\r', '\n');
                if (trimmed.StartsWith("\"portraits\"") && portraitsIdx < 0) portraitsIdx = i;
                if (trimmed.StartsWith("\"visuals\"") && portraitsIdx >= 0) { visualsIdx = i; break; }
            }

            Assert.That(portraitsIdx, Is.GreaterThanOrEqualTo(0), "portraits block should exist");
            Assert.That(visualsIdx, Is.GreaterThan(portraitsIdx), "visuals should come after portraits");

            var portraitsRegion = lines.Skip(portraitsIdx).Take(visualsIdx - portraitsIdx).ToArray();

            var headerLine = portraitsRegion.First();
            var iconLine = portraitsRegion.FirstOrDefault(l => l.TrimStart().StartsWith("\"icon\""));
            var fovLine = portraitsRegion.FirstOrDefault(l => l.Contains("PortraitFOV"));

            Assert.That(iconLine, Is.Not.Null, "icon block should exist in portraits");
            Assert.That(fovLine, Is.Not.Null, "PortraitFOV should exist in portraits");

            int headerTabs = headerLine.TakeWhile(c => c == '\t').Count();
            int iconTabs = iconLine!.TakeWhile(c => c == '\t').Count();
            int fovTabs = fovLine!.TakeWhile(c => c == '\t').Count();

            Assert.That(iconTabs, Is.GreaterThan(headerTabs),
                $"icon ({iconTabs} tabs) should be deeper than portraits ({headerTabs} tabs)");
            Assert.That(fovTabs, Is.GreaterThan(iconTabs),
                $"PortraitFOV ({fovTabs} tabs) should be deeper than icon ({iconTabs} tabs)");
        }

        #endregion

        #region RemoveParticleCreateEntries Tests

        [Test]
        public void RemoveParticleCreateEntries_PreservesModelEntries()
        {
            var visuals = @"""visuals""
{
	""asset_modifier0""
	{
		""type""		""courier""
		""modifier""		""models/courier/test.vmdl""
		""asset""		""radiant""
	}
	""asset_modifier1""
	{
		""type""		""particle_create""
		""modifier""		""particles/econ/courier/test/test_ambient.vpcf""
	}
}";
            var result = CourierPatcherService.RemoveParticleCreateEntries(visuals);

            Assert.That(result, Does.Contain("\"courier\""));
            Assert.That(result, Does.Contain("test.vmdl"));
            Assert.That(result, Does.Not.Contain("particle_create"));
            Assert.That(result, Does.Not.Contain("test_ambient.vpcf"));
        }

        [Test]
        public void RemoveParticleCreateEntries_RemovesAll4Particles()
        {
            var visualsBlock = CourierPatcherService.ExtractVisualsBlock(MultiParticleCourier);
            Assert.That(visualsBlock, Is.Not.Null);

            var result = CourierPatcherService.RemoveParticleCreateEntries(visualsBlock!);

            Assert.That(result, Does.Not.Contain("particle_create"));
            Assert.That(result, Does.Not.Contain("multi_ambient.vpcf"));
            Assert.That(result, Does.Not.Contain("multi_ambient_flying.vpcf"));
            Assert.That(result, Does.Not.Contain("multi_dire_ambient.vpcf"));
            Assert.That(result, Does.Not.Contain("multi_dire_ambient_flying.vpcf"));

            Assert.That(result, Does.Contain("multi.vmdl"));
            Assert.That(result, Does.Contain("multi_dire.vmdl"));
            Assert.That(result, Does.Contain("multi_flying.vmdl"));
            Assert.That(result, Does.Contain("multi_dire_flying.vmdl"));
        }

        [Test]
        public void RemoveParticleCreateEntries_EmptyBlock_ReturnsEmpty()
        {
            Assert.That(CourierPatcherService.RemoveParticleCreateEntries(""), Is.EqualTo(""));
            Assert.That(CourierPatcherService.RemoveParticleCreateEntries(null!), Is.EqualTo(""));
        }

        #endregion

        #region AppendEtherealEffects with replaceExisting Tests

        [Test]
        public void AppendEtherealEffects_ReplaceExisting_RemovesOldAndAddsNew()
        {
            var visuals = @"""visuals""
{
	""asset_modifier0""
	{
		""type""		""courier""
		""modifier""		""models/courier/test.vmdl""
		""asset""		""radiant""
	}
	""asset_modifier1""
	{
		""type""		""particle_create""
		""modifier""		""particles/econ/courier/old/old_ambient.vpcf""
	}
}";
            var effects = new List<string>
            {
                "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 1, replaceExisting: true);

            Assert.That(result, Does.Not.Contain("old_ambient.vpcf"), "Old particle should be removed");
            Assert.That(result, Does.Contain("golden_roshan_ambient.vpcf"), "New ethereal should be added");
            Assert.That(result, Does.Contain("particle_create"), "Should have particle_create entry");
            Assert.That(result, Does.Contain("test.vmdl"), "Model entry should survive");
        }

        [Test]
        public void AppendEtherealEffects_ReplaceExisting_WithMultipleNativeParticles()
        {
            var visualsBlock = CourierPatcherService.ExtractVisualsBlock(MultiParticleCourier);
            Assert.That(visualsBlock, Is.Not.Null);

            var effects = new List<string>
            {
                "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf"
            };

            var resultWithout = CourierPatcherService.AppendEtherealEffects(visualsBlock!, effects, 4, replaceExisting: false);
            Assert.That(resultWithout, Does.Not.Contain("golden_roshan_ambient.vpcf"),
                "Without replaceExisting, ethereal should be blocked by 4 existing particles");

            var resultWith = CourierPatcherService.AppendEtherealEffects(visualsBlock!, effects, 4, replaceExisting: true);
            Assert.That(resultWith, Does.Contain("golden_roshan_ambient.vpcf"),
                "With replaceExisting, ethereal should be applied after removing native particles");
            Assert.That(resultWith, Does.Not.Contain("multi_ambient.vpcf"),
                "Native particles should be removed");
            Assert.That(resultWith, Does.Contain("multi.vmdl"),
                "Model entries should survive");
        }

        [Test]
        public void AppendEtherealEffects_ReplaceExisting_RespectsMaxSlots()
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
                "particles/econ/courier/effect1/effect1.vpcf",
                "particles/econ/courier/effect2/effect2.vpcf",
                "particles/econ/courier/effect3/effect3.vpcf"
            };

            var result = CourierPatcherService.AppendEtherealEffects(visuals, effects, 0, replaceExisting: true);

            Assert.That(result, Does.Contain("effect1.vpcf"));
            Assert.That(result, Does.Contain("effect2.vpcf"));
            Assert.That(result, Does.Not.Contain("effect3.vpcf"));
        }

        #endregion
    }
}
