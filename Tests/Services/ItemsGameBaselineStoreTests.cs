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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ItemsGameBaselineStoreTests
    {
        private string _root = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AMT_BaselineTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        private string Write(string relativePath, string content)
        {
            var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        private (string gameVpk, string itemsGame) BuildTree()
        {
            var gameVpk = Write(DotaPaths.GameVpk, "pretend game vpk bytes");
            Write(DotaPaths.ModsVpk, "pretend mod vpk bytes");
            var itemsGame = Write("extracted/items_game.txt", "\"101\" { \"name\" \"a\" }");
            return (gameVpk, itemsGame);
        }

        [Test]
        public async Task WritePendingThenCommit_ProducesRecordBoundToBothPackages()
        {
            var (gameVpk, itemsGame) = BuildTree();

            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, new[] { "101", "102" });

            var record = await ItemsGameBaselineStore.ReadAsync(_root);

            Assert.That(record, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(record!.VanillaItemsGameSha, Is.Not.Empty);
                Assert.That(record.VanillaVpk, Is.EqualTo(VpkStamp.Read(gameVpk)!.Value));
                Assert.That(record.ModVpk, Is.EqualTo(VpkStamp.Read(Path.Combine(_root, DotaPaths.ModsVpk))!.Value));
                Assert.That(record.PatchedIds, Is.EquivalentTo(new[] { "101", "102" }));
            });
        }

        [Test]
        public async Task Commit_RemovesThePendingHalf()
        {
            var (gameVpk, itemsGame) = BuildTree();

            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, null);

            Assert.That(File.Exists(Path.Combine(_root, DotaPaths.ItemsGameBaselinePending)), Is.False);
        }

        [Test]
        public async Task Commit_WithoutPending_DropsAnyStaleRecord_AndDoesNotThrow()
        {
            var (gameVpk, itemsGame) = BuildTree();
            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, null);
            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Not.Null, "precondition");

            await ItemsGameBaselineStore.CommitAsync(_root, null);

            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null);
        }

        [Test]
        public async Task WritePendingWithoutCommit_LeavesNoReadableRecord()
        {
            var (gameVpk, itemsGame) = BuildTree();

            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);

            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null);
            Assert.That(File.Exists(Path.Combine(_root, DotaPaths.ItemsGameBaselinePending)), Is.True);
        }

        [Test]
        public async Task Read_ReturnsNull_ForMissingCorruptOrIncompleteRecords()
        {
            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null, "missing");
            Assert.That(await ItemsGameBaselineStore.ReadAsync(null), Is.Null, "no path");

            Write(DotaPaths.ItemsGameBaseline, "{ this is not json");
            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null, "corrupt");

            Write(DotaPaths.ItemsGameBaseline, "{\"VanillaItemsGameSha\":\"\"}");
            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null, "no hash");
        }

        [Test]
        public async Task Rebind_UpdatesPackageStamp_AndKeepsTheRecordedHash()
        {
            var (gameVpk, itemsGame) = BuildTree();
            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, new[] { "101" });
            var before = await ItemsGameBaselineStore.ReadAsync(_root);
            var stampBeforeRepack = VpkStamp.Read(Path.Combine(_root, DotaPaths.ModsVpk));

            Write(DotaPaths.ModsVpk, "a rebuilt mod vpk with different bytes entirely");
            await ItemsGameBaselineStore.RebindAsync(_root, stampBeforeRepack);

            var after = await ItemsGameBaselineStore.ReadAsync(_root);
            Assert.Multiple(() =>
            {
                Assert.That(after!.ModVpk, Is.EqualTo(VpkStamp.Read(Path.Combine(_root, DotaPaths.ModsVpk))!.Value));
                Assert.That(after.ModVpk, Is.Not.EqualTo(before!.ModVpk));
                Assert.That(after.VanillaItemsGameSha, Is.EqualTo(before.VanillaItemsGameSha));
            });
        }

        [Test]
        public async Task Rebind_WhenTheRecordDescribedADifferentPackage_DropsIt()
        {
            var (gameVpk, itemsGame) = BuildTree();
            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, new[] { "101" });
            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Not.Null, "precondition");

            Write(DotaPaths.ModsVpk, "a hand-installed third-party vpk");
            var foreignStamp = VpkStamp.Read(Path.Combine(_root, DotaPaths.ModsVpk));
            Write(DotaPaths.ModsVpk, "that third-party vpk, now repacked with misc mods added");

            await ItemsGameBaselineStore.RebindAsync(_root, foreignStamp);

            Assert.That(await ItemsGameBaselineStore.ReadAsync(_root), Is.Null);
        }

        [Test]
        public async Task Restamp_UpdatesGamePackageStamp_AndKeepsEverythingElse()
        {
            var (gameVpk, itemsGame) = BuildTree();
            await ItemsGameBaselineStore.WritePendingAsync(_root, gameVpk, itemsGame);
            await ItemsGameBaselineStore.CommitAsync(_root, new[] { "101" });
            var before = await ItemsGameBaselineStore.ReadAsync(_root);

            var moved = new VpkStamp(before!.VanillaVpk.Length + 4096, before.VanillaVpk.LastWriteUtc.AddHours(1));
            await ItemsGameBaselineStore.RestampVanillaAsync(_root, moved);

            var after = await ItemsGameBaselineStore.ReadAsync(_root);
            Assert.Multiple(() =>
            {
                Assert.That(after!.VanillaVpk, Is.EqualTo(moved));
                Assert.That(after.VanillaItemsGameSha, Is.EqualTo(before.VanillaItemsGameSha));
                Assert.That(after.ModVpk, Is.EqualTo(before.ModVpk));
            });
        }

        [Test]
        public void EveryEntryPoint_SwallowsBadInput()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await ItemsGameBaselineStore.WritePendingAsync(null, null, null);
                await ItemsGameBaselineStore.WritePendingAsync(_root, "no/such.vpk", "no/such.txt");
                await ItemsGameBaselineStore.CommitAsync(null, null);
                await ItemsGameBaselineStore.RebindAsync(null, null);
                await ItemsGameBaselineStore.RestampVanillaAsync(null, default);
            });
        }

        [Test]
        public void VpkStamp_Read_ReturnsNull_WhenFileMissing()
        {
            Assert.That(VpkStamp.Read(Path.Combine(_root, "nope.vpk")), Is.Null);
            Assert.That(VpkStamp.Read(null), Is.Null);
            Assert.That(VpkStamp.Read("   "), Is.Null);
        }
    }
}
