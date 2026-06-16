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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Manual hero-database (heroes.json) maintenance for the Settings panel: report the local copy's
    /// status, check it against the live remote copy by SHA-256, and force it up to date. Lets users
    /// on impaired connections self-fix the "stale heroes.json" desync that hides set thumbnails.
    /// </summary>
    public interface IHeroDatabaseService
    {
        /// <summary>Describe the hero database copy currently in effect (cdn/manual/bundled).</summary>
        Task<HeroDatabaseStatus> GetStatusAsync(CancellationToken ct = default);

        /// <summary>Fetch the live copy and compare it to the local one by SHA-256, without persisting.</summary>
        Task<HeroDatabaseCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

        /// <summary>Fetch the live copy and persist it as last-known-good when it differs from the local one.</summary>
        Task<HeroDatabaseUpdateResult> UpdateAsync(CancellationToken ct = default);
    }
}
