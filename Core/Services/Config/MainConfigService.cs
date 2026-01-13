using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Config
{
    /// <summary>
    /// Service for managing application configuration.
    /// </summary>
    public class MainConfigService : IConfigService
    {
        private readonly string _defaultDir;
        private readonly string _configFile;
        private ConfigData _data;
        private bool _isDirty;

        public MainConfigService()
        {
            _defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArdysaModsTools");

            _configFile = Path.Combine(_defaultDir, "config.json");

            if (!Directory.Exists(_defaultDir))
                Directory.CreateDirectory(_defaultDir);

            _data = LoadConfig();
        }

        private class ConfigData
        {
            public string? LastTargetPath { get; set; }
            public string? AppVersion { get; set; }
            public Dictionary<string, object>? CustomSettings { get; set; }
        }

        /// <inheritdoc />
        public void SetLastTargetPath(string? path)
        {
            _data.LastTargetPath = path;
            _isDirty = true;
            Save();
        }

        /// <inheritdoc />
        public string? GetLastTargetPath()
        {
            return _data.LastTargetPath;
        }

        /// <inheritdoc />
        public T GetValue<T>(string key, T defaultValue)
        {
            if (_data.CustomSettings == null || !_data.CustomSettings.TryGetValue(key, out var value))
                return defaultValue;

            try
            {
                // Handle JsonElement conversion
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
                }

                if (value is T typedValue)
                    return typedValue;

                // Try to convert
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <inheritdoc />
        public void SetValue<T>(string key, T value)
        {
            _data.CustomSettings ??= new Dictionary<string, object>();
            _data.CustomSettings[key] = value!;
            _isDirty = true;
        }

        /// <inheritdoc />
        public void Save()
        {
            if (!_isDirty) return;

            try
            {
                if (string.IsNullOrEmpty(_data.LastTargetPath) && 
                    (_data.CustomSettings == null || _data.CustomSettings.Count == 0))
                {
                    // No data to save, delete config file if exists
                    if (File.Exists(_configFile))
                        File.Delete(_configFile);
                    return;
                }

                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_configFile, json);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainConfigService] Failed to save: {ex.Message}");
            }
        }

        private ConfigData LoadConfig()
        {
            try
            {
                if (!File.Exists(_configFile))
                    return new ConfigData();

                string json = File.ReadAllText(_configFile);
                return JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
            }
            catch
            {
                return new ConfigData();
            }
        }
    }
}
