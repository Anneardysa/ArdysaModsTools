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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Interfaces
{
    public interface IConflictDetector
    {
        Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
            IEnumerable<ModSource> modsToApply,
            string targetPath,
            CancellationToken ct = default);

        Task<ModConflict?> CheckSingleConflictAsync(
            ModSource newMod,
            ModSource existingMod,
            CancellationToken ct = default);

        bool HasCriticalConflicts(IEnumerable<ModConflict> conflicts);

        bool RequiresUserIntervention(IEnumerable<ModConflict> conflicts);

        IReadOnlyList<ModConflict> GetConflictsBySeverity(
            IEnumerable<ModConflict> conflicts,
            ConflictSeverity severity);
    }
}
