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

namespace ArdysaModsTools.Core.Services.Conflict
{
    /// <summary>
    /// Service for managing mod priorities.
    /// Handles configuration persistence and priority lookup.
    /// </summary>
    public class ModPriorityService : IModPriorityService
    {
        private readonly IAppLogger? _logger;
        
        // Cache to avoid repeated disk reads
        private ModPriorityConfig? _cachedConfig;
        private string? _cachedTargetPath;
        private DateTime _cacheTime;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Creates a new ModPriorityService instance.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ModPriorityService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ModPriorityConfig> LoadConfigAsync(
            string targetPath,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            // Check cache validity
            if (IsCacheValid(targetPath))
            {
                return _cachedConfig!;
            }

            _logger?.Log("ModPriorityService: Loading priority configuration...");

            var config = ModPriorityConfig.Load(targetPath);

            if (config == null)
            {
                _logger?.Log("ModPriorityService: No existing config found, creating default.");
                config = ModPriorityConfig.CreateDefault();
            }

            // Update cache
            _cachedConfig = config;
            _cachedTargetPath = targetPath;
            _cacheTime = DateTime.UtcNow;

            return config;
        }

        /// <inheritdoc/>
        public async Task SaveConfigAsync(
            ModPriorityConfig config,
            string targetPath,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            _logger?.Log("ModPriorityService: Saving priority configuration...");

            try
            {
                config.Save(targetPath);

                // Update cache
                _cachedConfig = config;
                _cachedTargetPath = targetPath;
                _cacheTime = DateTime.UtcNow;

                _logger?.Log("ModPriorityService: Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"ModPriorityService: Failed to save config: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> GetModPriorityAsync(
            string modId,
            string targetPath,
            CancellationToken ct = default)
        {
            var config = await LoadConfigAsync(targetPath, ct);
            return config.GetPriority(modId);
        }

        /// <inheritdoc/>
        public async Task SetModPriorityAsync(
            string modId,
            string modName,
            string category,
            int priority,
            string targetPath,
            CancellationToken ct = default)
        {
            var config = await LoadConfigAsync(targetPath, ct);
            config.SetPriority(modId, modName, category, priority);
            await SaveConfigAsync(config, targetPath, ct);

            _logger?.Log($"ModPriorityService: Set priority for '{modName}' to {priority}");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModPriority>> GetOrderedPrioritiesAsync(
            string targetPath,
            string? category = null,
            CancellationToken ct = default)
        {
            var config = await LoadConfigAsync(targetPath, ct);

            var priorities = config.Priorities
                .Where(p => string.IsNullOrEmpty(category) || 
                           p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.ModName)
                .ToList();

            return priorities;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModSource>> ApplyPrioritiesAsync(
            IEnumerable<ModSource> modSources,
            string targetPath,
            CancellationToken ct = default)
        {
            var config = await LoadConfigAsync(targetPath, ct);
            var sources = modSources.ToList();

            foreach (var source in sources)
            {
                source.Priority = config.GetPriority(source.ModId);
            }

            _logger?.Log($"ModPriorityService: Applied priorities to {sources.Count} mod source(s).");

            return sources.OrderBy(s => s.Priority).ToList();
        }

        /// <summary>
        /// Checks if the cached configuration is still valid.
        /// </summary>
        private bool IsCacheValid(string targetPath)
        {
            if (_cachedConfig == null || _cachedTargetPath == null)
            {
                return false;
            }

            if (_cachedTargetPath != targetPath)
            {
                return false;
            }

            if (DateTime.UtcNow - _cacheTime > CacheDuration)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Invalidates the cached configuration.
        /// </summary>
        public void InvalidateCache()
        {
            _cachedConfig = null;
            _cachedTargetPath = null;
        }
    }
}
