/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.FileTransactions;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class FileTransactionTests
    {
        private string _testDir = null!;

        [SetUp]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ArdysaTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Test]
        public async Task Transaction_FullSuccess_CommitsAll()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(source, "hello");

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new CopyOperation(source, dest, true));
                
                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            Assert.That(File.Exists(dest), Is.True);
            Assert.That(File.ReadAllText(dest), Is.EqualTo("hello"));
            Assert.That(File.Exists(dest + ".transaction_bak"), Is.False);
        }

        [Test]
        public async Task Transaction_Failure_RollsBackAll()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            string invalidSource = Path.Combine(_testDir, "nonexistent.txt");
            string dest2 = Path.Combine(_testDir, "dest2.txt");
            
            File.WriteAllText(source, "hello");

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new CopyOperation(source, dest, true));
                transaction.AddOperation(new CopyOperation(invalidSource, dest2, true));

                Assert.ThrowsAsync<FileNotFoundException>(async () => await transaction.ExecuteAsync());
                
            }

            Assert.That(File.Exists(dest), Is.False, "First operation should have been rolled back");
            Assert.That(File.Exists(dest2), Is.False, "Second operation should not have completed");
        }

        [Test]
        public async Task MoveOperation_Rollback_RestoresOriginals()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(source, "source_content");
            File.WriteAllText(dest, "original_dest_content");

            var op = new MoveOperation(source, dest);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllText(dest), Is.EqualTo("source_content"));
            Assert.That(File.Exists(source), Is.False);

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(File.Exists(source), Is.True);
            Assert.That(File.ReadAllText(source), Is.EqualTo("source_content"));
            Assert.That(File.ReadAllText(dest), Is.EqualTo("original_dest_content"));
        }

        [Test]
        public async Task Transaction_SecondPatchWriteFails_RestoresBothGameFiles_EvenWhenRolledBackTwice()
        {
            string gameInfo = Path.Combine(_testDir, "gameinfo_branchspecific.gi");
            string signatures = Path.Combine(_testDir, "dota.signatures");
            string stagedGameInfo = gameInfo + ".tmp";
            string missingSignatures = signatures + ".tmp";

            File.WriteAllText(gameInfo, "ORIGINAL_GAMEINFO");
            File.WriteAllText(signatures, "ORIGINAL_SIGNATURES");
            File.WriteAllText(stagedGameInfo, "PATCHED_GAMEINFO");

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new MoveOperation(stagedGameInfo, gameInfo));
                transaction.AddOperation(new MoveOperation(missingSignatures, signatures));

                Assert.ThrowsAsync<FileNotFoundException>(async () => await transaction.ExecuteAsync());

                await transaction.RollbackAsync(CancellationToken.None);
            }

            Assert.That(File.Exists(gameInfo), Is.True, "gameinfo must survive a failed patch");
            Assert.That(File.ReadAllText(gameInfo), Is.EqualTo("ORIGINAL_GAMEINFO"),
                "gameinfo must be rolled back to its original content, not left patched");
            Assert.That(File.Exists(signatures), Is.True, "signatures must survive a failed patch");
            Assert.That(File.ReadAllText(signatures), Is.EqualTo("ORIGINAL_SIGNATURES"));
            Assert.That(File.Exists(gameInfo + ".transaction_bak"), Is.False, "backup should not leak");
        }

        [Test]
        public async Task CreateDirectoryOperation_Rollback_RemovesDirectory()
        {
            string path = Path.Combine(_testDir, "new_dir");
            var op = new CreateDirectoryOperation(path);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(Directory.Exists(path), Is.True);

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(Directory.Exists(path), Is.False);
        }

        [Test]
        public async Task WriteContentOperation_Rollback_RestoresFile()
        {
            string path = Path.Combine(_testDir, "write_content.bin");
            byte[] originalContent = new byte[] { 1, 2, 3 };
            byte[] newContent = new byte[] { 4, 5, 6, 7 };
            
            File.WriteAllBytes(path, originalContent);

            var op = new WriteContentOperation(path, newContent);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(newContent));

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task WriteTextOperation_Rollback_RestoresFile()
        {
            string path = Path.Combine(_testDir, "write_text.txt");
            string originalContent = "original text";
            string newContent = "new text content";
            
            File.WriteAllText(path, originalContent);

            var op = new WriteTextOperation(path, newContent);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllText(path), Is.EqualTo(newContent));

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(File.ReadAllText(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task WriteContentOperation_NewFile_Rollback_DeletesFile()
        {
            string path = Path.Combine(_testDir, "new_content.bin");
            byte[] content = new byte[] { 1, 2, 3, 4 };

            var op = new WriteContentOperation(path, content);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.Exists(path), Is.True);

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public async Task ReplaceFileOperation_Rollback_RestoresFile()
        {
            string path = Path.Combine(_testDir, "replace_file.bin");
            byte[] originalContent = new byte[] { 10, 20, 30 };
            byte[] newContent = new byte[] { 40, 50, 60, 70 };
            
            File.WriteAllBytes(path, originalContent);

            var op = new ReplaceFileOperation(path, newContent);

            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(newContent));
            Assert.That(File.Exists(path + ".tmp"), Is.False, "Temp file should be cleaned up");

            await op.RollbackAsync(CancellationToken.None);

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task Transaction_ProgressChanged_ReportsCorrectly()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest1 = Path.Combine(_testDir, "dest1.txt");
            string dest2 = Path.Combine(_testDir, "dest2.txt");
            File.WriteAllText(source, "hello");

            var progressReports = new System.Collections.Generic.List<(int current, int total)>();

            using (var transaction = new FileTransaction())
            {
                transaction.ProgressChanged += (current, total) => progressReports.Add((current, total));
                transaction.AddOperation(new CopyOperation(source, dest1, true));
                transaction.AddOperation(new CopyOperation(source, dest2, true));

                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            Assert.That(progressReports.Count, Is.EqualTo(2));
            Assert.That(progressReports[0], Is.EqualTo((1, 2)));
            Assert.That(progressReports[1], Is.EqualTo((2, 2)));
        }

        [Test]
        public async Task CopyOperation_OverHiddenSystemDestination_Succeeds()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(dest, "old");
            File.SetAttributes(dest, FileAttributes.Hidden | FileAttributes.System);

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new CopyOperation(source, dest, true));
                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            Assert.That(File.ReadAllText(dest), Is.EqualTo("new"));
        }

        [Test]
        public async Task WriteContentOperation_OverHiddenSystemFile_Succeeds()
        {
            string path = Path.Combine(_testDir, "file.bin");
            File.WriteAllBytes(path, new byte[] { 1 });
            File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new WriteContentOperation(path, new byte[] { 2, 3 }));
                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 2, 3 }));
        }

        [Test]
        public async Task CopyOperation_RollbackAfterHiddenDestination_RestoresContentAndAttributes()
        {
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            string invalidSource = Path.Combine(_testDir, "nonexistent.txt");
            string dest2 = Path.Combine(_testDir, "dest2.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(dest, "old");
            File.SetAttributes(dest, FileAttributes.Hidden | FileAttributes.System);

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new CopyOperation(source, dest, true));
                transaction.AddOperation(new CopyOperation(invalidSource, dest2, true));

                Assert.ThrowsAsync<FileNotFoundException>(async () => await transaction.ExecuteAsync());
            }

            Assert.That(File.ReadAllText(dest), Is.EqualTo("old"), "content restored");
            var attrs = File.GetAttributes(dest);
            Assert.That(attrs.HasFlag(FileAttributes.Hidden), Is.True, "Hidden restored");
            Assert.That(attrs.HasFlag(FileAttributes.System), Is.True, "System restored");
        }
    }
}

