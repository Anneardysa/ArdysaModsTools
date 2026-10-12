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

        private static readonly string[] PackageTextRoots = { "scripts/items/items_game.txt", "resource/localization" };

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

        public static bool CanSplit(string targetPath)
        {
            try
            {
                string gi = Path.Combine(targetPath, DotaPaths.GameInfo);
                if (!File.Exists(gi))
                {
                    FallbackLogger.LogFileOnly($"ProtectedVpkStore.CanSplit: no gameinfo at {gi} — keeping the single-package layout.");
                    return false;
                }

                string text = File.ReadAllText(gi);
                if (IsMountedBy(text))
                    return true;

                if (MountsSearchPath(text, "_ArdysaMods"))
                {
                    FallbackLogger.LogFileOnly("ProtectedVpkStore.CanSplit: gameinfo mounts _ArdysaMods but not mod (legacy patch) — keeping the single-package layout.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.CanSplit failed: {ex.Message}");
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
                    HideAttributesRecursively(protDir);
                }

                string tempDir = Path.Combine(targetPath, "game", "_ArdysaMods", "_temp");
                if (Directory.Exists(tempDir))
                {
                    string[] logFiles = { "hero_extraction_log.json", "hero_selections.json" };
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

            var main = VpkStamp.Read(MainVpkPath(root));
            var prot = VpkStamp.Read(VpkPath(root));

            if (main == null) return prot;
            if (prot == null) return main;

            return new VpkStamp(
                main.Value.Length + prot.Value.Length,
                main.Value.LastWriteUtc > prot.Value.LastWriteUtc ? main.Value.LastWriteUtc : prot.Value.LastWriteUtc);
        }

        public static async Task<bool> DeployAsync(string targetPath, string? newProtectedVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !Path.IsPathFullyQualified(targetPath))
            {
                FallbackLogger.LogFileOnly("ProtectedVpkStore.DeployAsync: refused an empty or relative target path.");
                return false;
            }

            Ensure(targetPath);
            bool success = await VpkReplacerService.DeployVpkAsync(
                VpkPath(targetPath), newProtectedVpkPath, hideOutput: true, log, ct, logger).ConfigureAwait(false);

            if (success)
            {
                try { SafeTempPathHelper.HideDirectory(Dir(targetPath)); } catch { }
            }

            return success;
        }

        public static async Task<bool> DeployPairAsync(string targetPath, string? newProtectedVpkPath,
            Func<Task<bool>> deployMain, Action<string> log, IAppLogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !Path.IsPathFullyQualified(targetPath))
            {
                FallbackLogger.LogFileOnly("ProtectedVpkStore.DeployPairAsync: refused an empty or relative target path.");
                return false;
            }

            Ensure(targetPath);
            string protVpk = VpkPath(targetPath);
            if (!await VpkReplacerService.DeployVpkAsync(protVpk, newProtectedVpkPath, hideOutput: true, log,
                    CancellationToken.None, logger, keepBackup: true).ConfigureAwait(false))
                return false;

            bool mainOk = false;
            try
            {
                mainOk = await deployMain().ConfigureAwait(false);
            }
            finally
            {
                if (mainOk)
                    VpkReplacerService.CommitBackup(protVpk);
                else if (!VpkReplacerService.RollbackToBackup(protVpk, hideOutput: true, logger))
                    log("Your previous mod package could not be put back yet — generate again to finish the install.");
                try { SafeTempPathHelper.HideDirectory(Dir(targetPath)); } catch { }
            }
            return mainOk;
        }

        internal static Task<bool> DeployInstallPairAsync(string targetPath, string mainVpkPath,
            string newMainVpkPath, string? newProtectedVpkPath, Action<string> log, IAppLogger? logger = null)
            => DeployPairAsync(targetPath, newProtectedVpkPath,
                () => VpkReplacerService.DeployVpkAsync(mainVpkPath, newMainVpkPath, hideOutput: false, log,
                    CancellationToken.None, logger),
                log, logger);

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

        public static int MovePackageText(string extractDir, string protectedDir,
            IAppLogger? logger = null, CancellationToken ct = default)
            => MoveProtected(extractDir, protectedDir, ListPackageText(extractDir), logger, ct);

        public static int Split(string targetPath, string extractDir, string protectedDir,
            IReadOnlyCollection<string>? assetPaths = null,
            IAppLogger? logger = null, CancellationToken ct = default)
        {
            if (!CanSplit(targetPath))
            {
                FallbackLogger.LogFileOnly(
                    $"ProtectedVpkStore.Split: skipped — package text and {assetPaths?.Count ?? 0} protected asset(s) stay in the main package.");
                return 0;
            }

            Ensure(targetPath);
            int moved = assetPaths is { Count: > 0 }
                ? MoveProtected(extractDir, protectedDir, assetPaths, logger, ct)
                : 0;
            moved += MovePackageText(extractDir, protectedDir, logger, ct);
            moved += Misc.MapCleanupService.ApplyTo(protectedDir, Misc.MapCleanupService.ReadEnabled(targetPath));
            return moved;
        }

        public static int BorrowPackageText(string protectedDir, string extractDir,
            IAppLogger? logger = null, CancellationToken ct = default)
            => MoveProtected(protectedDir, extractDir, ListPackageText(protectedDir), logger, ct);

        private static List<string> ListPackageText(string root)
        {
            var found = new List<string>();
            foreach (var rel in PackageTextRoots)
            {
                string native = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(native))
                {
                    found.Add(rel);
                }
                else if (Directory.Exists(native))
                {
                    foreach (var file in Directory.GetFiles(native, "*", SearchOption.AllDirectories))
                        found.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
                }
            }
            return found;
        }

        public static bool DeletePermanently(string targetPath, IAppLogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !Path.IsPathFullyQualified(targetPath))
            {
                FallbackLogger.LogFileOnly("ProtectedVpkStore.DeletePermanently: refused an empty or relative target path.");
                return false;
            }

            string dir = Dir(targetPath);
            try
            {
                if (!Directory.Exists(dir))
                    return true;

                NormalizeAttributesRecursively(dir);
                Directory.Delete(dir, true);
                FallbackLogger.LogFileOnly($"ProtectedVpkStore: permanently deleted {dir}");
                return true;
            }
            catch (Exception ex)
            {
                HideAttributesRecursively(dir);
                logger?.Log($"ProtectedVpkStore.DeletePermanently failed: {ex.Message}");
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.DeletePermanently error: {ex.Message}");
                return false;
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

        internal static void HideAttributesRecursively(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                const FileAttributes hidden = FileAttributes.Hidden | FileAttributes.System;
                var dirInfo = new DirectoryInfo(dir);

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Attributes |= hidden; } catch { }
                }

                foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try { subDir.Attributes |= hidden; } catch { }
                }

                try { dirInfo.Attributes |= hidden; } catch { }
            }
            catch { }
        }
    }
}
