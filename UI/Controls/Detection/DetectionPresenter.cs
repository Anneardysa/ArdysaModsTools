/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.UI.Controls.Detection
{
    /// <summary>
    /// Presenter for the DetectionPanel UserControl.
    /// Contains all business logic for Dota 2 path detection, isolated from UI.
    /// </summary>
    public class DetectionPresenter
    {
        private readonly IDetectionPanelView _view;
        private readonly IDetectionService _detectionService;
        private readonly IConfigService _configService;
        private readonly IAppLogger _logger;

        /// <summary>
        /// The currently detected Dota 2 path.
        /// </summary>
        public string? CurrentPath { get; private set; }

        /// <summary>
        /// Event raised when a path is successfully detected.
        /// </summary>
        public event Action<string>? PathDetected;

        /// <summary>
        /// Creates a new DetectionPresenter.
        /// </summary>
        public DetectionPresenter(
            IDetectionPanelView view,
            IDetectionService detectionService,
            IConfigService configService,
            IAppLogger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to view events
            _view.AutoDetectRequested += async (s, e) => await AutoDetectAsync();
            _view.ManualDetectRequested += (s, e) => ManualDetect();

            // Load last path from config
            LoadLastPath();
        }

        /// <summary>
        /// Loads the last detected path from configuration.
        /// </summary>
        public void LoadLastPath()
        {
            var lastPath = _configService.GetLastTargetPath();
            if (!string.IsNullOrEmpty(lastPath) && Directory.Exists(lastPath))
            {
                CurrentPath = lastPath;
                _view.SetDetectedPath(lastPath);
            }
        }

        /// <summary>
        /// Performs automatic Dota 2 path detection.
        /// </summary>
        public async Task AutoDetectAsync()
        {
            _view.SetButtonsEnabled(false);
            _view.ShowDetectingState();

            try
            {
                string? detectedPath = await _detectionService.AutoDetectAsync();

                if (!string.IsNullOrEmpty(detectedPath))
                {
                    OnPathDetected(detectedPath);
                    _logger.Log($"[DETECT] Auto-detected: {detectedPath}");
                }
                else
                {
                    _view.ShowFailed("Could not auto-detect Dota 2 path");
                    _logger.LogWarning("[DETECT] Auto-detection failed");
                }
            }
            catch (Exception ex)
            {
                _view.ShowFailed("Detection error");
                _logger.LogError("[DETECT] Auto-detect failed", ex);
            }
            finally
            {
                _view.SetButtonsEnabled(true);
            }
        }

        /// <summary>
        /// Opens folder dialog for manual path selection.
        /// </summary>
        public void ManualDetect()
        {
            _view.SetButtonsEnabled(false);

            try
            {
                string? selectedPath = _detectionService.ManualDetect();

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    OnPathDetected(selectedPath);
                    _logger.Log($"[DETECT] Manual path: {selectedPath}");
                }
                else
                {
                    // User cancelled - restore state
                    if (!string.IsNullOrEmpty(CurrentPath))
                    {
                        _view.SetDetectedPath(CurrentPath);
                    }
                    else
                    {
                        _view.ShowFailed("No path selected");
                    }
                }
            }
            catch (Exception ex)
            {
                _view.ShowFailed("Selection error");
                _logger.LogError("[DETECT] Manual detect failed", ex);
            }
            finally
            {
                _view.SetButtonsEnabled(true);
            }
        }

        private void OnPathDetected(string path)
        {
            CurrentPath = path;
            _configService.SetLastTargetPath(path);
            _view.ShowSuccess(path);
            PathDetected?.Invoke(path);
        }
    }
}
