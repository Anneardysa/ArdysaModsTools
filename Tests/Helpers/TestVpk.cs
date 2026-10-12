using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArdysaModsTools.Tests.Helpers
{
    public static class TestVpk
    {
        public sealed class Entry
        {
            public string Dir = " ";
            public string Name = string.Empty;
            public string Ext = string.Empty;
            public byte[] Data = Array.Empty<byte>();
            public ushort ArchiveIndex = 0x7FFF;
        }

        public static byte[] Minimal() => Build(ItemsGame("\"items_game\"\n{\n\t\"items\"\n\t{\n\t}\n}\n"));

        public static Entry ItemsGame(string text) => new()
        {
            Dir = "scripts/items",
            Name = "items_game",
            Ext = "txt",
            Data = Encoding.UTF8.GetBytes(text)
        };

        public static Entry Blob(string dir, string name, string ext, int size) => new()
        {
            Dir = dir,
            Name = name,
            Ext = ext,
            Data = new byte[size]
        };

        public static byte[] Build(params Entry[] entries)
        {
            var data = new MemoryStream();
            var offsets = new Dictionary<Entry, uint>();
            foreach (var e in entries)
            {
                offsets[e] = (uint)data.Length;
                data.Write(e.Data, 0, e.Data.Length);
            }

            var tree = new MemoryStream();
            void Cstr(string s)
            {
                var b = Encoding.UTF8.GetBytes(s);
                tree.Write(b, 0, b.Length);
                tree.WriteByte(0);
            }
            void U16(ushort v) { tree.WriteByte((byte)v); tree.WriteByte((byte)(v >> 8)); }
            void U32(uint v) { for (int i = 0; i < 4; i++) tree.WriteByte((byte)(v >> (8 * i))); }

            foreach (var extGroup in Group(entries, e => e.Ext))
            {
                Cstr(extGroup.Key);
                foreach (var dirGroup in Group(extGroup.Value, e => e.Dir))
                {
                    Cstr(dirGroup.Key);
                    foreach (var e in dirGroup.Value)
                    {
                        Cstr(e.Name);
                        U32(0);
                        U16(0);
                        U16(e.ArchiveIndex);
                        U32(offsets[e]);
                        U32((uint)e.Data.Length);
                        U16(0xFFFF);
                    }
                    tree.WriteByte(0);
                }
                tree.WriteByte(0);
            }
            tree.WriteByte(0);

            var treeBytes = tree.ToArray();
            var vpk = new MemoryStream();
            var w = new BinaryWriter(vpk);
            w.Write(0x55AA1234u);
            w.Write(1u);
            w.Write((uint)treeBytes.Length);
            w.Write(treeBytes);
            w.Write(data.ToArray());
            w.Flush();
            return vpk.ToArray();
        }

        private static List<KeyValuePair<string, List<Entry>>> Group(
            IEnumerable<Entry> entries, Func<Entry, string> key)
        {
            var result = new List<KeyValuePair<string, List<Entry>>>();
            foreach (var e in entries)
            {
                var k = key(e);
                var bucket = result.Find(p => p.Key == k).Value;
                if (bucket == null)
                {
                    bucket = new List<Entry>();
                    result.Add(new KeyValuePair<string, List<Entry>>(k, bucket));
                }
                bucket.Add(e);
            }
            return result;
        }
    }
}
