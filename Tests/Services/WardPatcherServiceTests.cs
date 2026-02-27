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
    /// Tests for WardPatcherService: KV block parsing, model mapping, and block merging.
    /// </summary>
    [TestFixture]
    public class WardPatcherServiceTests
    {
        #region Test Data

        /// <summary>
        /// Default Ward block (ID 596).
        /// </summary>
        private const string DefaultWardBlock = @"
""596""
{
	""name""		""Default Ward""
	""prefab""		""ward""
	""baseitem""		""1""
	""creation_date""		""2015-07-24""
	""image_inventory""		""econ/wards/default_ward""
	""item_name""		""#DOTA_Item_Default_Ward""
	""item_quality""		""base""
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
			""type""		""entity_model""
			""modifier""		""models/props_gameplay/default_ward.vmdl""
		}
	}
}";

        /// <summary>
        /// Sample cosmetic ward block — simple single model ward.
        /// </summary>
        private const string CosmeticWardBlock = @"
""15001""
{
	""name""		""Nightsilver Sentinel""
	""prefab""		""ward""
	""image_inventory""		""econ/wards/nightsilver/nightsilver""
	""item_description""		""#DOTA_Item_Desc_Nightsilver_Sentinel""
	""item_name""		""#DOTA_Item_Nightsilver_Sentinel""
	""item_quality""		""rare""
	""item_rarity""		""rare""
	""portraits""
	{
		""icon""
		{
			""PortraitLightPosition""		""100.0 -50.0 200.0""
			""PortraitAnimationActivity""		""ACT_DOTA_IDLE""
			""cameras""
			{
				""default""
				{
					""PortraitPosition""		""200.0 -200.0 250.0""
					""PortraitFOV""		""35.000000""
				}
			}
		}
	}
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""entity_model""
			""modifier""		""models/items/wards/nightsilver/nightsilver.vmdl""
		}
	}
}";

        /// <summary>
        /// Ward with particle_create entry (ambient effect).
        /// </summary>
        private const string WardWithParticle = @"
""15002""
{
	""name""		""Frozen Formation""
	""prefab""		""ward""
	""item_quality""		""mythical""
	""item_rarity""		""mythical""
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""entity_model""
			""modifier""		""models/items/wards/frozen_formation/frozen_formation.vmdl""
		}
		""asset_modifier1""
		{
			""type""		""particle_create""
			""modifier""		""particles/econ/wards/frozen_formation/frozen_formation_ambient.vpcf""
		}
	}
}";

        /// <summary>
        /// Styled ward with different models per style.
        /// </summary>
        private const string StyledWardBlock = @"
""15003""
{
	""name""		""Mega-Kills: Styled Ward""
	""prefab""		""ward""
	""item_quality""		""rare""
	""visuals""
	{
		""asset_modifier0""
		{
			""type""		""entity_model""
			""modifier""		""models/items/wards/styled_ward/styled_ward_lvl0.vmdl""
			""style""		""0""
		}
		""asset_modifier1""
		{
			""type""		""entity_model""
			""modifier""		""models/items/wards/styled_ward/styled_ward_lvl3.vmdl""
			""style""		""3""
		}
		""styles""
		{
			""0""
			{
				""name""		""Default""
			}
			""3""
			{
				""name""		""Ascended""
			}
		}
	}
}";

        /// <summary>
        /// Ward with "skin" field in visuals (different appearance via skin index).
        /// </summary>
        private const string WardWithSkinBlock = @"
""15004""
{
	""name""		""Golden Sentinel""
	""prefab""		""ward""
	""image_inventory""		""econ/wards/golden_sentinel/golden_sentinel""
	""item_name""		""#DOTA_Item_Golden_Sentinel""
	""item_quality""		""unusual""
	""item_rarity""		""immortal""
	""visuals""
	{
		""skin""		""2""
		""asset_modifier0""
		{
			""type""		""entity_model""
			""modifier""		""models/items/wards/golden_sentinel/golden_sentinel.vmdl""
		}
	}
}";

        #endregion

        #region ParseWardVisuals Tests

        [Test]
        public void ParseWardVisuals_SingleModel_ExtractsCorrectly()
        {
            var models = WardPatcherService.ParseWardVisuals(CosmeticWardBlock);

            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(models[0].ModelPath, Does.Contain("nightsilver.vmdl"));
            Assert.That(models[0].StyleIndex, Is.Null);
        }

        [Test]
        public void ParseWardVisuals_EmptyBlock_ReturnsEmpty()
        {
            var models = WardPatcherService.ParseWardVisuals("");
            Assert.That(models, Is.Empty);
        }

        [Test]
        public void ParseWardVisuals_NoVisuals_ReturnsEmpty()
        {
            const string block = @"""999"" { ""name"" ""Test"" ""prefab"" ""ward"" }";
            var models = WardPatcherService.ParseWardVisuals(block);
            Assert.That(models, Is.Empty);
        }

        [Test]
        public void ParseWardVisuals_WithParticle_OnlyExtractsEntityModel()
        {
            var models = WardPatcherService.ParseWardVisuals(WardWithParticle);

            // Should only extract entity_model entries, not particle_create
            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(models[0].ModelPath, Does.Contain("frozen_formation.vmdl"));
        }

        [Test]
        public void ParseWardVisuals_WithStyleFilter_ReturnsOnlyMatchingStyle()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock, styleIndex: 3);

            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(models[0].ModelPath, Does.Contain("lvl3"));
            Assert.That(models[0].StyleIndex, Is.EqualTo(3));
        }

        [Test]
        public void ParseWardVisuals_WithStyleFilter0_ReturnsOnlyStyle0()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock, styleIndex: 0);

            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(models[0].ModelPath, Does.Contain("lvl0"));
        }

        [Test]
        public void ParseWardVisuals_NoStyleFilter_ReturnsAll()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock);

            Assert.That(models, Has.Count.EqualTo(2));
        }

        [Test]
        public void ParseWardVisuals_StyleIndex_IsPopulated()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock);

            Assert.That(models.All(m => m.StyleIndex.HasValue), Is.True);
            Assert.That(models.Count(m => m.StyleIndex == 0), Is.EqualTo(1));
            Assert.That(models.Count(m => m.StyleIndex == 3), Is.EqualTo(1));
        }

        [Test]
        public void ParseWardVisuals_UnstyledWard_StyleIndexIsNull()
        {
            var models = WardPatcherService.ParseWardVisuals(CosmeticWardBlock);

            Assert.That(models.All(m => m.StyleIndex == null), Is.True);
        }

        #endregion

        #region ExtractParticleCreateEntries Tests

        [Test]
        public void ExtractParticleCreateEntries_WardWithParticle_ExtractsCorrectly()
        {
            var particles = WardPatcherService.ExtractParticleCreateEntries(WardWithParticle);

            Assert.That(particles, Has.Count.EqualTo(1));
            Assert.That(particles[0], Does.Contain("particle_create"));
            Assert.That(particles[0], Does.Contain("frozen_formation_ambient.vpcf"));
        }

        [Test]
        public void ExtractParticleCreateEntries_NoParticles_ReturnsEmpty()
        {
            var particles = WardPatcherService.ExtractParticleCreateEntries(CosmeticWardBlock);
            Assert.That(particles, Is.Empty);
        }

        [Test]
        public void ExtractParticleCreateEntries_DefaultWard_ReturnsEmpty()
        {
            var particles = WardPatcherService.ExtractParticleCreateEntries(DefaultWardBlock);
            Assert.That(particles, Is.Empty);
        }

        #endregion

        #region GetModelMapping Tests

        [Test]
        public void GetModelMapping_SingleModel_MapsToBaseFile()
        {
            var models = WardPatcherService.ParseWardVisuals(CosmeticWardBlock);
            var mappings = WardPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(1));
            Assert.That(mappings[0].TargetFileName, Is.EqualTo("default_ward.vmdl_c"));
            Assert.That(mappings[0].SourcePath, Does.Contain("nightsilver.vmdl"));
        }

        [Test]
        public void GetModelMapping_EmptyModels_ReturnsEmpty()
        {
            var mappings = WardPatcherService.GetModelMapping(new List<WardModelInfo>());
            Assert.That(mappings, Is.Empty);
        }

        [Test]
        public void GetModelMapping_StyledWard_UsesFirstUniqueModel()
        {
            // No style filter — returns both models, mapping uses first
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock);
            var mappings = WardPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(1));
            Assert.That(mappings[0].TargetFileName, Is.EqualTo("default_ward.vmdl_c"));
        }

        [Test]
        public void GetModelMapping_StyledWardWithFilter_UsesFilteredModel()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock, styleIndex: 3);
            var mappings = WardPatcherService.GetModelMapping(models);

            Assert.That(mappings, Has.Count.EqualTo(1));
            Assert.That(mappings[0].SourcePath, Does.Contain("lvl3"));
        }

        #endregion

        #region GetVpkExtractionPaths Tests

        [Test]
        public void GetVpkExtractionPaths_AppendsSuffix()
        {
            var models = WardPatcherService.ParseWardVisuals(CosmeticWardBlock);
            var paths = WardPatcherService.GetVpkExtractionPaths(models);

            Assert.That(paths, Has.Count.EqualTo(1));
            Assert.That(paths[0], Does.EndWith("_c"));
            Assert.That(paths[0], Does.Contain("nightsilver.vmdl_c"));
        }

        [Test]
        public void GetVpkExtractionPaths_StyledWard_DeduplicatesPaths()
        {
            var models = WardPatcherService.ParseWardVisuals(StyledWardBlock);
            var paths = WardPatcherService.GetVpkExtractionPaths(models);

            // 2 unique styled models
            Assert.That(paths, Has.Count.EqualTo(2));
            Assert.That(paths.All(p => p.EndsWith("_c")), Is.True);
        }

        #endregion

        #region BuildMergedWardBlock Tests

        [Test]
        public void BuildMergedWardBlock_PreservesImmutableFields()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // Immutable fields from default must be preserved
            Assert.That(merged, Does.Contain("\"name\"\t\t\"Default Ward\""));
            Assert.That(merged, Does.Contain("\"prefab\"\t\t\"ward\""));
            Assert.That(merged, Does.Contain("\"baseitem\"\t\t\"1\""));
        }

        [Test]
        public void BuildMergedWardBlock_MergesMutableFields()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // Mutable fields should come from selected ward
            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"rare\""));
            Assert.That(merged, Does.Contain("\"item_rarity\"\t\t\"rare\""));
            Assert.That(merged, Does.Contain("nightsilver"));
        }

        [Test]
        public void BuildMergedWardBlock_PreservesDefaultModel()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // Default ward model reference must stay in visuals
            // (actual model swap happens at file level)
            Assert.That(merged, Does.Contain("default_ward.vmdl"));
        }

        [Test]
        public void BuildMergedWardBlock_AppendsParticleCreate()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, WardWithParticle);

            // particle_create from selected ward should be appended
            Assert.That(merged, Does.Contain("particle_create"));
            Assert.That(merged, Does.Contain("frozen_formation_ambient.vpcf"));
        }

        [Test]
        public void BuildMergedWardBlock_UsesCorrectItemId()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // Must use default ward ID 596, NOT the selected ward's ID
            Assert.That(merged, Does.Contain("\"596\""));
            Assert.That(merged, Does.Not.Contain("\"15001\""));
        }

        [Test]
        public void BuildMergedWardBlock_EmptyInputs_ReturnsDefault()
        {
            var result = WardPatcherService.BuildMergedWardBlock("", CosmeticWardBlock);
            Assert.That(result, Is.EqualTo(""));

            var result2 = WardPatcherService.BuildMergedWardBlock(DefaultWardBlock, "");
            Assert.That(result2, Is.EqualTo(DefaultWardBlock));
        }

        [Test]
        public void BuildMergedWardBlock_InjectsSkinField()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, WardWithSkinBlock);

            // Skin field from selected ward should be injected into default visuals
            Assert.That(merged, Does.Contain("\"skin\"\t\t\"2\""));
            // Default model must still be present
            Assert.That(merged, Does.Contain("default_ward.vmdl"));
            // Mutable fields from selected
            Assert.That(merged, Does.Contain("\"item_quality\"\t\t\"unusual\""));
        }

        [Test]
        public void BuildMergedWardBlock_NoSkinField_WhenNotPresent()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // Cosmetic ward has no skin field — should not appear
            Assert.That(merged, Does.Not.Contain("\"skin\""));
        }

        [Test]
        public void BuildMergedWardBlock_SelectedModelNotInMergedVisuals()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            // The selected ward's model path should NOT appear in the merged visuals
            // because model swap is done at file level
            Assert.That(merged, Does.Not.Contain("nightsilver.vmdl"));
            // But the default model must remain
            Assert.That(merged, Does.Contain("default_ward.vmdl"));
        }

        [Test]
        public void BuildMergedWardBlock_PreservesRelativeIndentation()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, CosmeticWardBlock);

            var lines = merged.Split('\n');

            // Find the portraits block region
            int portraitsIdx = -1, visualsIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart('\t', ' ').TrimEnd('\r', '\n');
                if (trimmed.StartsWith("\"portraits\"") && portraitsIdx < 0) portraitsIdx = i;
                if (trimmed.StartsWith("\"visuals\"") && portraitsIdx >= 0) { visualsIdx = i; break; }
            }

            Assert.That(portraitsIdx, Is.GreaterThanOrEqualTo(0), "portraits block should exist");
            Assert.That(visualsIdx, Is.GreaterThan(portraitsIdx), "visuals should come after portraits");

            // Verify nested indentation depth increases
            var portraitsRegion = lines.Skip(portraitsIdx).Take(visualsIdx - portraitsIdx).ToArray();

            var headerLine = portraitsRegion.First();
            var fovLine = portraitsRegion.FirstOrDefault(l => l.Contains("PortraitFOV"));

            Assert.That(fovLine, Is.Not.Null, "PortraitFOV should exist in portraits");

            int headerTabs = headerLine.TakeWhile(c => c == '\t').Count();
            int fovTabs = fovLine!.TakeWhile(c => c == '\t').Count();

            Assert.That(fovTabs, Is.GreaterThan(headerTabs),
                $"PortraitFOV ({fovTabs} tabs) should be deeper than portraits ({headerTabs} tabs)");
        }

        [Test]
        public void BuildMergedWardBlock_WithStyle_KeepsDefaultModel()
        {
            var merged = WardPatcherService.BuildMergedWardBlock(
                DefaultWardBlock, StyledWardBlock, styleIndex: 3);

            // Default model must stay regardless of style
            Assert.That(merged, Does.Contain("default_ward.vmdl"));
            // Styled model paths should NOT be in visuals
            Assert.That(merged, Does.Not.Contain("styled_ward_lvl3.vmdl"));
            Assert.That(merged, Does.Not.Contain("styled_ward_lvl0.vmdl"));
        }

        #endregion

        #region Constants Tests

        [Test]
        public void DefaultWardItemId_Is596()
        {
            Assert.That(WardPatcherService.DefaultWardItemId, Is.EqualTo("596"));
        }

        [Test]
        public void AllBaseFiles_ContainsDefaultWard()
        {
            Assert.That(WardPatcherService.AllBaseFiles, Has.Length.EqualTo(1));
            Assert.That(WardPatcherService.AllBaseFiles[0], Is.EqualTo("default_ward.vmdl_c"));
        }

        #endregion
    }
}
