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
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public interface IVpkReplacer
    {
        Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default, bool hideOutput = false);
    }

    public sealed class VpkReplacerService : IVpkReplacer
    {
        private readonly IAppLogger? _logger;

        public VpkReplacerService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default, bool hideOutput = false)
        {
            string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);

            string currentVpk = Path.Combine(modsDir, "pak01_dir.vpk");
            string backupVpk = currentVpk + ".bak";

            ct.ThrowIfCancellationRequested();

            try { if (File.Exists(backupVpk)) File.Delete(backupVpk); } catch { }

            await WaitForFileReadyAsync(newVpkPath, ct).ConfigureAwait(false);

            bool hasBackup = false;
            if (File.Exists(currentVpk))
            {
                if (!await TryMoveAsideAsync(currentVpk, backupVpk, ct).ConfigureAwait(false))
                {
                    log("The current mod package is locked by another program — close Dota 2 and try again.");
                    _logger?.Log($"VpkReplacerService: rename-aside failed, {currentVpk} is locked.");
                    return false;
                }
                hasBackup = true;
            }

            try
            {
                File.Copy(newVpkPath, currentVpk, true);

                if (hideOutput)
                    try { File.SetAttributes(currentVpk, FileAttributes.Hidden | FileAttributes.System); } catch { }

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

                if (hasBackup)
                    try { File.Delete(backupVpk); } catch { }

                return true;
            }
            catch (Exception ex)
            {
                log($"Failed to replace VPK: {ex.Message}");
                _logger?.Log($"VpkReplacerService replace failed: {ex}");

                if (hasBackup)
                {
                    try { File.Move(backupVpk, currentVpk, overwrite: true); }
                    catch (Exception restoreEx)
                    {
                        _logger?.Log($"VpkReplacerService: backup restore failed: {restoreEx}");
                    }
                }
                return false;
            }
        }

        private static async Task<bool> TryMoveAsideAsync(string source, string destination, CancellationToken ct)
        {
            const int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    File.Move(source, destination);
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

        private async Task WaitForFileReadyAsync(string filePath, CancellationToken ct)
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
                catch (IOException)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }
    }
}

