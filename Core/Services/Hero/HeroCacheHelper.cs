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

namespace ArdysaModsTools.Core.Services;

/// <summary>
/// Consolidates hero download cache cleanup logic.
/// Replaces duplicate implementations in SelectHero.cs and SelectHeroPresenter.cs.
/// </summary>
public static class HeroCacheHelper
{
    /// <summary>
    /// Cleans up all hero download cache folders to prevent game errors.
    /// Removes temp directories (ArdysaSelectHero, ArdysaHero_*),
    /// the sets cache, and optionally the _hero_downloads folder under the target path.
    /// </summary>
    /// <param name="targetPath">Optional Dota 2 game path — cleans _hero_downloads if provided.</param>
    public static void Cleanup(string? targetPath = null)
    {
        try
        {
            var tempPath = Path.GetTempPath();

            // Clean temp directories
            var selectHeroTemp = Path.Combine(tempPath, "ArdysaSelectHero");
            if (Directory.Exists(selectHeroTemp))
            {
                Directory.Delete(selectHeroTemp, true);
            }

            foreach (var dir in Directory.GetDirectories(tempPath, "ArdysaHero_*"))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch { /* Ignore individual failures */ }
            }

            // Clean sets cache
            var setsCache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "sets");
            if (Directory.Exists(setsCache))
            {
                Directory.Delete(setsCache, true);
            }

            // Clean _hero_downloads under target path (if provided)
            if (!string.IsNullOrEmpty(targetPath))
            {
                var heroDownloadDir = Path.Combine(targetPath, "_hero_downloads");
                if (Directory.Exists(heroDownloadDir))
                {
                    Directory.Delete(heroDownloadDir, true);
                }
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine("[HeroCacheHelper] Hero cache cleanup completed");
#endif
        }
        catch
        {
            // Silently ignore cleanup failures — best-effort operation
        }
    }
}
