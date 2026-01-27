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
    /// </summary>
    public sealed class FileTransaction : IDisposable
    {
        private readonly List<IFileOperation> _operations = new();
        private readonly ILogger? _logger;
        private bool _committed;
        private bool _rolledBack;

        public FileTransaction(ILogger? logger = null)
        {
            _logger = logger;
        }

        public void AddOperation(IFileOperation operation)
        {
            if (_committed || _rolledBack)
                throw new InvalidOperationException("Cannot add operations to a completed transaction.");
            
            _operations.Add(operation);
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            for (int i = 0; i < _operations.Count; i++)
            {
                try
                {
                    await _operations[i].ExecuteAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Operation failed: {ex.Message}. Starting rollback...");
                    await RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_committed) return;
            _rolledBack = true;

            // Rollback in reverse order
            for (int i = _operations.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _operations[i].RollbackAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Rollback failed for operation {i}: {ex.Message}");
                }
            }
        }

        public void Commit()
        {
            if (_rolledBack) throw new InvalidOperationException("Cannot commit a rolled back transaction.");
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

        public void Dispose()
        {
            if (!_committed && !_rolledBack)
            {
                // Implicit rollback if disposed without commit
                // Note: Async rollback in Dispose is tricky, but we should at least try cleanup
                foreach (var op in _operations) op.Cleanup();
            }
        }
    }
}
