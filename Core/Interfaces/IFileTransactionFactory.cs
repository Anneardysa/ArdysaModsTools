/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Factory for creating file transactions. Register this with DI to enable
    /// transaction creation throughout the application.
    /// </summary>
    public interface IFileTransactionFactory
    {
        /// <summary>
        /// Creates a new file transaction.
        /// </summary>
        /// <param name="operationName">
        /// Optional name for the transaction, used in logging.
        /// </param>
        /// <returns>A new <see cref="IFileTransaction"/> instance.</returns>
        IFileTransaction CreateTransaction(string? operationName = null);
    }
}
