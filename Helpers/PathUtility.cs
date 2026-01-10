using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Helpers
{
    /// <summary>
    /// Provides safe and fault-tolerant normalization for user-selected Dota 2 paths.
    /// Ensures consistent folder structure: 
    ///     ...\dota 2 beta\game\_ArdysaMods\pak01_dir.vpk
    /// </summary>
    public static class PathUtility
    {
        /// <summary>
        /// Normalizes any user-selected target path into a valid Dota 2 installation root.
        /// Handles malformed patterns like "game_ArdysaMods", extra underscores, spacing, and case differences.
        /// </summary>
        /// <param name="path">The user-selected or input path.</param>
        /// <returns>The normalized base path leading to "dota 2 beta".</returns>
        /// <exception cref="ArgumentException">Thrown if path is empty or null.</exception>
        public static string NormalizeTargetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Target path cannot be empty.");

            // Normalize slashes, trim spaces
            path = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            path = path.Replace('/', '\\');

            // --- 🧩 Fix malformed folder names like game_ArdysaMods / game__ArdysaMods / etc.
            path = Regex.Replace(path, @"game[_\s-]*_?ArdysaMods", @"game\\_ArdysaMods", RegexOptions.IgnoreCase);

            // --- 🧩 Handle if user picked directly inside "_ArdysaMods"
            if (path.EndsWith("_ArdysaMods", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(path)?.Parent?.FullName;
                if (parent != null)
                    return parent;
            }

            // --- 🧩 Handle if user picked inside the "game" folder
            if (path.EndsWith("game", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(path)?.FullName;
                if (parent != null)
                    return parent;
            }

            // --- 🧩 If "_ArdysaMods" folder exists under this path → assume correct root
            if (Directory.Exists(Path.Combine(path, "game", "_ArdysaMods")))
                return path;

            // --- 🧩 Handle if user selected too high (e.g., common/ or steamapps/)
            var expected = Path.Combine(path, "dota 2 beta", "game", "_ArdysaMods");
            if (Directory.Exists(expected))
                return Path.Combine(path, "dota 2 beta");

            // --- 🧩 As a fallback, try to auto-detect the correct base
            string? betaPath = Directory.GetDirectories(path, "dota 2 beta", SearchOption.AllDirectories)
                .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "game", "_ArdysaMods")));
            if (!string.IsNullOrEmpty(betaPath))
                return betaPath;

            // --- Default return as-is
            return path;
        }

        /// <summary>
        /// Builds the absolute path to pak01_dir.vpk safely, 
        /// after normalizing the target root.
        /// </summary>
        public static string GetVpkPath(string targetPath)
        {
            string normalized = NormalizeTargetPath(targetPath);
            return Path.Combine(normalized, "game", "_ArdysaMods", "pak01_dir.vpk");
        }
    }
}
