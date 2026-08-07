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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ItemsGameSyncServiceTests
    {
        private string _root = null!;
        private Mock<IGameItemsGameExtractor> _extractor = null!;

        private const string VanillaItems =
            "\"101\" { \"name\" \"a\" \"model_player\" \"models/vanilla_a.vmdl\" }\n" +
            "\"102\" { \"name\" \"b\" \"model_player\" \"models/vanilla_b.vmdl\" }\n";

        private const string ModdedItems =
            "\"101\" { \"name\" \"a\" \"model_player\" \"models/MODDED_a.vmdl\" }\n" +
            "\"102\" { \"name\" \"b\" \"model_player\" \"models/vanilla_b.vmdl\" }\n";

        private const string VanillaItemsAfterUpdate = VanillaItems +
            "\"103\" { \"name\" \"c\" \"model_player\" \"models/vanilla_c.vmdl\" }\n";

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AMT_SyncTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _extractor = new Mock<IGameItemsGameExtractor>(MockBehavior.Strict);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        #region Fixture helpers

        private string Write(string relativePath, string content)
        {
            var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        private void BuildPackages(string gameVpkBody = "game vpk v1")
        {
            Write(DotaPaths.GameVpk, gameVpkBody);
            Write(DotaPaths.ModsVpk, "mod vpk");
        }

        private void SetupExtractor(string? gameItemData, string? modItemData)
        {
            _extractor
                .Setup(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                                                    It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
                .Returns((string vpk, string dest, Action<string>? _, CancellationToken __) =>
                {
                    string? body = vpk.Contains("_ArdysaMods") ? modItemData : gameItemData;
                    if (body == null) return Task.FromResult(false);

                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    return Task.FromResult(true);
                });
        }

        private async Task WriteRecordAsync(string vanillaItemDataAtBuildTime, IEnumerable<string>? patchedIds = null)
        {
            var extracted = Write("build/items_game.txt", vanillaItemDataAtBuildTime);
            await ItemsGameBaselineStore.WritePendingAsync(_root, Path.Combine(_root, DotaPaths.GameVpk), extracted);
            await ItemsGameBaselineStore.CommitAsync(_root, patchedIds);
        }

        private ItemsGameSyncService NewService() => new(_extractor.Object);

        private void MoveGamePackage() => Write(DotaPaths.GameVpk, "game vpk v2 — a Dota 2 update landed");

        #endregion

        [Test]
        public void Current_BeforeAnyRefresh_IsUnknown()
        {
            Assert.That(NewService().Current.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public async Task Refresh_WithNoPath_IsUnknown()
        {
            var verdict = await NewService().RefreshAsync(null);
            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public async Task Refresh_WithNoModPackageInstalled_IsUnknown()
        {
            Write(DotaPaths.GameVpk, "game vpk");

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }


        [Test]
        public async Task Refresh_GamePackageUnchangedSinceBuild_IsInSync_WithoutUnpackingAnything()
        {
            BuildPackages();
            await WriteRecordAsync(VanillaItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.InSync));
            _extractor.Verify(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Refresh_RepeatedWithNothingMoved_EvaluatesOnce()
        {
            BuildPackages();
            SetupExtractor(VanillaItems, ModdedItems);
            await WriteRecordAsync(VanillaItemsAfterUpdate);

            var service = NewService();
            await service.RefreshAsync(_root);
            _extractor.Invocations.Clear();

            await service.RefreshAsync(_root);
            await service.RefreshAsync(_root);
            await service.RefreshAsync(_root);

            Assert.That(_extractor.Invocations, Is.Empty);
        }


        [Test]
        public async Task Refresh_PackageMovedButItemDataUnchanged_IsInSync_AndRestampsTheRecord()
        {
            BuildPackages();
            await WriteRecordAsync(VanillaItems);
            SetupExtractor(VanillaItems, ModdedItems);
            MoveGamePackage();

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.InSync));

            var record = await ItemsGameBaselineStore.ReadAsync(_root);
            Assert.That(record!.VanillaVpk,
                Is.EqualTo(VpkStamp.Read(Path.Combine(_root, DotaPaths.GameVpk))!.Value),
                "the moved stamp should have been recorded, or every later sweep re-unpacks");
        }

        [Test]
        public async Task Refresh_GameItemDataChangedSinceBuild_IsStale()
        {
            BuildPackages();
            await WriteRecordAsync(VanillaItems);
            SetupExtractor(VanillaItemsAfterUpdate, ModdedItems);
            MoveGamePackage();

            var verdict = await NewService().RefreshAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Stale));
                Assert.That(verdict.Diagnostic, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public async Task Refresh_RecordBelongsToADifferentPackage_FallsBackToDirectComparison()
        {
            BuildPackages();
            await WriteRecordAsync(VanillaItems);
            Write(DotaPaths.ModsVpk, "a completely different, hand-installed vpk");

            SetupExtractor(VanillaItems, ModdedItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown),
                "the record must not speak for a package it did not describe");
        }


        [Test]
        public async Task Refresh_NoRecord_GameHasItemsThePackageLacks_IsStale()
        {
            BuildPackages();
            SetupExtractor(VanillaItemsAfterUpdate, ModdedItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Stale));
                Assert.That(verdict.DetailKey, Is.EqualTo("verify.sync.failLegacy"));
            });
        }

        [Test]
        public async Task Refresh_NoRecord_SameItemsDifferentValues_IsUnknown_NotStale()
        {
            BuildPackages();
            SetupExtractor(VanillaItems, ModdedItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public async Task Refresh_NoRecord_IdenticalItemData_IsInSync()
        {
            BuildPackages();
            SetupExtractor(VanillaItems, VanillaItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.InSync));
        }

        [Test]
        public async Task Refresh_NoRecord_PackageCarriesNoItemData_IsUnknown()
        {
            BuildPackages();
            SetupExtractor(VanillaItems, null);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
                Assert.That(verdict.DetailKey, Is.EqualTo("verify.sync.noPackage"));
            });
        }


        [Test]
        public async Task Refresh_CannotReadTheGamePackage_IsUnknown_NeverStale()
        {
            BuildPackages();
            SetupExtractor(null, ModdedItems);

            var verdict = await NewService().RefreshAsync(_root);

            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public async Task Refresh_ExtractorThrows_IsUnknown_AndDoesNotPropagate()
        {
            BuildPackages();
            _extractor
                .Setup(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                                                    It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("the game is running and holds the file"));

            ItemsGameSyncVerdict verdict = null!;
            Assert.DoesNotThrowAsync(async () => verdict = await NewService().RefreshAsync(_root));
            Assert.That(verdict.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public void Refresh_Cancelled_Throws_AndLeavesThePreviousVerdictStanding()
        {
            BuildPackages();
            using var cts = new CancellationTokenSource();
            _extractor
                .Setup(e => e.ExtractItemsGameAsync(It.IsAny<string>(), It.IsAny<string>(),
                                                    It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
                .Returns(() => { cts.Cancel(); throw new OperationCanceledException(); });

            var service = NewService();

            Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RefreshAsync(_root, cts.Token));
            Assert.That(service.Current.State, Is.EqualTo(ItemsGameSyncState.Unknown));
        }

        [Test]
        public async Task Refresh_AfterAPackageWasMomentarilyUnavailable_ReevaluatesInsteadOfCachingTheFailure()
        {
            BuildPackages();
            SetupExtractor(VanillaItems, VanillaItems);
            var service = NewService();

            await service.RefreshAsync(_root);
            Assert.That(service.Current.State, Is.EqualTo(ItemsGameSyncState.InSync), "precondition");

            string modVpk = Path.Combine(_root, DotaPaths.ModsVpk);
            var saved = File.ReadAllBytes(modVpk);
            var savedStamp = VpkStamp.Read(modVpk)!.Value;
            File.Delete(modVpk);

            var whileMissing = await service.RefreshAsync(_root);
            Assert.That(whileMissing.State, Is.EqualTo(ItemsGameSyncState.Unknown));

            File.WriteAllBytes(modVpk, saved);
            File.SetLastWriteTimeUtc(modVpk, savedStamp.LastWriteUtc);
            Assert.That(VpkStamp.Read(modVpk), Is.EqualTo(savedStamp), "precondition: the stamp is unchanged");

            var recovered = await service.RefreshAsync(_root);

            Assert.That(recovered.State, Is.EqualTo(ItemsGameSyncState.InSync));
        }


        [Test]
        public async Task Refresh_ReachingTheSameVerdictAgain_DoesNotAnnounceItTwice()
        {
            BuildPackages();
            SetupExtractor(VanillaItemsAfterUpdate, ModdedItems);

            var service = NewService();
            int announcements = 0;
            service.Changed += _ => announcements++;

            await service.RefreshAsync(_root);
            MoveGamePackage();
            await service.RefreshAsync(_root);
            Write(DotaPaths.GameVpk, "game vpk v3");
            await service.RefreshAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(service.Current.State, Is.EqualTo(ItemsGameSyncState.Stale));
                Assert.That(announcements, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Refresh_WhenTheVerdictActuallyChanges_Announces()
        {
            BuildPackages();
            SetupExtractor(VanillaItemsAfterUpdate, ModdedItems);

            var service = NewService();
            var seen = new List<ItemsGameSyncState>();
            service.Changed += v => seen.Add(v.State);

            await service.RefreshAsync(_root);

            SetupExtractor(VanillaItems, VanillaItems);
            MoveGamePackage();
            await service.RefreshAsync(_root);

            Assert.That(seen, Is.EqualTo(new[] { ItemsGameSyncState.Stale, ItemsGameSyncState.InSync }));
        }
    }
}
