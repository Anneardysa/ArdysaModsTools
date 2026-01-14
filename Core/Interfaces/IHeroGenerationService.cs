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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for hero set generation operations.
    /// Orchestrates the full pipeline: Download → Modify → Recompile → Replace.
    /// </summary>
    public interface IHeroGenerationService
    {
        /// <summary>
        /// Generate and install a single hero set.
        /// </summary>
        Task<OperationResult> GenerateHeroSetAsync(
            string targetPath,
            HeroModel hero,
            string selectedSetName,
            Action<string> log,
            CancellationToken ct = default);

        /// <summary>
        /// Generate and install multiple hero sets in a single VPK operation.
        /// Flow: Download Original.zip → (Merge + Patch each hero) → Recompile → Replace.
        /// </summary>
        Task<OperationResult> GenerateBatchAsync(
            string targetPath,
            IReadOnlyList<(HeroModel hero, string setName)> heroSets,
            Action<string> log,
            IProgress<(int current, int total, string heroName)>? progress = null,
            IProgress<(int percent, string stage)>? stageProgress = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            CancellationToken ct = default);
    }
}

