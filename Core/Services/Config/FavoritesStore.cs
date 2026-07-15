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

namespace ArdysaModsTools.Core.Services
{
    public static class FavoritesStore
    {
        private static readonly object _lock = new object();
        private const string FileName = "favorites.json";

        private static string GetStorageFolder()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "ArdysaModsTools");
            Directory.CreateDirectory(appDataFolder);
            return appDataFolder;
        }

        public static HashSet<string> Load()
        {
            lock (_lock)
            {
                try
                {
                    var folder = GetStorageFolder();
                    var filePath = Path.Combine(folder, FileName);
                    
                    if (!File.Exists(filePath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var json = File.ReadAllText(filePath);
                    var arr = JsonSerializer.Deserialize<string[]>(json);
                    return arr == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(arr, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public static void Save(HashSet<string> favorites)
        {
            if (favorites == null) return;
            lock (_lock)
            {
                try
                {
                    var folder = GetStorageFolder();
                    var filePath = Path.Combine(folder, FileName);
                    
                    var arr = new List<string>(favorites);
                    var json = JsonSerializer.Serialize(arr, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                }
                catch
                {
                }
            }
        }
    }
}

