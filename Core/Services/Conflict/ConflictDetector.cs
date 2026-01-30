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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.Conflict
{
    /// <summary>
    /// Service for detecting conflicts between mods.
    /// Analyzes file paths, script modifications, and asset overlaps.
    /// </summary>
    /// <remarks>
    /// Conflict detection follows these steps:
    /// 1. Collect all affected files from each mod source
    /// 2. Identify overlapping file paths between mods
    /// 3. Classify conflicts by type (File, Script, Asset, Configuration)
    /// 4. Determine severity based on overlap count and file types
    /// 5. Generate available resolution options
    /// </remarks>
    public class ConflictDetector : IConflictDetector
    {
        private readonly IAppLogger? _logger;

        // File patterns that indicate different conflict types
        private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".kv", ".vdf"
        };

        private static readonly HashSet<string> AssetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".vtex_c", ".vmat_c", ".vmdl_c", ".vpcf_c", ".vsnd_c", ".vpk"
        };

        private static readonly HashSet<string> ConfigPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "gameinfo", "default", "settings"
        };

        /// <summary>
        /// Creates a new ConflictDetector instance.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ConflictDetector(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
            IEnumerable<ModSource> modsToApply,
            string targetPath,
            CancellationToken ct = default)
        {
            var conflicts = new List<ModConflict>();
            var modList = modsToApply.ToList();

            if (modList.Count < 2)
            {
                _logger?.Log("ConflictDetector: Less than 2 mods, no conflicts possible.");
                return conflicts;
            }

            _logger?.Log($"ConflictDetector: Checking {modList.Count} mods for conflicts...");

            await Task.Yield(); // Allow for async context

            // Compare each pair of mods
            for (int i = 0; i < modList.Count; i++)
            {
                for (int j = i + 1; j < modList.Count; j++)
                {
                    ct.ThrowIfCancellationRequested();

                    var conflict = await CheckSingleConflictAsync(modList[i], modList[j], ct);
                    if (conflict != null)
                    {
                        conflicts.Add(conflict);
                        _logger?.Log($"ConflictDetector: Found {conflict.Severity} conflict between " +
                                     $"{modList[i].ModName} and {modList[j].ModName}");
                    }
                }
            }

            // Sort by severity (Critical first)
            conflicts.Sort((a, b) => b.Severity.CompareTo(a.Severity));

            _logger?.Log($"ConflictDetector: Detected {conflicts.Count} conflict(s).");
            return conflicts;
        }

        /// <inheritdoc/>
        public async Task<ModConflict?> CheckSingleConflictAsync(
            ModSource newMod,
            ModSource existingMod,
            CancellationToken ct = default)
        {
            await Task.Yield(); // Allow for async context
            ct.ThrowIfCancellationRequested();

            // Find overlapping files
            var newFiles = new HashSet<string>(
                newMod.AffectedFiles, 
                StringComparer.OrdinalIgnoreCase);
            
            var overlappingFiles = existingMod.AffectedFiles
                .Where(f => newFiles.Contains(f))
                .ToList();

            if (overlappingFiles.Count == 0)
            {
                return null; // No conflict
            }

            // Classify the conflict based on file types
            var conflictType = ClassifyConflictType(overlappingFiles);
            var severity = DetermineSeverity(overlappingFiles, conflictType);

            // Check for critical conditions
            if (IsCriticalConflict(newMod, existingMod, overlappingFiles))
            {
                return ModConflict.CreateCriticalConflict(
                    existingMod,
                    newMod,
                    $"Both mods modify {overlappingFiles.Count} critical file(s)");
            }

            // Create appropriate conflict type
            return conflictType switch
            {
                ConflictType.Script => ModConflict.CreateScriptConflict(
                    existingMod,
                    newMod,
                    overlappingFiles.First(),
                    Path.GetFileNameWithoutExtension(overlappingFiles.First())),

                _ => ModConflict.CreateFileConflict(
                    existingMod,
                    newMod,
                    overlappingFiles)
            };
        }

        /// <inheritdoc/>
        public bool HasCriticalConflicts(IEnumerable<ModConflict> conflicts)
        {
            return conflicts.Any(c => c.Severity == ConflictSeverity.Critical);
        }

        /// <inheritdoc/>
        public bool RequiresUserIntervention(IEnumerable<ModConflict> conflicts)
        {
            return conflicts.Any(c => 
                c.RequiresUserIntervention || 
                c.AvailableResolutions.Any(r => r.Strategy == ResolutionStrategy.Interactive));
        }

        /// <inheritdoc/>
        public IReadOnlyList<ModConflict> GetConflictsBySeverity(
            IEnumerable<ModConflict> conflicts,
            ConflictSeverity severity)
        {
            return conflicts.Where(c => c.Severity == severity).ToList();
        }

        /// <summary>
        /// Classifies the conflict type based on the file extensions involved.
        /// </summary>
        private ConflictType ClassifyConflictType(IReadOnlyList<string> overlappingFiles)
        {
            // Check file extensions to determine conflict type
            var extensions = overlappingFiles
                .Select(f => Path.GetExtension(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fileNames = overlappingFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            // Configuration conflicts (gameinfo, settings files)
            if (fileNames.Any(name => ConfigPatterns.Any(p => 
                name.Contains(p, StringComparison.OrdinalIgnoreCase))))
            {
                return ConflictType.Configuration;
            }

            // Script/KV conflicts
            if (extensions.Any(ext => ScriptExtensions.Contains(ext)))
            {
                return ConflictType.Script;
            }

            // Asset conflicts
            if (extensions.Any(ext => AssetExtensions.Contains(ext)))
            {
                return ConflictType.Asset;
            }

            // Default to file conflict
            return ConflictType.File;
        }

        /// <summary>
        /// Determines the severity of a conflict based on file count and type.
        /// </summary>
        private ConflictSeverity DetermineSeverity(
            IReadOnlyList<string> overlappingFiles,
            ConflictType conflictType)
        {
            var fileCount = overlappingFiles.Count;

            // Configuration conflicts are more severe
            if (conflictType == ConflictType.Configuration)
            {
                return fileCount > 3 ? ConflictSeverity.Critical : ConflictSeverity.High;
            }

            // Script conflicts need careful handling
            if (conflictType == ConflictType.Script)
            {
                return fileCount > 5 ? ConflictSeverity.High : ConflictSeverity.Medium;
            }

            // Asset and File conflicts severity based on count
            return fileCount switch
            {
                <= 2 => ConflictSeverity.Low,
                <= 5 => ConflictSeverity.Medium,
                <= 10 => ConflictSeverity.High,
                _ => ConflictSeverity.Critical
            };
        }

        /// <summary>
        /// Checks if this is a critical conflict that cannot be auto-resolved.
        /// </summary>
        private bool IsCriticalConflict(
            ModSource newMod,
            ModSource existingMod,
            IReadOnlyList<string> overlappingFiles)
        {
            // Same category with many overlapping files is critical
            if (newMod.Category == existingMod.Category && overlappingFiles.Count > 10)
            {
                return true;
            }

            // Core game files are critical
            if (overlappingFiles.Any(f => 
                f.Contains("gameinfo", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("pak01_dir", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
