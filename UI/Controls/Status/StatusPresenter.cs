/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Drawing;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Controls.Status
{
    /// <summary>
    /// Presenter for the StatusIndicator UserControl.
    /// Contains all business logic for mod status checking, isolated from UI.
    /// </summary>
    public class StatusPresenter
    {
        private readonly IStatusIndicatorView _view;
        private readonly IStatusService _statusService;
        private readonly IAppLogger _logger;

        /// <summary>
        /// Event raised when status changes, containing full status info.
        /// </summary>
        public event Action<ModStatusInfo?>? StatusChanged;

        /// <summary>
        /// Creates a new StatusPresenter.
        /// </summary>
        /// <param name="view">The view to control.</param>
        /// <param name="statusService">Service for checking mod status.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public StatusPresenter(
            IStatusIndicatorView view,
            IStatusService statusService,
            IAppLogger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to view events
            _view.RefreshRequested += async (s, e) => await RefreshStatusAsync();
        }

        private string? _currentPath;

        /// <summary>
        /// Sets the Dota 2 path and checks status.
        /// </summary>
        public async Task SetPathAndCheckAsync(string? dotaPath)
        {
            _currentPath = dotaPath;

            if (string.IsNullOrEmpty(dotaPath))
            {
                _view.SetStatus(StatusColors.Grey, "Not Checked");
                return;
            }

            await CheckStatusAsync(dotaPath);
        }

        /// <summary>
        /// Refreshes the current status.
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                _view.SetStatus(StatusColors.Grey, "No path set");
                return;
            }

            _view.SetRefreshEnabled(false);
            try
            {
                _view.ShowCheckingState();
                
                // Force refresh from service
                var statusInfo = await _statusService.ForceRefreshAsync(_currentPath);
                UpdateViewFromStatus(statusInfo);
                StatusChanged?.Invoke(statusInfo);
                
                _logger.Log($"[STATUS] Refreshed: {statusInfo?.StatusText ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                _view.ShowError("Error");
                _logger.LogError("[STATUS] Refresh failed", ex);
            }
            finally
            {
                _view.SetRefreshEnabled(true);
            }
        }

        private async Task CheckStatusAsync(string dotaPath)
        {
            try
            {
                _view.ShowCheckingState();
                
                var statusInfo = await _statusService.GetDetailedStatusAsync(dotaPath);
                UpdateViewFromStatus(statusInfo);
                StatusChanged?.Invoke(statusInfo);
            }
            catch (Exception ex)
            {
                _view.ShowError("Error");
                _logger.LogError("[STATUS] Check failed", ex);
            }
        }

        private void UpdateViewFromStatus(ModStatusInfo? statusInfo)
        {
            if (statusInfo == null)
            {
                _view.SetStatus(StatusColors.Grey, "Unknown");
                return;
            }

            var color = statusInfo.Status switch
            {
                ModStatus.Ready => StatusColors.Green,
                ModStatus.NeedUpdate => StatusColors.Orange,
                ModStatus.NotInstalled => StatusColors.Grey,
                ModStatus.Error => StatusColors.Red,
                _ => StatusColors.Grey
            };

            _view.SetStatus(color, statusInfo.StatusText);
        }
    }

    /// <summary>
    /// Predefined status indicator colors.
    /// </summary>
    public static class StatusColors
    {
        public static readonly Color Green = Color.FromArgb(0, 200, 100);
        public static readonly Color Orange = Color.FromArgb(255, 165, 0);
        public static readonly Color Red = Color.FromArgb(255, 80, 80);
        public static readonly Color Grey = Color.FromArgb(150, 150, 150);
    }
}
