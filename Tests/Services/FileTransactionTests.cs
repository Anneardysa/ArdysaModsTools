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

        [Test]
        public async Task WriteContentOperation_Rollback_RestoresFile()
        {
            // Arrange
            string path = Path.Combine(_testDir, "write_content.bin");
            byte[] originalContent = new byte[] { 1, 2, 3 };
            byte[] newContent = new byte[] { 4, 5, 6, 7 };
            
            File.WriteAllBytes(path, originalContent);

            var op = new WriteContentOperation(path, newContent);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(newContent));

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task WriteTextOperation_Rollback_RestoresFile()
        {
            // Arrange
            string path = Path.Combine(_testDir, "write_text.txt");
            string originalContent = "original text";
            string newContent = "new text content";
            
            File.WriteAllText(path, originalContent);

            var op = new WriteTextOperation(path, newContent);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllText(path), Is.EqualTo(newContent));

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.ReadAllText(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task WriteContentOperation_NewFile_Rollback_DeletesFile()
        {
            // Arrange
            string path = Path.Combine(_testDir, "new_content.bin");
            byte[] content = new byte[] { 1, 2, 3, 4 };

            var op = new WriteContentOperation(path, content);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.Exists(path), Is.True);

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public async Task ReplaceFileOperation_Rollback_RestoresFile()
        {
            // Arrange
            string path = Path.Combine(_testDir, "replace_file.bin");
            byte[] originalContent = new byte[] { 10, 20, 30 };
            byte[] newContent = new byte[] { 40, 50, 60, 70 };
            
            File.WriteAllBytes(path, originalContent);

            var op = new ReplaceFileOperation(path, newContent);

            // Act
            await op.ExecuteAsync(CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(newContent));
            Assert.That(File.Exists(path + ".tmp"), Is.False, "Temp file should be cleaned up");

            await op.RollbackAsync(CancellationToken.None);

            // Assert
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalContent));
        }

        [Test]
        public async Task Transaction_ProgressChanged_ReportsCorrectly()
        {
            // Arrange
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

                // Act
                await transaction.ExecuteAsync();
                transaction.Commit();
            }

            // Assert
            Assert.That(progressReports.Count, Is.EqualTo(2));
            Assert.That(progressReports[0], Is.EqualTo((1, 2)));
            Assert.That(progressReports[1], Is.EqualTo((2, 2)));
        }
    }
}

