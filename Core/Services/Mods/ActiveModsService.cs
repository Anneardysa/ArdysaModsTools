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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Provides a unified query layer over installed mods.
    /// Reads existing extraction logs (<see cref="HeroExtractionLog"/> and <see cref="MiscExtractionLog"/>)
    /// to build a snapshot of all active mods without introducing new state.
    /// 
    /// <para><b>Design decisions:</b></para>
    /// <list type="bullet">
    ///   <item>Read-only query service — does not modify any files</item>
    ///   <item>No caching — always reads current state from disk for accuracy</item>
    ///   <item>Null-safe — gracefully handles missing or corrupt log files</item>
    ///   <item>Thread-safe — stateless methods safe for concurrent access</item>
    /// </list>
    /// </summary>
    public class ActiveModsService : IActiveModsService
    {
        private readonly IAppLogger? _logger;

        public ActiveModsService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<ActiveModInfo> GetActiveModsAsync(string dotaPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dotaPath))
            {
                return Task.FromResult(new ActiveModInfo
                {
                    OverallStatus = ModStatus.NotChecked
                });
            }

            ct.ThrowIfCancellationRequested();

            try
            {
                var heroMods = LoadHeroMods(dotaPath);
                var miscMods = LoadMiscMods(dotaPath);
                var lastGenerated = DetermineLastGenerated(dotaPath);

                var overallStatus = (heroMods.Count > 0 || miscMods.Count > 0)
                    ? ModStatus.Ready
                    : ModStatus.NotInstalled;

                var result = new ActiveModInfo
                {
                    OverallStatus = overallStatus,
                    HeroMods = heroMods,
                    MiscMods = miscMods,
                    LastGeneratedAt = lastGenerated
                };

                _logger?.Log($"[ActiveMods] Loaded {result.TotalModCount} active mod(s): " +
                             $"{heroMods.Count} hero, {miscMods.Count} misc");

                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[ActiveMods] Error loading active mods: {ex.Message}");
                return Task.FromResult(new ActiveModInfo
                {
                    OverallStatus = ModStatus.Error
                });
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ActiveHeroMod>> GetActiveHeroModsAsync(
            string dotaPath, CancellationToken ct = default)
        {
            var info = await GetActiveModsAsync(dotaPath, ct).ConfigureAwait(false);
            return info.HeroMods;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ActiveMiscMod>> GetActiveMiscModsAsync(
            string dotaPath, CancellationToken ct = default)
        {
            var info = await GetActiveModsAsync(dotaPath, ct).ConfigureAwait(false);
            return info.MiscMods;
        }

        /// <inheritdoc/>
        public async Task<ActiveHeroMod?> GetActiveHeroModAsync(
            string dotaPath, string heroId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                return null;

            var heroMods = await GetActiveHeroModsAsync(dotaPath, ct).ConfigureAwait(false);
            return heroMods.FirstOrDefault(m =>
                string.Equals(m.HeroId, heroId, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public async Task<ActiveMiscMod?> GetActiveMiscModAsync(
            string dotaPath, string category, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(category))
                return null;

            var miscMods = await GetActiveMiscModsAsync(dotaPath, ct).ConfigureAwait(false);
            return miscMods.FirstOrDefault(m =>
                string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        #region Private Helpers

        /// <summary>
        /// Loads hero mod data from <c>hero_extraction_log.json</c>.
        /// Returns empty list if log doesn't exist or is corrupt.
        /// </summary>
        private List<ActiveHeroMod> LoadHeroMods(string dotaPath)
        {
            var log = HeroExtractionLog.Load(dotaPath);
            if (log?.InstalledSets == null || log.InstalledSets.Count == 0)
                return new List<ActiveHeroMod>();

            return log.InstalledSets
                .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
                .Select(entry => new ActiveHeroMod
                {
                    HeroId = entry.HeroId,
                    SetName = entry.SetName ?? string.Empty,
                    InstalledFiles = entry.Files ?? new List<string>()
                })
                .ToList();
        }

        /// <summary>
        /// Loads misc mod data from <c>misc_extraction_log.json</c>.
        /// Returns empty list if log doesn't exist or is corrupt.
        /// </summary>
        private List<ActiveMiscMod> LoadMiscMods(string dotaPath)
        {
            var log = MiscExtractionLog.Load(dotaPath);
            if (log?.Selections == null || log.Selections.Count == 0)
                return new List<ActiveMiscMod>();

            return log.Selections
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => new ActiveMiscMod
                {
                    Category = kvp.Key,
                    SelectedChoice = kvp.Value,
                    InstalledFiles = log.InstalledFiles.TryGetValue(kvp.Key, out var files)
                        ? files
                        : new List<string>()
                })
                .ToList();
        }

        /// <summary>
        /// Determines the most recent generation timestamp across both logs.
        /// </summary>
        private DateTime? DetermineLastGenerated(string dotaPath)
        {
            var miscLog = MiscExtractionLog.Load(dotaPath);
            if (miscLog != null && miscLog.GeneratedAt != default)
                return miscLog.GeneratedAt;

            // HeroExtractionLog doesn't have a timestamp — fall back to file modification time
            var heroLogPath = HeroExtractionLog.GetLogPath(dotaPath);
            if (System.IO.File.Exists(heroLogPath))
            {
                try
                {
                    return System.IO.File.GetLastWriteTimeUtc(heroLogPath);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        #endregion
    }
}
