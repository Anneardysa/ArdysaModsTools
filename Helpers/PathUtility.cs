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

namespace ArdysaModsTools.Helpers
{
    public static class PathUtility
    {
        public static string NormalizeTargetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Target path cannot be empty.");

            path = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            path = path.Replace('/', '\\');

            path = Regex.Replace(path, @"game[_\s-]*_?ArdysaMods", @"game\\_ArdysaMods", RegexOptions.IgnoreCase);

            if (path.EndsWith("_ArdysaMods", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(path)?.Parent?.FullName;
                if (parent != null)
                    return parent;
            }

            if (path.EndsWith("game", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(path)?.FullName;
                if (parent != null)
                    return parent;
            }

            if (Directory.Exists(Path.Combine(path, "game", "_ArdysaMods")))
                return path;

            var expected = Path.Combine(path, "dota 2 beta", "game", "_ArdysaMods");
            if (Directory.Exists(expected))
                return Path.Combine(path, "dota 2 beta");

            string? betaPath = Directory.GetDirectories(path, "dota 2 beta", SearchOption.AllDirectories)
                .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "game", "_ArdysaMods")));
            if (!string.IsNullOrEmpty(betaPath))
                return betaPath;

            return path;
        }

        public static string GetVpkPath(string targetPath)
        {
            string normalized = NormalizeTargetPath(targetPath);
            return Path.Combine(normalized, "game", "_ArdysaMods", "pak01_dir.vpk");
        }
    }
}

