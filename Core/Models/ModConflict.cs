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
    public class ModSource
    {
        [JsonPropertyName("modId")]
        public string ModId { get; init; } = string.Empty;

        [JsonPropertyName("modName")]
        public string ModName { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 100;

        [JsonPropertyName("appliedAt")]
        public DateTime AppliedAt { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("affectedFiles")]
        public List<string> AffectedFiles { get; init; } = new();

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

    public class ModConflict
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

        [JsonPropertyName("type")]
        public ConflictType Type { get; init; }

        [JsonPropertyName("severity")]
        public ConflictSeverity Severity { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("affectedFiles")]
        public IReadOnlyList<string> AffectedFiles { get; init; } = Array.Empty<string>();

        [JsonPropertyName("conflictingSources")]
        public IReadOnlyList<ModSource> ConflictingSources { get; init; } = Array.Empty<ModSource>();

        [JsonPropertyName("availableResolutions")]
        public IReadOnlyList<ConflictResolutionOption> AvailableResolutions { get; init; } = Array.Empty<ConflictResolutionOption>();

        [JsonPropertyName("selectedResolution")]
        public ConflictResolutionOption? SelectedResolution { get; set; }

        [JsonIgnore]
        public bool IsResolved => SelectedResolution != null;

        [JsonIgnore]
        public bool RequiresUserIntervention => Severity == ConflictSeverity.Critical;

        [JsonIgnore]
        public ModSource? HighestPrioritySource => 
            ConflictingSources.OrderBy(s => s.Priority).FirstOrDefault();

        [JsonIgnore]
        public ModSource? MostRecentSource => 
            ConflictingSources.OrderByDescending(s => s.AppliedAt).FirstOrDefault();

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

    public class ConflictResolutionOption
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("strategy")]
        public ResolutionStrategy Strategy { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("preferredSource")]
        public ModSource? PreferredSource { get; init; }

        public override string ToString() => $"{Strategy}: {Description}";
    }

    public class ConflictResolutionResult
    {
        [JsonPropertyName("conflictId")]
        public string ConflictId { get; init; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("usedStrategy")]
        public ResolutionStrategy UsedStrategy { get; init; }

        [JsonPropertyName("winningSource")]
        public ModSource? WinningSource { get; init; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; init; }

        [JsonPropertyName("resolvedFiles")]
        public IReadOnlyList<string> ResolvedFiles { get; init; } = Array.Empty<string>();

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
