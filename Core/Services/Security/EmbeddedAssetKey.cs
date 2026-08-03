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
using System.Security.Cryptography;
using System.Text;

namespace ArdysaModsTools.Core.Services.Security
{
    internal static class EmbeddedAssetKey
    {
        internal static readonly int[] SupportedEpochs = { 1 };

        internal static int AssetEpoch => SupportedEpochs[0];

        private const int LegacyEpoch = 1;


        private static readonly byte[] FragmentA = new byte[32];
        private static readonly byte[] FragmentB = new byte[32];
        private static readonly byte[] PrevFragmentA = new byte[32];
        private static readonly byte[] PrevFragmentB = new byte[32];

        internal static byte[][] SupportedSecrets => _secrets.Value;

        private static readonly Lazy<byte[][]> _secrets = new(() =>
        {
            var list = new List<byte[]> { Xor(FragmentA, FragmentB) };

            var prev = Xor(PrevFragmentA, PrevFragmentB);
            if (Array.Exists(prev, b => b != 0))
                list.Add(prev);

            return list.ToArray();
        });

        private static byte[] Xor(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                throw new CryptographicException("Asset key fragments are misconfigured.");

            var result = new byte[a.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (byte)(a[i] ^ b[i]);
            return result;
        }

        private static byte[] GetMasterSecret() => SupportedSecrets[0];

        public static byte[] DeriveKey(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));

            return DeriveKey(assetPath, AssetEpoch);
        }

        internal static byte[] DeriveKey(string assetPath, int epoch)
            => DeriveKey(assetPath, epoch, GetMasterSecret());

        internal static byte[] DeriveKey(string assetPath, int epoch, byte[] secret)
            => HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(KeyMaterial(assetPath, epoch)));

        internal static byte[][] CandidateKeys(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));

            var secrets = SupportedSecrets;
            var keys = new byte[secrets.Length * SupportedEpochs.Length][];

            int i = 0;
            foreach (byte[] secret in secrets)
                foreach (int epoch in SupportedEpochs)
                    keys[i++] = DeriveKey(assetPath, epoch, secret);

            return keys;
        }

        internal static string KeyMaterial(string assetPath, int epoch)
        {
            if (assetPath.Contains(':'))
                throw new ArgumentException("Asset path must not contain ':'.", nameof(assetPath));

            return epoch == LegacyEpoch ? assetPath : $"e{epoch}:{assetPath}";
        }
    }
}
