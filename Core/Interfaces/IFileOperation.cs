/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Defines a single undoable file operation within a transaction.
    /// </summary>
    public interface IFileOperation
    {
        /// <summary>
        /// Executes the operation (e.g., Copy, Move).
        /// Should handle creation of temporary backups if necessary.
        /// </summary>
        Task ExecuteAsync(CancellationToken ct);

        /// <summary>
        /// Undoes the operation (e.g., restores from backup).
        /// </summary>
        Task RollbackAsync(CancellationToken ct);

        /// <summary>
        /// Cleaning up temporary backups after a successful transaction.
        /// </summary>
        void Cleanup();
    }
}
