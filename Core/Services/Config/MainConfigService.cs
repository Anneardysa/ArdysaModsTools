using System;
using System.IO;
using System.Text.Json;

namespace ArdysaModsTools.Core.Services
{
    public class MainConfigService
    {
        private readonly string _defaultDir;
        private readonly string _configFile;

        public MainConfigService()
        {
            _defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArdysaModsTools");

            _configFile = Path.Combine(_defaultDir, "config.json");

            if (!Directory.Exists(_defaultDir))
                Directory.CreateDirectory(_defaultDir);
        }

        private class ConfigData
        {
            public string? LastTargetPath { get; set; }
            public string? AppVersion { get; set; }
        }

        // === Save or clear the last target path ===
        public void SetLastTargetPath(string? path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    if (File.Exists(_configFile))
                        File.Delete(_configFile);
                    return;
                }

                var data = new ConfigData { LastTargetPath = path };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainConfigService] Failed to save: {ex.Message}");
            }
        }

        // === Load the last target path ===
        public string? GetLastTargetPath()
        {
            try
            {
                if (!File.Exists(_configFile))
                    return null;

                string json = File.ReadAllText(_configFile);
                var data = JsonSerializer.Deserialize<ConfigData>(json);
                return data?.LastTargetPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
