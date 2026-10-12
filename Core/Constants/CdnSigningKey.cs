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

namespace ArdysaModsTools.Core.Constants
{
    internal static class CdnSigningKey
    {
        private static readonly byte[] FragmentA = new byte[32];
        private static readonly byte[] FragmentB = new byte[32];

        internal static byte[]? Key => _key.Value;

        private static readonly Lazy<byte[]?> _key = new(() =>
        {
            if (FragmentA.Length != FragmentB.Length)
                return null;

            var key = new byte[FragmentA.Length];
            for (int i = 0; i < key.Length; i++)
                key[i] = (byte)(FragmentA[i] ^ FragmentB[i]);

            return Array.Exists(key, b => b != 0) ? key : null;
        });
    }
}
