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
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Core.Services
{
    public static class ProtectedVpkStore
    {
        private static readonly string[] StaysWithPackage = { "scripts", "resource" };

        private const string CipherAssetPath = "local/protected/pak01_dir.vpk";

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
                string vpk = VpkPath(targetPath);
                if (File.Exists(vpk))
                {
                    try { File.SetAttributes(vpk, FileAttributes.Normal); } catch { }
                    File.Delete(vpk);
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.Clear failed: {ex.Message}");
            }
        }

        public static async Task<bool> DeployAsync(string targetPath, string? newVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            Ensure(targetPath);
            bool ok = await VpkReplacerService.DeployVpkAsync(
                VpkPath(targetPath), newVpkPath, hideOutput: true, log, ct, logger).ConfigureAwait(false);

            if (ok && newVpkPath != null)
                EncryptAtRest(targetPath, logger);

            return ok;
        }

        public static bool IsEncryptedAtRest(string targetPath)
            => AssetCipher.IsEncrypted(VpkPath(targetPath));

        public static bool EncryptAtRest(string targetPath, IAppLogger? logger = null)
        {
            string vpk = VpkPath(targetPath);
            try
            {
                if (!File.Exists(vpk) || AssetCipher.IsEncrypted(vpk))
                    return true;

                byte[] plaintext = File.ReadAllBytes(vpk);
                byte[] container = AssetCipher.Encrypt(plaintext, CipherAssetPath);
                return SwapInPlace(vpk, container, logger, "encrypt");
            }
            catch (Exception ex)
            {
                logger?.LogError($"ProtectedVpkStore.EncryptAtRest failed: {ex.Message}", ex);
                return false;
            }
        }

        public static bool DecryptForPlay(string targetPath, IAppLogger? logger = null)
        {
            string vpk = VpkPath(targetPath);
            try
            {
                if (!File.Exists(vpk) || !AssetCipher.IsEncrypted(vpk))
                    return true;

                byte[] container = File.ReadAllBytes(vpk);
                byte[] plaintext = AssetCipher.Decrypt(container, CipherAssetPath);
                return SwapInPlace(vpk, plaintext, logger, "decrypt");
            }
            catch (Exception ex)
            {
                logger?.LogError($"ProtectedVpkStore.DecryptForPlay failed: {ex.Message}", ex);
                return false;
            }
        }

        private static bool SwapInPlace(string vpk, byte[] newContent, IAppLogger? logger, string op)
        {
            string backup = vpk + ".bak";
            string temp = vpk + ".tmp";

            try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }

            try
            {
                File.WriteAllBytes(temp, newContent);
            }
            catch (Exception ex)
            {
                logger?.LogError($"ProtectedVpkStore.SwapInPlace ({op}) write failed: {ex.Message}", ex);
                try { File.Delete(temp); } catch { }
                return false;
            }

            bool hasBackup = false;
            try
            {
                File.Move(vpk, backup);
                hasBackup = true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"ProtectedVpkStore.SwapInPlace ({op}) rename-aside failed: {ex.Message}", ex);
                try { File.Delete(temp); } catch { }
                return false;
            }

            try
            {
                File.Move(temp, vpk);
                try { File.SetAttributes(vpk, FileAttributes.Hidden | FileAttributes.System); } catch { }
                try { File.Delete(backup); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"ProtectedVpkStore.SwapInPlace ({op}) commit failed: {ex.Message}", ex);
                if (hasBackup)
                {
                    try { File.Move(backup, vpk, overwrite: true); }
                    catch (Exception restoreEx)
                    {
                        logger?.LogError($"ProtectedVpkStore.SwapInPlace ({op}) restore failed: {restoreEx.Message}", restoreEx);
                    }
                }
                try { File.Delete(temp); } catch { }
                return false;
            }
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
