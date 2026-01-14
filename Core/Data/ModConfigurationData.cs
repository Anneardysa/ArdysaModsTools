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
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Core.Data
{
    /// <summary>
    /// Centralized configuration data for mod URLs.
    /// Now uses remote configuration from GitHub.
    /// </summary>
    public static class ModConfigurationData
    {
        /// <summary>
        /// Gets the URL for a given selection key from the remote config.
        /// </summary>
        public static string? GetUrl(string category, string key)
        {
            // Map category names to option IDs
            var optionId = category switch
            {
                "Weather" => "Weather",
                "Map" => "Map",
                "Music" => "Music",
                "Emblems" => "Emblems",
                "Shader" => "Shader",
                "AtkModifier" => "atkModifier",
                "RadiantCreep" => "RadiantCreep",
                "DireCreep" => "DireCreep",
                "RadiantSiege" => "RadiantSiege",
                "DireSiege" => "DireSiege",
                "HUD" => "HUD",
                "Versus" => "Versus",
                "River" => "River",
                "RadiantTower" => "RadiantTower",
                "DireTower" => "DireTower",
                _ => category
            };

            return RemoteMiscConfigService.GetUrl(optionId, key);
        }

        // Legacy dictionary accessors for backward compatibility
        // These are now dynamically generated from remote config

        public static Dictionary<string, string> Weather => GetDictionary("Weather");
        public static Dictionary<string, string> Map => GetDictionary("Map");
        public static Dictionary<string, string> Music => GetDictionary("Music");
        public static Dictionary<string, string> Emblems => GetDictionary("Emblems");
        public static Dictionary<string, string> Shaders => GetDictionary("Shader");
        public static Dictionary<string, string> AtkModifier => GetDictionary("atkModifier");
        public static Dictionary<string, string> RadiantCreep => GetDictionary("RadiantCreep");
        public static Dictionary<string, string> DireCreep => GetDictionary("DireCreep");
        public static Dictionary<string, string> RadiantSiege => GetDictionary("RadiantSiege");
        public static Dictionary<string, string> DireSiege => GetDictionary("DireSiege");
        public static Dictionary<string, string> Hud => GetDictionary("HUD");
        public static Dictionary<string, string> Versus => GetDictionary("Versus");
        public static Dictionary<string, string> River => GetDictionary("River");
        public static Dictionary<string, string> RadiantTower => GetDictionary("RadiantTower");
        public static Dictionary<string, string> DireTower => GetDictionary("DireTower");

        /// <summary>
        /// Builds a dictionary from remote config for a specific option.
        /// </summary>
        private static Dictionary<string, string> GetDictionary(string optionId)
        {
            var config = RemoteMiscConfigService.GetConfigSync();
            var option = config.Options.FirstOrDefault(o => o.Id == optionId);

            if (option == null)
                return new Dictionary<string, string>();

            return option.Choices
                .Where(c => c.Url != null)
                .ToDictionary(c => c.Name, c => c.Url!);
        }
    }
}

