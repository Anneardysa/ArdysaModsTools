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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public interface IVpkReplacer
    {
        Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default);
    }

    public sealed class VpkReplacerService : IVpkReplacer
    {
        private readonly IAppLogger? _logger;

        public VpkReplacerService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(newVpkPath) ||
                string.IsNullOrWhiteSpace(targetPath) || !Path.IsPathFullyQualified(targetPath))
            {
                _logger?.LogError($"[{ErrorCodes.VPK_REPLACE_FAILED}] ReplaceAsync refused: target '{targetPath}', source '{newVpkPath}'.");
                return false;
            }

            string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);
            string destVpk = Path.Combine(modsDir, "pak01_dir.vpk");

            if (!await DeployVpkAsync(destVpk, newVpkPath, hideOutput: false, log, ct, _logger).ConfigureAwait(false))
                return false;

            try
            {
                new DirectoryInfo(modsDir).Attributes = FileAttributes.Normal;
                if (File.Exists(destVpk))
                    File.SetAttributes(destVpk, FileAttributes.Normal);
            }
            catch { }

            try
            {
                string hashFile = Path.Combine(modsDir, "ModsPack.hash");
                if (File.Exists(hashFile))
                    File.Delete(hashFile);
            }
            catch (Exception ex)
            {
                _logger?.Log($"VpkReplacerService: failed removing ModsPack.hash: {ex.Message}");
            }

            return true;
        }

        internal static async Task<bool> DeployVpkAsync(string destVpk, string? newVpkPath,
            bool hideOutput, Action<string> log, CancellationToken ct, IAppLogger? logger = null,
            bool keepBackup = false)
        {
            if (string.IsNullOrWhiteSpace(destVpk) || !Path.IsPathFullyQualified(destVpk))
            {
                logger?.LogError($"[{ErrorCodes.VPK_REPLACE_FAILED}] Refusing to deploy to a non-rooted path '{destVpk}'.");
                return false;
            }

            string backupVpk = destVpk + ".bak";
            string stagedVpk = destVpk + ".tmp";

            ct.ThrowIfCancellationRequested();

            RecoverInterruptedDeploy(destVpk, backupVpk, stagedVpk, logger);

            if (newVpkPath != null)
            {
                if (!File.Exists(newVpkPath))
                {
                    log("The rebuilt mod package is missing — your current mods were left untouched. Please generate again.");
                    logger?.LogError($"[{ErrorCodes.VPK_REPLACE_FAILED}] Refusing to deploy {destVpk}: source {newVpkPath} does not exist.");
                    return false;
                }

                await WaitForFileReadyAsync(newVpkPath, ct).ConfigureAwait(false);

                if (!VpkPackageValidator.TryValidate(newVpkPath, out var vpkError))
                {
                    log("The rebuilt mod package failed its integrity check — your current mods were left untouched. Please generate again.");
                    logger?.LogError($"[{ErrorCodes.VPK_INVALID_FORMAT}] Refusing to deploy {destVpk}: {vpkError} (source: {newVpkPath})");
                    return false;
                }
            }

            bool hasBackup = false;
            FileAttributes? originalAttributes = null;
            if (File.Exists(destVpk))
            {
                originalAttributes = TryGetAttributes(destVpk);
                try
                {
                    hasBackup = await TryMoveAsideAsync(destVpk, backupVpk, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (!hasBackup)
                        RestoreAttributes(destVpk, originalAttributes);
                }

                if (!hasBackup)
                {
                    log("The current mod package is locked by another program — close Dota 2 and try again.");
                    logger?.Log($"VpkReplacerService: rename-aside failed, {destVpk} is locked.");
                    return false;
                }
            }

            try
            {
                if (newVpkPath != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destVpk)!);
                    File.Copy(newVpkPath, stagedVpk, true);
                    File.Move(stagedVpk, destVpk, overwrite: true);

                    if (hideOutput)
                        try { File.SetAttributes(destVpk, FileAttributes.Hidden | FileAttributes.System); } catch { }
                }

                if (hasBackup && !keepBackup)
                    TryDelete(backupVpk);

                return true;
            }
            catch (Exception ex)
            {
                log($"Failed to replace VPK: {ex.Message}");
                logger?.Log($"VpkReplacerService replace failed: {ex}");
                TryDelete(stagedVpk);

                if (hasBackup)
                {
                    try
                    {
                        File.Move(backupVpk, destVpk, overwrite: true);
                        RestoreAttributes(destVpk, originalAttributes);
                    }
                    catch (Exception restoreEx)
                    {
                        log("Your previous mod package could not be put back yet — it will be restored on the next generate.");
                        logger?.Log($"VpkReplacerService: backup restore failed: {restoreEx}");
                    }
                }
                return false;
            }
        }

        internal static void RecoverInterruptedDeploy(string destVpk, string backupVpk, string stagedVpk,
            IAppLogger? logger = null)
        {
            TryDelete(stagedVpk);
            if (!File.Exists(backupVpk))
                return;

            bool restore = !File.Exists(destVpk)
                || (!VpkPackageValidator.TryValidate(destVpk, out _) && VpkPackageValidator.TryValidate(backupVpk, out _));
            if (!restore)
            {
                TryDelete(backupVpk);
                return;
            }

            try
            {
                File.Move(backupVpk, destVpk, overwrite: true);
                logger?.Log($"VpkReplacerService: restored {destVpk} from a backup left by an interrupted deploy.");
            }
            catch (Exception ex)
            {
                logger?.Log($"VpkReplacerService: could not restore the interrupted-deploy backup {backupVpk}: {ex.Message}");
            }
        }

        internal static void CommitBackup(string destVpk) => TryDelete(destVpk + ".bak");

        internal static bool RollbackToBackup(string destVpk, bool hideOutput, IAppLogger? logger = null)
        {
            string backupVpk = destVpk + ".bak";
            try
            {
                if (File.Exists(backupVpk))
                {
                    if (File.Exists(destVpk))
                        try { File.SetAttributes(destVpk, FileAttributes.Normal); } catch { }
                    File.Move(backupVpk, destVpk, overwrite: true);
                    if (hideOutput)
                        try { File.SetAttributes(destVpk, FileAttributes.Hidden | FileAttributes.System); } catch { }
                }
                else if (File.Exists(destVpk))
                {
                    try { File.SetAttributes(destVpk, FileAttributes.Normal); } catch { }
                    File.Delete(destVpk);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.Log($"VpkReplacerService: rollback of {destVpk} failed: {ex.Message}");
                return false;
            }
        }

        private static FileAttributes? TryGetAttributes(string path)
        {
            try { return File.GetAttributes(path); } catch { return null; }
        }

        private static void RestoreAttributes(string path, FileAttributes? attributes)
        {
            if (attributes is FileAttributes a && File.Exists(path))
                try { File.SetAttributes(path, a); } catch { }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                File.Delete(path);
            }
            catch { }
        }

        private static async Task<bool> TryMoveAsideAsync(string source, string destination, CancellationToken ct)
        {
            const int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(destination))
                    {
                        try { File.SetAttributes(destination, FileAttributes.Normal); } catch { }
                        try { File.Delete(destination); } catch { }
                    }
                    if (File.Exists(source))
                    {
                        try { File.SetAttributes(source, FileAttributes.Normal); } catch { }
                    }
                    File.Move(source, destination, overwrite: true);
                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
            return false;
        }

        private static async Task WaitForFileReadyAsync(string filePath, CancellationToken ct)
        {
            const int maxAttempts = 30;
            for (int i = 0; i < maxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }
    }
}

