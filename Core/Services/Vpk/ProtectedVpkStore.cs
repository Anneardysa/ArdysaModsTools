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

                string mainPayload = MainPayloadStorePath(targetPath);
                if (File.Exists(mainPayload))
                {
                    try { File.SetAttributes(mainPayload, FileAttributes.Normal); } catch { }
                    try { File.Delete(mainPayload); } catch { }
                }

                string protPayload = PayloadStorePath(targetPath);
                if (File.Exists(protPayload))
                {
                    try { File.SetAttributes(protPayload, FileAttributes.Normal); } catch { }
                    try { File.Delete(protPayload); } catch { }
                }

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

        private static readonly byte[] StorageEntropy = { 0x41, 0x4D, 0x54, 0x5F, 0x56, 0x50, 0x4B, 0x5F, 0x53, 0x45, 0x43 };

        public static VpkStamp? GetActiveModVpkStamp(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return null;
            string root = PathUtility.NormalizeTargetPath(targetPath);

            string mainPayload = MainPayloadStorePath(root);
            if (File.Exists(mainPayload))
                return VpkStamp.Read(mainPayload);

            string modVpk = MainVpkPath(root);
            if (File.Exists(modVpk))
            {
                var fi = new FileInfo(modVpk);
                if (fi.Length != 28)
                    return VpkStamp.Read(modVpk);
            }

            string protPayload = PayloadStorePath(root);
            if (File.Exists(protPayload))
                return VpkStamp.Read(protPayload);

            return VpkStamp.Read(modVpk);
        }

        public static string MainPayloadStorePath(string targetPath)
            => Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", "main_payload.vpk");

        public static string MainVpkPath(string targetPath)
            => Path.Combine(targetPath, "game", "_ArdysaMods", "pak01_dir.vpk");

        public static string PayloadStorePath(string targetPath)
            => Path.Combine(targetPath, "game", "_ArdysaMods", "_temp", "protected_payload.vpk");

        public static async Task<bool> DeployMainAsync(string targetPath, string? newVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            string payloadDest = MainPayloadStorePath(targetPath);
            string vpkDest = MainVpkPath(targetPath);

            try
            {
                if (!string.IsNullOrEmpty(newVpkPath) && File.Exists(newVpkPath))
                {
                    byte[] plainBytes = await File.ReadAllBytesAsync(newVpkPath, ct).ConfigureAwait(false);
                    byte[] encBytes = System.Security.Cryptography.ProtectedData.Protect(
                        plainBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);

                    Directory.CreateDirectory(Path.GetDirectoryName(payloadDest)!);
                    try { SafeTempPathHelper.HideDirectory(Path.GetDirectoryName(payloadDest)!); } catch { }

                    if (File.Exists(payloadDest))
                    {
                        try { File.SetAttributes(payloadDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(payloadDest); } catch { }
                    }

                    await File.WriteAllBytesAsync(payloadDest, encBytes, ct).ConfigureAwait(false);
                    try { File.SetAttributes(payloadDest, FileAttributes.Hidden | FileAttributes.System); } catch { }

                    CreateEmptyDummyVpk(vpkDest);
                    try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    try { SafeTempPathHelper.HideDirectory(Path.GetDirectoryName(vpkDest)!); } catch { }
                }
                else
                {
                    if (File.Exists(payloadDest))
                    {
                        try { File.SetAttributes(payloadDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(payloadDest); } catch { }
                    }

                    if (File.Exists(vpkDest))
                    {
                        try { File.SetAttributes(vpkDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(vpkDest); } catch { }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.DeployMainAsync failed: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DeployAsync(string targetPath, string? newVpkPath,
            Action<string> log, CancellationToken ct = default, IAppLogger? logger = null)
        {
            Ensure(targetPath);
            string payloadDest = PayloadStorePath(targetPath);
            string vpkDest = VpkPath(targetPath);

            try
            {
                if (!string.IsNullOrEmpty(newVpkPath) && File.Exists(newVpkPath))
                {
                    byte[] plainBytes = await File.ReadAllBytesAsync(newVpkPath, ct).ConfigureAwait(false);
                    byte[] encBytes = System.Security.Cryptography.ProtectedData.Protect(
                        plainBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);

                    Directory.CreateDirectory(Path.GetDirectoryName(payloadDest)!);
                    try { SafeTempPathHelper.HideDirectory(Path.GetDirectoryName(payloadDest)!); } catch { }

                    if (File.Exists(payloadDest))
                    {
                        try { File.SetAttributes(payloadDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(payloadDest); } catch { }
                    }

                    await File.WriteAllBytesAsync(payloadDest, encBytes, ct).ConfigureAwait(false);
                    try { File.SetAttributes(payloadDest, FileAttributes.Hidden | FileAttributes.System); } catch { }

                    CreateEmptyDummyVpk(vpkDest);
                    try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    try { SafeTempPathHelper.HideDirectory(Dir(targetPath)); } catch { }
                }
                else
                {
                    if (File.Exists(payloadDest))
                    {
                        try { File.SetAttributes(payloadDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(payloadDest); } catch { }
                    }

                    if (File.Exists(vpkDest))
                    {
                        try { File.SetAttributes(vpkDest, FileAttributes.Normal); } catch { }
                        try { File.Delete(vpkDest); } catch { }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.DeployAsync failed: {ex.Message}");
                return false;
            }
        }

        public static void MountSession(string targetPath, IAppLogger? logger = null)
        {
            try
            {
                string mainPayloadSrc = MainPayloadStorePath(targetPath);
                string mainVpkDest = MainVpkPath(targetPath);
                if (File.Exists(mainPayloadSrc))
                {
                    byte[] encBytes = File.ReadAllBytes(mainPayloadSrc);
                    byte[] plainBytes;
                    try
                    {
                        plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                            encBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    }
                    catch
                    {
                        plainBytes = encBytes;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(mainVpkDest)!);
                    if (File.Exists(mainVpkDest))
                    {
                        try { File.SetAttributes(mainVpkDest, FileAttributes.Normal); } catch { }
                    }
                    File.WriteAllBytes(mainVpkDest, plainBytes);
                    try { File.SetAttributes(mainVpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    logger?.LogDebug("ProtectedVpkStore: mounted main session payload.");
                }

                Ensure(targetPath);
                string payloadSrc = PayloadStorePath(targetPath);
                string vpkDest = VpkPath(targetPath);

                if (File.Exists(payloadSrc))
                {
                    byte[] encBytes = File.ReadAllBytes(payloadSrc);
                    byte[] plainBytes;
                    try
                    {
                        plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                            encBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    }
                    catch
                    {
                        plainBytes = encBytes;
                    }

                    if (File.Exists(vpkDest))
                    {
                        try { File.SetAttributes(vpkDest, FileAttributes.Normal); } catch { }
                    }
                    File.WriteAllBytes(vpkDest, plainBytes);
                    try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    logger?.LogDebug("ProtectedVpkStore: mounted protected session payload.");
                }
                else
                {
                    CreateEmptyDummyVpk(vpkDest);
                    try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.MountSession failed: {ex.Message}");
            }
        }

        public static void UnmountSession(string targetPath, IAppLogger? logger = null)
        {
            try
            {
                string mainPayloadSrc = MainPayloadStorePath(targetPath);
                string mainVpkDest = MainVpkPath(targetPath);
                if (File.Exists(mainPayloadSrc))
                {
                    FileSecurityGuard.ReleaseDaclLock(mainVpkDest, logger);
                    CreateEmptyDummyVpk(mainVpkDest);
                    try { File.SetAttributes(mainVpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    logger?.LogDebug("ProtectedVpkStore: unmounted main session payload (reverted to dummy VPK).");
                }

                Ensure(targetPath);
                string payloadSrc = PayloadStorePath(targetPath);
                string vpkDest = VpkPath(targetPath);

                if (File.Exists(payloadSrc))
                {
                    FileSecurityGuard.ReleaseDaclLock(vpkDest, logger);
                    CreateEmptyDummyVpk(vpkDest);
                    try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    logger?.LogDebug("ProtectedVpkStore: unmounted session payload (reverted to dummy VPK).");
                }
                else if (File.Exists(vpkDest))
                {
                    FileSecurityGuard.ReleaseDaclLock(vpkDest, logger);
                    try { File.SetAttributes(vpkDest, FileAttributes.Normal); } catch { }
                    File.Delete(vpkDest);
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.UnmountSession failed: {ex.Message}");
            }
        }

        public static bool PanicWipe(string targetPath, IAppLogger? logger = null)
        {
            try
            {
                bool wiped = false;
                string mainVpk = MainVpkPath(targetPath);
                if (File.Exists(mainVpk))
                {
                    FileSecurityGuard.ReleaseDaclLock(mainVpk, logger);
                    CreateEmptyDummyVpk(mainVpk);
                    try { File.SetAttributes(mainVpk, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    wiped = true;
                }

                string protVpk = VpkPath(targetPath);
                if (File.Exists(protVpk))
                {
                    FileSecurityGuard.ReleaseDaclLock(protVpk, logger);
                    CreateEmptyDummyVpk(protVpk);
                    try { File.SetAttributes(protVpk, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    wiped = true;
                }

                logger?.LogDebug("ProtectedVpkStore: PanicWipe executed successfully (both VPKs reverted to dummy headers).");
                FallbackLogger.Log("[ProtectedVpkStore] PanicWipe executed — VPKs locked to dummy.");
                return wiped;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.PanicWipe failed: {ex.Message}");
                FallbackLogger.LogFileOnly($"ProtectedVpkStore.PanicWipe error: {ex.Message}");
                return false;
            }
        }

        public static void PurgeOrphanedSession(string targetPath, IAppLogger? logger = null)
        {
            try
            {
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                    return;

                string mainPayloadSrc = MainPayloadStorePath(targetPath);
                string mainVpkDest = MainVpkPath(targetPath);
                if (File.Exists(mainPayloadSrc))
                {
                    if (File.Exists(mainVpkDest))
                    {
                        var fi = new FileInfo(mainVpkDest);
                        if (fi.Length != 28)
                        {
                            CreateEmptyDummyVpk(mainVpkDest);
                            try { File.SetAttributes(mainVpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                        }
                    }
                    else
                    {
                        CreateEmptyDummyVpk(mainVpkDest);
                        try { File.SetAttributes(mainVpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    }
                }

                string payloadSrc = PayloadStorePath(targetPath);
                string vpkDest = VpkPath(targetPath);

                if (File.Exists(payloadSrc))
                {
                    if (File.Exists(vpkDest))
                    {
                        var fi = new FileInfo(vpkDest);
                        if (fi.Length != 28)
                        {
                            UnmountSession(targetPath, logger);
                        }
                    }
                    else
                    {
                        CreateEmptyDummyVpk(vpkDest);
                        try { File.SetAttributes(vpkDest, FileAttributes.Hidden | FileAttributes.System); } catch { }
                    }
                }
                else if (File.Exists(vpkDest))
                {
                    try { File.SetAttributes(vpkDest, FileAttributes.Normal); } catch { }
                    File.Delete(vpkDest);
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.PurgeOrphanedSession failed: {ex.Message}");
            }
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

            if (moved > 0)
            {
                try
                {
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger?.Log($"ProtectedVpkStore: poison pass skipped: {ex.Message}");
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

        public static bool DecryptMainPayloadToTempFile(string targetPath, string tempOutputPath, IAppLogger? logger = null)
        {
            try
            {
                string payloadSrc = MainPayloadStorePath(targetPath);
                if (!File.Exists(payloadSrc)) return false;

                byte[] encBytes = File.ReadAllBytes(payloadSrc);
                byte[] plainBytes;
                try
                {
                    plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                        encBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                }
                catch
                {
                    plainBytes = encBytes;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(tempOutputPath)!);
                File.WriteAllBytes(tempOutputPath, plainBytes);
                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.DecryptMainPayloadToTempFile failed: {ex.Message}");
                return false;
            }
        }

        public static bool DecryptPayloadToTempFile(string targetPath, string tempOutputPath, IAppLogger? logger = null)
        {
            try
            {
                string payloadSrc = PayloadStorePath(targetPath);
                if (!File.Exists(payloadSrc)) return false;

                byte[] encBytes = File.ReadAllBytes(payloadSrc);
                byte[] plainBytes;
                try
                {
                    plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                        encBytes, StorageEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                }
                catch
                {
                    plainBytes = encBytes;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(tempOutputPath)!);
                File.WriteAllBytes(tempOutputPath, plainBytes);
                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"ProtectedVpkStore.DecryptPayloadToTempFile failed: {ex.Message}");
                return false;
            }
        }

        public static void CreateEmptyDummyVpk(string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            try
            {
                if (File.Exists(outputPath))
                {
                    try { File.SetAttributes(outputPath, FileAttributes.Normal); } catch { }
                    try { File.Delete(outputPath); } catch { }
                }
            }
            catch { }

            byte[] header = new byte[28];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), 0x55aa1234);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), 2);
            File.WriteAllBytes(outputPath, header);
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
