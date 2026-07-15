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
    public class ConflictDetector : IConflictDetector
    {
        private readonly IAppLogger? _logger;

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

        public ConflictDetector(IAppLogger? logger = null)
        {
            _logger = logger;
        }

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

            await Task.Yield();

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

            conflicts.Sort((a, b) => b.Severity.CompareTo(a.Severity));

            _logger?.Log($"ConflictDetector: Detected {conflicts.Count} conflict(s).");
            return conflicts;
        }

        public async Task<ModConflict?> CheckSingleConflictAsync(
            ModSource newMod,
            ModSource existingMod,
            CancellationToken ct = default)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var newFiles = new HashSet<string>(
                newMod.AffectedFiles, 
                StringComparer.OrdinalIgnoreCase);
            
            var overlappingFiles = existingMod.AffectedFiles
                .Where(f => newFiles.Contains(f))
                .ToList();

            if (overlappingFiles.Count == 0)
            {
                return null;
            }

            var conflictType = ClassifyConflictType(overlappingFiles);
            var severity = DetermineSeverity(overlappingFiles, conflictType);

            if (IsCriticalConflict(newMod, existingMod, overlappingFiles))
            {
                return ModConflict.CreateCriticalConflict(
                    existingMod,
                    newMod,
                    $"Both mods modify {overlappingFiles.Count} critical file(s)");
            }

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

        public bool HasCriticalConflicts(IEnumerable<ModConflict> conflicts)
        {
            return conflicts.Any(c => c.Severity == ConflictSeverity.Critical);
        }

        public bool RequiresUserIntervention(IEnumerable<ModConflict> conflicts)
        {
            return conflicts.Any(c => 
                c.RequiresUserIntervention || 
                c.AvailableResolutions.Any(r => r.Strategy == ResolutionStrategy.Interactive));
        }

        public IReadOnlyList<ModConflict> GetConflictsBySeverity(
            IEnumerable<ModConflict> conflicts,
            ConflictSeverity severity)
        {
            return conflicts.Where(c => c.Severity == severity).ToList();
        }

        private ConflictType ClassifyConflictType(IReadOnlyList<string> overlappingFiles)
        {
            var extensions = overlappingFiles
                .Select(f => Path.GetExtension(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fileNames = overlappingFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            if (fileNames.Any(name => ConfigPatterns.Any(p => 
                name.Contains(p, StringComparison.OrdinalIgnoreCase))))
            {
                return ConflictType.Configuration;
            }

            if (extensions.Any(ext => ScriptExtensions.Contains(ext)))
            {
                return ConflictType.Script;
            }

            if (extensions.Any(ext => AssetExtensions.Contains(ext)))
            {
                return ConflictType.Asset;
            }

            return ConflictType.File;
        }

        private ConflictSeverity DetermineSeverity(
            IReadOnlyList<string> overlappingFiles,
            ConflictType conflictType)
        {
            var fileCount = overlappingFiles.Count;

            if (conflictType == ConflictType.Configuration)
            {
                return fileCount > 3 ? ConflictSeverity.Critical : ConflictSeverity.High;
            }

            if (conflictType == ConflictType.Script)
            {
                return fileCount > 5 ? ConflictSeverity.High : ConflictSeverity.Medium;
            }

            return fileCount switch
            {
                <= 2 => ConflictSeverity.Low,
                <= 5 => ConflictSeverity.Medium,
                <= 10 => ConflictSeverity.High,
                _ => ConflictSeverity.Critical
            };
        }

        private bool IsCriticalConflict(
            ModSource newMod,
            ModSource existingMod,
            IReadOnlyList<string> overlappingFiles)
        {
            if (newMod.Category == existingMod.Category && overlappingFiles.Count > 10)
            {
                return true;
            }

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
