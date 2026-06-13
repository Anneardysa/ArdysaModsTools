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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for AssetHashVerifier — streamed SHA-256 and file verification (size + hash).
    /// </summary>
    [TestFixture]
    public class AssetHashVerifierTests
    {
        private string _file = null!;

        [SetUp]
        public void Setup()
        {
            _file = Path.Combine(Path.GetTempPath(), "amt_hashtest_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllText(_file, "hello world", Encoding.UTF8);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (File.Exists(_file)) File.Delete(_file); } catch { }
        }

        private static string ExpectedHashOf(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        [Test]
        public async Task ComputeSha256Async_MatchesReferenceHash_UppercaseHex()
        {
            string expected = ExpectedHashOf(_file);

            string actual = await AssetHashVerifier.ComputeSha256Async(_file);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual, Is.EqualTo(actual.ToUpperInvariant()), "hash should be uppercase hex");
        }

        [Test]
        public async Task VerifyFileAsync_CorrectHashAndSize_ReturnsTrue()
        {
            var entry = new AssetHashEntry { Sha256 = ExpectedHashOf(_file), Size = new FileInfo(_file).Length };

            Assert.That(await AssetHashVerifier.VerifyFileAsync(_file, entry), Is.True);
        }

        [Test]
        public async Task VerifyFileAsync_LowercaseExpected_StillMatches()
        {
            var entry = new AssetHashEntry { Sha256 = ExpectedHashOf(_file).ToLowerInvariant(), Size = 0 };

            Assert.That(await AssetHashVerifier.VerifyFileAsync(_file, entry), Is.True);
        }

        [Test]
        public async Task VerifyFileAsync_WrongHash_ReturnsFalse()
        {
            var entry = new AssetHashEntry { Sha256 = new string('A', 64), Size = 0 };

            Assert.That(await AssetHashVerifier.VerifyFileAsync(_file, entry), Is.False);
        }

        [Test]
        public async Task VerifyFileAsync_WrongSize_ReturnsFalseWithoutHashing()
        {
            var entry = new AssetHashEntry { Sha256 = ExpectedHashOf(_file), Size = 999999 };

            Assert.That(await AssetHashVerifier.VerifyFileAsync(_file, entry), Is.False);
        }

        [Test]
        public async Task VerifyFileAsync_MissingFile_ReturnsFalse()
        {
            var entry = new AssetHashEntry { Sha256 = new string('A', 64), Size = 0 };

            Assert.That(await AssetHashVerifier.VerifyFileAsync(_file + ".nope", entry), Is.False);
        }
    }
}
