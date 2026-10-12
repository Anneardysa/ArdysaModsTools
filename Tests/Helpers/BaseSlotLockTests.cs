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
    public class BaseSlotLockTests
    {
        private const string HeroId = "npc_dota_hero_zuus";
        private const string Base = "Set 1";
        private const string Legacy = "Set 3";
        private const string HeadItem = "Set 4";

        private const string Hair = "models/heroes/zeus/zeus_hair.vmdl_c";
        private const string HeadFx = "particles/econ/items/zeus/arcana/zeus_arcana_head.vpcf_c";
        private const string SetWeapon = "models/items/zeus/storm/zeus_storm_weapon.vmdl_c";

        private static string HeadBlock(string tag) =>
            "\t\t\"604\"\n\t\t{\n" +
            "\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_hair.vmdl\"\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier1\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"particle_create\"\n" +
            $"\t\t\t\t\t\"modifier\"\t\t\"particles/econ/items/zeus/arcana/zeus_arcana_head.vpcf\"\n" +
            "\t\t\t\t}\n" +
            $"\t\t\t\t\"asset_modifier2\"\n\t\t\t\t{{\n\t\t\t\t\t\"type\"\t\t\"activity\"\n\t\t\t\t\t\"modifier\"\t\t\"{tag}\"\n\t\t\t\t}}\n" +
            "\t\t\t}\n\t\t}";

        private static readonly string ArcanaHead = HeadBlock("arcana");
        private static readonly string SetHead = HeadBlock("storm");
        private static readonly string ItemHead = HeadBlock("crown");
        private const string SetWeaponBlock =
            "\t\t\"593\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/items/zeus/storm/zeus_storm_weapon.vmdl\"\n\t\t}";

        private static HeroModel Zeus(bool locked = true) => new()
        {
            HeroId = HeroId,
            Name = "Zeus",
            ItemIds = new List<int> { 589, 593, 604, 605, 606, 607 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [Base] = new() { "https://cdn.example/Assets/models/Zeus/base_arcana.zip" },
                [Legacy] = new() { "https://cdn.example/Assets/models/Zeus/storm_set.zip" },
                [HeadItem] = new() { "https://cdn.example/Assets/models/Zeus/item_head_crown.zip" },
            },
            BaseLocks = locked
                ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { ["base_arcana"] = new() { "head" } }
                : new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
        };

        #region SlotsFor / LockedIds / IsLockedItemTag

        [Test]
        public void SlotsFor_LockingBase_ReturnsItsSlots()
            => Assert.That(BaseSlotLock.SlotsFor(Zeus(), Base), Is.EqualTo(new[] { "head" }));

        [Test]
        public void SlotsFor_NonBaseOrUnlistedOrUnknown_IsEmpty()
        {
            var hero = Zeus();
            hero.BaseLocks["storm_set"] = new() { "head" };
            Assert.Multiple(() =>
            {
                Assert.That(BaseSlotLock.SlotsFor(hero, Legacy), Is.Empty, "only a base_* archive locks");
                Assert.That(BaseSlotLock.SlotsFor(Zeus(locked: false), Base), Is.Empty);
                Assert.That(BaseSlotLock.SlotsFor(hero, "Set 99"), Is.Empty);
                Assert.That(BaseSlotLock.SlotsFor(hero, null), Is.Empty);
            });
        }

        [Test]
        public void LockedIds_OnlyThisHerosIdsInTheLockedSlot()
        {
            Assert.That(BaseSlotLock.LockedIds(new[] { 589, 593, 604 }, new[] { "HEAD" }), Is.EquivalentTo(new[] { "604" }));
            Assert.That(BaseSlotLock.LockedIds(new[] { 589, 593 }, new[] { "head" }), Is.Empty);
            Assert.That(BaseSlotLock.LockedIds(null, new[] { "head" }), Is.Empty);
        }

        [TestCase("head", true)]
        [TestCase("HEAD", true)]
        [TestCase("weapon", false)]
        [TestCase(null, false)]
        public void IsLockedItemTag_MatchesTheLockedSlot(string? tag, bool expected)
            => Assert.That(BaseSlotLock.IsLockedItemTag(tag, new[] { "head" }), Is.EqualTo(expected));

        [Test]
        public void IsLockedItemTag_UsesTheSameAliasesAsTheUi()
            => Assert.That(BaseSlotLock.IsLockedItemTag("arm", new[] { "arms" }), Is.True);

        [TestCase("head", true)]
        [TestCase("body", true)]
        [TestCase("weapon", false)]
        [TestCase("shoulder", false)]
        public void IsLockedItemTag_CompoundSlotCoversEachPart(string tag, bool expected)
            => Assert.That(BaseSlotLock.IsLockedItemTag(tag, new[] { "body_head" }), Is.EqualTo(expected));

        [Test]
        public void LockedIds_CompoundSlotMatchesTheRegistrySlotExactly()
        {
            Assert.That(BaseSlotLock.LockedIds(new[] { 90 }, new[] { "body_head" }), Is.EquivalentTo(new[] { "90" }));
            Assert.That(BaseSlotLock.LockedIds(new[] { 90 }, new[] { "head" }), Is.Empty, "head alone is not FV's slot");
        }

        #endregion

        #region Compute

        private static readonly ISet<string> Head = new HashSet<string> { "604" };

        private static Dictionary<string, ISet<string>> Files(params (string Layer, string[] Paths)[] f)
            => f.ToDictionary(x => x.Layer, x => (ISet<string>)new HashSet<string>(x.Paths, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        [Test]
        public void Compute_SetOwnsTheLockedHead_ReclaimsTheArcanaBlockAndRestoresItsFiles()
        {
            var final = new Dictionary<string, (string, string)> { ["604"] = (SetHead, Legacy), ["593"] = (SetWeaponBlock, Legacy) };
            var files = Files((Base, new[] { Hair, HeadFx }), (Legacy, new[] { Hair, SetWeapon }));
            var owner = new Dictionary<string, string> { [Hair] = Legacy, [HeadFx] = Base, [SetWeapon] = Legacy };

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = ArcanaHead }, Head, files, owner);

            Assert.Multiple(() =>
            {
                Assert.That(r.Reclaim.Keys, Is.EquivalentTo(new[] { "604" }));
                Assert.That(r.Reclaim["604"], Does.Contain("\"arcana\""));
                Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string> { [Hair] = Base }),
                    "the particle is already the arcana's; the weapon is not the arcana's business");
                Assert.That(r.Warnings, Is.Empty);
            });
        }

        [Test]
        public void Compute_BaseAlreadyOwnsEverything_IsClean()
        {
            var final = new Dictionary<string, (string, string)> { ["604"] = (ArcanaHead, Base) };
            var files = Files((Base, new[] { Hair, HeadFx }));
            var owner = new Dictionary<string, string> { [Hair] = Base, [HeadFx] = Base };

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = ArcanaHead }, Head, files, owner);

            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void Compute_BaseDoesNotDefineTheLockedId_WarnsAndLeavesIt()
        {
            var final = new Dictionary<string, (string, string)> { ["604"] = (SetHead, Legacy) };

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string>(), Head, Files(), new Dictionary<string, string>());

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings.Single(), Does.Contain("does not define it"));
        }

        [Test]
        public void Compute_NothingLocked_IsClean()
        {
            var final = new Dictionary<string, (string, string)> { ["604"] = (SetHead, Legacy) };
            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = ArcanaHead },
                new HashSet<string>(), Files(), new Dictionary<string, string>());
            Assert.That(r.IsClean && r.Warnings.Count == 0, Is.True);
        }

        private const string Vest = "models/heroes/zeus/zeus_vest.vmdl_c";
        private const string Bracers = "models/heroes/zeus/zeus_bracers.vmdl_c";
        private const string Body = "models/heroes/zeus/zeus_arcana.vmdl_c";
        private const string HairMorph = "models/heroes/zeus/zeus_hair/zeus_hair_vmorf.vtex_c";

        private static readonly Dictionary<string, string> ZeusSlotModels = new()
        {
            ["589"] = "models/heroes/zeus/zeus_bracers.vmdl", ["593"] = "", ["604"] = "models/heroes/zeus/zeus_hair.vmdl",
            ["605"] = "", ["606"] = "models/heroes/zeus/zeus_belt.vmdl", ["607"] = "models/heroes/zeus/zeus_vest.vmdl",
        };

        private static readonly string HidingArcanaHead = ArcanaHead.Replace("\t\t\t}\n\t\t}",
            "\t\t\t\t\"asset_modifier3\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_vest.vmdl\"\n\t\t\t\t\t\"modifier\"\t\t\"models/development/invisiblebox.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier4\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_bracers.vmdl\"\n\t\t\t\t\t\"modifier\"\t\t\"models/development/invisiblebox.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier5\"\n\t\t\t\t{\n\t\t\t\t\t\"type\"\t\t\"entity_model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"npc_dota_hero_zuus\"\n\t\t\t\t\t\"modifier\"\t\t\"models/heroes/zeus/zeus_arcana.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t}\n\t\t}");

        [Test]
        public void Compute_OtherSlotsDefaultModelsAndTheBody_AreNotTheLocksToRestore()
        {
            var final = new Dictionary<string, (string, string)> { ["604"] = (SetHead, Legacy) };
            var shared = new[] { Hair, Vest, Bracers, Body };
            var files = Files((Base, shared), (Legacy, shared));
            var owner = shared.ToDictionary(p => p, _ => Legacy);

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = HidingArcanaHead },
                Head, files, owner, ZeusSlotModels);

            Assert.That(r.Restore.Keys, Is.EquivalentTo(new[] { Hair }),
                "invisiblebox over the set's vest/bracers would blank them (WornModelYield's call); the body is BodyOwnership's");
        }

        [Test]
        public void Compute_LockedSlotsModelAndSidecars_RestoredEvenWhenTheBlockNeverQuotesThem()
        {
            var block = "\t\t\"604\"\n\t\t{\n\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t}\n\t\t}";
            var final = new Dictionary<string, (string, string)> { ["604"] = (SetHead, Legacy) };
            var files = Files((Base, new[] { Hair, HairMorph }), (Legacy, new[] { Hair, HairMorph }));
            var owner = new Dictionary<string, string> { [Hair] = Legacy, [HairMorph] = Legacy };

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = block }, Head, files, owner, ZeusSlotModels);

            Assert.That(r.Restore.Keys, Is.EquivalentTo(new[] { Hair, HairMorph }));
        }

        [Test]
        public void Compute_PathLastWrittenByAPrismatic_YieldsToIt()
        {
            const string Prismatic = "Set 6";
            var final = new Dictionary<string, (string, string)> { ["604"] = (ArcanaHead, Base) };
            var files = Files((Base, new[] { Hair, HeadFx }), (Prismatic, new[] { HeadFx }));
            var owner = new Dictionary<string, string> { [Hair] = Base, [HeadFx] = Prismatic };

            var r = BaseSlotLock.Compute(final, Base, new Dictionary<string, string> { ["604"] = ArcanaHead }, Head, files, owner,
                ZeusSlotModels, yieldTo: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Prismatic });

            Assert.That(r.IsClean, Is.True, "the prismatic recolours the arcana on purpose; it is applied last");
        }

        [Test]
        public void Compute_WarningsNameTheArcanaTheUserKnows()
        {
            var r = BaseSlotLock.Compute(new Dictionary<string, (string, string)>(), Base, new Dictionary<string, string>(), Head,
                Files(), new Dictionary<string, string>(), label: l => l == Base ? "Tempest Helm" : l);

            Assert.That(r.Warnings.Single(), Does.StartWith("'Tempest Helm' locks item 604"));
        }

        [Test]
        public void ReferencedFiles_PathsWithAndWithoutCompiledSuffix_KeywordsIgnored()
        {
            var refs = BaseSlotLock.ReferencedFiles(ArcanaHead).ToList();
            Assert.That(refs, Does.Contain(Hair).And.Contain(HeadFx));
            Assert.That(refs, Has.None.EqualTo("arcana").And.None.EqualTo("particle_create"));
        }

        #endregion

        #region Generation (real merge + apply)

        private string _tmp = "";

        [SetUp]
        public void SetUp() => _tmp = Path.Combine(Path.GetTempPath(), "amt_baselock_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tmp, true); } catch {  }
        }

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
            public Dictionary<string, Dictionary<string, (string block, string heroId)>> LayerBlocks = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ISet<string>> LayerFiles = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FileOwner = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (string Root, bool Encrypted)> Roots = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Warnings = new();
            public string ExtractDir = "";
            public string Shipped(string rel) => File.ReadAllText(Path.Combine(ExtractDir, rel.Replace('/', Path.DirectorySeparatorChar)));
        }

        private Task<Merge> RunAsync(HeroModel hero, params (string Layer, string Folder, bool Encrypted, (string Id, string Block)[] Blocks)[] layers)
            => RunAsync(hero, allPasses: false, layers);

        private async Task<Merge> RunAsync(HeroModel hero, bool allPasses, params (string Layer, string Folder, bool Encrypted, (string Id, string Block)[] Blocks)[] layers)
        {
            var service = new HeroGenerationService();
            var m = new Merge { ExtractDir = Path.Combine(_tmp, "extract") };
            Directory.CreateDirectory(m.ExtractDir);
            foreach (var (layer, folder, encrypted, blocks) in layers)
            {
                var (root, copied) = await service.MergeSetAssetsAsync(folder, m.ExtractDir, CancellationToken.None);
                m.Roots[layer] = (root, encrypted);
                m.LayerFiles[layer] = new HashSet<string>(copied, StringComparer.OrdinalIgnoreCase);
                foreach (var rel in copied)
                {
                    m.FileOwner[rel] = layer;
                    if (encrypted && ProtectedVpkStore.IsProtectable(rel)) m.Protected.Add(rel); else m.Protected.Remove(rel);
                }
                m.LayerBlocks[layer] = blocks.ToDictionary(b => b.Id, b => (b.Block, HeroId));
                foreach (var (id, block) in blocks)
                {
                    m.Blocks[id] = (block, HeroId);
                    m.BlockOwner[id] = layer;
                }
            }

            HeroGenerationService.ApplyBaseSlotLock(hero, Base, m.Blocks, m.BlockOwner, m.LayerBlocks, m.LayerFiles,
                m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add);
            if (allPasses)
            {
                var variants = m.Roots.ToDictionary(kv => kv.Key, kv => HeroGenerationService.ReadBodyVariants(kv.Value.Root), StringComparer.OrdinalIgnoreCase);
                HeroGenerationService.ApplyBodyOwnership(hero, m.Blocks, m.BlockOwner, m.LayerFiles, variants,
                    m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add);
                HeroGenerationService.ApplyWornModelYield(hero.DisplayName, m.Blocks, m.BlockOwner, m.LayerFiles, m.FileOwner,
                    m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add);
            }
            return m;
        }

        [Test]
        public async Task Generate_AllPasses_ZeusArcanaPlusLegacySet_HeadIsArcana_SetVestStaysVisible_BodyIsArcana()
        {
            const string ZeusBody = "models/heroes/zeus/zeus.vmdl_c";
            string vestBlock = "\t\t\"607\"\n\t\t{\n\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_vest.vmdl\"\n\t\t}";
            var arcana = (Base, SetFolder("base_arcana_full",
                    (Hair, "arcana_hair"), (HeadFx, "arcana_fx"), (Vest, "invisiblebox"), (Bracers, "invisiblebox"),
                    (Body, "arcana_mirror"), (ZeusBody, "arcana_body")),
                true, new[] { ("604", HidingArcanaHead) });
            var storm = (Legacy, SetFolder("storm_full",
                    (Hair, "storm_hair"), (Vest, "storm_vest"), (ZeusBody, "regular_body")),
                false, new[] { ("604", SetHead), ("607", vestBlock) });

            var m = await RunAsync(Zeus(), allPasses: true, arcana, storm);

            Assert.Multiple(() =>
            {
                Assert.That(m.BlockOwner["604"], Is.EqualTo(Base));
                Assert.That(m.Shipped(Hair), Is.EqualTo("arcana_hair"));
                Assert.That(m.Shipped(HeadFx), Is.EqualTo("arcana_fx"));
                Assert.That(m.Shipped(Vest), Is.EqualTo("storm_vest"), "the lock must not blank the set's vest with the arcana's invisiblebox");
                Assert.That(m.Blocks["604"].block, Does.Not.Contain("zeus_vest.vmdl"), "the worn pass lets the set's vest show");
                Assert.That(m.Blocks["604"].block, Does.Contain("zeus_bracers.vmdl"), "nobody wears the arms — the arcana keeps hiding them");
                Assert.That(m.Shipped(ZeusBody), Is.EqualTo("arcana_body"), "the body stays the arcana's (BodyOwnership)");
            });
        }

        [Test]
        public async Task Generate_PrismaticAppliedLast_KeepsItsRecolourOfTheArcanaHead()
        {
            const string Prismatic = "Set 6";
            var hero = Zeus();
            hero.Sets[Prismatic] = new() { "https://cdn.example/Assets/models/Zeus/prismatic_gold.zip" };
            var prismatic = (Prismatic, SetFolder("prismatic_gold", (HeadFx, "gold_fx")), false, Array.Empty<(string, string)>());

            var m = new Merge();
            var layers = new[] { BaseLayer(), LegacyLayer(), prismatic };
            var service = new HeroGenerationService();
            m.ExtractDir = Path.Combine(_tmp, "extract");
            Directory.CreateDirectory(m.ExtractDir);
            foreach (var (layer, folder, enc, blocks) in layers)
            {
                var (root, copied) = await service.MergeSetAssetsAsync(folder, m.ExtractDir, CancellationToken.None);
                m.Roots[layer] = (root, enc);
                m.LayerFiles[layer] = new HashSet<string>(copied, StringComparer.OrdinalIgnoreCase);
                foreach (var rel in copied) m.FileOwner[rel] = layer;
                m.LayerBlocks[layer] = blocks.ToDictionary(b => b.Item1, b => (b.Item2, HeroId));
                foreach (var (id, block) in blocks) { m.Blocks[id] = (block, HeroId); m.BlockOwner[id] = layer; }
            }

            HeroGenerationService.ApplyBaseSlotLock(hero, Base, m.Blocks, m.BlockOwner, m.LayerBlocks, m.LayerFiles,
                m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add,
                yieldTo: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Prismatic });

            Assert.That(m.BlockOwner["604"], Is.EqualTo(Base));
            Assert.That(m.Shipped(Hair), Is.EqualTo("arcana_hair"));
            Assert.That(m.Shipped(HeadFx), Is.EqualTo("gold_fx"), "a prismatic on the arcana is never undone by the lock");
        }

        private (string, string, bool, (string, string)[]) BaseLayer() => (Base,
            SetFolder("base_arcana", (Hair, "arcana_hair"), (HeadFx, "arcana_fx")), true, new[] { ("604", ArcanaHead) });

        private (string, string, bool, (string, string)[]) LegacyLayer() => (Legacy,
            SetFolder("storm_set", (Hair, "storm_hair"), (HeadFx, "storm_fx"), (SetWeapon, "storm_weapon")), false,
            new[] { ("604", SetHead), ("593", SetWeaponBlock) });

        private (string, string, bool, (string, string)[]) HeadItemLayer() => (HeadItem,
            SetFolder("item_head_crown", (Hair, "crown_hair")), false, new[] { ("604", ItemHead) });

        [Test]
        public async Task Generate_ArcanaThenLegacySetThenHeadItem_HeadStaysArcana_RestFollowsTheSet()
        {
            var m = await RunAsync(Zeus(), BaseLayer(), LegacyLayer(), HeadItemLayer());

            Assert.Multiple(() =>
            {
                Assert.That(m.Blocks["604"].block, Is.EqualTo(ArcanaHead), "head block — model, particles, animation — is the arcana's");
                Assert.That(m.BlockOwner["604"], Is.EqualTo(Base));
                Assert.That(m.Shipped(Hair), Is.EqualTo("arcana_hair"));
                Assert.That(m.Shipped(HeadFx), Is.EqualTo("arcana_fx"));
                Assert.That(m.Protected, Does.Contain(Hair), "restored files carry the arcana's protection");
                Assert.That(m.BlockOwner["593"], Is.EqualTo(Legacy), "the set keeps every slot the arcana does not lock");
                Assert.That(m.Shipped(SetWeapon), Is.EqualTo("storm_weapon"));
                Assert.That(m.Warnings, Is.Empty);
            });
        }

        [Test]
        public async Task Generate_WithoutTheLock_SetKeepsTheHead()
        {
            var m = await RunAsync(Zeus(locked: false), BaseLayer(), LegacyLayer());

            Assert.That(m.BlockOwner["604"], Is.EqualTo(Legacy), "no heroes.json lock, ADR-0008 ordering stands");
            Assert.That(m.Shipped(Hair), Is.EqualTo("storm_hair"));
        }

        [Test]
        public async Task Generate_ReferencedFileMissingFromTheBaseRoot_FailsTheHero()
        {
            var baseLayer = BaseLayer();
            var legacy = LegacyLayer();
            var service = new HeroGenerationService();
            var m = new Merge { ExtractDir = Path.Combine(_tmp, "extract") };
            Directory.CreateDirectory(m.ExtractDir);

            foreach (var (layer, folder, enc, blocks) in new[] { baseLayer, legacy })
            {
                var (root, copied) = await service.MergeSetAssetsAsync(folder, m.ExtractDir, CancellationToken.None);
                m.Roots[layer] = (root, enc);
                m.LayerFiles[layer] = new HashSet<string>(copied, StringComparer.OrdinalIgnoreCase);
                foreach (var rel in copied) m.FileOwner[rel] = layer;
                m.LayerBlocks[layer] = blocks.ToDictionary(b => b.Item1, b => (b.Item2, HeroId));
                foreach (var (id, block) in blocks) { m.Blocks[id] = (block, HeroId); m.BlockOwner[id] = layer; }
            }
            File.Delete(Path.Combine(m.Roots[Base].Root, Hair.Replace('/', Path.DirectorySeparatorChar)));

            Assert.That(() => HeroGenerationService.ApplyBaseSlotLock(Zeus(), Base, m.Blocks, m.BlockOwner, m.LayerBlocks,
                    m.LayerFiles, m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add),
                Throws.InvalidOperationException.With.Message.Contains("vanished"));
        }

        [Test]
        public async Task Generate_LockedSlotTheHeroDoesNotHave_WarnsAndChangesNothing()
        {
            var hero = Zeus();
            hero.BaseLocks["base_arcana"] = new() { "mount" };

            var m = await RunAsync(hero, BaseLayer(), LegacyLayer());

            Assert.That(m.BlockOwner["604"], Is.EqualTo(Legacy));
            Assert.That(m.Warnings.Single(), Does.Contain("no such slot"));
        }

        #endregion
    }
}
