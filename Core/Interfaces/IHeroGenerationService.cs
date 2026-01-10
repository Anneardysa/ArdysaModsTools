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
