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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class BodyOwnershipTests
    {
        private const string Hero = "npc_dota_hero_zuus";
        private const string Base = "Set 1";
        private const string Weapon = "Set 9";

        private const string Body = "models/heroes/zeus/zeus.vmdl_c";
        private const string BodyMorph = "models/heroes/zeus/zeus/zeus_arcana_base_vmorf.vtex_c";
        private const string WeaponBodyMorph = "models/heroes/zeus/zeus/zeus_base_vmorf.vtex_c";
        private const string Swap = "models/heroes/zeus/zeus_arcana.vmdl_c";
        private const string SwapMorph = "models/heroes/zeus/zeus_arcana/zeus_arcana_base_vmorf.vtex_c";
        private const string Hair = "models/heroes/zeus/zeus_hair.vmdl_c";
        private const string Bracers = "models/heroes/zeus/zeus_bracers.vmdl_c";
        private const string Vest = "models/heroes/zeus/zeus_vest.vmdl_c";

        private static readonly string[] BaseFiles = { Body, BodyMorph, Swap, SwapMorph, Hair, Bracers, Vest };
        private static readonly string[] WeaponFiles = { Body, WeaponBodyMorph, Swap, SwapMorph };
        private static readonly string[] BodyPaths = { Body, BodyMorph, Swap, SwapMorph };

        private static string ArcanaBlock(string id = "604", string unit = Hero) =>
            $"\t\t\"{id}\"\n\t\t{{\n" +
            "\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_hair.vmdl\"\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n" +
            "\t\t\t\t\"asset_modifier1\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"response_criteria\"\n\t\t\t\t\t\"asset\"\t\t\"arcana\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier2\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"entity_model\"\n" +
            $"\t\t\t\t\t\"asset\"\t\t\"{unit}\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/heroes/zeus/zeus_arcana.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier7\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_bracers.vmdl\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/development/invisiblebox.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t}\n\t\t}";

        private const string WeaponBlock =
            "\t\t\"593\"\n\t\t{\n" +
            "\t\t\t\"model_player\"\t\t\"models/items/zeus/lightning_weapon/mesh/zeus_lightning_weapon_model.vmdl\"\n" +
            "\t\t\t\"match_cycle_to_parent\"\t\t\"1\"\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier1\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"particle\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"particles/units/heroes/hero_zuus/zuus_base_attack.vpcf\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"particles/econ/items/zeus/lightning_weapon_fx/zuus_base_attack_immortal_lightning.vpcf\"\n" +
            "\t\t\t\t}\n\t\t\t}\n\t\t}";

        private static readonly HashSet<string> SlotModels = new(StringComparer.OrdinalIgnoreCase)
        {
            Hair, Bracers, Vest,
        };

        private static Dictionary<string, (string Block, string Layer)> Blocks(params (string Id, string Block, string Layer)[] b)
            => b.ToDictionary(x => x.Id, x => (x.Block, x.Layer));

        private static Dictionary<string, ISet<string>> Files(params (string Layer, string[] Paths)[] f)
            => f.ToDictionary(x => x.Layer, x => (ISet<string>)new HashSet<string>(x.Paths, StringComparer.OrdinalIgnoreCase));

        private static Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> NoVariants()
            => new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> Variant(string layer, string key, params string[] paths)
            => new(StringComparer.OrdinalIgnoreCase)
            {
                [layer] = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [key] = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase),
                },
            };

        private static Dictionary<string, string> Owners(params (string Layer, string[] Paths)[] inOrder)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (layer, paths) in inOrder)
                foreach (var p in paths) d[p] = layer;
            return d;
        }

        private static BodyOwnership.Result Compute(
            Dictionary<string, (string Block, string Layer)> blocks,
            Dictionary<string, ISet<string>> files,
            Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> variants,
            Dictionary<string, string> owners)
            => BodyOwnership.Compute(blocks, files, variants, owners, Hero, SlotModels, defaultModelOf: Registry);

        private static string? Registry(string id)
            => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer ?? "" : null;

        #region KeyValuesBlockHelper.GetEntityModelSwaps

        [Test]
        public void GetEntityModelSwaps_ReadsOnlyEntityModelRows()
        {
            var swaps = KeyValuesBlockHelper.GetEntityModelSwaps(ArcanaBlock());
            Assert.That(swaps, Is.EqualTo(new[] { (Hero, "models/heroes/zeus/zeus_arcana.vmdl") }));
            Assert.That(KeyValuesBlockHelper.GetEntityModelSwaps(WeaponBlock), Is.Empty);
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(ArcanaBlock()),
                Is.EqualTo(new[] { "models/heroes/zeus/zeus_bracers.vmdl" }));
        }

        #endregion

        #region Styled body keys (Bladeform Legacy: two styles, one entity_model)

        private static string StyledArcanaBlock(string skin, string extra = "") =>
            ArcanaBlock().Replace("\t\t\t\t\"asset_modifier7\"",
                "\t\t\t\t\"asset_modifier8\"\n\t\t\t\t{\n" +
                "\t\t\t\t\t\"type\"\t\t\"model_skin\"\n" + extra +
                $"\t\t\t\t\t\"skin\"\t\t\"{skin}\"\n\t\t\t\t}}\n" +
                "\t\t\t\t\"asset_modifier7\"");

        private static Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> Variants(string layer, params string[] keys)
            => new(StringComparer.OrdinalIgnoreCase)
            {
                [layer] = keys.ToDictionary(k => k, _ => (ISet<string>)new HashSet<string>(WeaponFiles, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase),
            };

        private static BodyOwnership.Result StyledCompute(string ownerBlock,
            Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> variants)
            => Compute(Blocks(("604", ownerBlock, Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), variants,
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

        [Test]
        public void GetModelSkin_ReadsTheUngatedAssetlessSkinOnly()
        {
            Assert.That(KeyValuesBlockHelper.GetModelSkin(StyledArcanaBlock("1")), Is.EqualTo("1"));
            Assert.That(KeyValuesBlockHelper.GetModelSkin(StyledArcanaBlock("0")), Is.EqualTo("0"));
            Assert.That(KeyValuesBlockHelper.GetModelSkin(ArcanaBlock()), Is.Null, "no model_skin row");
            Assert.That(KeyValuesBlockHelper.GetModelSkin(
                StyledArcanaBlock("1", "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_hair.vmdl\"\n")),
                Is.Null, "a skin on another model says nothing about the body");
            Assert.That(KeyValuesBlockHelper.GetModelSkin(
                StyledArcanaBlock("1", "\t\t\t\t\t\"criteria\"\t\t\"isbetween( effects_mask, 1, 1, 20, 20 )\"\n")),
                Is.Null, "a runtime condition is not the style");
            Assert.That(KeyValuesBlockHelper.GetModelSkin(""), Is.Null);
        }

        [Test]
        public void StyledKey_BareForDefaultSkin_SuffixedOtherwise()
        {
            Assert.That(BodyOwnership.StyledKey("juggernaut_arcana", null), Is.EqualTo("juggernaut_arcana"));
            Assert.That(BodyOwnership.StyledKey("juggernaut_arcana", "0"), Is.EqualTo("juggernaut_arcana"));
            Assert.That(BodyOwnership.StyledKey("juggernaut_arcana", " 1 "), Is.EqualTo("juggernaut_arcana.skin1"));
        }

        [Test]
        public void StyledOwner_TakesTheVariantBuiltForItsStyle()
        {
            var r = StyledCompute(StyledArcanaBlock("1"), Variants(Weapon, "zeus_arcana", "zeus_arcana.skin1"));
            Assert.That(r.BodyKey, Is.EqualTo("zeus_arcana"));
            Assert.That(r.VariantApply.Values.Select(v => v.Key).Distinct(), Is.EqualTo(new[] { "zeus_arcana.skin1" }),
                "the red style's body, not the green one's");
            Assert.That(r.VariantApply.Keys, Is.EquivalentTo(WeaponFiles));
            Assert.That(r.Restore, Is.Empty);
        }

        [Test]
        public void DefaultSkinOwner_TakesTheBareVariant_EvenBesideAStyledOne()
        {
            var r = StyledCompute(StyledArcanaBlock("0"), Variants(Weapon, "zeus_arcana", "zeus_arcana.skin1"));
            Assert.That(r.VariantApply.Values.Select(v => v.Key).Distinct(), Is.EqualTo(new[] { "zeus_arcana" }));
        }

        [Test]
        public void StyledOwner_WithOnlyALegacyBareVariant_StillGetsIt()
        {
            var r = StyledCompute(StyledArcanaBlock("1"), Variants(Weapon, "zeus_arcana"));
            Assert.That(r.VariantApply.Values.Select(v => v.Key).Distinct(), Is.EqualTo(new[] { "zeus_arcana" }));
            Assert.That(r.Restore, Is.Empty);
        }

        [Test]
        public void StyledOwner_AlreadyApplied_IsClean()
        {
            var owners = Owners((Base, BaseFiles), (Weapon, WeaponFiles));
            foreach (var p in WeaponFiles)
                owners[p] = BodyOwnership.VariantOwner(Weapon, "zeus_arcana.skin1");
            var r = Compute(Blocks(("604", StyledArcanaBlock("1"), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), Variants(Weapon, "zeus_arcana", "zeus_arcana.skin1"),
                owners);
            Assert.That(r.VariantApply, Is.Empty);
        }

        #endregion

        #region KeyValuesBlockHelper.RemoveTopLevelKeys

        [Test]
        public void RemoveTopLevelKeys_CutsOnlyTopLevelScalars_Verbatim()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "model_player", "match_cycle_to_parent" };
            string outBlock = KeyValuesBlockHelper.RemoveTopLevelKeys(WeaponBlock, keys, out var removed);

            Assert.That(removed, Is.EquivalentTo(keys));
            string expected = string.Join("\n", WeaponBlock.Split('\n')
                .Where(l => !l.Contains("\"model_player\"") && !l.Contains("\"match_cycle_to_parent\"")));
            Assert.That(outBlock, Is.EqualTo(expected), "every other byte stays as authored");
            Assert.That(outBlock, Does.Contain("zuus_base_attack"), "visuals untouched");
        }

        [Test]
        public void RemoveTopLevelKeys_NeverTouchesNestedKeysOrBlocks_AndReturnsSameInstance()
        {
            string nested = "\t\t\"1\"\n\t\t{\n\t\t\t\"visuals\"\n\t\t\t{\n" +
                            "\t\t\t\t\"model_player\"\t\t\"x.vmdl\"\n\t\t\t}\n\t\t}";
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "model_player", "visuals" };
            Assert.That(KeyValuesBlockHelper.RemoveTopLevelKeys(nested, keys, out var removed), Is.SameAs(nested));
            Assert.That(removed, Is.Empty);
            Assert.That(KeyValuesBlockHelper.RemoveTopLevelKeys(nested, new HashSet<string>(), out _), Is.SameAs(nested));
        }

        #endregion

        #region BodyOwnership.Compute

        [Test]
        public void BaseArcanaThenWeapon_RestoresEveryArcanaBodyFile()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.Owner, Is.EqualTo(Base));
            Assert.That(r.BodyKey, Is.EqualTo("zeus_arcana"));
            Assert.That(r.BodyFiles, Is.EquivalentTo(BodyPaths), "slot models (hair/bracers/vest) are not body");
            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string>
            {
                [Body] = Base, [Swap] = Base, [SwapMorph] = Base,
            }));
            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.Warnings, Has.Count.EqualTo(1));
            Assert.That(r.Warnings[0], Does.Contain("built for the regular body"));
        }

        [Test]
        public void StripWearable_BakedWeaponOnArcana_NoVariant_WearableGoes()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.StripWearable, Is.EquivalentTo(new[] { "593" }));
            Assert.That(r.IsClean, Is.False);
        }

        [Test]
        public void StripWearable_BakedWeaponOnArcana_WithVariant_WearableStays()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)),
                Variant(Weapon, "zeus_arcana", Body),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.StripWearable, Is.Empty);
        }

        [Test]
        public void StripWearable_NeverWithoutAnOwner_NorForSlotsWithAModel_NorUnknownIds()
        {
            var alone = Compute(Blocks(("593", WeaponBlock, Weapon)), Files((Weapon, WeaponFiles)),
                NoVariants(), Owners((Weapon, WeaponFiles)));
            Assert.That(alone.StripWearable, Is.Empty, "alone the regular body is right, and so is its wearable");

            string arms = "\t\t\"589\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_bracers.vmdl\"\n\t\t}";
            string modder = WeaponBlock.Replace("\"593\"", "\"9999\"");
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("589", arms, Weapon), ("9999", modder, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));
            Assert.That(r.StripWearable, Is.Empty);

            var noRegistry = BodyOwnership.Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)), Hero, SlotModels);
            Assert.That(noRegistry.StripWearable, Is.Empty, "no registry, no judgement");
        }

        [Test]
        public void WeaponThenBaseArcana_AlreadyRight_NothingToRestore()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Weapon, WeaponFiles), (Base, BaseFiles)));

            Assert.That(r.Restore, Is.Empty);
            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.Warnings, Has.Count.EqualTo(1), "the weapon's bake is still dropped - say so");
        }

        [Test]
        public void WeaponAlone_NoBodyOwner_KeepsLastWriterBehaviour()
        {
            var r = Compute(
                Blocks(("593", WeaponBlock, Weapon)),
                Files((Weapon, WeaponFiles)), NoVariants(), Owners((Weapon, WeaponFiles)));

            Assert.That(r.Owner, Is.Null);
            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void WeaponWithMatchingVariant_VariantReplacesTheOwnersBody()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)),
                Variant(Weapon, "zeus_arcana", Body, BodyMorph, Swap, SwapMorph),
                Owners((Weapon, WeaponFiles), (Base, BaseFiles)));

            Assert.That(r.VariantApply, Is.EquivalentTo(BodyPaths.ToDictionary(p => p, _ => (Weapon, "zeus_arcana"))));
            Assert.That(r.Restore, Is.Empty);
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void ItemVariantOfASlotModel_IsApplied_AndTheOwnersBodyStands()
        {
            var item = new[] { Hair };
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, item)),
                Variant(Weapon, "zeus_arcana", Hair),
                Owners((Base, BaseFiles), (Weapon, item)));

            Assert.That(r.VariantApply, Is.EquivalentTo(new Dictionary<string, (string, string)>
            {
                [Hair] = (Weapon, "zeus_arcana"),
            }));
            Assert.That(r.Restore, Is.Empty, "a slot model is not a body file - nothing to give back");
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void ItemVariantOfASlotModel_WithoutABodyOwner_IsNotApplied()
        {
            var item = new[] { Hair };
            var r = Compute(
                Blocks(("593", WeaponBlock, Weapon)),
                Files((Weapon, item)),
                Variant(Weapon, "zeus_arcana", Hair),
                Owners((Weapon, item)));

            Assert.That(r.VariantApply, Is.Empty, "alone, the item dresses the regular hero");
        }

        [Test]
        public void WeaponAppliedLastWithVariant_ItsPlainCopyIsNotMistakenForTheVariant()
        {
            var owners = Owners((Base, BaseFiles), (Weapon, WeaponFiles));
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)),
                Variant(Weapon, "zeus_arcana", Body, BodyMorph, Swap, SwapMorph), owners);

            Assert.That(r.VariantApply, Is.EquivalentTo(BodyPaths.ToDictionary(p => p, _ => (Weapon, "zeus_arcana"))));

            foreach (var p in BodyPaths) owners[p] = BodyOwnership.VariantOwner(Weapon, "zeus_arcana");
            var again = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)),
                Variant(Weapon, "zeus_arcana", Body, BodyMorph, Swap, SwapMorph), owners);
            Assert.That(again.VariantApply, Is.Empty);
            Assert.That(again.Restore, Is.Empty);
        }

        [Test]
        public void VariantForAnotherBody_IsIgnored_AndTheDropIsReported()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)),
                Variant(Weapon, "zeus_persona", Body),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.Restore.Keys, Does.Contain(Body));
            Assert.That(r.Warnings.Single(), Does.Contain("built for the regular body"));
        }

        [Test]
        public void TwoLayersVariantTheSamePath_OwnerKeepsItsBody()
        {
            var variants = Variant(Weapon, "zeus_arcana", Body);
            variants["Set 2"] = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["zeus_arcana"] = new HashSet<string>(new[] { Body }, StringComparer.OrdinalIgnoreCase),
            };
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles), ("Set 2", new[] { Body })),
                variants, Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.Restore[Body], Is.EqualTo(Base));
            Assert.That(r.Warnings.Any(w => w.Contains("can't be combined")), Is.True, string.Join("\n", r.Warnings));
        }

        [Test]
        public void EntityModelForAnotherUnit_IsNotABodyOwner()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(unit: "npc_dota_zeus_cloud"), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.Owner, Is.Null);
            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void TwoBodyOwners_FirstKeepsIt_AndItIsReported()
        {
            var r = Compute(
                Blocks(("604", ArcanaBlock(), Base), ("606", ArcanaBlock("606"), "Set 4")),
                Files((Base, BaseFiles), ("Set 4", new[] { Body })), NoVariants(),
                Owners((Base, BaseFiles), ("Set 4", new[] { Body })));

            Assert.That(r.Owner, Is.EqualTo(Base));
            Assert.That(r.Restore[Body], Is.EqualTo(Base));
            Assert.That(r.Warnings.Any(w => w.Contains("both swap the hero body")), Is.True);
        }

        [Test]
        public void UnparseableBlocks_PassThrough()
        {
            var r = Compute(
                Blocks(("604", "\"604\" { \"visuals\" {", Base), ("593", "garbage", Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)));

            Assert.That(r.Owner, Is.Null);
            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void Warnings_UseTheNameAUserKnows()
        {
            var r = BodyOwnership.Compute(
                Blocks(("604", ArcanaBlock(), Base), ("593", WeaponBlock, Weapon)),
                Files((Base, BaseFiles), (Weapon, WeaponFiles)), NoVariants(),
                Owners((Base, BaseFiles), (Weapon, WeaponFiles)), Hero, SlotModels,
                layer => layer == Base ? "Tempest Helm" : "Righteous Thunderbolt");

            Assert.That(r.Warnings.Single(),
                Is.EqualTo("'Righteous Thunderbolt' is built for the regular body — with 'Tempest Helm' it shows " +
                           "without its baked mesh."));
        }

        #endregion

        #region BodyOwnership.Compute — no entity_model (Tiny forms)

        private const string TinyHero = "npc_dota_hero_tiny";
        private const string Form = "Set 11";
        private const string Sword = "Set 40";
        private const string SwordKey = "item_weapon_majesty";
        private const string T4 = "models/heroes/tiny/tiny_04/tiny_04.vmdl_c";
        private const string T4Amt = "models/heroes/tiny/tiny_04/tiny_04_amt.vmdl_c";
        private const string T4FormMorph = "models/heroes/tiny/tiny_04/tiny_04/frozen_stonehenge_lvl_04_vmorf.vtex_c";
        private const string T4SwordMorph = "models/heroes/tiny/tiny_04/tiny_04/tiny_04_model_vmorf.vtex_c";
        private const string T1 = "models/heroes/tiny/tiny_01/tiny_01.vmdl_c";
        private const string Tree = "models/heroes/tiny/tiny_tree/tiny_tree.vmdl_c";

        private static readonly string[] FormFiles = { T4, T4Amt, T4FormMorph };
        private static readonly string[] SwordFiles = { T4, T4SwordMorph, T1, Tree };
        private const string FormBlock = "\t\t\"682\"\n\t\t{\n\t\t\t\"item_slot\"\t\t\"back\"\n\t\t}";
        private const string SwordBlock =
            "\t\t\"497\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/tiny/tiny_tree/tiny_tree.vmdl\"\n\t\t}";

        private static BodyOwnership.Result ComputeTiny(
            Dictionary<string, (string Block, string Layer)> blocks,
            Dictionary<string, ISet<string>> files,
            Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> variants,
            Dictionary<string, string> owners)
            => BodyOwnership.Compute(blocks, files, variants, owners, TinyHero,
                new HashSet<string>(new[] { Tree }, StringComparer.OrdinalIgnoreCase),
                defaultModelOf: Registry, stemOf: l => l == Sword ? SwordKey : "item_large_frostmoot");

        [Test]
        public void TinyFormThenSword_NoVariant_FormKeepsItsBody_AndTheDropIsReported()
        {
            var r = ComputeTiny(
                Blocks(("682", FormBlock, Form), ("497", SwordBlock, Sword)),
                Files((Form, FormFiles), (Sword, SwordFiles)), NoVariants(),
                Owners((Form, FormFiles), (Sword, SwordFiles)));

            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string> { [T4] = Form }),
                "tiny_01 is the sword's alone - nobody else's body to protect");
            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.StripWearable, Is.Empty, "the sword's model_player IS the sword");
            Assert.That(r.Warnings.Single(), Does.Contain($"'{Sword}'").And.Contain($"'{Form}'"));
        }

        [Test]
        public void TinySwordThenForm_AlreadyRight_StillReportsTheDroppedFix()
        {
            var r = ComputeTiny(
                Blocks(("682", FormBlock, Form), ("497", SwordBlock, Sword)),
                Files((Form, FormFiles), (Sword, SwordFiles)), NoVariants(),
                Owners((Sword, SwordFiles), (Form, FormFiles)));

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void TinyFormCarryingTheSwordVariant_VariantIsTheFinalBody()
        {
            var r = ComputeTiny(
                Blocks(("682", FormBlock, Form), ("497", SwordBlock, Sword)),
                Files((Form, FormFiles), (Sword, SwordFiles)),
                Variant(Form, SwordKey, T4, T4Amt),
                Owners((Form, FormFiles), (Sword, SwordFiles)));

            Assert.That(r.VariantApply, Is.EquivalentTo(new Dictionary<string, (string, string)>
            {
                [T4] = (Form, SwordKey), [T4Amt] = (Form, SwordKey),
            }));
            Assert.That(r.Restore, Is.Empty);
            Assert.That(r.Warnings, Is.Empty);

            var applied = Owners((Form, FormFiles), (Sword, SwordFiles));
            applied[T4] = applied[T4Amt] = BodyOwnership.VariantOwner(Form, SwordKey);
            Assert.That(ComputeTiny(
                Blocks(("682", FormBlock, Form), ("497", SwordBlock, Sword)),
                Files((Form, FormFiles), (Sword, SwordFiles)),
                Variant(Form, SwordKey, T4, T4Amt), applied).IsClean, Is.True);
        }

        [Test]
        public void TinyVariantForAnotherPartner_IsIgnored()
        {
            var r = ComputeTiny(
                Blocks(("682", FormBlock, Form), ("497", SwordBlock, Sword)),
                Files((Form, FormFiles), (Sword, SwordFiles)),
                Variant(Form, "item_weapon_other", T4, T4Amt),
                Owners((Form, FormFiles), (Sword, SwordFiles)));

            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.Restore[T4], Is.EqualTo(Form));
        }

        [Test]
        public void TwoModellessLayersOnOneBody_NoJudgement_LastWriterStands()
        {
            string medium = "\t\t\"681\"\n\t\t{\n\t\t}";
            var r = ComputeTiny(
                Blocks(("682", FormBlock, Form), ("681", medium, "Set 19")),
                Files((Form, FormFiles), ("Set 19", new[] { T4 })), NoVariants(),
                Owners((Form, FormFiles), ("Set 19", new[] { T4 })));

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings.Single(), Does.Contain("'Set 19' is the one used").And.Contain($"'{Form}'"),
                "two owners is no judgement - but the one that lost its body is named, never silent");
        }

        #endregion

        #region BodyOwnership.Compute — peer Items (Life Stealer golden head + back + claw)

        private const string LsHero = "npc_dota_hero_life_stealer";
        private const string LsBody = "models/heroes/life_stealer/life_stealer.vmdl_c";
        private const string LsMorph = "models/heroes/life_stealer/life_stealer/upper_body_lod0_vmorf.vtex_c";
        private const string Arms = "Set 2", Back = "Set 3", Head = "Set 4";
        private const string ArmsStem = "item_arms_dread", BackStem = "item_back_profane", HeadStem = "item_head_maw";
        private const string ArmsBlock = "\t\t\"444\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/life_stealer/life_stealer_chains.vmdl\"\n\t\t}";
        private const string BackBlock = "\t\t\"8659\"\n\t\t{\n\t\t}";
        private const string HeadBlock = "\t\t\"8658\"\n\t\t{\n\t\t}";
        private static readonly string[] LsFiles = { LsBody, LsMorph };

        private static string? LsRegistry(string id) => id switch
        {
            "444" => "models/heroes/life_stealer/life_stealer_chains.vmdl",
            "8658" or "8659" => "",
            _ => null,
        };

        private static BodyOwnership.Result ComputeLs(
            Dictionary<string, (string Block, string Layer)> blocks,
            Dictionary<string, ISet<string>> files,
            Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> variants,
            Dictionary<string, string> owners,
            Func<string, string?>? stemOf = null)
            => BodyOwnership.Compute(blocks, files, variants, owners, LsHero,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                defaultModelOf: LsRegistry,
                stemOf: stemOf ?? (l => l switch { Arms => ArmsStem, Back => BackStem, Head => HeadStem, _ => null }));

        private static Dictionary<string, (string Block, string Layer)> AllThree()
            => Blocks(("444", ArmsBlock, Arms), ("8659", BackBlock, Back), ("8658", HeadBlock, Head));

        private static Dictionary<string, ISet<string>> AllThreeFiles()
            => Files((Arms, LsFiles), (Back, LsFiles), (Head, LsFiles));

        [Test]
        public void PeerKey_IsTheStemsLowercasedSortedOrdinalPlusJoined()
        {
            Assert.That(BodyOwnership.PeerKey(new[] { HeadStem, BackStem }), Is.EqualTo($"{BackStem}+{HeadStem}"));
            Assert.That(BodyOwnership.PeerKey(new[] { "Item_Head_Maw", "item_back_profane" }),
                Is.EqualTo($"{BackStem}+{HeadStem}"), "a CDN URL spelled in another case still finds its variant");
            Assert.That(BodyOwnership.PeerKey(new[] { "item_b", "item_B2", "item_a" }), Is.EqualTo("item_a+item_b+item_b2"));
            Assert.That(BodyOwnership.PeerKey(new[] { SwordKey }), Is.EqualTo(SwordKey), "one partner: its stem, the Tiny key");
        }

        [Test]
        public void PeerItems_TheVariantBuiltForExactlyThisCompany_IsTheFinalBody()
        {
            string key = $"{BackStem}+{HeadStem}";
            var r = ComputeLs(AllThree(), AllThreeFiles(), Variant(Arms, key, LsBody, LsMorph),
                Owners((Arms, LsFiles), (Back, LsFiles), (Head, LsFiles)));

            Assert.That(r.VariantApply, Is.EquivalentTo(new Dictionary<string, (string, string)>
            {
                [LsBody] = (Arms, key), [LsMorph] = (Arms, key),
            }));
            Assert.That(r.Restore, Is.Empty);
            Assert.That(r.Warnings, Is.Empty);

            var applied = Owners((Arms, LsFiles), (Back, LsFiles), (Head, LsFiles));
            applied[LsBody] = applied[LsMorph] = BodyOwnership.VariantOwner(Arms, key);
            Assert.That(ComputeLs(AllThree(), AllThreeFiles(), Variant(Arms, key, LsBody, LsMorph), applied).IsClean,
                Is.True, "applied once, then settled - ApplyBodyOwnership's re-check depends on it");
        }

        [Test]
        public void PeerItems_TwoOfThree_TakeThePairVariant()
        {
            var r = ComputeLs(Blocks(("8659", BackBlock, Back), ("8658", HeadBlock, Head)),
                Files((Back, LsFiles), (Head, LsFiles)), Variant(Back, HeadStem, LsBody, LsMorph),
                Owners((Back, LsFiles), (Head, LsFiles)));

            Assert.That(r.VariantApply[LsBody], Is.EqualTo((Back, HeadStem)));
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void PeerItems_NoExactVariant_LastWriterStands_AndNamesEveryone()
        {
            var r = ComputeLs(AllThree(), AllThreeFiles(), Variant(Back, HeadStem, LsBody, LsMorph),
                Owners((Arms, LsFiles), (Back, LsFiles), (Head, LsFiles)));

            Assert.That(r.VariantApply, Is.Empty);
            Assert.That(r.IsClean, Is.True, "nothing to move: the last writer's copy is already on disk");
            Assert.That(r.Warnings.Single(), Does.Contain($"'{Head}' is the one used")
                .And.Contain($"'{Arms}'").And.Contain($"'{Back}'"));
        }

        [TestCase("arbiter")]
        [TestCase("legacy")]
        public void SetAndItem_ItemCarriesTheSetsVariant_WhateverTheStemOrder(string setStem)
        {
            const string sven = "npc_dota_hero_sven", body = "models/heroes/sven/sven.vmdl_c";
            const string set = "Set 4", item = "Set 11", itemStem = "item_weapon_shattered";
            var files = new[] { body };
            var r = BodyOwnership.Compute(
                Blocks(("581", "\t\t\"581\"\n\t\t{\n\t\t}", set),
                       ("17", "\t\t\"17\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/sven/sven_sword.vmdl\"\n\t\t}", item)),
                Files((set, files), (item, files)), Variant(item, setStem, body), Owners((set, files), (item, files)),
                sven, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                defaultModelOf: id => id switch { "581" => "", "17" => "models/heroes/sven/sven_sword.vmdl", _ => null },
                stemOf: l => l == set ? setStem : l == item ? itemStem : null);

            Assert.That(r.VariantApply[body], Is.EqualTo((item, setStem)));
            Assert.That(r.Restore, Is.Empty, "the set's bake must not win over the composed body");
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void SetAndItem_VariantCarriesTheSetsAnimInclude_AppliedWithTheBody_ThenSettled()
        {
            const string sven = "npc_dota_hero_sven", body = "models/heroes/sven/sven.vmdl_c";
            const string include = "kisilev_ind/models/sven/sven_kisilev_ind.vmdl_c";
            const string set = "Set 4", item = "Set 11";
            var setFiles = new[] { body, include };
            var blocks = Blocks(("581", "\t\t\"581\"\n\t\t{\n\t\t}", set),
                                ("17", "\t\t\"17\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/sven/sven_sword.vmdl\"\n\t\t}", item));
            BodyOwnership.Result Run(Dictionary<string, string> owners) => BodyOwnership.Compute(
                blocks, Files((set, setFiles), (item, new[] { body })), Variant(item, "legacy", body, include),
                owners, sven, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                defaultModelOf: id => id switch { "581" => "", "17" => "models/heroes/sven/sven_sword.vmdl", _ => null },
                stemOf: l => l == set ? "legacy" : l == item ? "item_weapon_shattered" : null);

            var r = Run(Owners((set, setFiles), (item, new[] { body })));

            Assert.That(r.VariantApply, Is.EquivalentTo(new Dictionary<string, (string, string)>
            {
                [body] = (item, "legacy"), [include] = (item, "legacy"),
            }));
            Assert.That(r.Restore, Is.Empty);

            var applied = Owners((set, setFiles), (item, new[] { body }));
            applied[body] = applied[include] = BodyOwnership.VariantOwner(item, "legacy");
            Assert.That(Run(applied).IsClean, Is.True, "ApplyBodyOwnership's re-check must settle");
        }

        [Test]
        public void SetAndItem_NoVariant_TheSetsBakeKeepsTheBody_AndSaysSo()
        {
            const string sven = "npc_dota_hero_sven", body = "models/heroes/sven/sven.vmdl_c";
            const string set = "Set 4", item = "Set 11";
            var files = new[] { body };
            var r = BodyOwnership.Compute(
                Blocks(("581", "\t\t\"581\"\n\t\t{\n\t\t}", set),
                       ("17", "\t\t\"17\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/sven/sven_sword.vmdl\"\n\t\t}", item)),
                Files((set, files), (item, files)), new Dictionary<string, IReadOnlyDictionary<string, ISet<string>>>(),
                Owners((set, files), (item, files)), sven, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                defaultModelOf: id => id switch { "581" => "", "17" => "models/heroes/sven/sven_sword.vmdl", _ => null },
                stemOf: l => l == set ? "legacy" : l == item ? "item_weapon_shattered" : null);

            Assert.That(r.Restore[body], Is.EqualTo(set));
            Assert.That(r.Warnings.Single(), Does.Contain($"'{item}'").And.Contain($"'{set}'"));
        }

        [Test]
        public void SetBesideModellessOwner_PrismaticAlsoShipsTheBody_TheSetsVariantStillApplies()
        {
            const string tb = "npc_dota_hero_terrorblade", body = "models/heroes/terrorblade/terrorblade.vmdl_c";
            const string armor = "models/heroes/terrorblade/armor.vmdl_c";
            const string arcana = "Set 9", gem = "Set 10", set = "Set 11";
            var blocks = Blocks(("913", "\t\t\"913\"\n\t\t{\n\t\t}", arcana),
                                ("476", "\t\t\"476\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/terrorblade/armor.vmdl\"\n\t\t}", set));
            var files = Files((arcana, new[] { body }), (set, new[] { armor }), (gem, new[] { body }));
            var gemOnly = new HashSet<string>(new[] { gem }, StringComparer.OrdinalIgnoreCase);
            BodyOwnership.Result Run(Dictionary<string, string> owners, ISet<string>? overlays) => BodyOwnership.Compute(
                blocks, files, Variant(set, "base_arcana", body, armor), owners, tb,
                new HashSet<string>(new[] { armor }, StringComparer.OrdinalIgnoreCase),
                defaultModelOf: id => id switch { "913" => "", "476" => "models/heroes/terrorblade/armor.vmdl", _ => null },
                stemOf: l => l == arcana ? "base_arcana" : l == set ? "rictus_1" : l == gem ? "prismatic_red" : null,
                overlays: overlays);
            var merged = Owners((arcana, new[] { body }), (set, new[] { armor }), (gem, new[] { body }));

            var r = Run(merged, gemOnly);

            Assert.That(r.VariantApply, Is.EquivalentTo(new Dictionary<string, (string, string)>
            {
                [body] = (set, "base_arcana"), [armor] = (set, "base_arcana"),
            }));
            Assert.That(r.Restore, Is.Empty);

            var applied = new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
            applied[body] = applied[armor] = BodyOwnership.VariantOwner(set, "base_arcana");
            Assert.That(Run(applied, gemOnly).IsClean, Is.True, "ApplyBodyOwnership's re-check must settle");

            var old = Run(merged, null);
            Assert.That(old.VariantApply, Is.Empty);
            Assert.That(old.Restore[body], Is.EqualTo(arcana));
        }

        [Test]
        public void PeerItems_KeyIgnoresPickOrderAndCase()
        {
            string key = $"{BackStem}+{HeadStem}";
            var r = ComputeLs(
                Blocks(("8658", HeadBlock, Head), ("444", ArmsBlock, Arms), ("8659", BackBlock, Back)),
                Files((Head, LsFiles), (Arms, LsFiles), (Back, LsFiles)),
                Variant(Arms, key, LsBody, LsMorph),
                Owners((Head, LsFiles), (Arms, LsFiles), (Back, LsFiles)),
                stemOf: l => l switch { Arms => "Item_Arms_Dread", Back => "ITEM_BACK_PROFANE", Head => "item_head_maw", _ => null });

            Assert.That(r.VariantApply[LsBody], Is.EqualTo((Arms, key)));
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void PeerItems_AStemlessLayer_NeverMatchesAVariant()
        {
            var r = ComputeLs(AllThree(), AllThreeFiles(), Variant(Arms, $"{BackStem}+{HeadStem}", LsBody),
                Owners((Arms, LsFiles), (Back, LsFiles), (Head, LsFiles)),
                stemOf: l => l == Head ? null : l == Arms ? ArmsStem : BackStem);

            Assert.That(r.VariantApply, Is.Empty, "a key built without one member's stem is a guess");
            Assert.That(r.Warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void TwoModelledItemsOnOneBody_NoModellessLayer_IsStillReported()
        {
            string otherArms = "\t\t\"443\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/life_stealer/x.vmdl\"\n\t\t}";
            var r = ComputeLs(Blocks(("444", ArmsBlock, Arms), ("443", otherArms, Back)),
                Files((Arms, LsFiles), (Back, LsFiles)), NoVariants(),
                Owners((Arms, LsFiles), (Back, LsFiles)),
                stemOf: l => l == Arms ? ArmsStem : "item_misc_other");

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings.Single(), Does.Contain($"'{Back}' is the one used").And.Contain($"'{Arms}'"));

            var withVariant = ComputeLs(Blocks(("444", ArmsBlock, Arms), ("443", otherArms, Back)),
                Files((Arms, LsFiles), (Back, LsFiles)), Variant(Arms, "item_misc_other", LsBody),
                Owners((Arms, LsFiles), (Back, LsFiles)),
                stemOf: l => l == Arms ? ArmsStem : "item_misc_other");
            Assert.That(withVariant.VariantApply[LsBody], Is.EqualTo((Arms, "item_misc_other")));
        }

        private const string Chains = "models/heroes/life_stealer/life_stealer_chains.vmdl_c";

        private static BodyOwnership.Result ComputeClaw(
            Dictionary<string, (string Block, string Layer)> blocks,
            Dictionary<string, ISet<string>> files,
            Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> variants,
            Dictionary<string, string> owners)
            => BodyOwnership.Compute(blocks, files, variants, owners, LsHero,
                new HashSet<string>(new[] { Chains }, StringComparer.OrdinalIgnoreCase),
                defaultModelOf: LsRegistry,
                stemOf: l => l switch { Arms => ArmsStem, Back => BackStem, Head => HeadStem, _ => null });

        private static Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> ClawCarries(params string[] keys)
            => new(StringComparer.OrdinalIgnoreCase)
            {
                [Arms] = keys.ToDictionary(k => k, _ => (ISet<string>)new HashSet<string>(LsFiles, StringComparer.OrdinalIgnoreCase),
                                           StringComparer.OrdinalIgnoreCase),
            };

        [Test]
        public void ClawShipsNoBody_ButIsAMemberOfTheCompany_AndCarriesItsBody()
        {
            var r = ComputeClaw(AllThree(),
                Files((Arms, new[] { Chains }), (Back, LsFiles), (Head, LsFiles)),
                ClawCarries(BackStem, $"{BackStem}+{HeadStem}"),
                Owners((Arms, new[] { Chains }), (Back, LsFiles), (Head, LsFiles)));

            Assert.That(r.VariantApply[LsBody], Is.EqualTo((Arms, $"{BackStem}+{HeadStem}")),
                "counting only writers would look for key 'item_head_maw' on the back and find nothing");
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void ClawWithOneBodyWriter_StillTakesThePairBody()
        {
            var r = ComputeClaw(Blocks(("444", ArmsBlock, Arms), ("8659", BackBlock, Back)),
                Files((Arms, new[] { Chains }), (Back, LsFiles)),
                ClawCarries(BackStem, $"{BackStem}+{HeadStem}"),
                Owners((Arms, new[] { Chains }), (Back, LsFiles)));

            Assert.That(r.VariantApply[LsBody], Is.EqualTo((Arms, BackStem)),
                "one writer is no collision - but without the pair body the claw's clips never land");
        }

        [Test]
        public void ClawAlone_OrWithoutItsCompanyBuilt_ChangesNothing()
        {
            var alone = ComputeClaw(Blocks(("444", ArmsBlock, Arms)), Files((Arms, new[] { Chains })),
                ClawCarries(BackStem), Owners((Arms, new[] { Chains })));
            Assert.That(alone.IsClean, Is.True);
            Assert.That(alone.Warnings, Is.Empty);

            var unbuilt = ComputeClaw(Blocks(("444", ArmsBlock, Arms), ("8659", BackBlock, Back)),
                Files((Arms, new[] { Chains }), (Back, LsFiles)), ClawCarries($"{BackStem}+{HeadStem}"),
                Owners((Arms, new[] { Chains }), (Back, LsFiles)));
            Assert.That(unbuilt.IsClean, Is.True, "the back's own body stands");
            Assert.That(unbuilt.Warnings, Is.Empty, "a single writer lost nothing to another writer");
        }

        [Test]
        public void PeerItems_OneWriter_IsNeverTouched()
        {
            var r = ComputeLs(Blocks(("8658", HeadBlock, Head)), Files((Head, LsFiles)),
                Variant(Head, BackStem, LsBody), Owners((Head, LsFiles)));

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Is.Empty, "alone, the head's own body is right");
        }

        #endregion

        #region HeroGenerationService (real files)

        private string _tmp = "";

        [SetUp]
        public void SetUp()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "amt_body_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tmp, true); } catch {  }
        }

        private static HeroModel Zeus() => new()
        {
            HeroId = Hero,
            Name = "Zeus",
            ItemIds = new List<int> { 589, 593, 604, 605, 606, 607 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [Base] = new() { "https://cdn.example/Assets/models/Zeus/base_arcana.zip" },
                [Weapon] = new() { "https://cdn.example/Assets/models/Zeus/item_weapon_thunderbolt.zip" },
                ["Set 2"] = new() { "https://cdn.example/Assets/models/Zeus/item_arms_tempest.zip" },
            },
            SetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["base_arcana"] = "Tempest Helm of the Thundergod",
                ["item_weapon_thunderbolt"] = "Righteous Thunderbolt",
            },
        };

        private string SetFolder(string name, params (string Rel, string Content)[] files)
        {
            string root = Path.Combine(_tmp, "sets", name);
            foreach (var (rel, content) in files)
            {
                string p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, content);
            }
            return root;
        }

        private sealed class Merge
        {
            public Dictionary<string, (string block, string heroId)> Blocks = new();
            public Dictionary<string, string> BlockOwner = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ISet<string>> LayerFiles = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, IReadOnlyDictionary<string, ISet<string>>> Variants = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FileOwner = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (string Root, bool Encrypted)> Roots = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Warnings = new();
            public string ExtractDir = "";
        }

        private Task<Merge> RunAsync(params (string Layer, string Folder, bool Encrypted, (string Id, string Block)[] Blocks)[] layers)
            => RunAsync(Zeus(), layers);

        private async Task<Merge> RunAsync(HeroModel hero, params (string Layer, string Folder, bool Encrypted, (string Id, string Block)[] Blocks)[] layers)
        {
            var service = new HeroGenerationService();
            var m = new Merge { ExtractDir = Path.Combine(_tmp, "extract") };
            Directory.CreateDirectory(m.ExtractDir);
            foreach (var (layer, folder, encrypted, blocks) in layers)
            {
                var (root, copied) = await service.MergeSetAssetsAsync(folder, m.ExtractDir, CancellationToken.None);
                m.Roots[layer] = (root, encrypted);
                m.LayerFiles[layer] = new HashSet<string>(copied, StringComparer.OrdinalIgnoreCase);
                m.Variants[layer] = HeroGenerationService.ReadBodyVariants(root);
                foreach (var rel in copied)
                {
                    m.FileOwner[rel] = layer;
                    if (encrypted && ProtectedVpkStore.IsProtectable(rel)) m.Protected.Add(rel); else m.Protected.Remove(rel);
                }
                foreach (var (id, block) in blocks)
                {
                    m.Blocks[id] = (block, Hero);
                    m.BlockOwner[id] = layer;
                }
            }

            HeroGenerationService.ApplyBodyOwnership(hero, m.Blocks, m.BlockOwner, m.LayerFiles, m.Variants,
                m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add);
            HeroGenerationService.ApplyWornModelYield(hero.DisplayName, m.Blocks, m.BlockOwner, m.LayerFiles, m.FileOwner,
                m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add);
            return m;
        }

        private string Shipped(Merge m, string rel)
            => File.ReadAllText(Path.Combine(m.ExtractDir, rel.Replace('/', Path.DirectorySeparatorChar)));

        private (string, string, bool, (string, string)[]) BaseLayer() => (Base, SetFolder("base_arcana",
                (Body, "arcana_body"), (BodyMorph, "arcana_morph"), (Swap, "arcana_mirror"),
                (SwapMorph, "arcana_swap_morph"), (Hair, "arcana_hair"), (Bracers, "invisiblebox"), (Vest, "invisiblebox")),
            true, new[] { ("604", ArcanaBlock()) });

        private (string, string, bool, (string, string)[]) WeaponLayer(bool withVariant = false)
        {
            var files = new List<(string, string)>
            {
                (Body, "regular_body_bolt"), (WeaponBodyMorph, "regular_morph"),
                (Swap, "arcana_bolt_owned"), (SwapMorph, "weapon_swap_morph"),
            };
            if (withVariant)
                files.AddRange(new[]
                {
                    ("variants/zeus_arcana/" + Body, "arcana_body_bolt"),
                    ("variants/zeus_arcana/" + BodyMorph, "arcana_bolt_morph"),
                    ("variants/zeus_arcana/" + Swap, "arcana_mirror_bolt"),
                    ("variants/zeus_arcana/" + SwapMorph, "arcana_bolt_swap_morph"),
                });
            return (Weapon, SetFolder(withVariant ? "thunderbolt_v" : "thunderbolt", files.ToArray()),
                false, new[] { ("593", WeaponBlock) });
        }

        [Test]
        public async Task Generate_Method1_BaseArcanaThenWeapon_PublicBodyIsArcana()
        {
            var m = await RunAsync(BaseLayer(), WeaponLayer());

            Assert.That(Shipped(m, Body), Is.EqualTo("arcana_body"), "a public match loads this file");
            Assert.That(Shipped(m, Swap), Is.EqualTo("arcana_mirror"), "demo and public match the same body");
            Assert.That(Shipped(m, SwapMorph), Is.EqualTo("arcana_swap_morph"));
            Assert.That(Shipped(m, BodyMorph), Is.EqualTo("arcana_morph"));
            Assert.That(m.Protected, Is.SupersetOf(new[] { Body, Swap, SwapMorph }.Where(ProtectedVpkStore.IsProtectable)),
                "restored files carry the owner's protection");
            Assert.That(m.Warnings.Single(), Does.Contain("'Righteous Thunderbolt' is built for the regular body"));
            Assert.That(m.Blocks["593"].block, Does.Not.Contain("model_player").And.Not.Contain("match_cycle_to_parent"),
                "no demo-only bolt wearable floating beside the arcana");
            Assert.That(m.Blocks["593"].block, Does.Contain("zuus_base_attack"), "its effects stay");
        }

        [Test]
        public async Task Generate_WeaponThenBaseArcana_SameResult()
        {
            var m = await RunAsync(WeaponLayer(), BaseLayer());

            Assert.That(Shipped(m, Body), Is.EqualTo("arcana_body"));
            Assert.That(Shipped(m, Swap), Is.EqualTo("arcana_mirror"));
        }

        [Test]
        public async Task Generate_WeaponWithArcanaVariant_ShipsTheComposedBody_AndNoVariantsFolder()
        {
            var m = await RunAsync(BaseLayer(), WeaponLayer(withVariant: true));

            Assert.That(Shipped(m, Body), Is.EqualTo("arcana_body_bolt"));
            Assert.That(Shipped(m, BodyMorph), Is.EqualTo("arcana_bolt_morph"));
            Assert.That(Shipped(m, Swap), Is.EqualTo("arcana_mirror_bolt"));
            Assert.That(Shipped(m, SwapMorph), Is.EqualTo("arcana_bolt_swap_morph"));
            Assert.That(Directory.Exists(Path.Combine(m.ExtractDir, "variants")), Is.False,
                "variants/ must never reach the package");
            Assert.That(m.LayerFiles[Weapon].Any(HeroGenerationService.IsUnderVariants), Is.False);
            Assert.That(m.Warnings, Is.Empty);
            Assert.That(m.Protected.Contains(Body), Is.False, "the (unencrypted) variant owns the path now");
        }

        [Test]
        public async Task Generate_WeaponAlone_ShipsItsOwnBody()
        {
            var m = await RunAsync(WeaponLayer(withVariant: true));

            Assert.That(Shipped(m, Body), Is.EqualTo("regular_body_bolt"));
            Assert.That(m.Warnings, Is.Empty);
            Assert.That(m.Blocks["593"].block, Does.Contain("model_player"), "alone, nothing to strip");
        }

        [Test]
        public async Task Generate_BodyAndWornPassesCompose_ArmsStillWorn()
        {
            string armsBlock = "\t\t\"589\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_bracers.vmdl\"\n\t\t}";
            var arms = ("Set 2", SetFolder("arms", (Bracers, "ti8_arms")), false, new[] { ("589", armsBlock) });

            var m = await RunAsync(BaseLayer(), arms, WeaponLayer());

            Assert.That(Shipped(m, Body), Is.EqualTo("arcana_body"));
            Assert.That(Shipped(m, Bracers), Is.EqualTo("ti8_arms"));
            Assert.That(m.Blocks["604"].block, Does.Not.Contain("zeus_bracers"));
            Assert.That(m.Blocks["604"].block, Does.Contain("entity_model"), "the body swap row itself stays");
        }

        private static HeroModel Tiny() => new()
        {
            HeroId = TinyHero,
            Name = "Tiny",
            ItemIds = new List<int> { 493, 494, 495, 496, 497, 516, 679, 680, 681, 682 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [Form] = new() { "https://cdn.example/Assets/models/Tiny/item_large_frostmoot.zip" },
                [Sword] = new() { "https://cdn.example/Assets/models/Tiny/item_weapon_majesty.zip" },
            },
            SetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["item_large_frostmoot"] = "Large Frostmoot",
                ["item_weapon_majesty"] = "Sword of the Colossus",
            },
        };

        private (string, string, bool, (string, string)[]) FormLayer(bool withVariant)
        {
            var files = new List<(string, string)> { (T4, "frostmoot"), (T4Amt, "amt"), (T4FormMorph, "frost_morph") };
            if (withVariant)
                files.AddRange(new[] { ($"variants/{SwordKey}/{T4}", "frostmoot_detached"), ($"variants/{SwordKey}/{T4Amt}", "amt_detached") });
            return (Form, SetFolder(withVariant ? "frost_v" : "frost", files.ToArray()), true, new[] { ("682", FormBlock) });
        }

        private (string, string, bool, (string, string)[]) SwordLayer() => (Sword, SetFolder("sword",
                (T4, "vanilla_detached"), (T4SwordMorph, "vanilla_morph"), (T1, "vanilla1_detached"), (Tree, "sword")),
            false, new[] { ("497", SwordBlock) });

        [Test]
        public async Task Generate_TinyFormThenSword_ShipsTheFormBody()
        {
            var m = await RunAsync(Tiny(), FormLayer(withVariant: false), SwordLayer());

            Assert.That(Shipped(m, T4), Is.EqualTo("frostmoot"), "the reported bug: this was vanilla_detached");
            Assert.That(Shipped(m, T1), Is.EqualTo("vanilla1_detached"), "no form on level 1 - the sword's body stands");
            Assert.That(m.Protected.Contains(T4), Is.EqualTo(ProtectedVpkStore.IsProtectable(T4)), "restored with the form's protection");
            Assert.That(m.Warnings.Single(), Does.Contain("'Sword of the Colossus'").And.Contain("'Large Frostmoot'"));
            Assert.That(m.Blocks["497"].block, Does.Contain("model_player"), "the sword itself stays worn");
        }

        [Test]
        public async Task Generate_TinyFormWithSwordVariant_ShipsTheComposedBody()
        {
            var m = await RunAsync(Tiny(), FormLayer(withVariant: true), SwordLayer());

            Assert.That(Shipped(m, T4), Is.EqualTo("frostmoot_detached"));
            Assert.That(Shipped(m, T4Amt), Is.EqualTo("amt_detached"));
            Assert.That(Directory.Exists(Path.Combine(m.ExtractDir, "variants")), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public async Task Generate_TinyFormWithSwordVariant_WithoutTheSword_ShipsThePlainForm()
        {
            var m = await RunAsync(Tiny(), FormLayer(withVariant: true));

            Assert.That(Shipped(m, T4), Is.EqualTo("frostmoot"));
            Assert.That(Shipped(m, T4Amt), Is.EqualTo("amt"));
            Assert.That(m.Warnings, Is.Empty);
        }

        private static HeroModel LifeStealer() => new()
        {
            HeroId = LsHero,
            Name = "Lifestealer",
            ItemIds = new List<int> { 443, 444, 530, 8658, 8659 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [Arms] = new() { $"https://cdn.example/Assets/models/Lifestealer/{ArmsStem}.zip" },
                [Back] = new() { $"https://cdn.example/Assets/models/Lifestealer/{BackStem}.zip" },
                [Head] = new() { $"https://cdn.example/Assets/models/Lifestealer/{HeadStem}.zip" },
            },
            SetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ArmsStem] = "Golden Dread Requisition",
                [BackStem] = "Golden Profane Union",
                [HeadStem] = "Golden Dark Maw Inhibitor",
            },
        };

        private (string, string, bool, (string, string)[]) LsLayer(string layer, string id, string block,
            params (string Key, string Content)[] variants)
        {
            var files = new List<(string, string)> { (LsBody, layer + "_alone"), (LsMorph, layer + "_morph") };
            foreach (var (key, content) in variants)
                files.AddRange(new[] { ($"variants/{key}/{LsBody}", content), ($"variants/{key}/{LsMorph}", content + "_morph") });
            return (layer, SetFolder("ls_" + layer.Replace(' ', '_') + variants.Length, files.ToArray()), false, new[] { (id, block) });
        }

        [Test]
        public async Task Generate_LifestealerThreePeers_ShipsTheComposedBody()
        {
            var arms = LsLayer(Arms, "444", ArmsBlock, (BackStem, "arms_back"), ($"{BackStem}+{HeadStem}", "all_three"));
            var m = await RunAsync(LifeStealer(), LsLayer(Head, "8658", HeadBlock), arms, LsLayer(Back, "8659", BackBlock));

            Assert.That(Shipped(m, LsBody), Is.EqualTo("all_three"), "the reported bug: the last pick's body won");
            Assert.That(Shipped(m, LsMorph), Is.EqualTo("all_three_morph"), "the atlas travels with its body");
            Assert.That(Directory.Exists(Path.Combine(m.ExtractDir, "variants")), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public async Task Generate_ClawZipWithoutABody_StillComposesTheBodyItCarries()
        {
            var claw = (Arms, SetFolder("ls_claw", (Chains, "claw_mesh"),
                    ($"variants/{BackStem}/{LsBody}", "claw_back"),
                    ($"variants/{BackStem}+{HeadStem}/{LsBody}", "claw_back_head"),
                    ($"variants/{BackStem}+{HeadStem}/{LsMorph}", "claw_back_head_morph")),
                false, new[] { ("444", ArmsBlock) });

            var m = await RunAsync(LifeStealer(), LsLayer(Back, "8659", BackBlock), claw, LsLayer(Head, "8658", HeadBlock));

            Assert.That(Shipped(m, LsBody), Is.EqualTo("claw_back_head"));
            Assert.That(Shipped(m, LsMorph), Is.EqualTo("claw_back_head_morph"));
            Assert.That(Shipped(m, Chains), Is.EqualTo("claw_mesh"), "the claw's own slot model is untouched");
            Assert.That(m.Warnings, Is.Empty);

            var pair = await RunAsync(LifeStealer(), claw, LsLayer(Back, "8659", BackBlock));
            Assert.That(Shipped(pair, LsBody), Is.EqualTo("claw_back"));
        }

        [Test]
        public async Task Generate_LifestealerTwoPeers_ShipsThePairBody()
        {
            var arms = LsLayer(Arms, "444", ArmsBlock, (BackStem, "arms_back"), ($"{BackStem}+{HeadStem}", "all_three"));
            var m = await RunAsync(LifeStealer(), LsLayer(Back, "8659", BackBlock), arms);

            Assert.That(Shipped(m, LsBody), Is.EqualTo("arms_back"));
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public async Task Generate_LifestealerPeersWithoutVariants_LastWriterStands_AndSaysSo()
        {
            var m = await RunAsync(LifeStealer(), LsLayer(Arms, "444", ArmsBlock),
                LsLayer(Back, "8659", BackBlock), LsLayer(Head, "8658", HeadBlock));

            Assert.That(Shipped(m, LsBody), Is.EqualTo(Head + "_alone"));
            Assert.That(m.Warnings.Single(), Does.Contain("'Golden Dark Maw Inhibitor' is the one used")
                .And.Contain("'Golden Dread Requisition'").And.Contain("'Golden Profane Union'"),
                "warnings use the names a user picked, not layer keys");
        }

        private const string SfHero = "npc_dota_hero_nevermore";
        private const string SfBody = "models/heroes/shadow_fiend/shadow_fiend.vmdl_c";
        private const string SfMirror = "models/heroes/shadow_fiend/shadow_fiend_arcana.vmdl_c";
        private const string SfMorph = "models/heroes/shadow_fiend/shadow_fiend/shadow_fiend_base_lod0_vmorf.vtex_c";
        private const string SfArms = "models/heroes/shadow_fiend/shadow_fiend_arms.vmdl_c";
        private const string SfBase = "Set 14", SfDeso = "Set 13", SfKey = "shadow_fiend_arcana";
        private const string SfBaseBlock =
            "\t\t\"826\"\n\t\t{\n\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"entity_model\"\n\t\t\t\t\t\"asset\"\t\t\"" + SfHero + "\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/heroes/shadow_fiend/shadow_fiend_arcana.vmdl\"\n\t\t\t\t}\n\t\t\t}\n\t\t}";
        private const string SfDesoBlock =
            "\t\t\"488\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/shadow_fiend/shadow_fiend_arms.vmdl\"\n\t\t}";

        private static HeroModel ShadowFiend() => new()
        {
            HeroId = SfHero,
            Name = "Shadow Fiend",
            ItemIds = new List<int> { 387, 486, 488, 509, 826 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [SfDeso] = new() { "https://cdn.example/Assets/models/Shadow_Fiend/item_hand_desolator.zip" },
                [SfBase] = new() { "https://cdn.example/Assets/models/Shadow_Fiend/base_arcana.zip" },
            },
            SetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["item_hand_desolator"] = "Arms of Desolation",
                ["base_arcana"] = "Demon Eater",
            },
        };

        private (string, string, bool, (string, string)[]) SfBaseLayer() => (SfBase, SetFolder("sf_base_arcana",
                (SfBody, "arcana_plain_run"), (SfMirror, "arcana_mirror_plain_run")),
            true, new[] { ("826", SfBaseBlock) });

        private (string, string, bool, (string, string)[]) SfDesoLayer(bool withVariant)
        {
            var files = new List<(string, string)>
            {
                (SfBody, "regular_desolation_run"), (SfMorph, "regular_morph"), (SfArms, "deso_arms"),
            };
            if (withVariant)
                files.AddRange(new[]
                {
                    ($"variants/{SfKey}/{SfBody}", "arcana_desolation_run"),
                    ($"variants/{SfKey}/{SfMirror}", "arcana_mirror_desolation_run"),
                });
            return (SfDeso, SetFolder(withVariant ? "sf_deso_v" : "sf_deso", files.ToArray()),
                true, new[] { ("488", SfDesoBlock) });
        }

        [Test]
        public async Task Generate_ShadowFiendMethod2_NoVariant_ArcanaKeepsItsBody_AndSaysSo()
        {
            var m = await RunAsync(ShadowFiend(), SfDesoLayer(withVariant: false), SfBaseLayer());

            Assert.That(Shipped(m, SfBody), Is.EqualTo("arcana_plain_run"), "no variant: the desolation run is lost");
            Assert.That(Shipped(m, SfMirror), Is.EqualTo("arcana_mirror_plain_run"));
            Assert.That(m.Warnings.Single(), Does.Contain("'Arms of Desolation'").And.Contain("'Demon Eater'"),
                "the loss is named, never silent");
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Generate_ShadowFiendDesolatorCarriesArcanaVariant_DesolationRunShips(bool baseLast)
        {
            var layers = baseLast
                ? new[] { SfDesoLayer(withVariant: true), SfBaseLayer() }
                : new[] { SfBaseLayer(), SfDesoLayer(withVariant: true) };
            var m = await RunAsync(ShadowFiend(), layers);

            Assert.That(Shipped(m, SfBody), Is.EqualTo("arcana_desolation_run"), "what a public match loads");
            Assert.That(Shipped(m, SfMirror), Is.EqualTo("arcana_mirror_desolation_run"), "what an arcana owner loads");
            Assert.That(Shipped(m, SfArms), Is.EqualTo("deso_arms"), "a slot model is no body");
            Assert.That(Directory.Exists(Path.Combine(m.ExtractDir, "variants")), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public async Task Generate_ShadowFiendDesolatorAlone_ShipsItsPromotedRegularBody()
        {
            var m = await RunAsync(ShadowFiend(), SfDesoLayer(withVariant: true));

            Assert.That(Shipped(m, SfBody), Is.EqualTo("regular_desolation_run"), "no owner: the variant never applies");
            Assert.That(File.Exists(Path.Combine(m.ExtractDir, SfMirror.Replace('/', Path.DirectorySeparatorChar))), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        private const string JgHero = "npc_dota_hero_juggernaut";
        private const string JgBody = "models/heroes/juggernaut/juggernaut.vmdl_c";
        private const string JgMirror = "models/heroes/juggernaut/juggernaut_arcana.vmdl_c";
        private const string JgMorph = "models/heroes/juggernaut/juggernaut/juggernaut_base_arcana_vmorf.vtex_c";
        private const string JgSword = "models/items/juggernaut/ancient_exile_weapon/ancient_exile_weapon.vmdl_c";
        private const string JgBase = "Set 3", JgWeapon = "Set 4", JgKey = "juggernaut_arcana";
        private const string JgBaseBlock =
            "\t\t\"811\"\n\t\t{\n\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"entity_model\"\n\t\t\t\t\t\"asset\"\t\t\"" + JgHero + "\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/heroes/juggernaut/juggernaut_arcana.vmdl\"\n\t\t\t\t}\n\t\t\t}\n\t\t}";
        private const string JgWeaponBlock =
            "\t\t\"7\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/items/juggernaut/ancient_exile_weapon/ancient_exile_weapon.vmdl\"\n\t\t}";

        private static HeroModel Juggernaut() => new()
        {
            HeroId = JgHero,
            Name = "Juggernaut",
            ItemIds = new List<int> { 7, 811 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [JgWeapon] = new() { "https://cdn.example/Assets/models/Juggernaut/item_weapon_ancient_exile.zip" },
                [JgBase] = new() { "https://cdn.example/Assets/models/Juggernaut/base_bladeform.zip" },
            },
            SetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["item_weapon_ancient_exile"] = "Ancient Exile - Weapon",
                ["base_bladeform"] = "Bladeform Legacy",
            },
        };

        private (string, string, bool, (string, string)[]) JgBaseLayer() => (JgBase, SetFolder("jg_base",
                (JgBody, "arcana_run"), (JgMirror, "arcana_mirror_run"), (JgMorph, "arcana_morph")),
            true, new[] { ("811", JgBaseBlock) });

        private (string, string, bool, (string, string)[]) JgWeaponLayer(bool withVariant)
        {
            var files = new List<(string, string)> { (JgBody, "regular_odachi_run"), (JgSword, "exile_sword") };
            if (withVariant)
                files.AddRange(new[]
                {
                    ($"variants/{JgKey}/{JgBody}", "arcana_odachi_run"),
                    ($"variants/{JgKey}/{JgMirror}", "arcana_mirror_odachi_run"),
                });
            return (JgWeapon, SetFolder(withVariant ? "jg_weapon_v" : "jg_weapon", files.ToArray()),
                true, new[] { ("7", JgWeaponBlock) });
        }

        [Test]
        public async Task Generate_JuggernautNoVariant_ArcanaKeepsItsBody_AndSaysSo()
        {
            var m = await RunAsync(Juggernaut(), JgWeaponLayer(withVariant: false), JgBaseLayer());

            Assert.That(Shipped(m, JgBody), Is.EqualTo("arcana_run"), "the regular-body odachi run never lands on the arcana");
            Assert.That(Shipped(m, JgMirror), Is.EqualTo("arcana_mirror_run"));
            Assert.That(Shipped(m, JgMorph), Is.EqualTo("arcana_morph"), "the body's morph sidecar follows its owner");
            Assert.That(Shipped(m, JgSword), Is.EqualTo("exile_sword"));
            Assert.That(m.Warnings.Single(), Does.Contain("'Ancient Exile - Weapon'").And.Contain("'Bladeform Legacy'"),
                "the loss is named, never silent");
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Generate_JuggernautWeaponCarriesArcanaVariant_OdachiBodyShips(bool baseLast)
        {
            var layers = baseLast
                ? new[] { JgWeaponLayer(withVariant: true), JgBaseLayer() }
                : new[] { JgBaseLayer(), JgWeaponLayer(withVariant: true) };
            var m = await RunAsync(Juggernaut(), layers);

            Assert.That(Shipped(m, JgBody), Is.EqualTo("arcana_odachi_run"), "what a public match loads");
            Assert.That(Shipped(m, JgMirror), Is.EqualTo("arcana_mirror_odachi_run"), "what an arcana owner loads");
            Assert.That(Shipped(m, JgSword), Is.EqualTo("exile_sword"), "a slot model is no body");
            Assert.That(Directory.Exists(Path.Combine(m.ExtractDir, "variants")), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public async Task Generate_JuggernautWeaponAlone_ShipsItsPromotedRegularBody()
        {
            var m = await RunAsync(Juggernaut(), JgWeaponLayer(withVariant: true));

            Assert.That(Shipped(m, JgBody), Is.EqualTo("regular_odachi_run"), "no owner: the variant never applies");
            Assert.That(File.Exists(Path.Combine(m.ExtractDir, JgMirror.Replace('/', Path.DirectorySeparatorChar))), Is.False);
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public void ReadBodyVariants_KeysByFolder_PathsRelativeToIt()
        {
            string root = SetFolder("v", ("variants/zeus_arcana/" + Body, "x"), ("variants/zeus_persona/" + Swap, "y"), (Body, "z"));

            var v = HeroGenerationService.ReadBodyVariants(root);

            Assert.That(v.Keys, Is.EquivalentTo(new[] { "zeus_arcana", "zeus_persona" }));
            Assert.That(v["zeus_arcana"], Is.EquivalentTo(new[] { Body }));
            Assert.That(HeroGenerationService.ReadBodyVariants(SetFolder("none", (Body, "z"))), Is.Empty);
            Assert.That(HeroGenerationService.IsUnderVariants(@"variants\zeus_arcana\" + Body), Is.True);
            Assert.That(HeroGenerationService.IsUnderVariants("models/variants/x.vmdl_c"), Is.False);
        }

        #endregion
    }
}
