using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ArdysaModsTools.Core.Services
{
    // Simple file-based favorites store; thread-safe for basic app usage.
    public static class FavoritesStore
    {
        private static readonly object _lock = new object();
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdysaModsTools");
        private static readonly string FilePath = Path.Combine(AppFolder, "favorites.json");

        public static HashSet<string> Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(AppFolder)) Directory.CreateDirectory(AppFolder);
                    if (!File.Exists(FilePath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var json = File.ReadAllText(FilePath);
                    var arr = JsonSerializer.Deserialize<string[]>(json);
                    return arr == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(arr, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    // On error return empty set — do not throw to keep UI stable
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
                    if (!Directory.Exists(AppFolder)) Directory.CreateDirectory(AppFolder);
                    var arr = new List<string>(favorites);
                    var json = JsonSerializer.Serialize(arr, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(FilePath, json);
                }
                catch
                {
                    // swallow errors; optionally log using your FallbackLogger
                }
            }
        }
    }
}
