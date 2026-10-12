using System;
using System.IO;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class VpkPackageValidatorTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AmtVpkValidator_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Test]
        public void ValidPackage_Passes()
        {
            var path = Write("ok.vpk", TestVpk.Minimal());

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.True, error);
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void TruncatedPackage_Fails()
        {
            var full = TestVpk.Build(
                TestVpk.ItemsGame("\"items_game\"\n{\n}\n"),
                TestVpk.Blob("materials/dev", "blob", "vtex_c", 4096));
            var path = Write("truncated.vpk", full[..(full.Length - 2048)]);

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.False);
            Assert.That(error, Does.Contain("past the end"));
        }

        [Test]
        public void RawDataChunkMistakenForPackage_Fails()
        {
            var path = Write("pak01_000.vpk", new byte[4096]);

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.False);
            Assert.That(error, Does.Contain("signature"));
        }

        [Test]
        public void EntryInMissingArchiveChunk_Fails()
        {
            var entry = TestVpk.Blob("materials/dev", "blob", "vtex_c", 4096);
            entry.ArchiveIndex = 0;
            var path = Write("pak01_dir.vpk", TestVpk.Build(TestVpk.ItemsGame("\"items_game\"\n{\n}\n"), entry));

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.False);
            Assert.That(error, Does.Contain("archive 000"));
        }

        [Test]
        public void CrlfItemData_Fails()
        {
            var path = Write("crlf.vpk", TestVpk.Build(TestVpk.ItemsGame("\"items_game\"\r\n{\r\n}\r\n")));

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.False);
            Assert.That(error, Does.Contain("CRLF"));
        }

        [Test]
        public void EmptyItemData_Fails()
        {
            var path = Write("empty.vpk", TestVpk.Build(TestVpk.ItemsGame(string.Empty)));

            Assert.That(VpkPackageValidator.TryValidate(path, out var error), Is.False);
            Assert.That(error, Does.Contain("empty"));
        }

        [Test]
        public void MissingFile_IsNotAVerdict()
        {
            Assert.That(VpkPackageValidator.TryValidate(Path.Combine(_dir, "nope.vpk"), out var error), Is.True);
            Assert.That(error, Is.Empty);
        }

        private string Write(string name, byte[] bytes)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
