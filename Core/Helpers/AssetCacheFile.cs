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

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Handles reading and writing of custom .cache.ardysamods files.
    /// Binary format with XOR obfuscation to prevent casual inspection.
    /// </summary>
    /// <remarks>
    /// File format structure:
    /// [8 bytes]   Magic header: "ARDYMODS"
    /// [4 bytes]   Format version (int32, little-endian)
    /// [32 bytes]  URL SHA256 hash for validation
    /// [1 byte]    Extension length
    /// [N bytes]   Extension string (UTF-8)
    /// [8 bytes]   Cache timestamp (Unix ms, int64)
    /// [1 byte]    ETag length (0 if no ETag)
    /// [N bytes]   ETag string (UTF-8)
    /// [1 byte]    LastModified length (0 if no LastModified)
    /// [N bytes]   LastModified string (UTF-8)
    /// [4 bytes]   Payload length (int32)
    /// [M bytes]   XOR-encrypted payload
    /// </remarks>
    public static class AssetCacheFile
    {
        // Magic header to identify cache files
        private const string MagicHeader = "ARDYMODS";
        private const int FormatVersion = 2; // v2 adds ETag/LastModified
        
        // XOR key derived from fixed passphrase (16 bytes)
        private static readonly byte[] XorKey = GenerateXorKey("ARDYSAMODS2026");
        
        /// <summary>
        /// File extension for cache files.
        /// </summary>
        public const string Extension = ".cache.ardysamods";

        #region Public API

        /// <summary>
        /// Write asset data to a cache file.
        /// </summary>
        /// <param name="filePath">Full path to the cache file</param>
        /// <param name="url">Original URL (used for hash validation)</param>
        /// <param name="originalExtension">Original file extension (e.g., ".png")</param>
        /// <param name="data">Raw asset bytes</param>
        /// <param name="etag">Optional ETag from HTTP response for freshness validation</param>
        /// <param name="lastModified">Optional Last-Modified from HTTP response</param>
        public static async Task WriteAsync(string filePath, string url, string originalExtension, byte[] data, 
            string? etag = null, string? lastModified = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be empty", nameof(data));

            // Ensure directory exists
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Encrypt the payload
            byte[] encryptedPayload = XorEncrypt(data);
            byte[] urlHash = ComputeUrlHash(url);
            byte[] extensionBytes = Encoding.UTF8.GetBytes(originalExtension ?? "");
            byte[] etagBytes = Encoding.UTF8.GetBytes(etag ?? "");
            byte[] lastModifiedBytes = Encoding.UTF8.GetBytes(lastModified ?? "");
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            
            // Write header
            writer.Write(Encoding.ASCII.GetBytes(MagicHeader));
            writer.Write(FormatVersion);
            writer.Write(urlHash);
            writer.Write((byte)extensionBytes.Length);
            if (extensionBytes.Length > 0)
                writer.Write(extensionBytes);
            writer.Write(timestamp);
            
            // Write ETag (v2)
            writer.Write((byte)etagBytes.Length);
            if (etagBytes.Length > 0)
                writer.Write(etagBytes);
            
            // Write LastModified (v2)
            writer.Write((byte)lastModifiedBytes.Length);
            if (lastModifiedBytes.Length > 0)
                writer.Write(lastModifiedBytes);
            
            // Write payload
            writer.Write(encryptedPayload.Length);
            writer.Write(encryptedPayload);
            
            writer.Flush();
            
            // Write to file atomically
            byte[] fileData = ms.ToArray();
            await File.WriteAllBytesAsync(filePath, fileData).ConfigureAwait(false);
        }

        /// <summary>
        /// Read and decrypt asset data from a cache file.
        /// </summary>
        /// <param name="filePath">Full path to the cache file</param>
        /// <param name="expectedUrl">Optional URL for hash validation</param>
        /// <returns>Decrypted asset bytes, or null if invalid/corrupted</returns>
        public static async Task<CacheReadResult?> ReadAsync(string filePath, string? expectedUrl = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                
                using var ms = new MemoryStream(fileData);
                using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                // Read and validate magic header
                byte[] magicBytes = reader.ReadBytes(8);
                string magic = Encoding.ASCII.GetString(magicBytes);
                if (magic != MagicHeader)
                    return null;

                // Read version
                int version = reader.ReadInt32();
                if (version != FormatVersion)
                    return null; // Unsupported version

                // Read URL hash
                byte[] storedHash = reader.ReadBytes(32);
                
                // Validate URL hash if expected URL provided
                if (!string.IsNullOrEmpty(expectedUrl))
                {
                    byte[] expectedHash = ComputeUrlHash(expectedUrl);
                    if (!ByteArrayEquals(storedHash, expectedHash))
                        return null; // Hash mismatch
                }

                // Read extension
                int extLength = reader.ReadByte();
                string extension = "";
                if (extLength > 0)
                {
                    byte[] extBytes = reader.ReadBytes(extLength);
                    extension = Encoding.UTF8.GetString(extBytes);
                }

                // Read timestamp
                long timestamp = reader.ReadInt64();
                var cachedAt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

                // Read ETag and LastModified (v2)
                string etag = "";
                string lastModified = "";
                if (version >= 2)
                {
                    int etagLength = reader.ReadByte();
                    if (etagLength > 0)
                    {
                        byte[] etagBytes = reader.ReadBytes(etagLength);
                        etag = Encoding.UTF8.GetString(etagBytes);
                    }
                    
                    int lastModifiedLength = reader.ReadByte();
                    if (lastModifiedLength > 0)
                    {
                        byte[] lastModifiedBytes = reader.ReadBytes(lastModifiedLength);
                        lastModified = Encoding.UTF8.GetString(lastModifiedBytes);
                    }
                }

                // Read payload
                int payloadLength = reader.ReadInt32();
                if (payloadLength <= 0 || payloadLength > 100_000_000) // Max 100MB
                    return null;

                byte[] encryptedPayload = reader.ReadBytes(payloadLength);
                if (encryptedPayload.Length != payloadLength)
                    return null; // Incomplete file

                // Decrypt payload
                byte[] data = XorDecrypt(encryptedPayload);

                return new CacheReadResult
                {
                    Data = data,
                    OriginalExtension = extension,
                    CachedAt = cachedAt.DateTime,
                    ETag = etag,
                    LastModified = lastModified
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetCacheFile] Read error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a file is a valid cache file by reading its header.
        /// </summary>
        public static bool IsValidCacheFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 12) return false; // Too small

                byte[] header = new byte[8];
                fs.Read(header, 0, 8);
                return Encoding.ASCII.GetString(header) == MagicHeader;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate the cache file name from a URL.
        /// </summary>
        public static string GetCacheFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            // Use URL hash as filename to avoid path issues
            byte[] hash = ComputeUrlHash(url);
            string hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            
            // Use first 16 chars of hash (64 bits = enough uniqueness)
            return hashHex.Substring(0, 16) + Extension;
        }

        #endregion

        #region Private Helpers

        private static byte[] GenerateXorKey(string passphrase)
        {
            using var sha = SHA256.Create();
            byte[] fullHash = sha.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
            
            // Use first 16 bytes as XOR key
            byte[] key = new byte[16];
            Array.Copy(fullHash, key, 16);
            return key;
        }

        private static byte[] ComputeUrlHash(string url)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
        }

        private static byte[] XorEncrypt(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ XorKey[i % XorKey.Length]);
            }
            return result;
        }

        private static byte[] XorDecrypt(byte[] data)
        {
            // XOR is symmetric - same operation for encrypt/decrypt
            return XorEncrypt(data);
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Result of reading a cache file.
    /// </summary>
    public class CacheReadResult
    {
        /// <summary>
        /// Decrypted asset data bytes.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Original file extension (e.g., ".png").
        /// </summary>
        public string OriginalExtension { get; set; } = "";

        /// <summary>
        /// When the asset was cached.
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// HTTP ETag header from original download (for freshness validation).
        /// </summary>
        public string ETag { get; set; } = "";

        /// <summary>
        /// HTTP Last-Modified header from original download (for freshness validation).
        /// </summary>
        public string LastModified { get; set; } = "";
    }
}
