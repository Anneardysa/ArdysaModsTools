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
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents the source of a mod for conflict tracking.
    /// Contains identification, priority, and timestamp information.
    /// </summary>
    public class ModSource
    {
        /// <summary>
        /// Unique identifier for the mod (e.g., "Weather_Ash", "River_Lava").
        /// </summary>
        [JsonPropertyName("modId")]
        public string ModId { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable display name for the mod.
        /// </summary>
        [JsonPropertyName("modName")]
        public string ModName { get; init; } = string.Empty;

        /// <summary>
        /// Category the mod belongs to (e.g., "Environment", "Audio").
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Priority level for conflict resolution. Lower number = higher priority.
        /// Default is 100 (middle priority).
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// When this mod was applied/selected.
        /// </summary>
        [JsonPropertyName("appliedAt")]
        public DateTime AppliedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Files this mod affects (relative paths).
        /// </summary>
        [JsonPropertyName("affectedFiles")]
        public List<string> AffectedFiles { get; init; } = new();

        /// <summary>
        /// Creates a ModSource from a category and selection.
        /// </summary>
        public static ModSource FromSelection(string category, string selection)
        {
            return new ModSource
            {
                ModId = $"{category}_{selection}".Replace(" ", "_"),
                ModName = selection,
                Category = category,
                AppliedAt = DateTime.UtcNow
            };
        }

        public override string ToString() => $"{Category}/{ModName} (Priority: {Priority})";
    }

    /// <summary>
    /// Represents a conflict between two or more mods.
    /// Contains all information needed to detect, display, and resolve the conflict.
    /// </summary>
    public class ModConflict
    {
        /// <summary>
        /// Unique identifier for this conflict.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Type of conflict (File, Script, Asset, Configuration).
        /// </summary>
        [JsonPropertyName("type")]
        public ConflictType Type { get; init; }

        /// <summary>
        /// Severity level determining if manual intervention is needed.
        /// </summary>
        [JsonPropertyName("severity")]
        public ConflictSeverity Severity { get; init; }

        /// <summary>
        /// Human-readable description of the conflict.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Files affected by this conflict (relative paths).
        /// </summary>
        [JsonPropertyName("affectedFiles")]
        public IReadOnlyList<string> AffectedFiles { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Mods involved in this conflict.
        /// </summary>
        [JsonPropertyName("conflictingSources")]
        public IReadOnlyList<ModSource> ConflictingSources { get; init; } = Array.Empty<ModSource>();

        /// <summary>
        /// Available resolution options for this conflict.
        /// </summary>
        [JsonPropertyName("availableResolutions")]
        public IReadOnlyList<ConflictResolutionOption> AvailableResolutions { get; init; } = Array.Empty<ConflictResolutionOption>();

        /// <summary>
        /// The resolution option selected/applied for this conflict.
        /// Null if not yet resolved.
        /// </summary>
        [JsonPropertyName("selectedResolution")]
        public ConflictResolutionOption? SelectedResolution { get; set; }

        /// <summary>
        /// Whether this conflict has been resolved.
        /// </summary>
        [JsonIgnore]
        public bool IsResolved => SelectedResolution != null;

        /// <summary>
        /// Whether this conflict requires user intervention (Critical severity).
        /// </summary>
        [JsonIgnore]
        public bool RequiresUserIntervention => Severity == ConflictSeverity.Critical;

        /// <summary>
        /// Gets the highest priority mod source involved in this conflict.
        /// </summary>
        [JsonIgnore]
        public ModSource? HighestPrioritySource => 
            ConflictingSources.OrderBy(s => s.Priority).FirstOrDefault();

        /// <summary>
        /// Gets the most recently applied mod source.
        /// </summary>
        [JsonIgnore]
        public ModSource? MostRecentSource => 
            ConflictingSources.OrderByDescending(s => s.AppliedAt).FirstOrDefault();

        /// <summary>
        /// Creates a file conflict between two mod sources.
        /// </summary>
        public static ModConflict CreateFileConflict(
            ModSource source1,
            ModSource source2,
            IEnumerable<string> overlappingFiles)
        {
            var files = overlappingFiles.ToList();
            var severity = files.Count > 5 ? ConflictSeverity.High : ConflictSeverity.Medium;

            return new ModConflict
            {
                Type = ConflictType.File,
                Severity = severity,
                Description = $"File conflict between '{source1.ModName}' and '{source2.ModName}': " +
                             $"{files.Count} overlapping file(s)",
                AffectedFiles = files,
                ConflictingSources = new[] { source1, source2 },
                AvailableResolutions = GenerateDefaultResolutions(source1, source2)
            };
        }

        /// <summary>
        /// Creates a script conflict where KV modifications overlap.
        /// </summary>
        public static ModConflict CreateScriptConflict(
            ModSource source1,
            ModSource source2,
            string scriptPath,
            string conflictingKey)
        {
            return new ModConflict
            {
                Type = ConflictType.Script,
                Severity = ConflictSeverity.Medium,
                Description = $"Script conflict: Both '{source1.ModName}' and '{source2.ModName}' " +
                             $"modify key '{conflictingKey}' in {scriptPath}",
                AffectedFiles = new[] { scriptPath },
                ConflictingSources = new[] { source1, source2 },
                AvailableResolutions = GenerateDefaultResolutions(source1, source2, includeMerge: true)
            };
        }

        /// <summary>
        /// Creates a critical conflict that requires user intervention.
        /// </summary>
        public static ModConflict CreateCriticalConflict(
            ModSource source1,
            ModSource source2,
            string reason)
        {
            return new ModConflict
            {
                Type = ConflictType.Configuration,
                Severity = ConflictSeverity.Critical,
                Description = $"Critical conflict between '{source1.ModName}' and '{source2.ModName}': {reason}",
                AffectedFiles = Array.Empty<string>(),
                ConflictingSources = new[] { source1, source2 },
                AvailableResolutions = new[]
                {
                    new ConflictResolutionOption
                    {
                        Id = "choose_first",
                        Strategy = ResolutionStrategy.Interactive,
                        Description = $"Use {source1.ModName} only",
                        PreferredSource = source1
                    },
                    new ConflictResolutionOption
                    {
                        Id = "choose_second",
                        Strategy = ResolutionStrategy.Interactive,
                        Description = $"Use {source2.ModName} only",
                        PreferredSource = source2
                    }
                }
            };
        }

        private static ConflictResolutionOption[] GenerateDefaultResolutions(
            ModSource source1,
            ModSource source2,
            bool includeMerge = false)
        {
            var options = new List<ConflictResolutionOption>
            {
                new()
                {
                    Id = "priority",
                    Strategy = ResolutionStrategy.HigherPriority,
                    Description = "Use mod with higher priority",
                    PreferredSource = source1.Priority <= source2.Priority ? source1 : source2
                },
                new()
                {
                    Id = "recent",
                    Strategy = ResolutionStrategy.MostRecent,
                    Description = "Use most recently applied mod",
                    PreferredSource = source1.AppliedAt >= source2.AppliedAt ? source1 : source2
                },
                new()
                {
                    Id = "keep_existing",
                    Strategy = ResolutionStrategy.KeepExisting,
                    Description = $"Keep {source1.ModName}",
                    PreferredSource = source1
                },
                new()
                {
                    Id = "use_new",
                    Strategy = ResolutionStrategy.UseNew,
                    Description = $"Use {source2.ModName}",
                    PreferredSource = source2
                }
            };

            if (includeMerge)
            {
                options.Insert(0, new ConflictResolutionOption
                {
                    Id = "merge",
                    Strategy = ResolutionStrategy.Merge,
                    Description = "Attempt to merge both changes"
                });
            }

            return options.ToArray();
        }

        public override string ToString() => 
            $"[{Severity}] {Type}: {Description}";
    }

    /// <summary>
    /// Represents an option for resolving a mod conflict.
    /// </summary>
    public class ConflictResolutionOption
    {
        /// <summary>
        /// Unique identifier for this resolution option.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// The strategy this option uses.
        /// </summary>
        [JsonPropertyName("strategy")]
        public ResolutionStrategy Strategy { get; init; }

        /// <summary>
        /// Human-readable description of this option.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// The mod source that will be used if this option is selected.
        /// Null for merge strategy.
        /// </summary>
        [JsonPropertyName("preferredSource")]
        public ModSource? PreferredSource { get; init; }

        public override string ToString() => $"{Strategy}: {Description}";
    }

    /// <summary>
    /// Result of applying a conflict resolution.
    /// </summary>
    public class ConflictResolutionResult
    {
        /// <summary>
        /// ID of the conflict that was resolved.
        /// </summary>
        [JsonPropertyName("conflictId")]
        public string ConflictId { get; init; } = string.Empty;

        /// <summary>
        /// Whether the resolution was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        /// <summary>
        /// The strategy that was used for resolution.
        /// </summary>
        [JsonPropertyName("usedStrategy")]
        public ResolutionStrategy UsedStrategy { get; init; }

        /// <summary>
        /// The mod source that "won" the conflict.
        /// </summary>
        [JsonPropertyName("winningSource")]
        public ModSource? WinningSource { get; init; }

        /// <summary>
        /// Error message if resolution failed.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Files that were affected by this resolution.
        /// </summary>
        [JsonPropertyName("resolvedFiles")]
        public IReadOnlyList<string> ResolvedFiles { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Creates a successful resolution result.
        /// </summary>
        public static ConflictResolutionResult Successful(
            string conflictId,
            ResolutionStrategy strategy,
            ModSource winner,
            IEnumerable<string>? resolvedFiles = null)
        {
            return new ConflictResolutionResult
            {
                ConflictId = conflictId,
                Success = true,
                UsedStrategy = strategy,
                WinningSource = winner,
                ResolvedFiles = (IReadOnlyList<string>?)resolvedFiles?.ToList() ?? Array.Empty<string>()
            };
        }

        /// <summary>
        /// Creates a failed resolution result.
        /// </summary>
        public static ConflictResolutionResult Failed(
            string conflictId,
            ResolutionStrategy attemptedStrategy,
            string errorMessage)
        {
            return new ConflictResolutionResult
            {
                ConflictId = conflictId,
                Success = false,
                UsedStrategy = attemptedStrategy,
                ErrorMessage = errorMessage
            };
        }

        public override string ToString() => 
            Success 
                ? $"Resolved {ConflictId} using {UsedStrategy} → {WinningSource?.ModName}"
                : $"Failed {ConflictId}: {ErrorMessage}";
    }
}
