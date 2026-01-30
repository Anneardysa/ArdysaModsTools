/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.FileTransactions
{
    /// <summary>
    /// Manages a sequence of file operations as an atomic transaction.
    /// If any operation fails, all previously executed operations are rolled back.
    /// </summary>
    public sealed class FileTransaction : IFileTransaction
    {
        private readonly List<IFileOperation> _operations = new();
        private readonly IAppLogger? _logger;
        private readonly string? _operationName;
        private bool _committed;
        private bool _rolledBack;

        /// <inheritdoc/>
        public int OperationCount => _operations.Count;

        /// <inheritdoc/>
        public event Action<int, int>? ProgressChanged;

        /// <summary>
        /// Creates a new FileTransaction.
        /// </summary>
        /// <param name="logger">Optional logger for operation logging.</param>
        /// <param name="operationName">Optional name for contextual logging.</param>
        public FileTransaction(IAppLogger? logger = null, string? operationName = null)
        {
            _logger = logger;
            _operationName = operationName;
        }

        /// <inheritdoc/>
        public void AddOperation(IFileOperation operation)
        {
            if (_committed || _rolledBack)
                throw new InvalidOperationException("Cannot add operations to a completed transaction.");
            
            _operations.Add(operation);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            var prefix = string.IsNullOrEmpty(_operationName) ? "" : $"[{_operationName}] ";
            
            for (int i = 0; i < _operations.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                
                try
                {
                    ProgressChanged?.Invoke(i + 1, _operations.Count);
                    await _operations[i].ExecuteAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger?.Log($"{prefix}Operation cancelled. Starting rollback...");
                    await RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.Log($"{prefix}Operation {i + 1}/{_operations.Count} failed: {ex.Message}. Starting rollback...");
                    await RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_committed) return;
            _rolledBack = true;

            var prefix = string.IsNullOrEmpty(_operationName) ? "" : $"[{_operationName}] ";

            // Rollback in reverse order
            for (int i = _operations.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _operations[i].RollbackAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"{prefix}Rollback failed for operation {i + 1}: {ex.Message}");
                }
            }
            
            _logger?.Log($"{prefix}Rollback completed for {_operations.Count} operations.");
        }

        /// <inheritdoc/>
        public void Commit()
        {
            if (_rolledBack) 
                throw new InvalidOperationException("Cannot commit a rolled back transaction.");
            
            _committed = true;

            foreach (var op in _operations)
            {
                try
                {
                    op.Cleanup();
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Cleanup failed: {ex.Message}");
                }
            }
            
            _operations.Clear();
        }

        /// <summary>
        /// Disposes the transaction. If not committed or rolled back,
        /// attempts to clean up any temporary files.
        /// </summary>
        public void Dispose()
        {
            if (!_committed && !_rolledBack)
            {
                // Implicit cleanup if disposed without commit
                foreach (var op in _operations)
                {
                    try { op.Cleanup(); } catch { }
                }
            }
        }
    }
}

