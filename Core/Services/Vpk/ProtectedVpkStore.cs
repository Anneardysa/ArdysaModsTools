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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public static class ProtectedVpkStore
    {
        private static readonly string[] StaysWithPackage = { "scripts", "resource" };

        public static string Dir(string targetPath)
            => Path.Combine(targetPath, "game", "mod");

        internal static bool IsMountedBy(string? gameInfoText)
            => !string.IsNullOrEmpty(gameInfoText)
               && Regex.IsMatch(gameInfoText, "^[ \t]*(?:Game|Mod)[ \t]+\"?mod\"?[ \t\r]*$",
                   RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static bool IsMounted(string targetPath)
        {
            try
            {
                string gi = Path.Combine(targetPath, DotaPaths.GameInfo);
                return File.Exists(gi) && IsMountedBy(File.ReadAllText(gi));
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ProtectedVpkStore.IsMounted failed: {ex.Message}");
                return false;
            }
        }

        public static string VpkPath(string targetPath)
            => Path.Combine(Dir(targetPath), "pak01_dir.vpk");

        public static void Ensure(string targetPath)
        {
            try
            {
                Directory.CreateDirectory(Dir(targetPath));
                SafeTempPathHelper.HideDirectory(Dir(targetPath));
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ProtectedVpkStore.Ensure failed: {ex.Message}");
            }
        }

        public static void Clear(string targetPath)
        {
            try
            {
                string vpk = VpkPath(targetPath);
                if (File.Exists(vpk))
                {
                    try { File.SetAttributes(vpk, FileAttributes.Normal); } catch { }
                    File.Delete(vpk);
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ProtectedVpkStore.Clear failed: {ex.Message}");
            }
        }

        public static async Task<bool> DeployAsync(string targetPath, string? newVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            Ensure(targetPath);
            return await VpkReplacerService.DeployVpkAsync(
                VpkPath(targetPath), newVpkPath, hideOutput: true, log, ct, logger).ConfigureAwait(false);
        }

        public static bool IsProtectable(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            string root = relativePath.Replace('\\', '/').TrimStart('/').Split('/')[0];
            return !Array.Exists(StaysWithPackage,
                s => string.Equals(s, root, StringComparison.OrdinalIgnoreCase));
        }

        public static int MoveProtected(string extractDir, string protectedDir,
            IEnumerable<string> relativePaths, IAppLogger? logger = null, CancellationToken ct = default)
        {
            int moved = 0;
            foreach (var rel in relativePaths)
            {
                ct.ThrowIfCancellationRequested();

                string source = Path.Combine(extractDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source))
                    continue;

                try
                {
                    string dest = Path.Combine(protectedDir, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(source, dest, overwrite: true);
                    moved++;
                }
                catch (Exception ex)
                {
                    logger?.Log($"ProtectedVpkStore: could not move '{rel}': {ex.Message}");
                }
            }
            return moved;
        }
    }
}
