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
    /// Tracks installed Misc mods and their files for cleanup.
    /// Stored at: game/_ArdysaMods/_temp/misc_extraction_log.json
    /// </summary>
    public class MiscExtractionLog
    {
        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "GenerateOnly";

        [JsonPropertyName("selections")]
        public Dictionary<string, string> Selections { get; set; } = new();

        /// <summary>
        /// Installed file paths (relative) for file-based mods.
        /// Key: category (Emblems, Shader, River, AtkModifier)
        /// Value: list of relative file paths installed
        /// </summary>
        [JsonPropertyName("installedFiles")]
        public Dictionary<string, List<string>> InstalledFiles { get; set; } = new();

        /// <summary>
        /// Loads the extraction log from the specified target path.
        /// </summary>
        public static MiscExtractionLog? Load(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            if (!File.Exists(logPath)) return null;

            try
            {
                var json = File.ReadAllText(logPath);
                return JsonSerializer.Deserialize<MiscExtractionLog>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the extraction log to the specified target path.
        /// </summary>
        public void Save(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                // Hide the _temp folder from users
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                }
                catch { /* Ignore if can't set hidden */ }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(logPath, json);
        }

        /// <summary>
        /// Deletes the extraction log if it exists.
        /// </summary>
        public static void Delete(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }

        /// <summary>
        /// Gets the log file path for the given target path.
        /// </summary>
        public static string GetLogPath(string targetPath)
        {
            return Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", "misc_extraction_log.json");
        }

        /// <summary>
        /// Adds installed files for a category.
        /// </summary>
        public void AddFiles(string category, IEnumerable<string> relativePaths)
        {
            if (!InstalledFiles.ContainsKey(category))
                InstalledFiles[category] = new List<string>();
            
            InstalledFiles[category].AddRange(relativePaths);
        }

        /// <summary>
        /// Gets installed files for a category.
        /// </summary>
        public List<string> GetFiles(string category)
        {
            return InstalledFiles.TryGetValue(category, out var files) ? files : new List<string>();
        }

        /// <summary>
        /// Clears installed files for a category.
        /// </summary>
        public void ClearFiles(string category)
        {
            if (InstalledFiles.ContainsKey(category))
                InstalledFiles[category].Clear();
        }
    }
}

