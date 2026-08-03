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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;

namespace ArdysaModsTools.Core.Services.Security
{
    public sealed class AssetVersionException : CryptographicException
    {
        public AssetVersionException(byte containerVersion, byte supportedVersion)
            : base($"Unsupported asset container version {containerVersion} (this build reads {supportedVersion}).")
        {
            ContainerVersion = containerVersion;
            SupportedVersion = supportedVersion;
        }

        public byte ContainerVersion { get; }

        public byte SupportedVersion { get; }
    }

    public static class AssetCipher
    {
        private static readonly byte[] Magic = { 0x41, 0x4D, 0x45, 0x31 };
        private const byte Version = 1;
        private const int NonceLen = 12;
        private const int TagLen = 16;
        private const int HeaderLen = 4 + 1 + NonceLen + TagLen;

        public static bool IsEncrypted(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Span<byte> head = stackalloc byte[4];
                int read = fs.Read(head);
                return read == 4
                    && head[0] == Magic[0] && head[1] == Magic[1]
                    && head[2] == Magic[2] && head[3] == Magic[3];
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> DecryptToTempAsync(string encPath, string assetPath, CancellationToken ct = default)
        {
            byte[] container = await File.ReadAllBytesAsync(encPath, ct).ConfigureAwait(false);
            byte[] plaintext = Decrypt(container, assetPath);

            string tempDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "dec");
            Directory.CreateDirectory(tempDir);
            SafeTempPathHelper.HideDirectory(tempDir);
            string tempPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip");
            await File.WriteAllBytesAsync(tempPath, plaintext, ct).ConfigureAwait(false);
            return tempPath;
        }

        public static byte[] Decrypt(byte[] container, string assetPath)
        {
            if (container == null || container.Length < HeaderLen || !HasMagic(container))
                throw new CryptographicException("Not a valid AMT asset container.");
            if (container[4] != Version)
                throw new AssetVersionException(container[4], Version);

            var nonce = new byte[NonceLen];
            Buffer.BlockCopy(container, 5, nonce, 0, NonceLen);
            var tag = new byte[TagLen];
            Buffer.BlockCopy(container, 5 + NonceLen, tag, 0, TagLen);

            int ctOffset = HeaderLen;
            int ctLen = container.Length - ctOffset;
            var ciphertext = new byte[ctLen];
            Buffer.BlockCopy(container, ctOffset, ciphertext, 0, ctLen);

            return DecryptWithKeys(nonce, tag, ciphertext, EmbeddedAssetKey.CandidateKeys(assetPath));
        }

        internal static byte[] DecryptWithKeys(
            byte[] nonce, byte[] tag, byte[] ciphertext, byte[][] keys)
        {
            if (keys == null || keys.Length == 0)
                throw new CryptographicException("No asset keys are configured.");

            AuthenticationTagMismatchException? lastMismatch = null;

            foreach (int index in Order(keys.Length, _lastGoodKey))
            {
                var plaintext = new byte[ciphertext.Length];
                try
                {
                    using var gcm = new AesGcm(keys[index], TagLen);
                    gcm.Decrypt(nonce, ciphertext, tag, plaintext);
                    _lastGoodKey = index;
                    return plaintext;
                }
                catch (AuthenticationTagMismatchException ex)
                {
                    lastMismatch = ex;
                }
            }

            throw lastMismatch!;
        }

        internal static byte[] DecryptWithEpochs(
            byte[] nonce, byte[] tag, byte[] ciphertext, string assetPath, int[] epochs)
        {
            if (epochs == null || epochs.Length == 0)
                throw new CryptographicException("No asset key epochs are configured.");

            var keys = new byte[epochs.Length][];
            for (int i = 0; i < epochs.Length; i++)
                keys[i] = EmbeddedAssetKey.DeriveKey(assetPath, epochs[i]);

            return DecryptWithKeys(nonce, tag, ciphertext, keys);
        }

        private static IEnumerable<int> Order(int count, int hint)
        {
            if (hint >= 0 && hint < count)
            {
                yield return hint;
                for (int i = 0; i < count; i++)
                    if (i != hint) yield return i;
                yield break;
            }

            for (int i = 0; i < count; i++) yield return i;
        }

        private static volatile int _lastGoodKey;

        public static byte[] Encrypt(byte[] plaintext, string assetPath)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));

            var nonce = RandomNumberGenerator.GetBytes(NonceLen);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagLen];

            byte[] key = EmbeddedAssetKey.DeriveKey(assetPath);
            using (var gcm = new AesGcm(key, TagLen))
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);

            var container = new byte[HeaderLen + ciphertext.Length];
            Buffer.BlockCopy(Magic, 0, container, 0, Magic.Length);
            container[4] = Version;
            Buffer.BlockCopy(nonce, 0, container, 5, NonceLen);
            Buffer.BlockCopy(tag, 0, container, 5 + NonceLen, TagLen);
            Buffer.BlockCopy(ciphertext, 0, container, HeaderLen, ciphertext.Length);
            return container;
        }

        private static bool HasMagic(byte[] b) =>
            b[0] == Magic[0] && b[1] == Magic[1] && b[2] == Magic[2] && b[3] == Magic[3];
    }
}
