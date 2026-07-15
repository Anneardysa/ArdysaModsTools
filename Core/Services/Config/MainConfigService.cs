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

        public void SetLastTargetPath(string? path)
        {
            _data.LastTargetPath = path;
            _isDirty = true;
            Save();
        }

        public string? GetLastTargetPath()
        {
            return _data.LastTargetPath;
        }

        public T GetValue<T>(string key, T defaultValue)
        {
            if (_data.CustomSettings == null || !_data.CustomSettings.TryGetValue(key, out var value))
                return defaultValue;

            try
            {
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
                }

                if (value is T typedValue)
                    return typedValue;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public void SetValue<T>(string key, T value)
        {
            _data.CustomSettings ??= new Dictionary<string, object>();
            _data.CustomSettings[key] = value!;
            _isDirty = true;
        }

        private const string KeyMinimizeToTray = "MinimizeToTray";
        private const string KeyShowNotifications = "ShowNotifications";
        private const string KeyPreloadAssetsOnLaunch = "PreloadAssetsOnLaunch";
        private const string KeyAutoDetectOnStartup = "AutoDetectOnStartup";
        private const string KeyLanguage = "Language";
        private const string KeySupportPromptSnoozeDate = "SupportPromptSnoozeDate";

        public bool MinimizeToTray
        {
            get => GetValue(KeyMinimizeToTray, false);
            set { SetValue(KeyMinimizeToTray, value); Save(); }
        }

        public bool ShowNotifications
        {
            get => GetValue(KeyShowNotifications, true);
            set { SetValue(KeyShowNotifications, value); Save(); }
        }

        public bool PreloadAssetsOnLaunch
        {
            get => GetValue(KeyPreloadAssetsOnLaunch, true);
            set { SetValue(KeyPreloadAssetsOnLaunch, value); Save(); }
        }

        public bool AutoDetectOnStartup
        {
            get => GetValue(KeyAutoDetectOnStartup, true);
            set { SetValue(KeyAutoDetectOnStartup, value); Save(); }
        }

        public string? Language
        {
            get => GetValue<string?>(KeyLanguage, null);
            set { SetValue(KeyLanguage, value); Save(); }
        }

        public string? SupportPromptSnoozeDate
        {
            get => GetValue<string?>(KeySupportPromptSnoozeDate, null);
            set { SetValue(KeySupportPromptSnoozeDate, value); Save(); }
        }

        public void Save()
        {
            if (!_isDirty) return;

            try
            {
                if (string.IsNullOrEmpty(_data.LastTargetPath) && 
                    (_data.CustomSettings == null || _data.CustomSettings.Count == 0))
                {
                    if (File.Exists(_configFile))
                        File.Delete(_configFile);
                    _isDirty = false;
                    return;
                }

                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                string tempFile = _configFile + ".tmp";
                File.WriteAllText(tempFile, json);
                
                if (File.Exists(_configFile))
                    File.Delete(_configFile);
                File.Move(tempFile, _configFile);
                
                _isDirty = false;
            }
            catch (Exception ex)
            {
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

