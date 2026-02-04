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
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.App;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.UI.Forms;
using ArdysaModsTools.UI.Services;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Presenter for the Settings form.
    /// Handles all logic and coordination between services.
    /// </summary>
    public class SettingsPresenter
    {
        private readonly SettingsForm _view;
        private readonly IConfigService _configService;
        private readonly AppLifecycleService _lifecycleService;
        private readonly CacheCleaningService _cacheService;
        private readonly UpdaterService _updaterService;
        private readonly TrayService? _trayService;

        public SettingsPresenter(
            SettingsForm view,
            IConfigService configService,
            AppLifecycleService lifecycleService,
            CacheCleaningService cacheService,
            UpdaterService updaterService,
            TrayService? trayService = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _updaterService = updaterService ?? throw new ArgumentNullException(nameof(updaterService));
            _trayService = trayService;

            // Wire up events
            _view.RunOnStartupChanged += OnRunOnStartupChanged;
            _view.MinimizeToTrayChanged += OnMinimizeToTrayChanged;
            _view.ShowNotificationsChanged += OnShowNotificationsChanged;
            _view.CheckUpdatesClicked += OnCheckUpdatesClicked;
            _view.ClearCacheClicked += OnClearCacheClicked;

            // Initialize view with current values
            InitializeView();
        }

        private void InitializeView()
        {
            // Load current settings
            _view.RunOnStartup = _lifecycleService.IsRunOnStartupEnabled;
            _view.MinimizeToTray = _trayService?.MinimizeToTrayEnabled ?? false;
            _view.ShowNotifications = _trayService?.NotificationsEnabled ?? true;

            // Set version
            _view.VersionText = $"Version: {_updaterService.CurrentVersion}";

            // Calculate cache size async
            Task.Run(() =>
            {
                long cacheSize = _cacheService.GetCacheSizeBytes();
                string formatted = CacheCleaningService.FormatBytes(cacheSize);

                if (_view.IsHandleCreated)
                {
                    _view.BeginInvoke(new Action(() =>
                    {
                        _view.CacheSizeText = $"Cache: {formatted}";
                    }));
                }
            });
        }

        private void OnRunOnStartupChanged(object? sender, EventArgs e)
        {
            bool success = _lifecycleService.SetRunOnStartup(_view.RunOnStartup);
            if (!success)
            {
                // Revert the checkbox if failed
                _view.RunOnStartup = _lifecycleService.IsRunOnStartupEnabled;
                _view.ShowStatus("Failed to update startup setting.", isSuccess: false);
            }
        }

        private void OnMinimizeToTrayChanged(object? sender, EventArgs e)
        {
            if (_trayService != null)
            {
                _trayService.MinimizeToTrayEnabled = _view.MinimizeToTray;
                _configService.Save();
            }
        }

        private void OnShowNotificationsChanged(object? sender, EventArgs e)
        {
            if (_trayService != null)
            {
                _trayService.NotificationsEnabled = _view.ShowNotifications;
                _configService.Save();
            }
        }

        private async void OnCheckUpdatesClicked(object? sender, EventArgs e)
        {
            _view.SetBusy(true);

            try
            {
                await _updaterService.CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                _view.ShowStatus($"Update check failed: {ex.Message}", isSuccess: false);
            }
            finally
            {
                _view.SetBusy(false);
            }
        }

        private async void OnClearCacheClicked(object? sender, EventArgs e)
        {
            _view.SetBusy(true);

            try
            {
                var result = await _cacheService.ClearAllCacheAsync();

                if (result.Success)
                {
                    string message = $"Cache cleared!\n" +
                                   $"Files deleted: {result.FilesDeleted}\n" +
                                   $"Space freed: {CacheCleaningService.FormatBytes(result.BytesFreed)}";
                    _view.ShowStatus(message);
                    _view.CacheSizeText = "Cache: 0 B";
                }
                else
                {
                    _view.ShowStatus($"Cache clearing failed: {result.ErrorMessage}", isSuccess: false);
                }
            }
            catch (Exception ex)
            {
                _view.ShowStatus($"Error clearing cache: {ex.Message}", isSuccess: false);
            }
            finally
            {
                _view.SetBusy(false);
            }
        }
    }
}
