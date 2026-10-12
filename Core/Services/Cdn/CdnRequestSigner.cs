/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Security.Cryptography;
using System.Text;

namespace ArdysaModsTools.Core.Services.Cdn
{
    public static class CdnRequestSigner
    {
        public const string HeaderName = "X-AMT-Auth";

        public const string ServerTimeHeaderName = "X-AMT-Now";

        public const string Version = "1";

        public const int LifetimeSeconds = 300;

        private const string Domain = "AMT1";

        public static string BuildHeaderValue(byte[] key, string assetPath, long expiryUnixSeconds)
            => $"{Version}.{expiryUnixSeconds}.{Sign(key, assetPath, expiryUnixSeconds)}";

        public static string Sign(byte[] key, string assetPath, long expiryUnixSeconds)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));

            var material = Encoding.UTF8.GetBytes(
                $"{Domain}\n{assetPath}\n{expiryUnixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            return Convert.ToHexString(HMACSHA256.HashData(key, material)).ToLowerInvariant();
        }
    }
}
