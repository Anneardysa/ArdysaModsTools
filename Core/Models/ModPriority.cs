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
    public class ModPriority
    {
        [JsonPropertyName("modId")]
        public string ModId { get; init; } = string.Empty;

        [JsonPropertyName("modName")]
        public string ModName { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 100;

        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        public override string ToString() => $"{ModName} [{Category}]: Priority {Priority}";
    }

    public class ModPriorityConfig
    {
        private const string ConfigFileName = "mod_priority.json";

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("priorities")]
        public List<ModPriority> Priorities { get; set; } = new();

        [JsonPropertyName("defaultStrategy")]
        public ResolutionStrategy DefaultStrategy { get; set; } = ResolutionStrategy.HigherPriority;

        [JsonPropertyName("autoResolveNonBreaking")]
        public bool AutoResolveNonBreaking { get; set; } = true;

        [JsonPropertyName("categoryStrategies")]
        public Dictionary<string, ResolutionStrategy> CategoryStrategies { get; set; } = new();

        public int GetPriority(string modId)
        {
            var entry = Priorities.Find(p => p.ModId == modId);
            return entry?.Priority ?? 100;
        }

        public void SetPriority(string modId, string modName, string category, int priority)
        {
            var existing = Priorities.Find(p => p.ModId == modId);
            if (existing != null)
            {
                if (existing.IsLocked) return;
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

        public ResolutionStrategy GetStrategyForCategory(string category)
        {
            return CategoryStrategies.TryGetValue(category, out var strategy) 
                ? strategy 
                : DefaultStrategy;
        }

        public static string GetConfigPath(string targetPath)
        {
            return Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", ConfigFileName);
        }

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

        public void Save(string targetPath)
        {
            var configPath = GetConfigPath(targetPath);
            var dir = Path.GetDirectoryName(configPath);
            
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                Core.Helpers.SafeTempPathHelper.HideDirectory(dir);
            }

            LastModified = DateTime.UtcNow;
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(configPath, json);
        }

        public static ModPriorityConfig CreateDefault()
        {
            return new ModPriorityConfig
            {
                DefaultStrategy = ResolutionStrategy.HigherPriority,
                AutoResolveNonBreaking = true,
                CategoryStrategies = new Dictionary<string, ResolutionStrategy>
                {
                    { "Weather", ResolutionStrategy.MostRecent },
                    { "River", ResolutionStrategy.MostRecent }
                }
            };
        }
    }
}
