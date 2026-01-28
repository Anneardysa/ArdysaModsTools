/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.FileTransactions
{
    /// <summary>
    /// Factory for creating file transactions with proper DI support.
    /// </summary>
    public sealed class FileTransactionFactory : IFileTransactionFactory
    {
        private readonly ILogger? _logger;

        /// <summary>
        /// Creates a new FileTransactionFactory.
        /// </summary>
        /// <param name="logger">Optional logger for transaction logging.</param>
        public FileTransactionFactory(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public IFileTransaction CreateTransaction(string? operationName = null)
        {
            return new FileTransaction(_logger, operationName);
        }
    }
}
