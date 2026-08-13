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

    public readonly record struct VpkStamp(long Length, DateTime LastWriteUtc)
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

    public record ItemsGameSyncVerdict
    {
        public ItemsGameSyncState State { get; init; } = ItemsGameSyncState.Unknown;

        public string DetailKey { get; init; } = "verify.sync.unknown";

        public object? DetailVars { get; init; }

        public string? Diagnostic { get; init; }

        public static ItemsGameSyncVerdict Cold { get; } = new();
    }
}
