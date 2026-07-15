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
    public sealed class FileTransaction : IDisposable
    {
        private readonly List<IFileOperation> _operations = new();
        private readonly IAppLogger? _logger;
        private readonly string? _operationName;
        private bool _committed;
        private bool _rolledBack;
        private int _attemptedCount;

        public int OperationCount => _operations.Count;

        public event Action<int, int>? ProgressChanged;

        public FileTransaction(IAppLogger? logger = null, string? operationName = null)
        {
            _logger = logger;
            _operationName = operationName;
        }

        public void AddOperation(IFileOperation operation)
        {
            if (_committed || _rolledBack)
                throw new InvalidOperationException("Cannot add operations to a completed transaction.");
            
            _operations.Add(operation);
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            var prefix = string.IsNullOrEmpty(_operationName) ? "" : $"[{_operationName}] ";
            
            for (int i = 0; i < _operations.Count; i++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    ProgressChanged?.Invoke(i + 1, _operations.Count);

                    _attemptedCount = i + 1;
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

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_committed || _rolledBack) return;
            _rolledBack = true;

            var prefix = string.IsNullOrEmpty(_operationName) ? "" : $"[{_operationName}] ";

            if (_attemptedCount == 0)
            {
                _logger?.Log($"{prefix}No operations were executed, nothing to rollback.");
                return;
            }

            for (int i = _attemptedCount - 1; i >= 0; i--)
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
            
            _logger?.Log($"{prefix}Rollback completed for {_attemptedCount} operations.");
        }

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

        public void Dispose()
        {
            if (!_committed && !_rolledBack)
            {
                foreach (var op in _operations)
                {
                    try { op.Cleanup(); } catch { }
                }
            }
        }
    }
}

