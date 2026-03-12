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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Mods;

/// <summary>
/// Checks whether a newer ModsPack is available by comparing remote and local hashes.
/// Only returns true when the user has an existing local install and the remote hash differs.
/// </summary>
public sealed class ModsPackUpdateService
{
    private readonly ModInstallerService _installer;
    private readonly IAppLogger? _logger;

    public ModsPackUpdateService(ModInstallerService installer, IAppLogger? logger = null)
    {
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _logger = logger;
    }

    /// <summary>
    /// Checks if a ModsPack update is available for the given Dota 2 path.
    /// Returns true only when the user has an existing install AND a newer version exists.
    /// Returns false on any error (no false positives).
    /// </summary>
    /// <param name="targetPath">The Dota 2 installation path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a ModsPack update is available; false otherwise.</returns>
    public async Task<bool> CheckForUpdateAsync(string targetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return false;

        try
        {
            ct.ThrowIfCancellationRequested();

            var (hasNewer, hasLocalInstall) = await _installer
                .CheckForNewerModsPackAsync(targetPath, ct)
                .ConfigureAwait(false);

            // Only trigger update dialog for existing installs with a newer version
            if (hasNewer && hasLocalInstall)
            {
                _logger?.Log("[ModsPack] Update available — remote hash differs from local.");
                return true;
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ModsPack] Update check failed: {ex.Message}");
            return false; // Fail safely — no false positives
        }
    }
}
