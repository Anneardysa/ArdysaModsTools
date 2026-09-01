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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ItemsGameMergeServiceTests
    {
        private const string Vanilla =
            "\"items_game\"\n{\n\t\"items\"\n\t{\n" +
            "\t\t\"101\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t\t\"model_player\"\t\"models/vanilla.vmdl\"\n\t\t}\n" +
            "\t}\n}\n";

        private static string Modded => Vanilla.Replace("models/vanilla.vmdl", "models/MODDED.vmdl");

        private static string VanillaWithExtraItem => Vanilla.Replace("\t}\n}\n",
            "\t\t\"103\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_lina\" \"1\" }\n\t\t\t\"model_player\"\t\"models/new.vmdl\"\n\t\t}\n\t}\n}\n");

        private string _root = null!;
        private Mock<IGameItemsGameExtractor> _itemsGame = null!;
        private Mock<IVpkExtractor> _vpk = null!;
        private Mock<IVpkRecompiler> _recompiler = null!;
        private Mock<IVpkReplacer> _replacer = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AMT_MergeSvc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Write(DotaPaths.GameVpk, "game vpk");
            Write(DotaPaths.ModsVpk, "mod vpk");

            _itemsGame = new Mock<IGameItemsGameExtractor>();
            _vpk = new Mock<IVpkExtractor>();
            _recompiler = new Mock<IVpkRecompiler>();
            _replacer = new Mock<IVpkReplacer>();

            SetupItemData(Vanilla, Modded);

            _vpk.Setup(v => v.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>(), It.IsAny<bool>()))
                .Returns((string _, string __, string dir, Action<string> ___, CancellationToken ____,
                          IProgress<SpeedMetrics> _____, bool ______) =>
                {
                    var p = Path.Combine(dir, "scripts", "items", "items_game.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.WriteAllText(p, Modded);
                    return Task.FromResult(true);
                });

            _recompiler.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>()))
                .Returns((string _, string __, string buildDir, string ___, Action<string> ____,
                          CancellationToken _____, IProgress<SpeedMetrics> ______) =>
                {
                    Directory.CreateDirectory(buildDir);
                    var p = Path.Combine(buildDir, "pak01_dir.vpk");
                    File.WriteAllText(p, "rebuilt");
                    return Task.FromResult<string?>(p);
                });

            _replacer.Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        private string Write(string relative, string content)
        {
            var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        private void SetupItemData(string? gameData, string? modData, string? protectedData = null)
        {
            _itemsGame.Setup(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Returns((string vpk, string dest, Action<string>? _, CancellationToken __, bool normalize) =>
                {
                    string? body = PackageFolder(vpk) switch
                    {
                        "_ArdysaMods" => modData,
                        "mod" => protectedData,
                        _ => gameData,
                    };
                    if (body == null) return Task.FromResult(false);
                    if (normalize) body = body.Replace("\r\n", "\n").Replace("\r", "\n");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    return Task.FromResult(true);
                });
        }

        private static string PackageFolder(string vpkPath) =>
            Path.GetFileName(Path.GetDirectoryName(vpkPath)!);

        private string WriteProtectedPackage()
        {
            string protVpk = ProtectedVpkStore.VpkPath(_root);
            Directory.CreateDirectory(Path.GetDirectoryName(protVpk)!);
            File.WriteAllBytes(protVpk, TestVpk.Minimal());
            return protVpk;
        }

        private void RecompileToRealPackage(byte[] bytes)
        {
            _recompiler.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>()))
                .Returns((string _, string __, string buildDir, string ___, Action<string> ____,
                          CancellationToken _____, IProgress<SpeedMetrics> ______) =>
                {
                    Directory.CreateDirectory(buildDir);
                    var p = Path.Combine(buildDir, "pak01_dir.vpk");
                    File.WriteAllBytes(p, bytes);
                    return Task.FromResult<string?>(p);
                });
        }

        [Test]
        public async Task Merge_WhenItemDataLivesInTheProtectedPackage_RepairsThatPackageAndLeavesTheMainOneAlone()
        {
            string protVpk = WriteProtectedPackage();
            string mainVpk = Path.Combine(_root, DotaPaths.ModsVpk.Replace('/', Path.DirectorySeparatorChar));
            byte[] mainBefore = File.ReadAllBytes(mainVpk);

            SetupItemData(VanillaWithExtraItem, null, Modded);

            string? extractedFrom = null;
            _vpk.Setup(v => v.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>(), It.IsAny<bool>()))
                .Returns((string _, string vpk, string dir, Action<string> __, CancellationToken ___,
                          IProgress<SpeedMetrics> ____, bool _____) =>
                {
                    extractedFrom = vpk;
                    var p = Path.Combine(dir, "scripts", "items", "items_game.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.WriteAllText(p, Modded);
                    return Task.FromResult(true);
                });

            byte[] repaired = TestVpk.Build(TestVpk.ItemsGame(Modded), TestVpk.Blob("models", "repaired", "vmdl_c", 16));
            RecompileToRealPackage(repaired);

            var result = await NewService().MergeAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Merged));
                Assert.That(VpkSignatureSection.IsApplied(protVpk), Is.True,
                    "the repaired package must carry the VpkSignatureSection guard");
                Assert.That(VpkPackageValidator.TryValidate(protVpk, out var valErr), Is.True, valErr);
                _replacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                    Times.Never, "repairing the protected package must never touch the main package");
                Assert.That(File.ReadAllBytes(mainVpk), Is.EqualTo(mainBefore),
                    "the copyable package must not gain a copy of the item data");
            });
        }

        [Test]
        public async Task Merge_WhenBothPackagesCarryItemData_RepairsTheOneMountOrderPrefers()
        {
            string protVpk = WriteProtectedPackage();
            byte[] protectedBefore = File.ReadAllBytes(protVpk);

            SetupItemData(VanillaWithExtraItem, Modded, Modded);
            RecompileToRealPackage(TestVpk.Minimal());

            var result = await NewService().MergeAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Merged));
                _replacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                    Times.Once, "_ArdysaMods wins over mod, so that is the copy to repair");
                Assert.That(File.ReadAllBytes(protVpk), Is.EqualTo(protectedBefore),
                    "the shadowed package is not the one causing the crash — leave it alone");
            });
        }

        private ItemsGameMergeService NewService() =>
            new(_itemsGame.Object, _vpk.Object, _recompiler.Object, _replacer.Object);

        private void VerifyNoRebuild() =>
            _vpk.Verify(v => v.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>(), It.IsAny<bool>()),
                Times.Never, "the package must not be unpacked when the merge changes nothing");

        [Test]
        public async Task Merge_NoRecordButPackageAlreadyConsistent_DoesNotRebuild()
        {
            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.AlreadyCurrent));
            VerifyNoRebuild();
        }

        [Test]
        public async Task Merge_NoRecordButAlreadyConsistent_WritesABuildRecord()
        {
            await NewService().MergeAsync(_root);

            var record = await ItemsGameBaselineStore.ReadAsync(_root);
            Assert.That(record, Is.Not.Null);
            Assert.That(record!.ModVpk, Is.EqualTo(VpkStamp.Read(Path.Combine(_root, DotaPaths.ModsVpk))!.Value));
        }

        [Test]
        public async Task Merge_WhenPackageItemDataIsCrlf_RebuildsEvenThoughContentMatches()
        {
            SetupItemData(Vanilla, Modded.Replace("\n", "\r\n"));

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Merged),
                "a CRLF package was reported as already current — the game would still reject it");
            _replacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Merge_WhenTheGameAddedItems_RebuildsAndReinstalls()
        {
            SetupItemData(VanillaWithExtraItem, Modded);

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Merged));
            _replacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Merge_WithNoPackageInstalled_ReportsNothingToMerge()
        {
            File.Delete(Path.Combine(_root, DotaPaths.ModsVpk.Replace('/', Path.DirectorySeparatorChar)));

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.NothingToMerge));
            VerifyNoRebuild();
        }

        [Test]
        public async Task Merge_WhenThePackageCarriesNoItemData_ReportsNothingToMerge()
        {
            SetupItemData(Vanilla, null);

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.NothingToMerge));
            VerifyNoRebuild();
        }

        [Test]
        public async Task Merge_WhenTheGamesItemDataCannotBeRead_FailsWithoutTouchingThePackage()
        {
            SetupItemData(null, Modded);

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Failed));
            Assert.That(result.FailureKey, Is.EqualTo("play.merge.readFailed"));
            VerifyNoRebuild();
        }

        [Test]
        public async Task Merge_WhenTheRepackFails_NeverInstallsAnything()
        {
            SetupItemData(VanillaWithExtraItem, Modded);
            _recompiler.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>()))
                .ReturnsAsync((string?)null);

            var result = await NewService().MergeAsync(_root);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Failed));
            _replacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Merge_WithNoPath_FailsCleanly()
        {
            var result = await NewService().MergeAsync(null);

            Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Failed));
        }

        [Test]
        public async Task Merge_PreservesMiscModsAndDifferingBlocks_DuringRebuild()
        {
            const string vanillaWithMisc =
                "\"items_game\"\n{\n\t\"items\"\n\t{\n" +
                "\t\t\"101\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t\t\"model_player\"\t\"models/vanilla_axe.vmdl\"\n\t\t}\n" +
                "\t\t\"555\"\n\t\t{\n\t\t\t\"prefab\"\t\"weather\"\n\t\t\t\"model_player\"\t\"particles/vanilla_weather.vpcf\"\n\t\t}\n" +
                "\t\t\"103\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_lina\" \"1\" }\n\t\t\t\"model_player\"\t\"models/vanilla_lina.vmdl\"\n\t\t}\n" +
                "\t}\n}\n";

            const string moddedWithMisc =
                "\"items_game\"\n{\n\t\"items\"\n\t{\n" +
                "\t\t\"101\"\n\t\t{\n\t\t\t\"prefab\"\t\"wearable\"\n\t\t\t\"used_by_heroes\"\t{ \"npc_dota_hero_axe\" \"1\" }\n\t\t\t\"model_player\"\t\"models/MODDED_axe.vmdl\"\n\t\t}\n" +
                "\t\t\"555\"\n\t\t{\n\t\t\t\"prefab\"\t\"weather\"\n\t\t\t\"model_player\"\t\"particles/MODDED_weather.vpcf\"\n\t\t}\n" +
                "\t}\n}\n";

            SetupItemData(vanillaWithMisc, moddedWithMisc);

            var miscLog = new MiscExtractionLog
            {
                Selections = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Weather", "Ash" }
                }
            };
            miscLog.Save(_root);

            _vpk.Setup(v => v.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<SpeedMetrics>>(), It.IsAny<bool>()))
                .Returns((string _, string __, string dir, Action<string> ___, CancellationToken ____,
                          IProgress<SpeedMetrics> _____, bool ______) =>
                {
                    var p = Path.Combine(dir, "scripts", "items", "items_game.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                    File.WriteAllText(p, moddedWithMisc);
                    return Task.FromResult(true);
                });

            var result = await NewService().MergeAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome, Is.EqualTo(ItemsGameMergeOutcome.Merged));
                Assert.That(result.Applied, Is.EqualTo(2), "both axe skin and weather mod must be kept");
            });

            var baseline = await ItemsGameBaselineStore.ReadAsync(_root);
            Assert.Multiple(() =>
            {
                Assert.That(baseline, Is.Not.Null);
                Assert.That(baseline!.PatchedIds, Does.Contain("101"));
                Assert.That(baseline.PatchedIds, Does.Contain("555"));
            });
        }
    }
}
