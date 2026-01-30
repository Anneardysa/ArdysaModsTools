/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Controls.Actions
{
    /// <summary>
    /// Presenter for the ActionPanel UserControl.
    /// Manages action button state (Install/Disable/Cancel) and operation lifecycle.
    /// Raises events for parent components to handle actual operations.
    /// </summary>
    public class ActionPresenter
    {
        private readonly IActionPanelView _view;
        private readonly IAppLogger _logger;

        private string? _currentPath;
        private bool _operationInProgress;
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Gets whether an operation is currently in progress.
        /// </summary>
        public bool IsOperationInProgress => _operationInProgress;

        /// <summary>
        /// Gets the current cancellation token source for the active operation.
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource => _cancellationTokenSource;

        /// <summary>
        /// Event raised when Install button is clicked and path is valid.
        /// Parent component should handle the actual installation.
        /// </summary>
        public event Func<CancellationToken, Task>? OnInstallRequested;

        /// <summary>
        /// Event raised when Disable button is clicked and path is valid.
        /// Parent component should handle the actual disable operation.
        /// </summary>
        public event Func<CancellationToken, Task>? OnDisableRequested;

        /// <summary>
        /// Event raised when Cancel button is clicked.
        /// </summary>
        public event System.Action? OnCancelRequested;

        /// <summary>
        /// Event raised when an operation starts.
        /// Used to notify parent components for UI updates.
        /// </summary>
        public event Action<string>? OperationStarted;

        /// <summary>
        /// Event raised when an operation ends (success or failure).
        /// </summary>
        public event Action<bool>? OperationEnded;

        /// <summary>
        /// Creates a new ActionPresenter.
        /// </summary>
        /// <param name="view">The view to control.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public ActionPresenter(IActionPanelView view, IAppLogger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to view events
            _view.InstallRequested += OnInstallClicked;
            _view.DisableRequested += OnDisableClicked;
            _view.CancelRequested += OnCancelClicked;

            // Initialize state
            UpdateButtonStates();
        }

        /// <summary>
        /// Sets the current Dota 2 path and updates button states.
        /// </summary>
        /// <param name="path">The Dota 2 path, or null if not detected.</param>
        public void SetDotaPath(string? path)
        {
            _currentPath = path;
            _logger.LogDebug($"[ACTION] Path set: {path ?? "(null)"}");
            UpdateButtonStates();
        }

        /// <summary>
        /// Starts an operation, showing progress state and returning a cancellation token.
        /// </summary>
        /// <param name="operationName">Name of the operation for logging.</param>
        /// <returns>CancellationTokenSource for the operation.</returns>
        public CancellationTokenSource StartOperation(string operationName)
        {
            if (_operationInProgress)
            {
                throw new InvalidOperationException("An operation is already in progress.");
            }

            _operationInProgress = true;
            _cancellationTokenSource = new CancellationTokenSource();

            _view.ShowOperationInProgress(operationName);
            _view.SetCancelVisible(true);
            _view.SetAllButtonsEnabled(false);

            _logger.Log($"[ACTION] Operation started: {operationName}");
            OperationStarted?.Invoke(operationName);

            return _cancellationTokenSource;
        }

        /// <summary>
        /// Ends the current operation and restores normal button states.
        /// </summary>
        /// <param name="success">Whether the operation completed successfully.</param>
        public void EndOperation(bool success = true)
        {
            if (!_operationInProgress)
                return;

            _operationInProgress = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _view.ShowOperationComplete();
            _view.SetCancelVisible(false);
            UpdateButtonStates();

            _logger.Log($"[ACTION] Operation ended: {(success ? "success" : "failed")}");
            OperationEnded?.Invoke(success);
        }

        /// <summary>
        /// Cancels the current operation.
        /// </summary>
        public void CancelOperation()
        {
            if (!_operationInProgress || _cancellationTokenSource == null)
                return;

            _logger.Log("[ACTION] Operation cancellation requested");
            _cancellationTokenSource.Cancel();
            OnCancelRequested?.Invoke();
        }

        /// <summary>
        /// Enables or disables all action buttons.
        /// Used by parent components for external state control.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetButtonsEnabled(bool enabled)
        {
            if (_operationInProgress)
                return; // Don't override during operation

            _view.SetAllButtonsEnabled(enabled);
        }

        /// <summary>
        /// Updates mod status and adjusts button states accordingly.
        /// </summary>
        /// <param name="status">The current mod status.</param>
        public void UpdateFromModStatus(ModStatus? status)
        {
            // Both Install and Disable are enabled when path is valid
            // Status only affects visual feedback, not button availability
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasPath = !string.IsNullOrEmpty(_currentPath);
            bool canAct = hasPath && !_operationInProgress;

            _view.SetInstallEnabled(canAct);
            _view.SetDisableEnabled(canAct);
            _view.SetCancelVisible(_operationInProgress);
        }

        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath) || _operationInProgress)
                return;

            if (OnInstallRequested == null)
            {
                _logger.LogWarning("[ACTION] Install requested but no handler attached");
                return;
            }

            var cts = StartOperation("Installing...");

            try
            {
                await OnInstallRequested.Invoke(cts.Token);
                EndOperation(true);
            }
            catch (OperationCanceledException)
            {
                _logger.Log("[ACTION] Install cancelled");
                EndOperation(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("[ACTION] Install failed", ex);
                EndOperation(false);
            }
        }

        private async void OnDisableClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath) || _operationInProgress)
                return;

            if (OnDisableRequested == null)
            {
                _logger.LogWarning("[ACTION] Disable requested but no handler attached");
                return;
            }

            var cts = StartOperation("Disabling...");

            try
            {
                await OnDisableRequested.Invoke(cts.Token);
                EndOperation(true);
            }
            catch (OperationCanceledException)
            {
                _logger.Log("[ACTION] Disable cancelled");
                EndOperation(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("[ACTION] Disable failed", ex);
                EndOperation(false);
            }
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            CancelOperation();
        }
    }
}
