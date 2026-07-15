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
    public static class AssetCacheFile
    {
        private const string MagicHeader = "ARDYMODS";
        private const int FormatVersion = 2;
        
        private static readonly byte[] XorKey = GenerateXorKey("ARDYSAMODS2026");
        
        public const string Extension = ".cache.ardysamods";

        #region Public API

        public static async Task WriteAsync(string filePath, string url, string originalExtension, byte[] data, 
            string? etag = null, string? lastModified = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be empty", nameof(data));

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            byte[] encryptedPayload = XorEncrypt(data);
            byte[] urlHash = ComputeUrlHash(url);
            byte[] extensionBytes = Encoding.UTF8.GetBytes(originalExtension ?? "");
            byte[] etagBytes = Encoding.UTF8.GetBytes(etag ?? "");
            byte[] lastModifiedBytes = Encoding.UTF8.GetBytes(lastModified ?? "");
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            
            writer.Write(Encoding.ASCII.GetBytes(MagicHeader));
            writer.Write(FormatVersion);
            writer.Write(urlHash);
            writer.Write((byte)extensionBytes.Length);
            if (extensionBytes.Length > 0)
                writer.Write(extensionBytes);
            writer.Write(timestamp);
            
            writer.Write((byte)etagBytes.Length);
            if (etagBytes.Length > 0)
                writer.Write(etagBytes);
            
            writer.Write((byte)lastModifiedBytes.Length);
            if (lastModifiedBytes.Length > 0)
                writer.Write(lastModifiedBytes);
            
            writer.Write(encryptedPayload.Length);
            writer.Write(encryptedPayload);
            
            writer.Flush();
            
            byte[] fileData = ms.ToArray();
            await File.WriteAllBytesAsync(filePath, fileData).ConfigureAwait(false);
        }

        public static async Task<CacheReadResult?> ReadAsync(string filePath, string? expectedUrl = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                
                using var ms = new MemoryStream(fileData);
                using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                byte[] magicBytes = reader.ReadBytes(8);
                string magic = Encoding.ASCII.GetString(magicBytes);
                if (magic != MagicHeader)
                    return null;

                int version = reader.ReadInt32();
                if (version != FormatVersion)
                    return null;

                byte[] storedHash = reader.ReadBytes(32);
                
                if (!string.IsNullOrEmpty(expectedUrl))
                {
                    byte[] expectedHash = ComputeUrlHash(expectedUrl);
                    if (!ByteArrayEquals(storedHash, expectedHash))
                        return null;
                }

                int extLength = reader.ReadByte();
                string extension = "";
                if (extLength > 0)
                {
                    byte[] extBytes = reader.ReadBytes(extLength);
                    extension = Encoding.UTF8.GetString(extBytes);
                }

                long timestamp = reader.ReadInt64();
                var cachedAt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

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

                int payloadLength = reader.ReadInt32();
                if (payloadLength <= 0 || payloadLength > 100_000_000)
                    return null;

                byte[] encryptedPayload = reader.ReadBytes(payloadLength);
                if (encryptedPayload.Length != payloadLength)
                    return null;

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

        public static bool IsValidCacheFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 12) return false;

                byte[] header = new byte[8];
                fs.Read(header, 0, 8);
                return Encoding.ASCII.GetString(header) == MagicHeader;
            }
            catch
            {
                return false;
            }
        }

        public static string GetCacheFileName(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            byte[] hash = ComputeUrlHash(url);
            string hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            
            return hashHex.Substring(0, 16) + Extension;
        }

        #endregion

        #region Private Helpers

        private static byte[] GenerateXorKey(string passphrase)
        {
            using var sha = SHA256.Create();
            byte[] fullHash = sha.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
            
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

    public class CacheReadResult
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public string OriginalExtension { get; set; } = "";

        public DateTime CachedAt { get; set; }

        public string ETag { get; set; } = "";

        public string LastModified { get; set; } = "";
    }
}
