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
    public interface IModPriorityService
    {
        Task<ModPriorityConfig> LoadConfigAsync(
            string targetPath,
            CancellationToken ct = default);

        Task SaveConfigAsync(
            ModPriorityConfig config,
            string targetPath,
            CancellationToken ct = default);

        Task<int> GetModPriorityAsync(
            string modId,
            string targetPath,
            CancellationToken ct = default);

        Task SetModPriorityAsync(
            string modId,
            string modName,
            string category,
            int priority,
            string targetPath,
            CancellationToken ct = default);

        Task<IReadOnlyList<ModPriority>> GetOrderedPrioritiesAsync(
            string targetPath,
            string? category = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<ModSource>> ApplyPrioritiesAsync(
            IEnumerable<ModSource> modSources,
            string targetPath,
            CancellationToken ct = default);

        void InvalidateCache();
    }
}
