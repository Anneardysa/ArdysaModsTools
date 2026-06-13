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
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services.Cdn
{
    /// <summary>
    /// Stateless SHA-256 content verification for downloaded asset files. Mirrors the streamed
    /// hashing used by the anti-tamper check (<c>IntegrityCheck.ComputeExecutableHash</c>) and
    /// produces uppercase hex to match the published manifest.
    /// </summary>
    // [AMT:OPUS] Download integrity gate (ADR-0010). A weakening here lets a corrupt or tampered
    // archive be extracted/installed — verify hashing correctness and the size precheck before
    // modifying. Hash comparison is ordinal-ignore-case on hex; do not short-circuit it.
    public static class AssetHashVerifier
    {
        /// <summary>Buffer size for streamed hashing (80 KB), matching the download buffer.</summary>
        private const int BufferSize = 81920;

        /// <summary>
        /// Compute the SHA-256 of a file as uppercase hex, streamed so large archives are not
        /// buffered in memory.
        /// </summary>
        public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            await using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

            using var sha = SHA256.Create();
            byte[] hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Verify a file against an expected manifest entry. Checks the declared size first (cheap
        /// reject) then the SHA-256. Returns false on any mismatch.
        /// </summary>
        public static async Task<bool> VerifyFileAsync(string filePath, AssetHashEntry expected, CancellationToken ct = default)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (string.IsNullOrEmpty(expected.Sha256))
                return false; // No expected hash to verify against — treat as a failed verification.

            if (!File.Exists(filePath))
                return false;

            if (expected.Size > 0)
            {
                long actualSize = new FileInfo(filePath).Length;
                if (actualSize != expected.Size)
                    return false;
            }

            string actualHash = await ComputeSha256Async(filePath, ct).ConfigureAwait(false);
            return string.Equals(actualHash, expected.Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
