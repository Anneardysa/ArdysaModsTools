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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Specifies the type of conflict between mods.
    /// Used to categorize and appropriately handle different conflict scenarios.
    /// </summary>
    public enum ConflictType
    {
        /// <summary>
        /// No conflict detected.
        /// </summary>
        None = 0,

        /// <summary>
        /// Same file path with different content from multiple mods.
        /// Example: Two mods modifying the same weather config file.
        /// Resolution: Priority-based or user choice.
        /// </summary>
        File = 1,

        /// <summary>
        /// Overlapping script or KeyValue modifications.
        /// Example: Two mods changing the same ability values.
        /// Resolution: Merge if compatible, otherwise priority-based.
        /// </summary>
        Script = 2,

        /// <summary>
        /// Same VPK entry from multiple sources.
        /// Example: Two mods including the same hero model.
        /// Resolution: Priority-based selection.
        /// </summary>
        Asset = 3,

        /// <summary>
        /// Conflicting game configuration settings.
        /// Example: Two mods setting different default terrain.
        /// Resolution: Last-write or priority-based.
        /// </summary>
        Configuration = 4
    }

    /// <summary>
    /// Indicates the severity level of a mod conflict.
    /// Determines whether automatic or manual resolution is required.
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>
        /// No conflict or negligible difference.
        /// No action required.
        /// </summary>
        None = 0,

        /// <summary>
        /// Minor cosmetic conflict that can be auto-resolved.
        /// Example: Duplicate texture files with identical content.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Functional conflict requiring priority-based resolution.
        /// Example: Overlapping script modifications.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// Significant conflict that may require user intervention.
        /// Example: Incompatible game mode configurations.
        /// </summary>
        High = 3,

        /// <summary>
        /// Cannot proceed without explicit resolution.
        /// Example: Mutually exclusive mods that break functionality.
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Strategy for resolving mod conflicts.
    /// Each strategy defines how conflicting mods are prioritized.
    /// </summary>
    public enum ResolutionStrategy
    {
        /// <summary>
        /// Mod with higher priority (lower priority number) wins.
        /// This is the default and most predictable strategy.
        /// </summary>
        HigherPriority = 0,

        /// <summary>
        /// Mod with lower priority (higher priority number) wins.
        /// Useful for layering effects where later mods should override.
        /// </summary>
        LowerPriority = 1,

        /// <summary>
        /// Most recently applied mod wins.
        /// Based on the AppliedAt timestamp of ModSource.
        /// </summary>
        MostRecent = 2,

        /// <summary>
        /// Attempt to merge compatible changes.
        /// Only works for Script and Configuration conflict types.
        /// Falls back to HigherPriority if merge fails.
        /// </summary>
        Merge = 3,

        /// <summary>
        /// Preserve the existing/current state.
        /// New mod's conflicting changes are discarded.
        /// </summary>
        KeepExisting = 4,

        /// <summary>
        /// Apply the new mod, discard existing conflicting content.
        /// </summary>
        UseNew = 5,

        /// <summary>
        /// Require user to make an explicit choice.
        /// Used for Critical severity conflicts.
        /// </summary>
        Interactive = 6
    }
}
