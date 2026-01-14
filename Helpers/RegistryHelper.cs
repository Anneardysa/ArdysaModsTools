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
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Helpers
{
    public static class RegistryHelper
    {
        public static string? GetSteamExe()
        {
            try
            {
                return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) as string;
            }
            catch (Exception ex)
            {
                ArdysaModsTools.Core.Services.FallbackLogger.Log($"GetSteamExe error: {ex.Message}");
                return null;
            }
        }

        public static IEnumerable<string> GetSteamLibraryPaths(string steamExe)
        {
            var result = new List<string>();
            try
            {
                string steamBase = Path.GetDirectoryName(steamExe) ?? "";
                string vdfPath = Path.Combine(steamBase, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdfPath)) return result;

                string text = File.ReadAllText(vdfPath);
                foreach (Match m in Regex.Matches(text, @"""path""\s*""([^""]+)"""))
                {
                    result.Add(m.Groups[1].Value);
                }
                result.Add(steamBase);
            }
            catch { }
            return result;
        }

        public static string? GetDotaFromClassRoot()
        {
            try
            {
                string? value = Registry.GetValue(@"HKEY_CLASSES_ROOT\dota2\Shell\Open\Command", "", null) as string;
                if (string.IsNullOrEmpty(value)) return null;
                Match m = Regex.Match(value, @"""([^""]+dota2\.exe)""", RegexOptions.IgnoreCase);
                if (!m.Success) return null;
                string exe = m.Groups[1].Value;
                if (!File.Exists(exe)) return null;

                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(exe)!, "..", "..", "..", ".."));
            }
            catch { return null; }
        }

        public static string? GetDotaFromUninstall()
        {
            string[] keys =
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570"
            };
            foreach (string k in keys)
            {
                try
                {
                    string? val = Registry.GetValue(k, "InstallLocation", null) as string;
                    if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                        return val;
                }
                catch { }
            }
            return null;
        }
    }
}

