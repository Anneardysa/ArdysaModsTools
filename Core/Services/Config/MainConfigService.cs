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
                
                // Atomic write: write to temp file first, then move
                string tempFile = _configFile + ".tmp";
                File.WriteAllText(tempFile, json);
                
                // Replace original with temp file atomically
                if (File.Exists(_configFile))
                    File.Delete(_configFile);
                File.Move(tempFile, _configFile);
                
                _isDirty = false;
            }
            catch (Exception ex)
            {
                // Use FallbackLogger instead of Console.WriteLine
                FallbackLogger.Log($"[MainConfigService] Failed to save config: {ex.Message}");
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

