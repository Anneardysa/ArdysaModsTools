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
using System.IO;
using System.Text.Json;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Single source-of-truth for small application settings (LastTargetPath, AppVersion, etc).
    /// Thread-safe enough for simple usage.
    /// </summary>
    public sealed class ConfigService
    {
        private readonly string _configDir;
        private readonly string _configFile;
        private readonly object _lock = new();

        public ConfigService()
        {
            _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdysaModsTools");
            _configFile = Path.Combine(_configDir, "config.json");
            try { if (!Directory.Exists(_configDir)) Directory.CreateDirectory(_configDir); } catch { }
        }

        private class Data
        {
            public string? LastTargetPath { get; set; }
            public string? AppVersion { get; set; }
        }

        private Data Load()
        {
            try
            {
                lock (_lock)
                {
                    if (!File.Exists(_configFile)) return new Data();
                    var text = File.ReadAllText(_configFile);
                    var d = JsonSerializer.Deserialize<Data>(text) ?? new Data();
                    return d;
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ConfigService.Load failed: {ex.Message}");
                return new Data();
            }
        }

        private void Save(Data d)
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configFile, json);
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ConfigService.Save failed: {ex.Message}");
            }
        }

        public string? GetLastTargetPath()
        {
            return Load().LastTargetPath;
        }

        public void SetLastTargetPath(string? path)
        {
            var d = Load();
            d.LastTargetPath = path;
            Save(d);
        }

        public string? GetAppVersion()
        {
            return Load().AppVersion;
        }

        public void SetAppVersion(string? v)
        {
            var d = Load();
            d.AppVersion = v;
            Save(d);
        }
    }
}

