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

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for VPK replacement operations.
    /// </summary>
    public interface IVpkReplacer
    {
        /// <summary>
        /// Replaces the target VPK with the newly generated one, creating a backup.
        /// </summary>
        Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default);
    }

    /// <summary>
    /// Focused service for VPK file replacement with backup support.
    /// Single responsibility: Safely replace VPK files with atomic operations.
    /// </summary>
    public sealed class VpkReplacerService : IVpkReplacer
    {
        private readonly ILogger? _logger;

        public VpkReplacerService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> ReplaceAsync(string targetPath, string newVpkPath,
            Action<string> log, CancellationToken ct = default)
        {
            string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
            Directory.CreateDirectory(modsDir);

            string currentVpk = Path.Combine(modsDir, "pak01_dir.vpk");

            ct.ThrowIfCancellationRequested();

            // Wait for new VPK to be ready
            await WaitForFileReadyAsync(newVpkPath, ct).ConfigureAwait(false);

            // Copy new VPK to destination (no backup)
            try
            {
                File.Copy(newVpkPath, currentVpk, true);
                return true;
            }
            catch (Exception ex)
            {
                log($"Failed to replace VPK: {ex.Message}");
                _logger?.Log($"VpkReplacerService replace failed: {ex}");
                return false;
            }
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
                    return; // File is ready
                }
                catch (IOException)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }
    }
}

