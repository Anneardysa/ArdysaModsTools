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
using System.Threading;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Mods
{
    /// <summary>
    /// Service for monitoring file system changes in the Dota 2 directory.
    /// Uses FileSystemWatcher for real-time updates with debouncing.
    /// </summary>
    public sealed class FileWatcherService : IDisposable
    {
        #region Private Fields

        private readonly IAppLogger? _logger;
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly object _lock = new();
        private System.Threading.Timer? _debounceTimer;
        private string? _currentPath;
        private bool _disposed;
        
        /// <summary>
        /// Debounce delay to prevent multiple rapid-fire events.
        /// </summary>
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

        #endregion

        #region Events

        /// <summary>
        /// Fired when relevant mod files change (debounced).
        /// </summary>
        public event Action? OnFilesChanged;

        /// <summary>
        /// Fired immediately when a change is detected (for UI feedback).
        /// </summary>
        public event Action? OnChangeDetected;

        #endregion

        #region Constructor

        public FileWatcherService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start watching the Dota 2 directory for relevant file changes.
        /// </summary>
        public void StartWatching(string dotaPath)
        {
            if (string.IsNullOrEmpty(dotaPath) || !Directory.Exists(dotaPath))
            {
                _logger?.Log("[WATCHER] Invalid path, not starting file watcher");
                return;
            }

            lock (_lock)
            {
                StopWatchingInternal();
                _currentPath = dotaPath;

                try
                {
                    // Watch for ModsPack VPK changes
                    var modsDir = Path.GetDirectoryName(Path.Combine(dotaPath, DotaPaths.ModsVpk));
                    if (!string.IsNullOrEmpty(modsDir) && Directory.Exists(modsDir))
                    {
                        var vpkWatcher = CreateWatcher(modsDir, "pak01_dir.vpk");
                        _watchers.Add(vpkWatcher);
                    }

                    // Watch for gameinfo changes
                    var gameInfoDir = Path.GetDirectoryName(Path.Combine(dotaPath, DotaPaths.GameInfo));
                    if (!string.IsNullOrEmpty(gameInfoDir) && Directory.Exists(gameInfoDir))
                    {
                        var gameInfoWatcher = CreateWatcher(gameInfoDir, "gameinfo_branchspecific.gi");
                        _watchers.Add(gameInfoWatcher);
                    }

                    // Watch for signatures changes
                    var sigDir = Path.GetDirectoryName(Path.Combine(dotaPath, DotaPaths.Signatures));
                    if (!string.IsNullOrEmpty(sigDir) && Directory.Exists(sigDir))
                    {
                        var sigWatcher = CreateWatcher(sigDir, "*.signatures");
                        _watchers.Add(sigWatcher);
                    }

                    // Watch for steam.inf changes (Dota updates)
                    var steamInfPath = Path.Combine(dotaPath, "steam.inf");
                    var steamInfDir = Path.GetDirectoryName(steamInfPath);
                    if (!string.IsNullOrEmpty(steamInfDir) && Directory.Exists(steamInfDir))
                    {
                        var steamWatcher = CreateWatcher(steamInfDir, "steam.inf");
                        _watchers.Add(steamWatcher);
                    }

                    _logger?.Log($"[WATCHER] Started watching {_watchers.Count} locations");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[WATCHER] Error starting watchers: {ex.Message}");
                    StopWatchingInternal();
                }
            }
        }

        /// <summary>
        /// Stop watching for file changes.
        /// </summary>
        public void StopWatching()
        {
            lock (_lock)
            {
                StopWatchingInternal();
            }
        }

        /// <summary>
        /// Check if currently watching a path.
        /// </summary>
        public bool IsWatching => _watchers.Count > 0;

        #endregion

        #region Private Methods

        private FileSystemWatcher CreateWatcher(string path, string filter)
        {
            var watcher = new FileSystemWatcher(path, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite 
                             | NotifyFilters.FileName 
                             | NotifyFilters.Size
                             | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += (s, e) => OnFileEvent(s, e);
            watcher.Error += OnWatcherError;

            return watcher;
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            _logger?.Log($"[WATCHER] File change detected: {e.Name} ({e.ChangeType})");
            
            // Fire immediate event for UI feedback (shows "Checking..." state)
            OnChangeDetected?.Invoke();

            // Debounce the actual status refresh
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(
                    _ => OnFilesChanged?.Invoke(),
                    null,
                    DebounceDelay,
                    Timeout.InfiniteTimeSpan);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger?.Log($"[WATCHER] Error: {e.GetException().Message}");
            
            // Try to restart watchers
            if (_currentPath != null)
            {
                StartWatching(_currentPath);
            }
        }

        private void StopWatchingInternal()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
            
            _logger?.Log("[WATCHER] Stopped watching");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            
            lock (_lock)
            {
                StopWatchingInternal();
            }
            
            _disposed = true;
        }

        #endregion
    }
}
