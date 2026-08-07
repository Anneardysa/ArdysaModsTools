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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public sealed class SteamAppStateService : ISteamAppStateService
    {
        private const string Dota2AppId = "570";

        private static readonly Regex ValuePattern = new(
            "\"(StateFlags|BytesToDownload|BytesDownloaded)\"\\s*\"(\\d+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IAppLogger? _logger;

        public SteamAppStateService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public SteamAppState Read(string? targetPath)
        {
            string? manifest = ResolveManifestPath(targetPath);
            if (manifest == null)
                return SteamAppState.Unknown;

            try
            {
                using var fs = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string text = reader.ReadToEnd();

                long flags = 0, toDownload = 0, downloaded = 0;
                foreach (Match m in ValuePattern.Matches(text))
                {
                    if (!long.TryParse(m.Groups[2].Value, out long value)) continue;

                    switch (m.Groups[1].Value.ToLowerInvariant())
                    {
                        case "stateflags": flags = value; break;
                        case "bytestodownload": toDownload = value; break;
                        case "bytesdownloaded": downloaded = value; break;
                    }
                }

                return new SteamAppState
                {
                    ManifestFound = true,
                    StateFlags = flags,
                    BytesToDownload = toDownload,
                    BytesDownloaded = downloaded
                };
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[STEAM] Could not read {manifest}: {ex.Message}");
                return SteamAppState.Unknown;
            }
        }

        internal static string? ResolveManifestPath(string? targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return null;

            try
            {
                var common = Directory.GetParent(PathUtility.NormalizeTargetPath(targetPath));
                var steamapps = common?.Parent;
                if (steamapps == null) return null;

                string manifest = Path.Combine(steamapps.FullName, $"appmanifest_{Dota2AppId}.acf");
                return File.Exists(manifest) ? manifest : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
