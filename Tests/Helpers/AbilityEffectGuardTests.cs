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
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class AbilityEffectGuardTests
    {
        private const string Cleave = "particles/units/heroes/hero_sven/sven_spell_great_cleave.vpcf_c";

        [TestCase("ability1")]
        [TestCase("ability2")]
        [TestCase("ability9")]
        public void IsValidTag_AbilityNumber_IsAccepted(string tag) =>
            Assert.That(AbilityEffectGuard.IsValidTag(tag), Is.True);

        [TestCase("effect_ability1_head_apex", "item_head_apex")]
        [TestCase("Effect_Ability9_Back_Artistry_Cape", "item_back_artistry_cape")]
        [TestCase(" effect_ability2_shoulder_faith_golden ", "item_shoulder_faith_golden")]
        [TestCase("effect_ability0_head_apex", null)]
        [TestCase("effect_ability10_head_apex", null)]
        [TestCase("effect_ability1_", null)]
        [TestCase("effect_head_apex", null)]
        [TestCase("item_head_apex", null)]
        [TestCase("effect_ability1_../x", null)]
        [TestCase(null, null)]
        public void CarrierStem_MapsEffectBackToItsItem(string? effect, string? item) =>
            Assert.That(AbilityEffectGuard.CarrierStem(effect), Is.EqualTo(item));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ability0")]
        [TestCase("ability10")]
        [TestCase("Ability2")]
        [TestCase("weapon")]
        [TestCase("ultimate")]
        public void IsValidTag_AnythingElse_IsRejected(string? tag) =>
            Assert.That(AbilityEffectGuard.IsValidTag(tag), Is.False);

        [Test]
        public void ValidateLayerFiles_HeroAndEconParticles_AreClean()
        {
            var problems = AbilityEffectGuard.ValidateLayerFiles(new[]
            {
                Cleave,
                @"particles\units\heroes\hero_sven\sven_spell_great_cleave_crit.vpcf_c",
                "particles/econ/items/sven/sven_ti7_sword/sven_ti7_sword_spell_great_cleave.vpcf_c",
            });

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ValidateLayerFiles_FlatAbilityIcon_IsClean()
        {
            var problems = AbilityEffectGuard.ValidateLayerFiles(new[]
            {
                "particles/units/heroes/hero_invoker/invoker_quas_orb.vpcf_c",
                @"panorama\images\spellicons\invoker_quas_png.vtex_c",
            });

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ValidateLayerFiles_IconOnly_IsClean()
        {
            Assert.That(AbilityEffectGuard.ValidateLayerFiles(new[] { "panorama/images/spellicons/sven_great_cleave_png.vtex_c" }),
                Is.Empty);
        }

        [Test]
        public void ValidateLayerFiles_NoFiles_IsRejected()
        {
            Assert.That(AbilityEffectGuard.ValidateLayerFiles(Array.Empty<string>()), Has.Count.EqualTo(1));
            Assert.That(AbilityEffectGuard.ValidateLayerFiles(null), Has.Count.EqualTo(1));
        }

        [TestCase("models/heroes/sven/sven_sword.vmdl_c")]
        [TestCase("soundevents/game_sounds_heroes/game_sounds_sven.vsndevts_c")]
        [TestCase("index.txt")]
        [TestCase("particles/units/heroes/hero_sven/sven_sword.vpcf")]
        [TestCase("materials/particle/sven/cleave.vmat_c")]
        [TestCase("particles/items_fx/blink_dagger_start.vpcf_c")]
        [TestCase("particles/items2_fx/manta_phase.vpcf_c")]
        [TestCase("particles/../models/heroes/sven/sven.vmdl_c")]
        [TestCase("panorama/images/spellicons/invoker/magus_apex/invoker_quas_png.vtex_c")]
        [TestCase("panorama/images/items/blink_png.vtex_c")]
        [TestCase("panorama/images/heroes/npc_dota_hero_invoker_png.vtex_c")]
        [TestCase("panorama/layout/hud/hud.vxml_c")]
        [TestCase("panorama/images/spellicons/invoker_quas.png")]
        [TestCase("panorama/images/spellicons/../../layout/x.vtex_c")]
        [TestCase("sounds/null.vsnd_c")]
        [TestCase("sounds/ui/ui_select.vsnd_c")]
        [TestCase("sounds/items/blink_dagger.vsnd_c")]
        [TestCase("sounds/music/valve_dota_001/stingers/kill.vsnd_c")]
        [TestCase("sounds/weapons/hero/lion/lion_voodoo.vsnd")]
        [TestCase("sounds/../models/heroes/lion/lion.vmdl_c")]
        [TestCase("models/heroes/lion/lion.vmdl_c")]
        [TestCase("models/items/hex/fish_hex/fish_hex.vmdl_c")]
        [TestCase("models/props_gameplay/frog.vmdl")]
        [TestCase("models/props_gameplay/x/frog.vmdl_c")]
        [TestCase("models/props_gameplay/pig.vmdl_c")]
        [TestCase("models/creeps/neutral_creeps/n_creep_troll_skeleton/n_creep_skeleton_melee.vmdl_c")]
        [TestCase("materials/models/props_gameplay/frog.vmat_c")]
        [TestCase("/particles/units/heroes/hero_sven/x.vpcf_c")]
        [TestCase(@"C:\particles\x.vpcf_c")]
        public void ValidateLayerFiles_ForbiddenPath_IsRejected(string bad)
        {
            var problems = AbilityEffectGuard.ValidateLayerFiles(new[] { Cleave, bad });

            Assert.That(problems, Has.Count.EqualTo(1));
        }

        private static IEnumerable<string> EffectModelPaths() => AbilityEffectGuard.EffectModels;

        [TestCaseSource(nameof(EffectModelPaths))]
        public void ValidateLayerFiles_HexOrSummonModel_IsClean(string model) =>
            Assert.That(AbilityEffectGuard.ValidateLayerFiles(new[] { Cleave, model }), Is.Empty);

        [Test]
        public void ValidateLayerFiles_FinKingsCharmShape_IsClean()
        {
            var files = new[]
            {
                "particles/units/heroes/hero_lion/lion_spell_voodoo.vpcf_c",
                "particles/units/heroes/hero_lion/lion_spell_voodoo_ambient.vpcf_c",
                @"models\props_gameplay\frog.vmdl_c",
                "panorama/images/spellicons/lion_voodoo_png.vtex_c",
            };

            Assert.That(AbilityEffectGuard.ValidateLayerFiles(files), Is.Empty);
        }

        [TestCase("sounds/weapons/hero/lion/lion_voodoo.vsnd_c")]
        [TestCase("sounds/ambient/frog.vsnd_c")]
        [TestCase(@"sounds\weapons\hero\sven\warcry.vsnd_c")]
        public void ValidateLayerFiles_AbilitySound_IsClean(string sound)
        {
            Assert.That(AbilityEffectGuard.IsEffectSound(sound), Is.True);
            Assert.That(AbilityEffectGuard.ValidateLayerFiles(new[] { Cleave, sound }), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("sounds/null.vsnd_c")]
        [TestCase("SOUNDS/UI/x.vsnd_c")]
        [TestCase("soundevents/game_sounds_heroes/game_sounds_lion.vsndevts_c")]
        public void IsEffectSound_SharedOrNotAudio_IsFalse(string? path) =>
            Assert.That(AbilityEffectGuard.IsEffectSound(path), Is.False);

        [Test]
        public void EffectModels_AreFlatCompiledVanillaPaths()
        {
            Assert.That(AbilityEffectGuard.EffectModels, Is.Not.Empty);
            foreach (var m in AbilityEffectGuard.EffectModels)
            {
                Assert.That(m, Does.Match(@"^models/(props_gameplay|heroes/[a-z0-9_]+)/[a-z0-9_]+\.vmdl_c$"), m);
                var folder = m.Split('/')[^2];
                Assert.That(m, Does.Not.EndWith($"/{folder}/{folder}.vmdl_c"), $"{m} is a hero body");
            }
        }

        [Test]
        public void FindOverlaps_TwoHexEffectsShareTheProp_NamesBothLayers()
        {
            var overlaps = AbilityEffectGuard.FindOverlaps(new Dictionary<string, ISet<string>>
            {
                ["Set 3"] = new HashSet<string> { "models/props_gameplay/frog.vmdl_c" },
                ["Set 4"] = new HashSet<string> { @"models\props_gameplay\frog.vmdl_c" },
            });

            Assert.That(overlaps["models/props_gameplay/frog.vmdl_c"], Is.EquivalentTo(new[] { "Set 3", "Set 4" }));
        }

        [Test]
        public void FindOverlaps_SameFileInTwoEffects_NamesBothLayers()
        {
            var overlaps = AbilityEffectGuard.FindOverlaps(new Dictionary<string, ISet<string>>
            {
                ["Set 7"] = new HashSet<string> { Cleave },
                ["Set 8"] = new HashSet<string> { Cleave.Replace('/', '\\'), "particles/units/heroes/hero_sven/sven_warcry.vpcf_c" },
            });

            Assert.That(overlaps.Keys, Is.EquivalentTo(new[] { Cleave }));
            Assert.That(overlaps[Cleave], Is.EquivalentTo(new[] { "Set 7", "Set 8" }));
        }

        [Test]
        public void FindOverlaps_SameIconInTwoEffects_IsAnOverlap()
        {
            const string icon = "panorama/images/spellicons/invoker_quas_png.vtex_c";
            var overlaps = AbilityEffectGuard.FindOverlaps(new Dictionary<string, ISet<string>>
            {
                ["Set 22"] = new HashSet<string> { icon },
                ["Set 25"] = new HashSet<string> { icon.ToUpperInvariant() },
            });

            Assert.That(overlaps, Has.Count.EqualTo(1));
        }

        [Test]
        public void FindOverlaps_DisjointEffects_IsEmpty()
        {
            var overlaps = AbilityEffectGuard.FindOverlaps(new Dictionary<string, ISet<string>>
            {
                ["Set 7"] = new HashSet<string> { Cleave },
                ["Set 8"] = new HashSet<string> { "particles/units/heroes/hero_sven/sven_warcry.vpcf_c" },
            });

            Assert.That(overlaps, Is.Empty);
            Assert.That(AbilityEffectGuard.FindOverlaps(null), Is.Empty);
        }
    }
}
