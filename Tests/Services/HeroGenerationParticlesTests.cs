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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroGenerationParticlesTests
    {
        private const string Jugg = "npc_dota_hero_juggernaut";
        private const string Gold = "particles/econ/seasonal/new_bloom/seasonal_ambient_gold_drop.vpcf";
        private const string Raven = "particles/econ/seasonal/crownfall/seasonal_ambient_screeauk_v2.vpcf";

        private static HeroModel JuggHero() => new()
        {
            HeroId = Jugg,
            Name = "Juggernaut",
            ItemIds = new List<int> { 6, 7, 8, 9, 62, 811 }
        };

        private static string Block(string id, string model) =>
            $"\t\t\"{id}\"\n\t\t{{\n\t\t\t\"name\"\t\t\"x\"\n\t\t\t\"model_player\"\t\t\"{model}\"\n\t\t\t\"item_rarity\"\t\t\"common\"\n\t\t}}";

        private static IReadOnlyDictionary<string, IReadOnlyList<ParticlePick>> Picks(params (string id, string host)[] picks) =>
            new Dictionary<string, IReadOnlyList<ParticlePick>>
            {
                [Jugg] = picks.Select(p => new ParticlePick { Id = p.id, Host = p.host }).ToList()
            };

        #region ApplyHeroParticles

        [Test]
        public void ApplyHeroParticles_OwnedHost_CarriesItsParticle_UnownedHostIsSkipped()
        {
            var merged = new Dictionary<string, (string block, string heroId)> { ["7"] = (Block("7", "models/sword.vmdl"), Jugg) };
            var manifest = new List<SkinManifestEntry>();
            var warnings = new List<string>();

            HeroGenerationService.ApplyHeroParticles(new[] { JuggHero() }, Picks(("833", "7"), ("837", "6")), merged,
                manifest, _ => { }, warnings.Add);

            Assert.That(merged["7"].block, Does.Contain(Gold).And.Contain("\"item_quality\"\t\t\"unusual\""));
            Assert.That(merged.ContainsKey("6"), Is.False, "no picked layer has the head: no vanilla block is pulled in");
            Assert.That(warnings.Single(), Does.Contain("Ravenblight").And.Contain("no picked set, item or base"));
            Assert.That(manifest.Select(m => (m.SetName, m.Layer, m.Host)), Is.EqualTo(new[]
            {
                ("particle:833", "particle", (string?)"7"),
            }));
        }

        [Test]
        public void ApplyHeroParticles_TwoOnOneHost_TakeConsecutiveModifiers()
        {
            var merged = new Dictionary<string, (string block, string heroId)> { ["7"] = (Block("7", "models/sword.vmdl"), Jugg) };

            HeroGenerationService.ApplyHeroParticles(new[] { JuggHero() }, Picks(("833", "7"), ("837", "7")), merged,
                new List<SkinManifestEntry>(), _ => { }, _ => { });

            var block = merged["7"].block;
            Assert.That(Regex.IsMatch(block, @"""asset_modifier0""\s*\{[^}]*" + Regex.Escape(Gold)), Is.True);
            Assert.That(Regex.IsMatch(block, @"""asset_modifier1""\s*\{[^}]*" + Regex.Escape(Raven)), Is.True);
        }

        [Test]
        public void ApplyHeroParticles_HostWithoutModel_IsWrittenWithANote()
        {
            var merged = new Dictionary<string, (string block, string heroId)> { ["811"] = (Block("811", ""), Jugg) };
            var manifest = new List<SkinManifestEntry>();
            var warnings = new List<string>();
            var log = new List<string>();

            HeroGenerationService.ApplyHeroParticles(new[] { JuggHero() }, Picks(("833", "811")), merged,
                manifest, log.Add, warnings.Add);

            Assert.That(merged["811"].block, Does.Contain(Gold));
            Assert.That(manifest.Single().Host, Is.EqualTo("811"));
            Assert.That(warnings, Is.Empty);
            Assert.That(log.Last(), Does.StartWith("Note:").And.Contain("Gold Drop").And.Contain("may not render"));
        }

        [Test]
        public void ApplyHeroParticles_HeroThatDidNotBuild_GetsNothing()
        {
            var merged = new Dictionary<string, (string block, string heroId)>();
            var log = new List<string>();

            HeroGenerationService.ApplyHeroParticles(Array.Empty<HeroModel>(), Picks(("833", "7")), merged,
                new List<SkinManifestEntry>(), log.Add, _ => { });

            Assert.That(merged, Is.Empty);
            Assert.That(log.Single(), Does.StartWith("Note:"));
        }

        [Test]
        public void ApplyHeroParticles_PicksAreSanitizedAgain()
        {
            var merged = new Dictionary<string, (string block, string heroId)> { ["7"] = (Block("7", "models/sword.vmdl"), Jugg) };
            var manifest = new List<SkinManifestEntry>();

            HeroGenerationService.ApplyHeroParticles(new[] { JuggHero() },
                Picks(("bogus", "7"), ("833", "253"), ("833", "7"), ("837", "7"), ("826", "7")), merged,
                manifest, _ => { }, _ => { });

            Assert.That(manifest.Select(m => m.SetName), Is.EqualTo(new[] { "particle:833", "particle:837" }));
            Assert.That(merged["7"].block, Does.Not.Contain("seasonal_ambient_silver"));
        }

        #endregion

        #region GetParticleSlotsAsync

        private static HeroGenerationService SlotsService(string? indexText)
        {
            var index = new Mock<IHeroIndexProvider>();
            index.Setup(i => i.GetIndexTextAsync(It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(indexText);
            return new HeroGenerationService(patcher: new HeroSetPatcherService(), indexProvider: index.Object);
        }

        private static HeroModel JuggWithSets()
        {
            var hero = JuggHero();
            hero.ItemIds.Add(560);
            hero.Sets["Exile"] = new List<string> { "https://cdn.example/Assets/models/Juggernaut/exile.zip" };
            hero.Sets["Kantusa"] = new List<string> { "https://cdn.example/Assets/models/Juggernaut/item_weapon_kantusa.zip" };
            return hero;
        }

        private static string Def(string id, string? slot, string? model) =>
            $"\"{id}\"\n{{\n\t\"prefab\"\t\t\"default_item\"\n"
            + (slot != null ? $"\t\"item_slot\"\t\t\"{slot}\"\n" : "")
            + (model != null ? $"\t\"model_player\"\t\t\"{model}\"\n" : "")
            + "}\n";

        [Test]
        public async Task GetParticleSlotsAsync_Set_ListsItsModelledBlocks_AndAsks()
        {
            var text = Def("6", "head", "models/mask.vmdl") + Def("7", null, "models/sword.vmdl") + Def("560", "ability2", null);

            var slots = await SlotsService(text).GetParticleSlotsAsync(JuggWithSets(), "Exile");

            Assert.That(slots, Is.Not.Null);
            Assert.That(slots!.Slots.Select(s => s.Id), Is.EqualTo(new[] { "6", "7" }), "the ward block has no model to carry it");
            Assert.That(slots.Preferred, Is.Null, "a set asks which slot");
        }

        [Test]
        public async Task GetParticleSlotsAsync_Item_GoesStraightToItsOwnBlock()
        {
            var text = Def("6", "head", "models/mask.vmdl") + Def("7", null, "models/sword.vmdl");

            var slots = await SlotsService(text).GetParticleSlotsAsync(JuggWithSets(), "Kantusa");

            Assert.That(slots!.Preferred, Is.EqualTo("7"), "item_weapon_* lands on the weapon block");
        }

        [Test]
        public async Task GetParticleSlotsAsync_NoIndexOrUnknownSet_IsNull()
        {
            Assert.That(await SlotsService(null).GetParticleSlotsAsync(JuggWithSets(), "Exile"), Is.Null);
            Assert.That(await SlotsService(Def("7", null, "m.vmdl")).GetParticleSlotsAsync(JuggWithSets(), "Missing"), Is.Null);
        }

        #endregion

        #region RestoreFromPristine

        [Test]
        public void RestoreFromPristine_PutsBaseFilesBack_AndDeletesLayerOnlyFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "ArdysaRestore_" + Guid.NewGuid().ToString("N"));
            var pristine = Path.Combine(root, "pristine");
            var tree = Path.Combine(root, "tree");
            try
            {
                Directory.CreateDirectory(Path.Combine(pristine, "models"));
                Directory.CreateDirectory(Path.Combine(tree, "models"));
                File.WriteAllText(Path.Combine(pristine, "models", "a.vmdl_c"), "vanilla");
                File.WriteAllText(Path.Combine(tree, "models", "a.vmdl_c"), "from a layer");
                File.WriteAllText(Path.Combine(tree, "models", "new.vmdl_c"), "only the layer has it");
                File.WriteAllText(Path.Combine(tree, "models", "other.vmdl_c"), "another hero's");

                HeroGenerationService.RestoreFromPristine(new[] { "models/a.vmdl_c", "models/new.vmdl_c", "models/a.vmdl_c" }, pristine, tree);

                Assert.That(File.ReadAllText(Path.Combine(tree, "models", "a.vmdl_c")), Is.EqualTo("vanilla"));
                Assert.That(File.Exists(Path.Combine(tree, "models", "new.vmdl_c")), Is.False);
                Assert.That(File.ReadAllText(Path.Combine(tree, "models", "other.vmdl_c")), Is.EqualTo("another hero's"), "untouched");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        #endregion

        #region A hero that fails halfway ships nothing

        [Test]
        public async Task GenerateBatchAsync_HeroFailingAfterItsSetMerged_KeepsNoneOfItsBlocks()
        {
            var vpkStub = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            bool createdStub = false;
            var root = Path.Combine(Path.GetTempPath(), "ArdysaRollback_" + Guid.NewGuid().ToString("N"));
            var targetPath = Path.Combine(root, "dota");
            var pristine = Path.Combine(root, "pristine");
            try
            {
                if (!File.Exists(vpkStub)) { File.WriteAllText(vpkStub, "stub"); createdStub = true; }
                Directory.CreateDirectory(targetPath);
                Directory.CreateDirectory(Path.Combine(pristine, "scripts", "items"));
                File.WriteAllText(Path.Combine(pristine, "scripts", "items", "items_game.txt"), "\"items_game\" { }");

                var heroA = new HeroModel
                {
                    HeroId = "npc_dota_hero_a", Name = "A", ItemIds = { 123 },
                    Sets =
                    {
                        ["Set A"] = new List<string> { "https://cdn.example/Assets/models/a/set_a.zip" },
                        ["Item A"] = new List<string> { "https://cdn.example/Assets/models/a/item_weapon_a.zip" },
                    }
                };
                var heroB = new HeroModel
                {
                    HeroId = "npc_dota_hero_b", Name = "B", ItemIds = { 200 },
                    Sets = { ["Set B"] = new List<string> { "https://cdn.example/Assets/models/b/set_b.zip" } }
                };

                string SetFolder(string name)
                {
                    var dir = Path.Combine(root, "sets", name);
                    Directory.CreateDirectory(Path.Combine(dir, "models"));
                    File.WriteAllText(Path.Combine(dir, "models", name + ".vmdl_c"), name);
                    return dir;
                }

                var original = new Mock<IOriginalVpkProvider>();
                original.Setup(o => o.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                        It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()))
                    .ReturnsAsync(pristine);
                var downloader = new Mock<IHeroSetDownloader>();
                downloader.Setup(d => d.DownloadAndExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<Action?>()))
                    .ReturnsAsync((string _, string setName, string _, Action<string> _, CancellationToken _, IProgress<SpeedMetrics>? _, Action? _)
                        => SetFolder(setName.Replace(' ', '_')));
                var index = new Mock<IHeroIndexProvider>();
                index.Setup(i => i.GetIndexTextAsync(It.Is<string>(u => u.EndsWith("set_a.zip")), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("\"123\"\n{\n\t\"name\"\t\"a\"\n}");
                index.Setup(i => i.GetIndexTextAsync(It.Is<string>(u => u.EndsWith("set_b.zip")), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("\"200\"\n{\n\t\"name\"\t\"b\"\n}");
                index.Setup(i => i.GetIndexTextAsync(It.Is<string>(u => u.EndsWith("item_weapon_a.zip")), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string?)null);
                var extractor = new Mock<IGameItemsGameExtractor>();
                extractor.Setup(e => e.RefreshFromGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var real = new HeroSetPatcherService();
                var patcher = new Mock<IHeroSetPatcher>();
                patcher.Setup(p => p.ParseIndexText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>()))
                    .Returns((string text, string heroId, IReadOnlyList<int> ids) => real.ParseIndexText(text, heroId, ids));
                string[]? patched = null;
                patcher.Setup(p => p.PatchWithMergedBlocksAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, (string block, string heroId)>>(),
                        It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<Action<int, int>?>()))
                    .Callback((string _, Dictionary<string, (string block, string heroId)> blocks, Action<string> _, CancellationToken _, Action<int, int>? _)
                        => patched = blocks.Keys.ToArray())
                    .ReturnsAsync(false);

                var service = new HeroGenerationService(originalProvider: original.Object, downloader: downloader.Object,
                    patcher: patcher.Object, itemsGameExtractor: extractor.Object, indexProvider: index.Object);

                await service.GenerateBatchAsync(targetPath,
                    new[] { (heroA, "Set A"), (heroA, "Item A"), (heroB, "Set B") }, _ => { }, ct: CancellationToken.None);

                Assert.That(patched, Is.Not.Null, "hero B still reached the patcher");
                Assert.That(patched, Is.EquivalentTo(new[] { "200" }), "hero A's merged set block was rolled back");
            }
            finally
            {
                if (createdStub) { try { File.Delete(vpkStub); } catch { } }
                try { Directory.Delete(root, true); } catch { }
            }
        }

        #endregion
    }
}
