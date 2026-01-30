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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents the priority configuration for a single mod.
    /// Lower priority number means higher precedence in conflicts.
    /// </summary>
    public class ModPriority
    {
        /// <summary>
        /// Unique identifier matching ModSource.ModId.
        /// </summary>
        [JsonPropertyName("modId")]
        public string ModId { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable name of the mod.
        /// </summary>
        [JsonPropertyName("modName")]
        public string ModName { get; init; } = string.Empty;

        /// <summary>
        /// Category this mod belongs to (e.g., "Weather", "River").
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Priority level. Lower = higher priority (like z-index).
        /// Default: 100. Range: 1-999.
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// If true, this mod's priority cannot be changed by users.
        /// Used for critical system mods.
        /// </summary>
        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }

        /// <summary>
        /// Optional description or notes for this priority setting.
        /// </summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        public override string ToString() => $"{ModName} [{Category}]: Priority {Priority}";
    }

    /// <summary>
    /// Configuration for mod priorities and default conflict resolution strategies.
    /// Persisted to mod_priority.json in the _temp folder.
    /// </summary>
    public class ModPriorityConfig
    {
        private const string ConfigFileName = "mod_priority.json";

        /// <summary>
        /// When this configuration was last modified.
        /// </summary>
        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// List of mod priorities. Order in list is secondary sort key.
        /// </summary>
        [JsonPropertyName("priorities")]
        public List<ModPriority> Priorities { get; set; } = new();

        /// <summary>
        /// Default strategy when auto-resolving conflicts.
        /// </summary>
        [JsonPropertyName("defaultStrategy")]
        public ResolutionStrategy DefaultStrategy { get; set; } = ResolutionStrategy.HigherPriority;

        /// <summary>
        /// If true, automatically resolve Low and Medium severity conflicts.
        /// If false, prompt user for all conflicts.
        /// </summary>
        [JsonPropertyName("autoResolveNonBreaking")]
        public bool AutoResolveNonBreaking { get; set; } = true;

        /// <summary>
        /// Category-specific default strategies (optional override).
        /// Key: category name, Value: strategy for that category.
        /// </summary>
        [JsonPropertyName("categoryStrategies")]
        public Dictionary<string, ResolutionStrategy> CategoryStrategies { get; set; } = new();

        /// <summary>
        /// Gets the priority for a specific mod, or default if not found.
        /// </summary>
        public int GetPriority(string modId)
        {
            var entry = Priorities.Find(p => p.ModId == modId);
            return entry?.Priority ?? 100; // Default priority
        }

        /// <summary>
        /// Sets or updates the priority for a mod.
        /// </summary>
        public void SetPriority(string modId, string modName, string category, int priority)
        {
            var existing = Priorities.Find(p => p.ModId == modId);
            if (existing != null)
            {
                if (existing.IsLocked) return; // Can't modify locked priorities
                existing.Priority = Math.Clamp(priority, 1, 999);
            }
            else
            {
                Priorities.Add(new ModPriority
                {
                    ModId = modId,
                    ModName = modName,
                    Category = category,
                    Priority = Math.Clamp(priority, 1, 999)
                });
            }
            LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the resolution strategy for a category, falling back to default.
        /// </summary>
        public ResolutionStrategy GetStrategyForCategory(string category)
        {
            return CategoryStrategies.TryGetValue(category, out var strategy) 
                ? strategy 
                : DefaultStrategy;
        }

        /// <summary>
        /// Gets the path for the config file.
        /// </summary>
        public static string GetConfigPath(string targetPath)
        {
            return Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", ConfigFileName);
        }

        /// <summary>
        /// Loads the priority configuration from disk.
        /// </summary>
        public static ModPriorityConfig? Load(string targetPath)
        {
            var configPath = GetConfigPath(targetPath);
            if (!File.Exists(configPath)) return null;

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<ModPriorityConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the priority configuration to disk.
        /// </summary>
        public void Save(string targetPath)
        {
            var configPath = GetConfigPath(targetPath);
            var dir = Path.GetDirectoryName(configPath);
            
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                // Hide the _temp folder
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                }
                catch { /* Ignore if can't set hidden */ }
            }

            LastModified = DateTime.UtcNow;
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(configPath, json);
        }

        /// <summary>
        /// Creates a default configuration with sensible defaults.
        /// </summary>
        public static ModPriorityConfig CreateDefault()
        {
            return new ModPriorityConfig
            {
                DefaultStrategy = ResolutionStrategy.HigherPriority,
                AutoResolveNonBreaking = true,
                CategoryStrategies = new Dictionary<string, ResolutionStrategy>
                {
                    // Weather and River can usually be merged/layered
                    { "Weather", ResolutionStrategy.MostRecent },
                    { "River", ResolutionStrategy.MostRecent }
                }
            };
        }
    }
}
