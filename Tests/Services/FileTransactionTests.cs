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
            // Arrange
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(source, "hello");

            using (var transaction = new FileTransaction())
            {
                transaction.AddOperation(new CopyOperation(source, dest, true));
                
                // Act
                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            // Assert
            Assert.That(File.Exists(dest), Is.True);
            Assert.That(File.ReadAllText(dest), Is.EqualTo("hello"));
            Assert.That(File.Exists(dest + ".transaction_bak"), Is.False);
        }

        [Test]
        public async Task Transaction_Failure_RollsBackAll()
        {
            // Arrange
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            string invalidSource = Path.Combine(_testDir, "nonexistent.txt");
            string dest2 = Path.Combine(_testDir, "dest2.txt");
            
            File.WriteAllText(source, "hello");

            using (var transaction = new FileTransaction())
            {
                // First op succeeds
                transaction.AddOperation(new CopyOperation(source, dest, true));
                // Second op fails
                transaction.AddOperation(new CopyOperation(invalidSource, dest2, true));

                // Act & Assert
                Assert.ThrowsAsync<FileNotFoundException>(async () => await transaction.ExecuteAsync());
                
                // Note: FileTransaction.ExecuteAsync automatically calls Rollback on failure in its catch block
            }

            // Assert
            Assert.That(File.Exists(dest), Is.False, "First operation should have been rolled back");
            Assert.That(File.Exists(dest2), Is.False, "Second operation should not have completed");
        }

        [Test]
        public async Task MoveOperation_Rollback_RestoresOriginals()
        {
            // Arrange
            string source = Path.Combine(_testDir, "source.txt");
            string dest = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(source, "source_content");
            File.WriteAllText(dest, "original_dest_content");

            var op = new MoveOperation(source, dest);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllText(dest), Is.EqualTo("source_content"));
            Assert.That(File.Exists(source), Is.False);

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.Exists(source), Is.True);
            Assert.That(File.ReadAllText(source), Is.EqualTo("source_content"));
            Assert.That(File.ReadAllText(dest), Is.EqualTo("original_dest_content"));
        }

        [Test]
        public async Task DeleteOperation_Rollback_RestoresFile()
        {
            // Arrange
            string path = Path.Combine(_testDir, "delete_me.txt");
            File.WriteAllText(path, "content");

            var op = new DeleteOperation(path);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.Exists(path), Is.False);

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo("content"));
        }

        [Test]
        public async Task CreateDirectoryOperation_Rollback_RemovesDirectory()
        {
            // Arrange
            string path = Path.Combine(_testDir, "new_dir");
            var op = new CreateDirectoryOperation(path);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(Directory.Exists(path), Is.True);

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(Directory.Exists(path), Is.False);
        }
    }
}
