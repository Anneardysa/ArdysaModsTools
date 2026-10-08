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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class WornModelYieldTests
    {
        private const string Bracers = "models/heroes/zeus/zeus_bracers.vmdl";
        private const string Vest = "models/heroes/zeus/zeus_vest.vmdl";
        private const string CosmicArms = "models/items/zeus/zeus_cosmic/zeus_cosmic_arms.vmdl";

        private const string Arcana = "Tempest Helm";
        private const string Arms = "Tempest Revelation";
        private const string Back = "Immortal Pantheon";

        private const string ArcanaBlock =
            "\t\t\"604\"\n\t\t{\n" +
            "\t\t\t\"name\"\t\t\"Zeus' Head\"\n" +
            "\t\t\t\"model_player\"\t\t\"models/heroes/zeus/zeus_hair.vmdl\"\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n" +
            "\t\t\t\t\"asset_modifier2\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"entity_model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"npc_dota_hero_zuus\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/heroes/zeus/zeus_arcana.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier6\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_vest.vmdl\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/development/invisiblebox.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier7\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/zeus/zeus_bracers.vmdl\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/development/invisiblebox.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t\t\"skip_model_combine\"\t\t\"1\"\n" +
            "\t\t\t\t\"asset_modifier\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/items/zeus/zeus_cosmic/zeus_cosmic_arms.vmdl\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/items/zeus/zeus_cosmic/zeus_cosmic_arms_refit.vmdl\"\n\t\t\t\t}\n" +
            "\t\t\t}\n\t\t}";

        private static string Worn(string id, string model) =>
            $"\t\t\"{id}\"\n\t\t{{\n\t\t\t\"model_player\"\t\t\"{model}\"\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"asset_modifier\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"particle_create\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"particles/econ/items/zeus/ambient.vpcf\"\n\t\t\t\t}\n\t\t\t}\n\t\t}";

        private static readonly string ArmsBlock = Worn("589", Bracers);
        private static readonly string BackBlock = Worn("607", Vest);

        private static Dictionary<string, (string Block, string Layer)> Blocks(params (string Id, string Block, string Layer)[] b)
            => b.ToDictionary(x => x.Id, x => (x.Block, x.Layer));

        private static Dictionary<string, ISet<string>> Files(params (string Layer, string[] Paths)[] f)
            => f.ToDictionary(x => x.Layer, x => (ISet<string>)new HashSet<string>(x.Paths, StringComparer.OrdinalIgnoreCase));

        private static string? Registry(string id)
            => HeroDefaultItemRegistry.TryGetItem(id, out var info) ? info.ModelPlayer : null;

        private static readonly string[] ArcanaFiles = { Bracers + "_c", Vest + "_c", CosmicArms + "_c" };

        #region KeyValuesBlockHelper row helpers

        [Test]
        public void GetModelSwapAssets_ReturnsOnlyModelRows_InOrder()
        {
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(ArcanaBlock),
                Is.EqualTo(new[] { Vest, Bracers, CosmicArms }));
        }

        [Test]
        public void GetModelSwapAssets_IgnoresParticleRowOnTheSameAsset_AndUnparseableBlocks()
        {
            string particleOnly = "\"1\"\n{\n\t\"visuals\"\n\t{\n\t\t\"asset_modifier\"\n\t\t{\n" +
                                  $"\t\t\t\"type\"\t\t\"particle\"\n\t\t\t\"asset\"\t\t\"{Bracers}\"\n\t\t}}\n\t}}\n}}";
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(particleOnly), Is.Empty);
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets("\"1\" { \"visuals\" {"), Is.Empty);
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(""), Is.Empty);
        }

        [Test]
        public void RemoveModelSwapRows_CutsOnlyTheMatchingRow_RestIsByteIdentical()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Bracers };
            string outBlock = KeyValuesBlockHelper.RemoveModelSwapRows(ArcanaBlock, set, out var removed);

            Assert.That(removed, Is.EqualTo(new[] { Bracers }));
            Assert.That(outBlock, Does.Not.Contain("zeus_bracers"));
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(outBlock), Is.EqualTo(new[] { Vest, CosmicArms }));

            int cut = ArcanaBlock.IndexOf("\t\t\t\t\"asset_modifier7\"", StringComparison.Ordinal);
            int after = ArcanaBlock.IndexOf("\t\t\t\t\"skip_model_combine\"", StringComparison.Ordinal);
            Assert.That(outBlock, Is.EqualTo(ArcanaBlock.Remove(cut, after - cut)));
        }

        [Test]
        public void RemoveModelSwapRows_NothingToRemove_ReturnsSameInstance()
        {
            var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "models/heroes/zeus/zeus_belt.vmdl" };
            Assert.That(KeyValuesBlockHelper.RemoveModelSwapRows(ArcanaBlock, none, out var removed), Is.SameAs(ArcanaBlock));
            Assert.That(removed, Is.Empty);
            Assert.That(KeyValuesBlockHelper.RemoveModelSwapRows(ArcanaBlock, new HashSet<string>(), out _), Is.SameAs(ArcanaBlock));
        }

        [Test]
        public void RemoveModelSwapRows_HandlesCrlfAndUnnumberedDuplicateKeys()
        {
            string crlf = ArcanaBlock.Replace("\n", "\r\n");
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CosmicArms };
            string outBlock = KeyValuesBlockHelper.RemoveModelSwapRows(crlf, set, out var removed);

            Assert.That(removed, Is.EqualTo(new[] { CosmicArms }));
            Assert.That(outBlock, Does.Not.Contain("zeus_cosmic_arms"));
            Assert.That(outBlock, Does.Not.Contain("\r\n\r\n"), "no blank line left where the row was");
            Assert.That(KeyValuesBlockHelper.GetModelSwapAssets(outBlock), Is.EqualTo(new[] { Vest, Bracers }));
        }

        #endregion

        #region WornModelYield.Compute

        [Test]
        public void Registry_KnowsZeusDefaultSlotModels()
        {
            Assert.That(Registry("589"), Is.EqualTo(Bracers));
            Assert.That(Registry("607"), Is.EqualTo(Vest));
        }

        [Test]
        public void ArcanaAlone_KeepsValvesPairing()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana)),
                Files((Arcana, ArcanaFiles)),
                ArcanaFiles.ToDictionary(p => p, _ => Arcana), Registry);

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void ArcanaPlusArms_ArcanaFileLast_StripsBracersRowAndRestoresArmsMesh()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" })),
                new Dictionary<string, string> { [Bracers + "_c"] = Arcana, [Vest + "_c"] = Arcana },
                Registry);

            Assert.That(r.StripRows.Keys, Is.EquivalentTo(new[] { "604" }));
            Assert.That(r.StripRows["604"], Is.EquivalentTo(new[] { Bracers }), "vest and the cosmic refit row stay");
            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string> { [Bracers + "_c"] = Arms }));
            Assert.That(r.Yields.Single(), Is.EqualTo(new WornModelYield.Yield("604", Bracers, Arcana, Arms, "589")));
        }

        private const string SlardarBack = "models/heroes/slardar/slardar_back.vmdl";
        private const string Fin = "Fin of the First Spear";
        private const string Flood = "First of the Flood";

        private const string FinBlock =
            "\t\t\"275\"\n\t\t{\n" +
            "\t\t\t\"visuals\"\n\t\t\t{\n" +
            "\t\t\t\t\"styles\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"0\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"auto_style_rule\"\t\t\"modifier_slardar_sprint== 0\"\n\t\t\t\t\t}\n" +
            "\t\t\t\t\t\"1\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"auto_style_rule\"\t\t\"modifier_slardar_sprint > 0\"\n\t\t\t\t\t}\n" +
            "\t\t\t\t}\n" +
            "\t\t\t\t\"asset_modifier\"\n\t\t\t\t{\n" +
            "\t\t\t\t\t\"type\"\t\t\"model\"\n" +
            "\t\t\t\t\t\"asset\"\t\t\"models/heroes/slardar/slardar_back.vmdl\"\n" +
            "\t\t\t\t\t\"modifier\"\t\t\"models/items/slardar/slardar_ti10_immortal_head/slardar_ti10_immortal_head_back_refit.vmdl\"\n" +
            "\t\t\t\t}\n\t\t\t}\n\t\t}";

        private static readonly string[] FinFiles = { SlardarBack + "_c", "models/heroes/slardar/slardar.vmdl_c" };

        [Test]
        public void Registry_KnowsSlardarBackPath_AndModellessHead()
        {
            Assert.That(Registry("274"), Is.EqualTo(SlardarBack));
            Assert.That(Registry("275"), Is.Empty, "head is baked into the body - no slot model");
        }

        [Test]
        public void SlardarHeadPlusTi9Back_HeadLast_BackWinsAndRefitRowGoes()
        {
            var r = WornModelYield.Compute(
                Blocks(("275", FinBlock, Fin), ("274", Worn("274", SlardarBack), Flood)),
                Files((Fin, FinFiles), (Flood, new[] { SlardarBack + "_c" })),
                new Dictionary<string, string> { [SlardarBack + "_c"] = Fin }, Registry);

            Assert.That(r.StripRows["275"], Is.EquivalentTo(new[] { SlardarBack }));
            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string> { [SlardarBack + "_c"] = Flood }));
            Assert.That(r.Yields.Single(), Is.EqualTo(new WornModelYield.Yield("275", SlardarBack, Fin, Flood, "274")));
        }

        [Test]
        public void SlardarHeadPlusTi9Back_BackLast_StripsRowButNothingToRestore()
        {
            var r = WornModelYield.Compute(
                Blocks(("275", FinBlock, Fin), ("274", Worn("274", SlardarBack), Flood)),
                Files((Fin, FinFiles), (Flood, new[] { SlardarBack + "_c" })),
                new Dictionary<string, string> { [SlardarBack + "_c"] = Flood }, Registry);

            Assert.That(r.StripRows["275"], Is.EquivalentTo(new[] { SlardarBack }));
            Assert.That(r.Restore, Is.Empty);
        }

        [Test]
        public void SlardarHeadAlone_KeepsRefitOnDefaultBack()
        {
            var r = WornModelYield.Compute(
                Blocks(("275", FinBlock, Fin)),
                Files((Fin, FinFiles)),
                FinFiles.ToDictionary(p => p, _ => Fin), Registry);

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Is.Empty);
        }

        [Test]
        public void ArcanaPlusArms_ArmsFileLast_StripsRowWithoutRestore()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" })),
                new Dictionary<string, string> { [Bracers + "_c"] = Arms, [Vest + "_c"] = Arcana },
                Registry);

            Assert.That(r.StripRows["604"], Is.EquivalentTo(new[] { Bracers }));
            Assert.That(r.Restore, Is.Empty);
        }

        [Test]
        public void ArcanaPlusArms_ArmsVariantLast_IsTheArmsLayersOwnCopy_NoRestore()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" })),
                new Dictionary<string, string>
                {
                    [Bracers + "_c"] = BodyOwnership.VariantOwner(Arms, "zeus_arcana"),
                    [Vest + "_c"] = Arcana,
                },
                Registry);

            Assert.That(r.StripRows["604"], Is.EquivalentTo(new[] { Bracers }));
            Assert.That(r.Restore, Is.Empty);
        }

        [Test]
        public void ArcanaPlusArms_AnotherLayersVariantLast_StillRestoresTheWearer()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" })),
                new Dictionary<string, string>
                {
                    [Bracers + "_c"] = BodyOwnership.VariantOwner(Back, "zeus_arcana"),
                    [Vest + "_c"] = Arcana,
                },
                Registry);

            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string> { [Bracers + "_c"] = Arms }));
        }

        [Test]
        public void ArcanaPlusArmsPlusBack_BothRowsYield()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms), ("607", BackBlock, Back)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" }), (Back, new[] { Vest + "_c" })),
                new Dictionary<string, string> { [Bracers + "_c"] = Arcana, [Vest + "_c"] = Arcana },
                Registry);

            Assert.That(r.StripRows["604"], Is.EquivalentTo(new[] { Bracers, Vest }));
            Assert.That(r.Restore, Is.EquivalentTo(new Dictionary<string, string>
            {
                [Bracers + "_c"] = Arms,
                [Vest + "_c"] = Back,
            }));
        }

        [Test]
        public void WearerThatShipsNoMesh_DoesNotYield()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", ArmsBlock, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { "particles/econ/items/zeus/ambient.vpcf_c" })),
                ArcanaFiles.ToDictionary(p => p, _ => Arcana), Registry);

            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void ModderRefitRow_OnAnItemPath_IsNeverStripped()
        {
            string cosmicItem = Worn("9999", CosmicArms);
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("9999", cosmicItem, "Cosmic Arms (modder)")),
                Files((Arcana, ArcanaFiles), ("Cosmic Arms (modder)", new[] { CosmicArms + "_c" })),
                new Dictionary<string, string> { [CosmicArms + "_c"] = Arcana },
                Registry);

            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void ModderZipOnTheDefaultId_KeepingItsOwnModelPath_KeepsTheRefitRow()
        {
            string modderArms = Worn("589", CosmicArms);
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", modderArms, "Cosmic Arms (modder)")),
                Files((Arcana, ArcanaFiles), ("Cosmic Arms (modder)", new[] { CosmicArms + "_c" })),
                new Dictionary<string, string> { [CosmicArms + "_c"] = Arcana },
                Registry);

            Assert.That(r.IsClean, Is.True);
        }

        [Test]
        public void SameLayerHidesAndWears_IsLeftAloneWithAWarning()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, "Whole Set"), ("589", ArmsBlock, "Whole Set")),
                Files(("Whole Set", ArcanaFiles)),
                ArcanaFiles.ToDictionary(p => p, _ => "Whole Set"), Registry);

            Assert.That(r.IsClean, Is.True);
            Assert.That(r.Warnings, Has.Count.EqualTo(1));
            Assert.That(r.Warnings[0], Does.Contain(Bracers));
        }

        [Test]
        public void PathCaseAndSlashes_StillMatch()
        {
            string armsOddCase = Worn("589", @"Models\Heroes\Zeus\Zeus_Bracers.vmdl");
            var r = WornModelYield.Compute(
                Blocks(("604", ArcanaBlock, Arcana), ("589", armsOddCase, Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { "MODELS/heroes/zeus/zeus_bracers.vmdl_c" })),
                new Dictionary<string, string> { [Bracers + "_c"] = Arms }, Registry);

            Assert.That(r.StripRows["604"], Is.EquivalentTo(new[] { Bracers }));
        }

        [Test]
        public void UnparseableBlocks_PassThroughWithoutThrowing()
        {
            var r = WornModelYield.Compute(
                Blocks(("604", "\"604\" { \"visuals\" {", Arcana), ("589", "garbage", Arms)),
                Files((Arcana, ArcanaFiles), (Arms, new[] { Bracers + "_c" })),
                new Dictionary<string, string>(), Registry);

            Assert.That(r.IsClean, Is.True);
        }

        #endregion

        #region HeroGenerationService.ApplyWornModelYield (real files)

        private string _tmp = "";

        [SetUp]
        public void SetUp()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "amt_worn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tmp, true); } catch {  }
        }

        private sealed class Merge
        {
            public Dictionary<string, (string block, string heroId)> Blocks = new();
            public Dictionary<string, string> BlockOwner = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ISet<string>> LayerFiles = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FileOwner = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (string Root, bool Encrypted)> Roots = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Warnings = new();
            public string ExtractDir = "";
        }

        private string Layer(string name, params (string Rel, string Content)[] files)
        {
            string root = Path.Combine(_tmp, "layers", name);
            foreach (var (rel, content) in files)
            {
                string p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, content);
            }
            return root;
        }

        private Merge Run(params (string Name, string Root, bool Encrypted, (string Id, string Block)[] Blocks)[] layers)
        {
            var m = new Merge { ExtractDir = Path.Combine(_tmp, "extract") };
            foreach (var (name, root, encrypted, blocks) in layers)
            {
                var copied = new List<string>();
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                    string dest = Path.Combine(m.ExtractDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, overwrite: true);
                    copied.Add(rel);
                    m.FileOwner[rel] = name;
                    if (encrypted && ProtectedVpkStore.IsProtectable(rel)) m.Protected.Add(rel); else m.Protected.Remove(rel);
                }
                m.Roots[name] = (root, encrypted);
                m.LayerFiles[name] = new HashSet<string>(copied, StringComparer.OrdinalIgnoreCase);
                foreach (var (id, block) in blocks)
                {
                    m.Blocks[id] = (block, "npc_dota_hero_zuus");
                    m.BlockOwner[id] = name;
                }
            }

            HeroGenerationService.ApplyWornModelYield("Zeus", m.Blocks, m.BlockOwner, m.LayerFiles, m.FileOwner,
                m.Roots, m.ExtractDir, m.Protected, trace: _ => { }, warn: m.Warnings.Add);
            return m;
        }

        private string Shipped(Merge m, string rel)
            => File.ReadAllText(Path.Combine(m.ExtractDir, rel.Replace('/', Path.DirectorySeparatorChar)));

        [TestCase(true, TestName = "Apply_ArcanaAppliedLast_ArmsMeshRestored")]
        [TestCase(false, TestName = "Apply_ArmsAppliedLast_ArmsMeshKept")]
        public void Apply_ArmsMeshShips_AndDefNoLongerHidesIt_InEitherOrder(bool arcanaLast)
        {
            string arcanaRoot = Layer("arcana", (Bracers + "_c", "invisiblebox"), (Vest + "_c", "invisiblebox"));
            string armsRoot = Layer("arms", (Bracers + "_c", "ti8_arms_mesh"));
            var arcana = (Arcana, arcanaRoot, false, new[] { ("604", ArcanaBlock) });
            var arms = (Arms, armsRoot, true, new[] { ("589", ArmsBlock) });

            var m = arcanaLast ? Run(arms, arcana) : Run(arcana, arms);

            Assert.That(Shipped(m, Bracers + "_c"), Is.EqualTo("ti8_arms_mesh"));
            Assert.That(Shipped(m, Vest + "_c"), Is.EqualTo("invisiblebox"), "the default vest is still worn");
            Assert.That(m.Blocks["604"].block, Does.Not.Contain("zeus_bracers"));
            Assert.That(m.Blocks["604"].block, Does.Contain("zeus_vest").And.Contain("zeus_cosmic_arms_refit"));
            Assert.That(m.Blocks["604"].heroId, Is.EqualTo("npc_dota_hero_zuus"));
            Assert.That(m.FileOwner[Bracers + "_c"], Is.EqualTo(Arms));
            Assert.That(m.Protected.Contains(Bracers + "_c"), Is.EqualTo(ProtectedVpkStore.IsProtectable(Bracers + "_c")),
                "protection follows the restored (encrypted) owner");
            Assert.That(m.Warnings, Is.Empty);
        }

        [Test]
        public void Apply_ArcanaAlone_ChangesNothing()
        {
            string arcanaRoot = Layer("arcana", (Bracers + "_c", "invisiblebox"), (Vest + "_c", "invisiblebox"));
            var m = Run((Arcana, arcanaRoot, false, new[] { ("604", ArcanaBlock) }));

            Assert.That(m.Blocks["604"].block, Is.SameAs(ArcanaBlock));
            Assert.That(Shipped(m, Bracers + "_c"), Is.EqualTo("invisiblebox"));
        }

        [Test]
        public void Apply_RestoreSourceMissing_StillStripsRowAndWarns()
        {
            string arcanaRoot = Layer("arcana", (Bracers + "_c", "invisiblebox"));
            string armsRoot = Layer("arms", (Bracers + "_c", "ti8_arms_mesh"));
            var m = new Merge { ExtractDir = Path.Combine(_tmp, "extract") };
            Directory.CreateDirectory(Path.Combine(m.ExtractDir, "models", "heroes", "zeus"));
            File.WriteAllText(Path.Combine(m.ExtractDir, "models", "heroes", "zeus", "zeus_bracers.vmdl_c"), "invisiblebox");
            m.Blocks["604"] = (ArcanaBlock, "npc_dota_hero_zuus");
            m.Blocks["589"] = (ArmsBlock, "npc_dota_hero_zuus");
            m.BlockOwner["604"] = Arcana;
            m.BlockOwner["589"] = Arms;
            m.LayerFiles[Arcana] = new HashSet<string> { Bracers + "_c" };
            m.LayerFiles[Arms] = new HashSet<string> { Bracers + "_c" };
            m.FileOwner[Bracers + "_c"] = Arcana;
            m.Roots[Arcana] = (arcanaRoot, false);
            m.Roots[Arms] = (armsRoot, false);
            File.Delete(Path.Combine(armsRoot, "models", "heroes", "zeus", "zeus_bracers.vmdl_c"));

            Assert.DoesNotThrow(() => HeroGenerationService.ApplyWornModelYield("Zeus", m.Blocks, m.BlockOwner,
                m.LayerFiles, m.FileOwner, m.Roots, m.ExtractDir, m.Protected, _ => { }, m.Warnings.Add));

            Assert.That(m.Blocks["604"].block, Does.Not.Contain("zeus_bracers"));
            Assert.That(m.Warnings, Has.Count.EqualTo(1));
            Assert.That(m.Warnings[0], Does.Contain("could not restore"));
        }

        #endregion
    }
}
