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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Service for detecting Dota 2 installation folder.
    /// </summary>
    public class DetectionService : IDetectionService
    {
        private readonly IAppLogger? _logger;

        public DetectionService(IAppLogger? logger = null)
        {
            _logger = logger; // Logger is optional for DI compatibility
        }

        /// <summary>
        /// Try to automatically detect Dota 2 installation folder via registry and Steam libraries.
        /// </summary>
        public async Task<string?> AutoDetectAsync()
        {
            _logger?.Log("Auto-detecting Dota 2 folder...");

            string? steamReg = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) as string;
            if (!string.IsNullOrEmpty(steamReg))
            {
                string steamBase = Path.GetDirectoryName(steamReg)!;
                string vdfPath = Path.Combine(steamBase, "steamapps", "LibraryFolders.vdf");

                if (File.Exists(vdfPath))
                {
                    string vdfContent = await File.ReadAllTextAsync(vdfPath).ConfigureAwait(false);
                    MatchCollection libraryPaths = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");
                    foreach (Match match in libraryPaths.Cast<Match>())
                    {
                        string path = match.Groups[1].Value;
                        string dotaPath = Path.Combine(path, "steamapps", "common", "dota 2 beta", "game", "bin", "win64", "dota2.exe");

                        if (File.Exists(dotaPath))
                        {
                            string target = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(dotaPath))))!;
                            _logger?.Log($"Dota 2 folder detected via Steam libraries: {target}");
                            return target;
                        }
                    }

                    _logger?.Log("Dota 2 not found in Steam library folders.");
                }
                else
                {
                    _logger?.Log($"LibraryFolders.vdf not found at: {vdfPath}");
                }
            }

            string? regOutput = RunRegQuery(@"HKEY_CLASSES_ROOT\dota2\Shell\Open\Command", "/ve");
            if (!string.IsNullOrEmpty(regOutput))
            {
                Match regMatch = Regex.Match(regOutput, @"REG_SZ\s+(.+)", RegexOptions.IgnoreCase);
                if (regMatch.Success)
                {
                    string regValue = regMatch.Groups[1].Value.Trim();
                    Match exeMatch = Regex.Match(regValue, @"^""([^""]+dota2\.exe)""");
                    if (exeMatch.Success)
                    {
                        string dotaExe = exeMatch.Groups[1].Value;
                        if (File.Exists(dotaExe))
                        {
                            string targetPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(dotaExe))))!;
                            if (targetPath.EndsWith("game", StringComparison.OrdinalIgnoreCase))
                                targetPath = Path.GetDirectoryName(targetPath)!;
                            else if (targetPath.EndsWith("bin", StringComparison.OrdinalIgnoreCase))
                                targetPath = Path.GetDirectoryName(Path.GetDirectoryName(targetPath))!;

                            _logger?.Log($"Dota 2 folder detected via HKEY_CLASSES_ROOT: {targetPath}");
                            return targetPath;
                        }
                    }
                }
            }

            string? steamUninstallPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570", "InstallLocation", null) as string;
            if (string.IsNullOrEmpty(steamUninstallPath) || !File.Exists(Path.Combine(steamUninstallPath, "game", "bin", "win64", "dota2.exe")))
            {
                steamUninstallPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570", "InstallLocation", null) as string;
            }

            if (!string.IsNullOrEmpty(steamUninstallPath) && File.Exists(Path.Combine(steamUninstallPath, "game", "bin", "win64", "dota2.exe")))
            {
                _logger?.Log($"Dota 2 folder detected via HKLM Uninstall: {steamUninstallPath}");
                return steamUninstallPath;
            }

            if (!string.IsNullOrEmpty(steamReg))
            {
                string steamBase = Path.GetDirectoryName(steamReg)!;
                string defaultDotaPath = Path.Combine(steamBase, "steamapps", "common", "dota 2 beta");

                if (File.Exists(Path.Combine(defaultDotaPath, "game", "bin", "win64", "dota2.exe")))
                {
                    _logger?.Log($"Dota 2 folder detected via default Steam path: {defaultDotaPath}");
                    return defaultDotaPath;
                }
            }

            _logger?.Log("No Dota 2 folder found via any auto-detection method.");
            return null;
        }

        /// <summary>
        /// Opens a folder picker dialog for manual Dota 2 folder selection.
        /// </summary>
        public string? ManualDetect()
        {
            using var folderBrowser = new FolderBrowserDialog
            {
                Description = "Select your Dota 2 Folder (e.g., C:\\Program Files (x86)\\Steam\\steamapps\\common\\dota 2 beta)"
            };

            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                string selectedPath = folderBrowser.SelectedPath;
                string folderName = Path.GetFileName(selectedPath);

                if (string.Equals(folderName, "dota 2 beta", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.Log($"Dota 2 folder manually selected: {selectedPath}");
                    return selectedPath;
                }

                _logger?.Log("Invalid folder selected: Not 'dota 2 beta'.");
                MessageBox.Show("Please select the folder named 'dota 2 beta'.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                _logger?.Log("Manual detection canceled.");
            }

            return null;
        }

        /// <summary>
        /// Validates if the given path is a valid Dota 2 installation.
        /// Checks for the existence of dota2.exe at the expected location.
        /// </summary>
        /// <param name="path">The path to validate (should be "dota 2 beta" folder)</param>
        /// <returns>True if the path contains a valid Dota 2 installation</returns>
        public bool ValidateDotaPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string dota2Exe = Path.Combine(path, "game", "bin", "win64", "dota2.exe");
                return File.Exists(dota2Exe);
            }
            catch
            {
                return false;
            }
        }

        private string? RunRegQuery(string key, string args)
        {
            using Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $"query \"{key}\" {args}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
    }
}

