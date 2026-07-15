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
using System.Security.Cryptography;
using System.Text;

namespace ArdysaModsTools.Core.Services.Security
{
    internal static class EmbeddedAssetKey
    {

        private static readonly byte[] FragmentA = new byte[32];
        private static readonly byte[] FragmentB = new byte[32];

        private static byte[]? _cachedSecret;
        private static readonly object Gate = new();

        private static byte[] GetMasterSecret()
        {
            if (_cachedSecret != null) return _cachedSecret;
            lock (Gate)
            {
                if (_cachedSecret != null) return _cachedSecret;
                if (FragmentA.Length != FragmentB.Length)
                    throw new CryptographicException("Asset key fragments are misconfigured.");

                var secret = new byte[FragmentA.Length];
                for (int i = 0; i < secret.Length; i++)
                    secret[i] = (byte)(FragmentA[i] ^ FragmentB[i]);
                _cachedSecret = secret;
                return secret;
            }
        }

        public static byte[] DeriveKey(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));

            return HMACSHA256.HashData(GetMasterSecret(), Encoding.UTF8.GetBytes(assetPath));
        }
    }
}
