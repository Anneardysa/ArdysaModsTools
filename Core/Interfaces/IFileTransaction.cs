/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Represents an atomic file transaction that can execute multiple operations
    /// with automatic rollback on failure.
    /// </summary>
    public interface IFileTransaction : IDisposable
    {
        /// <summary>
        /// Gets the number of operations queued in this transaction.
        /// </summary>
        int OperationCount { get; }

        /// <summary>
        /// Fired when operation progress changes.
        /// Parameters: (currentOperation, totalOperations)
        /// </summary>
        event Action<int, int>? ProgressChanged;

        /// <summary>
        /// Adds an operation to the transaction queue.
        /// </summary>
        /// <param name="operation">The operation to add.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the transaction has already been committed or rolled back.
        /// </exception>
        void AddOperation(IFileOperation operation);

        /// <summary>
        /// Executes all queued operations in order.
        /// Automatically rolls back on failure.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task ExecuteAsync(CancellationToken ct = default);

        /// <summary>
        /// Manually rolls back all executed operations in reverse order.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task RollbackAsync(CancellationToken ct = default);

        /// <summary>
        /// Commits the transaction, cleaning up temporary backups.
        /// After commit, rollback is no longer possible.
        /// </summary>
        void Commit();
    }
}
