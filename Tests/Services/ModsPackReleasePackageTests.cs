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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Hero;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModsPackReleasePackageTests
    {
        private const string Marker = "materials/dev/deferred_light_cache.vtex_c";
        private const string Manifest = "version/_ardysamods";
        private const string ItemsGame = "scripts/items/items_game.txt";
        private const string Localization = "resource/localization/abilities_english.txt";
        private const string Credits = "Credits :\nApplication created by Anneardysa\n\n---\n\nMIT License";

        private static readonly (string, string)[] OneSet = { ("npc_dota_hero_axe", "Set A") };

        private string _root = null!;
        private string _tree = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), $"ModsPackReleaseTests_{Guid.NewGuid():N}");
            _tree = Path.Combine(_root, "pak01_dir");
            Directory.CreateDirectory(_tree);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, true); } catch {  }
        }

        private string P(string rel) => Path.Combine(_tree, rel.Replace('/', Path.DirectorySeparatorChar));

        private void Put(params string[] rels)
        {
            foreach (var rel in rels)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(P(rel))!);
                File.WriteAllText(P(rel), "x");
            }
        }

        private async Task WriteManifestAsync(params (string heroId, string setName)[] entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(P(Manifest))!);
            if (!File.Exists(P(Manifest)))
                await File.WriteAllTextAsync(P(Manifest), Credits);
            await SkinManifestStore.WriteToTreeAsync(_tree, new SkinManifest
            {
                Build = "2.9.3.2443",
                Entries = entries.Select(e => new SkinManifestEntry { HeroId = e.heroId, SetName = e.setName }).ToList()
            });
        }

        private async Task BuildReleaseTreeAsync()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(OneSet);
            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Is.Null);
        }

        private static Task<(VpkOrigin, bool)> Classify(string vpk, CancellationToken ct)
            => new ModInstallerService().ClassifyVpkAsync(vpk, ct);

        private string Pack()
        {
            string vpkTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vpk.exe");
            if (!File.Exists(vpkTool) || !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe")))
                Assert.Ignore("vpk.exe / HLExtract.exe not present in test output.");

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = vpkTool,
                Arguments = $"\"{_tree}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            proc.WaitForExit(30000);

            string vpk = Path.Combine(_root, "pak01_dir.vpk");
            Assert.That(File.Exists(vpk), "vpk.exe did not produce a package");
            return vpk;
        }

        #region PrepareTree — marker, package text, and the manifest matching the selection

        [Test]
        public async Task PrepareTree_KeepsCreditsAndManifest_WritesMissingMarker()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(OneSet);

            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Is.Null);

            string text = File.ReadAllText(P(Manifest));
            Assert.That(text, Does.StartWith("Credits :"), "the base's credits text ships with every release");
            Assert.That(SkinManifestStore.Parse(text)!.Entries.Single().SetName, Is.EqualTo("Set A"));
            Assert.That(File.Exists(P(Marker)), Is.True);
        }

        [Test]
        public async Task PrepareTree_KeepsAnExistingMarkerUntouched()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(OneSet);
            Directory.CreateDirectory(Path.GetDirectoryName(P(Marker))!);
            File.WriteAllText(P(Marker), "official bytes");

            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Is.Null);
            Assert.That(File.ReadAllText(P(Marker)), Is.EqualTo("official bytes"));
        }

        [Test]
        public async Task PrepareTree_NoItemsGame_Refused()
        {
            Put(Localization);
            await WriteManifestAsync(OneSet);
            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Does.Contain("items_game"));
        }

        [Test]
        public async Task PrepareTree_EmptyLocalization_Refused()
        {
            Put(ItemsGame);
            await WriteManifestAsync(OneSet);
            Directory.CreateDirectory(P("resource/localization"));
            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Does.Contain("localization"));
        }

        [Test]
        public void PrepareTree_MissingFolder_Refused()
        {
            Assert.That(ModsPackReleasePackage.PrepareTree(Path.Combine(_root, "nope"), OneSet), Is.Not.Null);
        }

        [Test]
        public void PrepareTree_CreditsWithoutManifest_Refused()
        {
            Put(ItemsGame, Localization);
            Directory.CreateDirectory(Path.GetDirectoryName(P(Manifest))!);
            File.WriteAllText(P(Manifest), Credits);

            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet), Does.Contain("manifest"));
        }

        [Test]
        public async Task PrepareTree_TamperedManifest_Refused()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(OneSet);
            File.WriteAllText(P(Manifest), File.ReadAllText(P(Manifest)).Replace("Set A", "Set B"));

            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, new[] { ("npc_dota_hero_axe", "Set B") }),
                Does.Contain("does not verify"));
        }

        [Test]
        public async Task PrepareTree_SelectedSetMissingFromManifest_Refused_NamesIt()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(OneSet);

            var error = ModsPackReleasePackage.PrepareTree(_tree,
                new[] { ("npc_dota_hero_axe", "Set A"), ("npc_dota_hero_pudge", "Hook") });

            Assert.That(error, Does.Contain("missing from the manifest").And.Contain("npc_dota_hero_pudge: Hook"));
        }

        [Test]
        public async Task PrepareTree_ManifestCarriesAnUnselectedSet_Refused_NamesIt()
        {
            Put(ItemsGame, Localization);
            await WriteManifestAsync(("npc_dota_hero_axe", "Set A"), ("npc_dota_hero_axe", "Stray"));

            Assert.That(ModsPackReleasePackage.PrepareTree(_tree, OneSet),
                Does.Contain("not selected").And.Contain("Stray"));
        }

        [Test]
        public void CompareSets_IgnoresCaseAndDuplicates()
        {
            Assert.That(ModsPackReleasePackage.CompareSets(
                new[] { ("npc_dota_hero_axe", "Set A"), ("npc_dota_hero_axe", "Set A") },
                new[] { ("NPC_DOTA_HERO_AXE", "set a") }, "x"), Is.Null);
        }

        [Test]
        public void CompareSets_ManyMissing_ListIsCapped()
        {
            var want = Enumerable.Range(0, 9).Select(i => ("h", $"S{i}")).ToArray();
            var error = ModsPackReleasePackage.CompareSets(want, Array.Empty<(string, string)>(), "the build");
            Assert.That(error, Does.Contain("(+4 more)"));
        }

        #endregion

        #region BuildIndex — modspack_index.txt the installers' rebuild applies

        private static (string, string) Block(string id, string body) =>
            ($"\t\"{id}\"\n\t{{\n{body}\n\t}}", "npc_dota_hero_axe");

        [Test]
        public void BuildIndex_RoundTripsThroughTheInstallersParser_InIdOrder()
        {
            var blocks = new Dictionary<string, (string block, string heroId)>
            {
                ["5000"] = Block("5000", "\t\t\"name\"\t\"Big\"\n\t\t\"styles\"\n\t\t{\n\t\t\t\"1\"\n\t\t\t{\n\t\t\t\t\"name\"\t\"s1\"\n\t\t\t}\n\t\t}"),
                ["123"] = Block("123", "\t\t\"name\"\t\"Mid\""),
                ["77"] = Block("77", "\t\t\"model_player\"\t\"models/x.vmdl\""),
            };

            string? index = ModsPackReleasePackage.BuildIndex(blocks, out var error);

            Assert.That(error, Is.Null);
            Assert.That(index!.IndexOf("\"77\"") < index.IndexOf("\"123\"") && index.IndexOf("\"123\"") < index.IndexOf("\"5000\""),
                "blocks are ordered by numeric id, like the ModsPack repo's generator");

            var parsed = new HeroSetPatcherService().ParseIndexText(index, "npc_dota_hero_axe", new[] { 77, 123, 5000, 1 });
            Assert.That(parsed!.Keys, Is.EquivalentTo(new[] { "77", "123", "5000" }), "a nested \"1\" style is never a top-level block");
            foreach (var (id, (block, _)) in blocks)
                Assert.That(parsed[id].block.TrimEnd(), Is.EqualTo(block.TrimEnd()), $"block {id} must come back byte-for-byte");
        }

        [Test]
        public void BuildIndex_NoBlocks_Refused()
        {
            Assert.That(ModsPackReleasePackage.BuildIndex(new Dictionary<string, (string, string)>(), out var error), Is.Null);
            Assert.That(error, Does.Contain("empty"));
        }

        [Test]
        public void BuildIndex_BlockThatDoesNotParseBack_Refused_NamesIt()
        {
            var blocks = new Dictionary<string, (string block, string heroId)>
            {
                ["9"] = ("\"9\"\n{\n\t\"name\"\t\"unterminated\"\n", "npc_dota_hero_axe"),
            };

            Assert.That(ModsPackReleasePackage.BuildIndex(blocks, out var error), Is.Null);
            Assert.That(error, Does.Contain("9"));
        }

        #endregion

        #region StripPackageText — the slim release

        [Test]
        public async Task StripPackageText_RemovesItemDataAndLocalization_KeepsManifestAndMarker()
        {
            await BuildReleaseTreeAsync();
            File.SetAttributes(P(ItemsGame), FileAttributes.ReadOnly);

            Assert.That(ModsPackReleasePackage.StripPackageText(_tree), Is.Null);

            Assert.That(File.Exists(P(ItemsGame)), Is.False);
            Assert.That(Directory.Exists(P("resource/localization")), Is.False);
            Assert.That(File.Exists(P(Manifest)) && File.Exists(P(Marker)), Is.True);
        }

        #endregion

        #region SealAsync — both packages against the real installer classifier

        [Test]
        public async Task Seal_SelfContainedWithManifest_IsTheInstallPackage()
        {
            await BuildReleaseTreeAsync();
            string vpk = Pack();

            Assert.That(await ModsPackReleasePackage.SealAsync(vpk, slim: false, Classify), Is.Null);

            Assert.That(VpkSignatureSection.IsApplied(vpk), Is.True);
            Assert.That(await Classify(vpk, default), Is.EqualTo((VpkOrigin.Official, false)),
                "manifest + item data installs as-is, like any Skin Selector build");
        }

        [Test]
        public async Task Seal_SlimWithManifest_IsAReleaseTheSkinSelectorStillReadsBack()
        {
            await BuildReleaseTreeAsync();
            Assert.That(ModsPackReleasePackage.StripPackageText(_tree), Is.Null);
            string vpk = Pack();

            Assert.That(await ModsPackReleasePackage.SealAsync(vpk, slim: true, Classify), Is.Null);
            Assert.That(await Classify(vpk, default), Is.EqualTo((VpkOrigin.Official, true)),
                "a slim release is always rebuilt on install");

            string target = Path.Combine(_root, "dota");
            string installed = Path.Combine(target, "game", "_ArdysaMods", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.Copy(vpk, installed);
            var read = await new SkinManifestReader(cachePath: Path.Combine(_root, "skinstate.amtx")).ReadInstalledAsync(target);

            Assert.That(read, Is.Not.Null, "the release's manifest must survive pack + signature section");
            Assert.That(read!.Entries.Select(e => (e.HeroId, e.SetName)), Is.EquivalentTo(OneSet));
        }

        [Test]
        public async Task Seal_SlimButItemDataLeftIn_Refused()
        {
            await BuildReleaseTreeAsync();
            string vpk = Pack();

            Assert.That(await ModsPackReleasePackage.SealAsync(vpk, slim: true, Classify), Does.Contain("as-is"));
        }

        [Test]
        public async Task Seal_SelfContainedWithoutManifest_Refused()
        {
            Put(Marker, ItemsGame);
            string vpk = Pack();

            Assert.That(await ModsPackReleasePackage.SealAsync(vpk, slim: false, Classify), Does.Contain("manifest"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Seal_PackWithoutMarker_Refused(bool slim)
        {
            Put(ItemsGame);
            string vpk = Pack();

            Assert.That(await ModsPackReleasePackage.SealAsync(vpk, slim, Classify), Does.Contain("Unofficial"));
        }

        [Test]
        public async Task Seal_UnreadableVerdict_Refused()
        {
            Put(Marker, ItemsGame);
            string vpk = Pack();

            var error = await ModsPackReleasePackage.SealAsync(vpk, slim: true,
                (_, _) => Task.FromResult((VpkOrigin.Unreadable, false)));
            Assert.That(error, Does.Contain("Unreadable"));
        }

        [Test]
        public async Task Seal_NotAPackage_RefusedBeforeClassifying()
        {
            string junk = Path.Combine(_root, "junk.vpk");
            File.WriteAllText(junk, "not a vpk");
            bool classified = false;

            var error = await ModsPackReleasePackage.SealAsync(junk, slim: true, (_, _) =>
            {
                classified = true;
                return Task.FromResult((VpkOrigin.Official, true));
            });

            Assert.That(error, Does.Contain("signature section"));
            Assert.That(classified, Is.False);
        }

        #endregion

        #region ExportAsync — the release folder

        private string FakeSlimVpk()
        {
            string vpk = Path.Combine(_root, "slim.vpk");
            File.WriteAllBytes(vpk, Enumerable.Range(0, 4096).Select(i => (byte)(i * 7)).ToArray());
            return vpk;
        }

        [Test]
        public async Task Export_WritesTheFourReleaseFiles_AndLeavesNoPartialFolder()
        {
            string vpk = FakeSlimVpk();
            string dir = Path.Combine(_root, "out", "20261010-120000");

            Assert.That(await ModsPackReleasePackage.ExportAsync(dir, vpk, "// index\n\"1\"\n{\n}\n"), Is.Null);

            Assert.That(File.ReadAllBytes(Path.Combine(dir, "pak01_dir.vpk")), Is.EqualTo(File.ReadAllBytes(vpk)));
            Assert.That(File.ReadAllText(Path.Combine(dir, "modspack_index.txt")), Is.EqualTo("// index\n\"1\"\n{\n}\n"));

            string zip = Path.Combine(dir, "ModsPack.zip");
            using (var archive = ZipFile.OpenRead(zip))
            {
                Assert.That(archive.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "pak01_dir.vpk" }),
                    "the installer unzips straight into _ArdysaMods");
                using var entry = archive.Entries[0].Open();
                using var copy = new MemoryStream();
                entry.CopyTo(copy);
                Assert.That(copy.ToArray(), Is.EqualTo(File.ReadAllBytes(vpk)));
            }

            string hash = File.ReadAllText(Path.Combine(dir, "ModsPack.hash"));
            Assert.That(hash, Does.Match("^[0-9a-f]{64}$"), "same form as remote/ModsPack.hash");
            Assert.That(hash, Is.EqualTo(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))).ToLowerInvariant()));
            Assert.That(Directory.Exists(dir + ".partial"), Is.False);
        }

        [Test]
        public async Task Export_ExistingFolder_RefusedAndLeftAlone()
        {
            string dir = Path.Combine(_root, "out", "taken");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "keep.txt"), "mine");

            Assert.That(await ModsPackReleasePackage.ExportAsync(dir, FakeSlimVpk(), "x"), Does.Contain("already exists"));
            Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EqualTo(new[] { "keep.txt" }));
            Assert.That(Directory.Exists(dir + ".partial"), Is.False);
        }

        [Test]
        public async Task Export_MissingPackage_LeavesNothingBehind()
        {
            string dir = Path.Combine(_root, "out", "never");

            Assert.That(await ModsPackReleasePackage.ExportAsync(dir, Path.Combine(_root, "gone.vpk"), "x"), Is.Not.Null);
            Assert.That(Directory.Exists(dir) || Directory.Exists(dir + ".partial"), Is.False);
        }

        [Test]
        public async Task Export_EmptyIndex_Refused()
        {
            string dir = Path.Combine(_root, "out", "noindex");

            Assert.That(await ModsPackReleasePackage.ExportAsync(dir, FakeSlimVpk(), " "), Does.Contain("empty"));
            Assert.That(Directory.Exists(dir), Is.False);
        }

        #endregion
    }
}
