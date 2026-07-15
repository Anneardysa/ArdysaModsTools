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
    public class HeroExtractionLog
    {
        [JsonPropertyName("installedSets")]
        public List<HeroSetEntry> InstalledSets { get; set; } = new();

        public static HeroExtractionLog? Load(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            if (!File.Exists(logPath)) return null;

            try
            {
                var json = File.ReadAllText(logPath);
                return JsonSerializer.Deserialize<HeroExtractionLog>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                Core.Helpers.SafeTempPathHelper.HideDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(logPath, json);
        }

        public static void Delete(string targetPath)
        {
            var logPath = GetLogPath(targetPath);
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }

        public static string GetLogPath(string targetPath)
        {
            return Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", "hero_extraction_log.json");
        }
    }

    public class HeroSetEntry
    {
        [JsonPropertyName("heroId")]
        public string HeroId { get; set; } = string.Empty;

        [JsonPropertyName("setName")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new();
    }
}

