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
    public interface IActiveModsService
    {
        Task<ActiveModInfo> GetActiveModsAsync(string dotaPath, CancellationToken ct = default);

        Task<IReadOnlyList<ActiveHeroMod>> GetActiveHeroModsAsync(string dotaPath, CancellationToken ct = default);

        Task<IReadOnlyList<ActiveMiscMod>> GetActiveMiscModsAsync(string dotaPath, CancellationToken ct = default);

        Task<ActiveHeroMod?> GetActiveHeroModAsync(string dotaPath, string heroId, CancellationToken ct = default);

        Task<ActiveMiscMod?> GetActiveMiscModAsync(string dotaPath, string category, CancellationToken ct = default);
    }
}
