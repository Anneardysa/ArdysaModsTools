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
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class AssetCipherTests
    {
        private const string AssetPath = "Assets/models/Drow_Ranger/dread.zip";

        private static byte[] SampleZip()
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("index.txt");
                using var w = new StreamWriter(entry.Open());
                w.Write("\"items_game\"\n{\n\t\"items\"\n\t{\n\t}\n}\n");
            }
            return ms.ToArray();
        }

        [Test]
        public void EncryptThenDecrypt_RoundTrips()
        {
            byte[] plaintext = SampleZip();

            byte[] container = AssetCipher.Encrypt(plaintext, AssetPath);
            byte[] recovered = AssetCipher.Decrypt(container, AssetPath);

            Assert.That(recovered, Is.EqualTo(plaintext));
        }

        [Test]
        public void Encrypt_ProducesRecognizableContainer()
        {
            byte[] container = AssetCipher.Encrypt(SampleZip(), AssetPath);

            Assert.That(container[0], Is.EqualTo((byte)'A'));
            Assert.That(container[1], Is.EqualTo((byte)'M'));
            Assert.That(container[2], Is.EqualTo((byte)'E'));
            Assert.That(container[3], Is.EqualTo((byte)'1'));
            Assert.That(container[4], Is.EqualTo((byte)1));
        }

        [Test]
        public void Decrypt_WithWrongAssetPath_Throws()
        {
            byte[] container = AssetCipher.Encrypt(SampleZip(), AssetPath);

            Assert.Catch<CryptographicException>(
                () => AssetCipher.Decrypt(container, "Assets/models/Drow_Ranger/other.zip"));
        }

        [Test]
        public void Decrypt_WithTamperedCiphertext_Throws()
        {
            byte[] container = AssetCipher.Encrypt(SampleZip(), AssetPath);
            container[^1] ^= 0xFF;

            Assert.Catch<CryptographicException>(() => AssetCipher.Decrypt(container, AssetPath));
        }

        [Test]
        public void Decrypt_NonContainerBytes_Throws()
        {
            byte[] notAContainer = Encoding.UTF8.GetBytes("PK\x03\x04 this is a plain zip, not a container");

            Assert.Throws<CryptographicException>(() => AssetCipher.Decrypt(notAContainer, AssetPath));
        }

        [Test]
        public void IsEncrypted_TrueForContainer_FalseForPlainZip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "amt_cipher_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string enc = Path.Combine(dir, "enc.zip");
                string plain = Path.Combine(dir, "plain.zip");
                File.WriteAllBytes(enc, AssetCipher.Encrypt(SampleZip(), AssetPath));
                File.WriteAllBytes(plain, SampleZip());

                Assert.That(AssetCipher.IsEncrypted(enc), Is.True);
                Assert.That(AssetCipher.IsEncrypted(plain), Is.False);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public async Task DecryptToTempAsync_ProducesExtractableZip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "amt_cipher_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string enc = Path.Combine(dir, "set.zip");
                File.WriteAllBytes(enc, AssetCipher.Encrypt(SampleZip(), AssetPath));

                string tempZip = await AssetCipher.DecryptToTempAsync(enc, AssetPath);
                try
                {
                    using var archive = ZipFile.OpenRead(tempZip);
                    Assert.That(archive.GetEntry("index.txt"), Is.Not.Null);
                }
                finally
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
