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
        public void Decrypt_AcrossEpochs_FailsAuthentication()
        {
            byte[] container = EncryptUnderEpoch(SampleZip(), AssetPath, epoch: 2);

            Assert.Throws<AuthenticationTagMismatchException>(
                () => AssetCipher.Decrypt(container, AssetPath));
        }

        [Test]
        public void Decrypt_UnknownContainerVersion_ThrowsAssetVersionException()
        {
            byte[] container = AssetCipher.Encrypt(SampleZip(), AssetPath);
            container[4] = 99;

            var ex = Assert.Throws<AssetVersionException>(
                () => AssetCipher.Decrypt(container, AssetPath));
            Assert.That(ex!.ContainerVersion, Is.EqualTo(99));
        }


        [TestCase(1)]
        [TestCase(2)]
        public void DecryptWithEpochs_ReadsAnySupportedEpoch(int assetEpoch)
        {
            byte[] plain = SampleZip();
            var (nonce, tag, ct) = PartsUnderEpoch(plain, AssetPath, assetEpoch);

            byte[] result = AssetCipher.DecryptWithEpochs(nonce, tag, ct, AssetPath, new[] { 2, 1 });

            Assert.That(result, Is.EqualTo(plain));
        }

        [Test]
        public void DecryptWithEpochs_RejectsEpochOutsideTheWindow()
        {
            var (nonce, tag, ct) = PartsUnderEpoch(SampleZip(), AssetPath, epoch: 3);

            Assert.Throws<AuthenticationTagMismatchException>(
                () => AssetCipher.DecryptWithEpochs(nonce, tag, ct, AssetPath, new[] { 2, 1 }));
        }

        [Test]
        public void DecryptWithEpochs_SingleEpochBuild_CannotReadRotatedTree()
        {
            var (nonce, tag, ct) = PartsUnderEpoch(SampleZip(), AssetPath, epoch: 2);

            Assert.Throws<AuthenticationTagMismatchException>(
                () => AssetCipher.DecryptWithEpochs(nonce, tag, ct, AssetPath, new[] { 1 }));
        }

        [Test]
        public void DecryptWithEpochs_WrongAssetPath_Fails()
        {
            var (nonce, tag, ct) = PartsUnderEpoch(SampleZip(), AssetPath, epoch: 1);

            Assert.Throws<AuthenticationTagMismatchException>(
                () => AssetCipher.DecryptWithEpochs(nonce, tag, ct, "Assets/models/Axe/other.zip", new[] { 2, 1 }));
        }

        [Test]
        public void DecryptWithEpochs_EmptyWindow_Throws()
        {
            var (nonce, tag, ct) = PartsUnderEpoch(SampleZip(), AssetPath, epoch: 1);

            Assert.Throws<CryptographicException>(
                () => AssetCipher.DecryptWithEpochs(nonce, tag, ct, AssetPath, Array.Empty<int>()));
        }

        [Test]
        public void ShippedEpochWindow_RoundTripsItsOwnOutput()
        {
            byte[] plain = SampleZip();
            byte[] container = AssetCipher.Encrypt(plain, AssetPath);

            Assert.That(AssetCipher.Decrypt(container, AssetPath), Is.EqualTo(plain));
        }


        [TestCase(0)]
        [TestCase(1)]
        public void DecryptWithKeys_ReadsEitherSecretInTheWindow(int keyedWith)
        {
            byte[] plain = SampleZip();
            byte[] newSecret = RandomNumberGenerator.GetBytes(32);
            byte[] oldSecret = RandomNumberGenerator.GetBytes(32);
            byte[][] window = { KeyFrom(newSecret), KeyFrom(oldSecret) };

            var (nonce, tag, ct) = PartsUnderKey(plain, window[keyedWith]);

            Assert.That(AssetCipher.DecryptWithKeys(nonce, tag, ct, window), Is.EqualTo(plain));
        }

        [Test]
        public void DecryptWithKeys_RejectsSecretOutsideTheWindow()
        {
            var (nonce, tag, ct) = PartsUnderKey(SampleZip(), KeyFrom(RandomNumberGenerator.GetBytes(32)));
            byte[][] window = { KeyFrom(RandomNumberGenerator.GetBytes(32)) };

            Assert.Throws<AuthenticationTagMismatchException>(
                () => AssetCipher.DecryptWithKeys(nonce, tag, ct, window));
        }

        [Test]
        public void DecryptWithKeys_EmptyWindow_Throws()
        {
            var (nonce, tag, ct) = PartsUnderKey(SampleZip(), KeyFrom(RandomNumberGenerator.GetBytes(32)));

            Assert.Throws<CryptographicException>(
                () => AssetCipher.DecryptWithKeys(nonce, tag, ct, Array.Empty<byte[]>()));
        }

        [Test]
        public void CandidateKeys_MatchesTheConfiguredWindowExactly()
        {
            int expected = EmbeddedAssetKey.SupportedSecrets.Length * EmbeddedAssetKey.SupportedEpochs.Length;

            Assert.That(EmbeddedAssetKey.CandidateKeys(AssetPath), Has.Length.EqualTo(expected));
        }

        [Test]
        public void SupportedSecrets_NeverCarriesMoreThanOneRotation()
        {
            Assert.That(EmbeddedAssetKey.SupportedSecrets, Has.Length.InRange(1, 2));
            Assert.That(EmbeddedAssetKey.SupportedSecrets[0],
                Is.Not.EqualTo(EmbeddedAssetKey.SupportedSecrets[^1]).Or.Length.EqualTo(32),
                "A duplicated secret would waste a decrypt attempt on every asset.");
        }

        [Test]
        public void Decrypt_ReadsAssetsKeyedWithEveryShippedSecret()
        {
            byte[] plain = SampleZip();

            for (int i = 0; i < EmbeddedAssetKey.SupportedSecrets.Length; i++)
            {
                byte[] key = EmbeddedAssetKey.DeriveKey(
                    AssetPath, EmbeddedAssetKey.AssetEpoch, EmbeddedAssetKey.SupportedSecrets[i]);
                var (nonce, tag, ct) = PartsUnderKey(plain, key);

                Assert.That(AssetCipher.DecryptWithKeys(nonce, tag, ct, EmbeddedAssetKey.CandidateKeys(AssetPath)),
                    Is.EqualTo(plain), $"Asset keyed with shipped secret #{i} did not decrypt.");
            }
        }

        [Test]
        public void CandidateKeys_DifferPerAsset()
        {
            Assert.That(EmbeddedAssetKey.CandidateKeys(AssetPath)[0],
                Is.Not.EqualTo(EmbeddedAssetKey.CandidateKeys("Assets/models/Axe/blade.zip")[0]));
        }

        private static byte[] KeyFrom(byte[] secret) =>
            EmbeddedAssetKey.DeriveKey(AssetPath, 1, secret);

        private static (byte[] nonce, byte[] tag, byte[] ciphertext) PartsUnderKey(
            byte[] plaintext, byte[] key)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var gcm = new AesGcm(key, 16))
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);
            return (nonce, tag, ciphertext);
        }

        private static (byte[] nonce, byte[] tag, byte[] ciphertext) PartsUnderEpoch(
            byte[] plaintext, string assetPath, int epoch)
        {
            byte[] key = EmbeddedAssetKey.DeriveKey(assetPath, epoch);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var gcm = new AesGcm(key, 16))
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);
            return (nonce, tag, ciphertext);
        }

        private static byte[] EncryptUnderEpoch(byte[] plaintext, string assetPath, int epoch)
        {
            byte[] key = EmbeddedAssetKey.DeriveKey(assetPath, epoch);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var gcm = new AesGcm(key, 16))
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);

            var container = new byte[33 + ciphertext.Length];
            Encoding.ASCII.GetBytes("AME1").CopyTo(container, 0);
            container[4] = 1;
            nonce.CopyTo(container, 5);
            tag.CopyTo(container, 17);
            ciphertext.CopyTo(container, 33);
            return container;
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
