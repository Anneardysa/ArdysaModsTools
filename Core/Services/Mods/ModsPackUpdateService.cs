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

public sealed class ModsPackUpdateService
{
    private readonly ModInstallerService _installer;
    private readonly IAppLogger? _logger;

    public ModsPackUpdateService(ModInstallerService installer, IAppLogger? logger = null)
    {
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _logger = logger;
    }

    public async Task<bool> CheckForUpdateAsync(string targetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return false;

        try
        {
            ct.ThrowIfCancellationRequested();

            bool hasUpdate = await _installer
                .CheckForNewerModsPackAsync(targetPath, ct)
                .ConfigureAwait(false);

            if (hasUpdate)
                _logger?.Log("[ModsPack] Update available — remote hash differs from local.");

            return hasUpdate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ModsPack] Update check failed: {ex.Message}");
            return false;
        }
    }
}
