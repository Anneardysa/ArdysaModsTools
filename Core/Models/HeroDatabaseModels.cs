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
    /// <summary>
    /// Snapshot of the locally-available hero database (heroes.json) for the Settings panel.
    /// </summary>
    public sealed class HeroDatabaseStatus
    {
        /// <summary>Where the active copy came from: "cdn", "manual", "bundled", or "none".</summary>
        public string Source { get; init; } = "unknown";

        /// <summary>Number of non-default cosmetic sets in the active copy.</summary>
        public int SetCount { get; init; }

        /// <summary>When the active copy was produced (download time, or bundled file timestamp), UTC.</summary>
        public DateTime? UpdatedUtc { get; init; }

        /// <summary>SHA-256 of the active copy (null for the bundled snapshot).</summary>
        public string? Sha256 { get; init; }
    }

    /// <summary>Result of comparing the local hero database against the live remote copy.</summary>
    public sealed class HeroDatabaseCheckResult
    {
        /// <summary>True when the remote copy was reachable and the comparison completed.</summary>
        public bool Success { get; init; }

        /// <summary>True when the local copy already matches the remote (by SHA-256).</summary>
        public bool UpToDate { get; init; }

        /// <summary>Set count reported by the remote copy, when reachable.</summary>
        public int? RemoteSetCount { get; init; }

        /// <summary>Display-ready status message.</summary>
        public string Message { get; init; } = "";
    }

    /// <summary>Result of forcing the local hero database to the latest remote copy.</summary>
    public sealed class HeroDatabaseUpdateResult
    {
        /// <summary>True when the remote copy was reachable and the operation completed.</summary>
        public bool Success { get; init; }

        /// <summary>True when the local copy was actually rewritten (the remote differed).</summary>
        public bool Changed { get; init; }

        /// <summary>Set count of the copy now in effect.</summary>
        public int SetCount { get; init; }

        /// <summary>Display-ready status message.</summary>
        public string Message { get; init; } = "";
    }
}
