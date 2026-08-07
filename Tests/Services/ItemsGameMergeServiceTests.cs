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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using Moq;
using NUnit.Framework;

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

        private void SetupItemData(string? gameData, string? modData)
        {
            _itemsGame.Setup(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
                .Returns((string vpk, string dest, Action<string>? _, CancellationToken __) =>
                {
                    string? body = vpk.Contains("_ArdysaMods") ? modData : gameData;
                    if (body == null) return Task.FromResult(false);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    return Task.FromResult(true);
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
    }
}
