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

namespace ArdysaModsTools.Core.Models
{
    public sealed class HeroDatabaseStatus
    {
        public string Source { get; init; } = "unknown";

        public int SetCount { get; init; }

        public DateTime? UpdatedUtc { get; init; }

        public string? Sha256 { get; init; }
    }

    public sealed class HeroDatabaseCheckResult
    {
        public bool Success { get; init; }

        public bool UpToDate { get; init; }

        public int LocalSetCount { get; init; }

        public int? RemoteSetCount { get; init; }

        public string Message { get; init; } = "";
    }

    public sealed class HeroDatabaseUpdateResult
    {
        public bool Success { get; init; }

        public bool Changed { get; init; }

        public int SetCount { get; init; }

        public string Message { get; init; } = "";
    }
}
