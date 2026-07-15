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
    public interface IFileOperation
    {
        Task ExecuteAsync(CancellationToken ct);

        Task RollbackAsync(CancellationToken ct);

        void Cleanup();
    }
}
