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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class HeroScopeGuardTests
    {
        private const string Lich = "npc_dota_hero_lich";

        private static bool Foreign(string heroId, string rel, string? named = null)
            => HeroScopeGuard.IsForeign(rel, HeroScopeGuard.OwnTokens(heroId), named);

        [TestCase(Lich, "models/heroes/leshrac/leshrac.vmdl_c")]
        [TestCase(Lich, "sounds/weapons/hero/leshrac/split_earth.vsnd_c")]
        [TestCase("npc_dota_hero_phoenix", "sounds/weapons/hero/leshrac/split_earth.vsnd_c")]
        [TestCase("npc_dota_hero_lycan", "sounds/weapons/hero/lich/frost_nova.vsnd_c")]
        [TestCase("npc_dota_hero_templar_assassin", "sounds/weapons/hero/axe/attack01.vsnd_c")]
        [TestCase("npc_dota_hero_witch_doctor", "models/heroes/meepo/hood.vmdl_c")]
        [TestCase("npc_dota_hero_witch_doctor", "particles/units/heroes/hero_meepo/meepo_earthbind.vpcf_c")]
        [TestCase(Lich, "soundevents/game_sounds_heroes/game_sounds_leshrac.vsndevts_c")]
        public void AnotherHerosFile_IsForeign(string heroId, string rel) =>
            Assert.That(Foreign(heroId, rel), Is.True);

        [TestCase(Lich, "models/heroes/lich/lich.vmdl_c")]
        [TestCase(Lich, "particles/units/heroes/hero_lich/lich_ambient.vpcf_c")]
        [TestCase(Lich, "sounds/weapons/hero/lich/frost_nova.vsnd_c")]
        [TestCase(Lich, "soundevents/game_sounds_heroes/game_sounds_lich.vsndevts_c")]
        [TestCase("npc_dota_hero_templar_assassin", "models/heroes/lanaya/lanaya.vmdl_c")]
        [TestCase("npc_dota_hero_tusk", "models/heroes/tuskarr/tuskarr.vmdl_c")]
        [TestCase("npc_dota_hero_riki", "models/heroes/rikimaru/rikimaru.vmdl_c")]
        [TestCase("npc_dota_hero_storm_spirit", "particles/units/heroes/hero_stormspirit/stormspirit_ball_lightning.vpcf_c")]
        [TestCase("npc_dota_hero_skeleton_king", "particles/units/heroes/hero_skeletonking/skeletonking_hellfireblast.vpcf_c")]
        [TestCase("npc_dota_hero_invoker", "models/heroes/invoker_kid/invoker_kid.vmdl_c")]
        [TestCase("npc_dota_hero_tiny", "models/heroes/tiny_04/tiny_04.vmdl_c")]
        [TestCase("npc_dota_hero_grimstroke", "particles/units/heroes/hero_demonartist/demonartist_ambient.vpcf_c")]
        [TestCase(Lich, "Models/Heroes/LICH/lich.vmdl_c")]
        public void OwnFile_IsNotForeign(string heroId, string rel) =>
            Assert.That(Foreign(heroId, rel), Is.False);

        [TestCase("materials/models/heroes/leshrac/leshrac_color.vmat_c")]
        [TestCase("particles/econ/items/leshrac/leshrac_tormented_staff_retro/x.vpcf_c")]
        [TestCase("kisilev_ind/models/leshrac/leshrac_kisilev_ind.vmdl_c")]
        [TestCase("amt_ardysa/models/items/leshrac/staff.vmdl_c")]
        [TestCase("panorama/images/spellicons/leshrac_split_earth_png.vtex_c")]
        [TestCase("models/heroes/shared/shared_ground.vmdl_c")]
        [TestCase("models/heroes/pedestal/effigy_pedestal.vmdl_c")]
        public void PathOutsideAnyHeroFolder_IsNeverForeign(string rel) =>
            Assert.That(Foreign(Lich, rel), Is.False);

        [Test]
        public void AFileTheHerosOwnBlockNames_IsKept()
        {
            const string block = "\"asset_modifier\"\n{\n\t\"type\"\t\t\"particle\"\n" +
                                 "\t\"asset\"\t\t\"particles/units/heroes/hero_sniper/sniper_headshot_slow.vpcf\"\n}";
            const string rel = "particles/units/heroes/hero_sniper/sniper_headshot_slow.vpcf_c";
            Assert.Multiple(() =>
            {
                Assert.That(Foreign("npc_dota_hero_snapfire", rel, block), Is.False);
                Assert.That(Foreign("npc_dota_hero_snapfire", rel, null), Is.True);
                Assert.That(Foreign("npc_dota_hero_snapfire", rel, block.Replace("sniper_headshot_slow", "other")), Is.True);
            });
        }

        [Test]
        public void OwnTokens_AlwaysHoldTheNpcName()
        {
            var own = HeroScopeGuard.OwnTokens("npc_dota_hero_not_in_registry");
            Assert.That(own, Is.EquivalentTo(new[] { "notinregistry" }));
        }


        private string _tmp = "";

        [SetUp]
        public void SetUp() => Directory.CreateDirectory(_tmp = Path.Combine(Path.GetTempPath(), "amt-scope-" + Guid.NewGuid().ToString("N")));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tmp, true); } catch (IOException) { }
        }

        private string SetFolder(string name, params (string Rel, string Text)[] files)
        {
            string root = Path.Combine(_tmp, name);
            foreach (var (rel, text) in files)
            {
                string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            return root;
        }

        [Test]
        public async Task Merge_ALaterSetCannotReplaceAnotherHerosBody()
        {
            const string body = "models/heroes/leshrac/leshrac.vmdl_c";
            var service = new HeroGenerationService();
            string extract = Path.Combine(_tmp, "extract");
            Directory.CreateDirectory(extract);

            await service.MergeSetAssetsAsync(SetFolder("leshrac_mix", (body, "staff body")), extract, CancellationToken.None);

            var own = HeroScopeGuard.OwnTokens(Lich);
            var skipped = new List<string>();
            var (_, copied) = await service.MergeSetAssetsAsync(
                SetFolder("lich_mix", ("models/heroes/lich/lich.vmdl_c", "lich body"), (body, "stray vanilla body")),
                extract, CancellationToken.None, rel => HeroScopeGuard.IsForeign(rel, own, null), skipped);

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(Path.Combine(extract, body.Replace('/', Path.DirectorySeparatorChar))),
                    Is.EqualTo("staff body"));
                Assert.That(copied, Is.EquivalentTo(new[] { "models/heroes/lich/lich.vmdl_c" }));
                Assert.That(skipped, Is.EquivalentTo(new[] { body }));
            });
        }

        [Test]
        public async Task Merge_WithoutAGuard_CopiesEverythingAsBefore()
        {
            var service = new HeroGenerationService();
            string extract = Path.Combine(_tmp, "extract");
            Directory.CreateDirectory(extract);
            var (_, copied) = await service.MergeSetAssetsAsync(
                SetFolder("lich_mix", ("models/heroes/lich/lich.vmdl_c", "a"), ("models/heroes/leshrac/leshrac.vmdl_c", "b")),
                extract, CancellationToken.None);
            Assert.That(copied, Has.Count.EqualTo(2));
        }
    }
}
