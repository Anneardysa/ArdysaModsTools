/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class HeroParticleInjectorTests
    {
        private const string Gold = "particles/econ/seasonal/new_bloom/seasonal_ambient_gold_drop.vpcf";
        private const string Raven = "particles/econ/seasonal/crownfall/seasonal_ambient_screeauk_v2.vpcf";

        private static string Block(string body) =>
            "\t\t\"7\"\n\t\t{\n\t\t\t\"name\"\t\t\"Juggernaut's Weapon\"\n" + body + "\t\t}";

        private static string Visuals(params string[] modifiers) =>
            "\t\t\t\"visuals\"\n\t\t\t{\n" + string.Concat(modifiers) + "\t\t\t}\n";

        private static string Mod(string key, string type, string value) =>
            $"\t\t\t\t\"{key}\"\n\t\t\t\t{{\n\t\t\t\t\t\"type\"\t\t\"{type}\"\n\t\t\t\t\t\"modifier\"\t\t\"{value}\"\n\t\t\t\t}}\n";

        private static int Count(string text, string pattern) => Regex.Matches(text, pattern).Count;

        #region Inject

        [Test]
        public void Inject_ExistingVisuals_AppendsNextIndexInsideIt()
        {
            var block = Block("\t\t\t\"item_rarity\"\t\t\"common\"\n" + Visuals(Mod("asset_modifier0", "model", "a.vmdl")));

            var result = HeroParticleInjector.Inject(block, Gold);

            Assert.That(result, Does.Contain("\t\t\t\t\"asset_modifier1\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"particle_create\"\n" +
                $"\t\t\t\t\t\"modifier\"\t\t\"{Gold}\"\n\t\t\t\t}}\n\t\t\t}}\n"));
            Assert.That(KeyValuesBlockHelper.ParseKvBlocks(result).ContainsKey("7"), Is.True, "still one balanced block");
            Assert.That(Count(result, "\"visuals\""), Is.EqualTo(1));
        }

        [Test]
        public void Inject_NoVisuals_CreatesOneBeforeTheClosingBrace()
        {
            var result = HeroParticleInjector.Inject(Block("\t\t\t\"item_rarity\"\t\t\"common\"\n"), Gold);

            Assert.That(result, Does.EndWith("\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier0\"\n\t\t\t\t{\n" +
                $"\t\t\t\t\t\"type\"\t\t\"particle_create\"\n\t\t\t\t\t\"modifier\"\t\t\"{Gold}\"\n\t\t\t\t}}\n\t\t\t}}\n\t\t}}"));
        }

        [Test]
        public void Inject_VisualsOnlyInsideStyles_StillCreatesTheTopLevelOne()
        {
            var block = Block("\t\t\t\"styles\"\n\t\t\t{\n\t\t\t\t\"visuals\"\n\t\t\t\t{\n\t\t\t\t}\n\t\t\t}\n");

            var result = HeroParticleInjector.Inject(block, Gold);

            Assert.That(Count(result, "\"visuals\""), Is.EqualTo(2));
            Assert.That(result, Does.Contain("\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier0\""));
        }

        [Test]
        public void Inject_NumberingWithHolesAndBareKeys_TakesMaxPlusOne()
        {
            var block = Block(Visuals(
                Mod("asset_modifier", "model", "a.vmdl"),
                Mod("asset_modifier0", "model", "b.vmdl"),
                Mod("asset_modifier3", "model", "c.vmdl")));

            var result = HeroParticleInjector.Inject(block, Gold);

            Assert.That(result, Does.Contain("\"asset_modifier4\""));
            Assert.That(Count(result, "\"asset_modifier4\""), Is.EqualTo(1));
        }

        [Test]
        public void Inject_TwoParticles_TakeConsecutiveIndices()
        {
            var once = HeroParticleInjector.Inject(Block(Visuals(Mod("asset_modifier0", "model", "a.vmdl"))), Gold);
            var twice = HeroParticleInjector.Inject(once, Raven);

            Assert.That(Regex.Match(twice, @"""asset_modifier1""\s*\{[^}]*" + Regex.Escape(Gold)).Success, Is.True);
            Assert.That(Regex.Match(twice, @"""asset_modifier2""\s*\{[^}]*" + Regex.Escape(Raven)).Success, Is.True);
        }

        [Test]
        public void Inject_SamePathTwice_IsIdempotent()
        {
            var once = HeroParticleInjector.Inject(Block(Visuals()), Gold);

            Assert.That(HeroParticleInjector.Inject(once, Gold), Is.EqualTo(once));
        }

        [Test]
        public void Inject_PathOnlyAsARedirectTarget_StillAddsTheParticle()
        {
            var block = Block(Visuals(Mod("asset_modifier0", "particle", Gold)));

            Assert.That(Count(HeroParticleInjector.Inject(block, Gold), "particle_create"), Is.EqualTo(1));
        }

        [Test]
        public void Inject_CrlfBlock_StaysCrlf()
        {
            var block = Block("\t\t\t\"item_rarity\"\t\t\"common\"\n" + Visuals(Mod("asset_modifier0", "model", "a.vmdl"))).Replace("\n", "\r\n");

            var result = HeroParticleInjector.Inject(block, Gold);

            Assert.That(Regex.IsMatch(result, "(?<!\r)\n"), Is.False, "no bare LF introduced");
        }

        [Test]
        public void Inject_LeavesEveryOriginalLineInPlace()
        {
            var block = Block("\t\t\t\"item_rarity\"\t\t\"common\"\n\t\t\t\"prefab\"\t\t\"default_item\"\n" + Visuals(Mod("asset_modifier0", "model", "a.vmdl")));

            var result = HeroParticleInjector.Inject(block, Gold);
            var quality = "\t\t\t\"item_quality\"\t\t\"unusual\"\n";
            var modifier = "\t\t\t\t\"asset_modifier1\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"particle_create\"\n" +
                           $"\t\t\t\t\t\"modifier\"\t\t\"{Gold}\"\n\t\t\t\t}}\n";

            Assert.That(result.Replace(quality, "").Replace(modifier, ""), Is.EqualTo(block),
                "only the two inserted pieces differ");
        }

        #endregion

        #region Quality

        [Test]
        public void Quality_LandsDirectlyAboveItemRarity()
        {
            var result = HeroParticleInjector.Inject(Block("\t\t\t\"item_rarity\"\t\t\"common\"\n"), Gold);

            Assert.That(result, Does.Contain("\t\t\t\"item_quality\"\t\t\"unusual\"\n\t\t\t\"item_rarity\"\t\t\"common\"\n"));
        }

        [Test]
        public void Quality_ExistingValue_IsOverwrittenNotDoubled()
        {
            var result = HeroParticleInjector.Inject(Block("\t\t\t\"item_quality\"\t\t\"genuine\"\n"), Gold);

            Assert.That(Count(result, "\"item_quality\""), Is.EqualTo(1));
            Assert.That(result, Does.Contain("\"item_quality\"\t\t\"unusual\""));
        }

        [Test]
        public void Quality_ForcedQuality_IsRemoved()
        {
            var result = HeroParticleInjector.Inject(Block("\t\t\t\"forced_item_quality\"\t\t\"strange\"\n\t\t\t\"item_rarity\"\t\t\"rare\"\n"), Gold);

            Assert.That(result, Does.Not.Contain("forced_item_quality"));
            Assert.That(result, Does.Contain("\"item_quality\"\t\t\"unusual\""));
        }

        [Test]
        public void Quality_NoRarity_GoesFirstInTheBlock()
        {
            var result = HeroParticleInjector.Inject(Block(""), Gold);

            Assert.That(result, Does.StartWith("\t\t\"7\"\n\t\t{\n\t\t\t\"item_quality\"\t\t\"unusual\"\n\t\t\t\"name\""));
        }

        [Test]
        public void Quality_NestedItemQuality_IsNotTheHeaderOne()
        {
            var block = Block("\t\t\t\"item_rarity\"\t\t\"common\"\n\t\t\t\"styles\"\n\t\t\t{\n\t\t\t\t\"item_quality\"\t\t\"genuine\"\n\t\t\t}\n");

            var result = HeroParticleInjector.Inject(block, Gold);

            Assert.That(result, Does.Contain("\t\t\t\t\"item_quality\"\t\t\"genuine\""), "nested value untouched");
            Assert.That(result, Does.Contain("\t\t\t\"item_quality\"\t\t\"unusual\"\n\t\t\t\"item_rarity\""));
        }

        #endregion

        #region Refusal

        [TestCase("../particles/x.vpcf")]
        [TestCase("particles/../x.vpcf")]
        [TestCase("particles/a\".vpcf")]
        [TestCase("particles/a.vpcf\n\"model_player\" \"x\"")]
        [TestCase("particles/a{b}.vpcf")]
        [TestCase("models/heroes/juggernaut/jugg_sword.vmdl")]
        [TestCase("particles/a.vpcf_c")]
        [TestCase("")]
        [TestCase(null)]
        public void Inject_PathThatIsNotAPlainParticle_IsRefused(string? path)
        {
            Assert.Throws<ArgumentException>(() => HeroParticleInjector.Inject(Block(""), path!));
        }

        [Test]
        public void Inject_NoBlock_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => HeroParticleInjector.Inject("", Gold));
        }

        #endregion

        #region HasWearableModel

        [Test]
        public void HasWearableModel_ReadsTheTopLevelModelOnly()
        {
            Assert.That(HeroParticleInjector.HasWearableModel(Block("\t\t\t\"model_player\"\t\t\"models/a.vmdl\"\n")), Is.True);
            Assert.That(HeroParticleInjector.HasWearableModel(Block("\t\t\t\"model_player\"\t\t\"\"\n")), Is.False, "blanked by the worn bake");
            Assert.That(HeroParticleInjector.HasWearableModel(Block("")), Is.False);
            Assert.That(HeroParticleInjector.HasWearableModel(Block(
                "\t\t\t\"styles\"\n\t\t\t{\n\t\t\t\t\"model_player\"\t\t\"models/a.vmdl\"\n\t\t\t}\n")), Is.False, "a style's model is not the item's");
            Assert.That(HeroParticleInjector.HasWearableModel(null), Is.False);
        }

        #endregion

        #region Sanitize

        private static HeroModel Jugg() => new()
        {
            HeroId = "npc_dota_hero_juggernaut",
            Name = "Juggernaut",
            ItemIds = new List<int> { 6, 7, 8, 9, 62, 811 }
        };

        private static ParticlePick P(string id, string host) => new() { Id = id, Host = host };

        [Test]
        public void Sanitize_NullInputs_AreEmpty()
        {
            Assert.That(HeroParticleInjector.Sanitize(null, new[] { P("833", "7") }), Is.Empty);
            Assert.That(HeroParticleInjector.Sanitize(Jugg(), null), Is.Empty);
            Assert.That(HeroParticleInjector.Sanitize(Jugg(), new ParticlePick?[] { null }), Is.Empty);
        }

        [Test]
        public void Sanitize_KeepsTheFirstTwoValidPicks_InOrder()
        {
            var trace = new List<string>();
            var kept = HeroParticleInjector.Sanitize(Jugg(), new[]
            {
                P("bogus", "7"),
                P("837", " 6 "),
                P("833", "253"),
                P("833", "7"),
                P("837", "6"),
                P("826", "8"),
            }, trace.Add);

            Assert.That(kept.Select(p => (p.Id, p.Host)), Is.EqualTo(new[] { ("837", "6"), ("833", "7") }));
            Assert.That(trace, Has.Some.Contains("not in the catalog"));
            Assert.That(trace, Has.Some.Contains("cannot carry"));
            Assert.That(trace, Has.Some.Contains("at most 2"));
        }

        [Test]
        public void Sanitize_SameParticleOnTwoSlots_IsTwoPicks()
        {
            var kept = HeroParticleInjector.Sanitize(Jugg(), new[] { P("833", "7"), P("833", "6") });

            Assert.That(kept, Has.Count.EqualTo(2));
        }

        [Test]
        public void Sanitize_CatalogIdIsCaseInsensitive_AndCanonicalized()
        {
            var kept = HeroParticleInjector.Sanitize(Jugg(), new[] { P("I11321.1", "7") });

            Assert.That(kept.Single().Id, Is.EqualTo("i11321.1"));
        }

        #endregion

        #region Slots

        private static (string Slot, string Label)? SlotOf(string id) => id switch
        {
            "6" => ("head", "Head"),
            "7" => ("", "Weapon"),
            "9" => ("arms", "Arms"),
            "610" => ("voice", "Voice"),
            "811" => ("hero_base", "Base"),
            _ => null,
        };

        private static IReadOnlyDictionary<string, (string block, string heroId)> Index(params (string id, string? slot, string? model)[] defs) =>
            defs.ToDictionary(d => d.id, d => (Block(
                (d.slot != null ? $"\t\t\t\"item_slot\"\t\t\"{d.slot}\"\n" : "")
                + (d.model != null ? $"\t\t\t\"model_player\"\t\t\"{d.model}\"\n" : "")), "npc_dota_hero_juggernaut"));

        [Test]
        public void Slots_Set_ListsModelledAndHeroBaseBlocks_InIdOrder_AndAsks()
        {
            var slots = HeroParticleInjector.Slots(
                Index(("811", "hero_base", null), ("610", "voice", null), ("9", "arms", "m/arms.vmdl"), ("7", null, "m/sword.vmdl")),
                HeroModelMapper.SkinCategory.LegacySet, null, SlotOf);

            Assert.That(slots.Slots.Select(s => (s.Id, s.Label, s.HasModel)), Is.EqualTo(new[]
            {
                ("7", "Weapon", true), ("9", "Arms", true), ("811", "Base", false),
            }), "voice has no model and is no hero_base");
            Assert.That(slots.Preferred, Is.Null);
        }

        [Test]
        public void Slots_Item_GoesToItsOwnSlot_WeaponAndAliasesIncluded()
        {
            var index = Index(("6", "head", "m/mask.vmdl"), ("7", null, "m/sword.vmdl"), ("9", "arms", "m/arms.vmdl"));

            Assert.That(HeroParticleInjector.Slots(index, HeroModelMapper.SkinCategory.Item, "weapon", SlotOf).Preferred, Is.EqualTo("7"));
            Assert.That(HeroParticleInjector.Slots(index, HeroModelMapper.SkinCategory.Item, "arm", SlotOf).Preferred, Is.EqualTo("9"));
            Assert.That(HeroParticleInjector.Slots(index, HeroModelMapper.SkinCategory.Item, "tail", SlotOf).Preferred, Is.Null,
                "no block of its slot: the page asks instead of guessing");
        }

        [Test]
        public void Slots_Base_GoesToHeroBase_ElseItsOnlyBlock()
        {
            var arcana = Index(("811", "hero_base", null), ("9", "arms", "m/arms.vmdl"));
            var zeusLike = Index(("6", "head", "m/hair.vmdl"));

            Assert.That(HeroParticleInjector.Slots(arcana, HeroModelMapper.SkinCategory.BaseHero, null, SlotOf).Preferred, Is.EqualTo("811"));
            Assert.That(HeroParticleInjector.Slots(zeusLike, HeroModelMapper.SkinCategory.BaseHero, null, SlotOf).Preferred, Is.EqualTo("6"));
        }

        #endregion
    }
}
