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
using System.Text;

namespace ArdysaModsTools.Core.Helpers
{
    public static class VpkPackageValidator
    {
        private const uint Signature = 0x55AA1234;
        private const ushort DirectoryArchiveIndex = 0x7FFF;
        private const ushort EntryTerminator = 0xFFFF;
        private const string ItemsGamePath = "scripts/items/items_game.txt";

        public static bool TryValidate(string vpkPath, out string error)
        {
            try
            {
                return Validate(vpkPath, out error);
            }
            catch (EndOfStreamException)
            {
                error = "package directory is truncated";
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = string.Empty;
                return true;
            }
        }

        private static bool Validate(string vpkPath, out string error)
        {
            using var fs = new FileStream(vpkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            long fileLength = fs.Length;
            if (fileLength < 12)
                return No($"file is {fileLength} bytes — not a package", out error);

            if (br.ReadUInt32() != Signature)
                return No("missing VPK signature (not a package, or a raw data chunk)", out error);

            uint version = br.ReadUInt32();
            if (version != 1 && version != 2)
                return No($"unsupported VPK version {version}", out error);

            uint treeSize = br.ReadUInt32();
            long headerSize = version == 1 ? 12 : 28;
            long dataEnd = fileLength;
            if (version == 2)
            {
                uint fileDataSectionSize = br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                if (fileDataSectionSize > 0)
                    dataEnd = headerSize + treeSize + fileDataSectionSize;
            }

            if (treeSize == 0)
                return No("package directory is empty", out error);
            if (headerSize + treeSize > fileLength)
                return No($"directory claims {treeSize} bytes but the file holds {fileLength - headerSize}", out error);

            long dataStart = headerSize + treeSize;
            if (dataEnd > fileLength) dataEnd = fileLength;

            if (dataEnd - dataStart > uint.MaxValue)
                return No("data section exceeds 4 GiB — too many mods for one package", out error);

            var tree = br.ReadBytes((int)treeSize);
            if (tree.Length != treeSize)
                return No("package directory is truncated", out error);

            var chunkLengths = new Dictionary<ushort, long>();
            int entries = 0;
            int pos = 0;

            while (true)
            {
                string ext = ReadCString(tree, ref pos);
                if (ext.Length == 0) break;

                while (true)
                {
                    string dir = ReadCString(tree, ref pos);
                    if (dir.Length == 0) break;

                    while (true)
                    {
                        string name = ReadCString(tree, ref pos);
                        if (name.Length == 0) break;

                        Need(tree, pos, 18);
                        pos += 4;
                        ushort preloadBytes = ReadUInt16(tree, ref pos);
                        ushort archiveIndex = ReadUInt16(tree, ref pos);
                        uint entryOffset = ReadUInt32(tree, ref pos);
                        uint entryLength = ReadUInt32(tree, ref pos);
                        if (ReadUInt16(tree, ref pos) != EntryTerminator)
                            return No("corrupt directory entry (bad terminator)", out error);

                        int preloadAt = pos;
                        Need(tree, pos, preloadBytes);
                        pos += preloadBytes;
                        entries++;

                        if (entryLength > 0)
                        {
                            if (archiveIndex == DirectoryArchiveIndex)
                            {
                                if (dataStart + entryOffset + entryLength > dataEnd)
                                    return No($"entry '{FullPath(dir, name, ext)}' points past the end of the package", out error);
                            }
                            else
                            {
                                if (!TryChunkLength(vpkPath, archiveIndex, chunkLengths, out long chunkLength))
                                    return No($"entry '{FullPath(dir, name, ext)}' needs archive {archiveIndex:D3}, which is missing", out error);
                                if ((long)entryOffset + entryLength > chunkLength)
                                    return No($"entry '{FullPath(dir, name, ext)}' points past the end of archive {archiveIndex:D3}", out error);
                            }
                        }

                        if (FullPath(dir, name, ext).Equals(ItemsGamePath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (preloadBytes == 0 && entryLength == 0)
                                return No("packed item data is empty", out error);

                            if (Array.IndexOf(tree, (byte)'\r', preloadAt, preloadBytes) >= 0 ||
                                (archiveIndex == DirectoryArchiveIndex &&
                                 ContainsCarriageReturn(fs, dataStart + entryOffset, entryLength)))
                                return No("packed item data carries CRLF line endings", out error);
                        }
                    }
                }
            }

            if (entries == 0)
                return No("package contains no files", out error);

            error = string.Empty;
            return true;
        }

        private static bool ContainsCarriageReturn(FileStream fs, long offset, uint length)
        {
            fs.Position = offset;
            var buffer = new byte[1 << 20];
            long remaining = length;
            while (remaining > 0)
            {
                int want = (int)Math.Min(buffer.Length, remaining);
                int read = fs.Read(buffer, 0, want);
                if (read <= 0) break;
                if (Array.IndexOf(buffer, (byte)'\r', 0, read) >= 0) return true;
                remaining -= read;
            }
            return false;
        }

        private static bool TryChunkLength(string vpkPath, ushort archiveIndex,
            Dictionary<ushort, long> cache, out long length)
        {
            if (cache.TryGetValue(archiveIndex, out length)) return length >= 0;

            string dir = Path.GetDirectoryName(vpkPath) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(vpkPath);
            if (stem.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
                stem = stem[..^4];

            string chunk = Path.Combine(dir, $"{stem}_{archiveIndex:D3}.vpk");
            length = File.Exists(chunk) ? new FileInfo(chunk).Length : -1;
            cache[archiveIndex] = length;
            return length >= 0;
        }

        private static string FullPath(string dir, string name, string ext)
        {
            string file = ext == " " ? name : $"{name}.{ext}";
            return dir == " " ? file : $"{dir}/{file}";
        }

        private static string ReadCString(byte[] tree, ref int pos)
        {
            int start = pos;
            while (pos < tree.Length && tree[pos] != 0) pos++;
            if (pos >= tree.Length) throw new EndOfStreamException();
            string s = Encoding.UTF8.GetString(tree, start, pos - start);
            pos++;
            return s;
        }

        private static void Need(byte[] tree, int pos, int count)
        {
            if (pos + count > tree.Length) throw new EndOfStreamException();
        }

        private static ushort ReadUInt16(byte[] tree, ref int pos)
        {
            ushort v = (ushort)(tree[pos] | (tree[pos + 1] << 8));
            pos += 2;
            return v;
        }

        private static uint ReadUInt32(byte[] tree, ref int pos)
        {
            uint v = (uint)(tree[pos] | (tree[pos + 1] << 8) | (tree[pos + 2] << 16) | (tree[pos + 3] << 24));
            pos += 4;
            return v;
        }

        private static bool No(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
