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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Update
{
    public sealed class DotaPatchWatcherService : IDisposable
    {
        private readonly IAppLogger? _logger;
        private readonly DotaVersionService _versionService;
        
        private FileSystemWatcher? _steamInfWatcher;
        private FileSystemWatcher? _signaturesWatcher;
        private CancellationTokenSource? _debounceCts;
        
        private string? _dotaPath;
        private DotaVersionInfo? _lastKnownVersion;
        private bool _isWatching;
        private bool _disposed;
        
        private const int DebounceDelayMs = 3000;
        private int _pendingChangeCount;
        
        public event Action<PatchDetectedEventArgs>? OnPatchDetected;
        
        public bool IsWatching => _isWatching;
        
        public DotaPatchWatcherService(IAppLogger? logger = null)
        {
            _logger = logger;
            _versionService = new DotaVersionService(logger ?? NullLogger.Instance);
        }
        
        public async Task StartWatchingAsync(string dotaPath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DotaPatchWatcherService));
            if (string.IsNullOrWhiteSpace(dotaPath)) return;
            if (_isWatching && _dotaPath == dotaPath) return;
            
            StopWatching();
            
            _dotaPath = dotaPath;
            
            _lastKnownVersion = await _versionService.GetVersionInfoAsync(dotaPath);
            
            _logger?.Log($"[PatchWatcher] Starting watcher for: {dotaPath}");
            _logger?.Log($"[PatchWatcher] Current version: {_lastKnownVersion.DotaVersion} (Build {_lastKnownVersion.BuildNumber})");
            
            string steamInfPath = Path.Combine(dotaPath, DotaPaths.SteamInfWindows);
            if (File.Exists(steamInfPath))
            {
                _steamInfWatcher = CreateWatcher(
                    Path.GetDirectoryName(steamInfPath)!,
                    Path.GetFileName(steamInfPath),
                    "steam.inf");
            }
            
            string signaturesPath = Path.Combine(dotaPath, DotaPaths.SignaturesWindows);
            if (File.Exists(signaturesPath))
            {
                _signaturesWatcher = CreateWatcher(
                    Path.GetDirectoryName(signaturesPath)!,
                    Path.GetFileName(signaturesPath),
                    "dota.signatures");
            }
            
            _isWatching = true;
            _logger?.Log("[PatchWatcher] Watcher active");
        }
        
        public void StopWatching()
        {
            bool wasWatching = _isWatching;
            
            _steamInfWatcher?.Dispose();
            _steamInfWatcher = null;
            
            _signaturesWatcher?.Dispose();
            _signaturesWatcher = null;
            
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
            
            _isWatching = false;
            
            if (wasWatching)
            {
                _logger?.Log("[PatchWatcher] Watcher stopped");
            }
        }
        
        private FileSystemWatcher CreateWatcher(string directory, string filter, string displayName)
        {
            var watcher = new FileSystemWatcher(directory, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            
            watcher.Changed += (s, e) => OnFileChanged(e.FullPath, displayName);
            watcher.Created += (s, e) => OnFileChanged(e.FullPath, displayName);
            
            watcher.Error += (s, e) =>
            {
                _logger?.Log($"[PatchWatcher] Error watching {displayName}: {e.GetException().Message}");
            };
            
            return watcher;
        }
        
        private void OnFileChanged(string filePath, string displayName)
        {
            Interlocked.Increment(ref _pendingChangeCount);
            
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _debounceCts, newCts);
            try { oldCts?.Cancel(); } catch { }
            oldCts?.Dispose();
            
            var token = newCts.Token;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, token);
                    int changes = Interlocked.Exchange(ref _pendingChangeCount, 0);
                    if (changes > 0)
                    {
                        _logger?.Log($"[PatchWatcher] Detected {changes} file change(s), checking for patch...");
                        await CheckForPatchAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[PatchWatcher] Error checking patch: {ex.Message}");
                }
            });
        }
        
        private async Task CheckForPatchAsync()
        {
            if (_dotaPath == null || _lastKnownVersion == null) return;
            
            var currentVersion = await _versionService.GetVersionInfoAsync(_dotaPath);
            
            bool versionChanged = !string.Equals(
                _lastKnownVersion.DotaVersion, 
                currentVersion.DotaVersion, 
                StringComparison.OrdinalIgnoreCase);
                
            bool digestChanged = !string.Equals(
                _lastKnownVersion.CurrentDigest, 
                currentVersion.CurrentDigest, 
                StringComparison.OrdinalIgnoreCase);
            
            if (versionChanged || digestChanged)
            {
                _logger?.Log($"[PatchWatcher] Dota 2 update detected! Version: {_lastKnownVersion.DotaVersion} → {currentVersion.DotaVersion}");
                _logger?.Log($"[PatchWatcher] Re-patching required - your mods need to be updated.");
                
                var args = new PatchDetectedEventArgs
                {
                    OldVersion = _lastKnownVersion.DotaVersion,
                    NewVersion = currentVersion.DotaVersion,
                    OldDigest = _lastKnownVersion.CurrentDigest,
                    NewDigest = currentVersion.CurrentDigest,
                    DetectedAt = DateTime.Now,
                    RequiresRepatch = currentVersion.NeedsRepatch
                };
                
                _lastKnownVersion = currentVersion;
                
                OnPatchDetected?.Invoke(args);
            }
            else
            {
                _logger?.Log($"[PatchWatcher] File activity detected - no Dota 2 update found. Mods still working.");
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            StopWatching();
        }
    }
}

