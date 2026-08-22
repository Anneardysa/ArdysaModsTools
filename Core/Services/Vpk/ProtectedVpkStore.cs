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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public static class ProtectedVpkStore
    {
        private static readonly string[] StaysWithPackage = { "scripts", "resource" };

        public static string Dir(string targetPath)
            => Path.Combine(targetPath, "game", "mod");

        internal static bool MountsSearchPath(string? gameInfoText, string searchPath)
            => !string.IsNullOrEmpty(gameInfoText)
               && !string.IsNullOrEmpty(searchPath)
               && Regex.IsMatch(gameInfoText,
                   $"^[ \t]*(?:Game|Mod)[ \t]+\"?{Regex.Escape(searchPath)}\"?[ \t\r]*$",
                   RegexOptions.Multiline | RegexOptions.IgnoreCase);

        internal static bool IsMountedBy(string? gameInfoText)
            => MountsSearchPath(gameInfoText, "mod");

        public static bool IsMounted(string targetPath)
        {
            try
            {
                string gi = Path.Combine(targetPath, DotaPaths.GameInfo);
                return File.Exists(gi) && IsMountedBy(File.ReadAllText(gi));
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.IsMounted failed: {ex.Message}");
                return false;
            }
        }

        public static string VpkPath(string targetPath)
            => Path.Combine(Dir(targetPath), "pak01_dir.vpk");

        public static string MainVpkPath(string targetPath)
            => Path.Combine(targetPath, "game", "_ArdysaMods", "pak01_dir.vpk");

        public static void Ensure(string targetPath)
        {
            try
            {
                Directory.CreateDirectory(Dir(targetPath));
                SafeTempPathHelper.HideDirectory(Dir(targetPath));
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.Ensure failed: {ex.Message}");
            }
        }

        public static void Clear(string targetPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                    return;

                string protVpk = VpkPath(targetPath);
                if (File.Exists(protVpk))
                {
                    try { File.SetAttributes(protVpk, FileAttributes.Normal); } catch { }
                    try { File.Delete(protVpk); } catch { }
                }
                string protDir = Dir(targetPath);
                if (Directory.Exists(protDir))
                {
                    NormalizeAttributesRecursively(protDir);
                }

                string tempDir = Path.Combine(targetPath, "game", "_ArdysaMods", "_temp");
                if (Directory.Exists(tempDir))
                {
                    string[] logFiles = { "hero_extraction_log.json", "hero_selections.json", "itemsgame_baseline.json" };
                    foreach (var file in logFiles)
                    {
                        string p = Path.Combine(tempDir, file);
                        if (File.Exists(p))
                        {
                            try { File.SetAttributes(p, FileAttributes.Normal); } catch { }
                            try { File.Delete(p); } catch { }
                        }
                    }
                }

                string mainVpk = MainVpkPath(targetPath);
                if (File.Exists(mainVpk))
                {
                    try { File.SetAttributes(mainVpk, FileAttributes.Normal); } catch { }
                }

                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                if (Directory.Exists(modsDir))
                {
                    try { new DirectoryInfo(modsDir).Attributes = FileAttributes.Normal; } catch { }
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.Clear failed: {ex.Message}");
            }
        }

        public static VpkStamp? GetActiveModVpkStamp(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return null;
            string root = PathUtility.NormalizeTargetPath(targetPath);

            string modVpk = MainVpkPath(root);
            if (File.Exists(modVpk))
                return VpkStamp.Read(modVpk);

            string protVpk = VpkPath(root);
            if (File.Exists(protVpk))
                return VpkStamp.Read(protVpk);

            return null;
        }

        public static async Task<bool> DeployAsync(string targetPath, string? newProtectedVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            Ensure(targetPath);
            bool success = await VpkReplacerService.DeployVpkAsync(
                VpkPath(targetPath), newProtectedVpkPath, hideOutput: true, log, ct, logger).ConfigureAwait(false);

            if (success)
            {
                try { SafeTempPathHelper.HideDirectory(Dir(targetPath)); } catch { }
            }

            return success;
        }

        public static bool IsProtectable(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            string root = normalized.Split('/')[0];
            if (Array.Exists(StaysWithPackage, s => string.Equals(s, root, StringComparison.OrdinalIgnoreCase)))
                return false;

            string wrapped = "/" + normalized.Trim('/') + "/";
            if (wrapped.Contains("/kisilev_ind/", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
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

        public static bool CopyItemsGame(string sourceExtractDir, string targetProtectedDir, IAppLogger? logger = null)
        {
            try
            {
                string src = Path.Combine(sourceExtractDir, "scripts", "items", "items_game.txt");
                if (!File.Exists(src))
                    return false;

                string dest = Path.Combine(targetProtectedDir, "scripts", "items", "items_game.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(src, dest, overwrite: true);
                logger?.LogDebug("ProtectedVpkStore: copied items_game.txt into protected package tree.");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore: could not copy items_game.txt to protected folder: {ex.Message}");
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.CopyItemsGame failed: {ex.Message}");
                return false;
            }
        }

        public static void DeletePermanently(string targetPath, IAppLogger? logger = null)
        {
            try
            {
                string dir = Dir(targetPath);
                if (Directory.Exists(dir))
                {
                    NormalizeAttributesRecursively(dir);
                    Directory.Delete(dir, true);
                    logger?.Log($"ProtectedVpkStore: permanently deleted {dir}");
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.DeletePermanently failed: {ex.Message}");
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.DeletePermanently error: {ex.Message}");
            }
        }

        public static void NormalizeAttributesRecursively(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                var dirInfo = new DirectoryInfo(dir);
                try { dirInfo.Attributes = FileAttributes.Normal; } catch { }

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Attributes = FileAttributes.Normal; } catch { }
                }

                foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try { subDir.Attributes = FileAttributes.Normal; } catch { }
                }
            }
            catch { }
        }
    }
}
