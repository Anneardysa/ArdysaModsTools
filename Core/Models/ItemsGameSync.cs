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

namespace ArdysaModsTools.Core.Models
{
    public enum ItemsGameSyncState
    {
        Unknown,

        InSync,

        Stale
    }

    public readonly record struct VpkStamp(long Length, DateTime LastWriteUtc) : IEquatable<VpkStamp>
    {
        public static VpkStamp? Read(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return null;
                return new VpkStamp(info.Length, info.LastWriteTimeUtc);
            }
            catch
            {
                return null;
            }
        }

        public bool Equals(VpkStamp other)
        {
            if (Length != other.Length) return false;
            return Math.Abs((LastWriteUtc - other.LastWriteUtc).TotalSeconds) < 2.0;
        }

        public override int GetHashCode() => HashCode.Combine(Length, (int)(LastWriteUtc.Ticks / (TimeSpan.TicksPerSecond * 2)));

        public bool IsEmpty => Length == 0 && LastWriteUtc == default;
    }

    public record ItemsGameBaseline
    {
        public VpkStamp VanillaVpk { get; init; }

        public string VanillaItemsGameSha { get; init; } = "";

        public VpkStamp ModVpk { get; init; }

        public IReadOnlyList<string> PatchedIds { get; init; } = Array.Empty<string>();

        public string AppVersion { get; init; } = "";

        public DateTime BuiltUtc { get; init; }
    }

    public record SyncItemDetail
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Category { get; init; } = "";
        public string Status { get; init; } = "modified";
        public string Description { get; init; } = "";
    }

    public record SyncDetailsReport
    {
        public bool IsStale { get; init; }
        public int AddedCount { get; init; }
        public int ModifiedCount { get; init; }
        public int ErrorCount { get; init; }
        public string Summary { get; init; } = "";
        public IReadOnlyList<SyncItemDetail> Items { get; init; } = Array.Empty<SyncItemDetail>();
    }

    public record ItemsGameSyncVerdict
    {
        public ItemsGameSyncState State { get; init; } = ItemsGameSyncState.Unknown;

        public string DetailKey { get; init; } = "verify.sync.unknown";

        public object? DetailVars { get; init; }

        public string? Diagnostic { get; init; }

        public static ItemsGameSyncVerdict Cold { get; } = new();
    }
}
