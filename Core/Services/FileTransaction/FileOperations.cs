/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.FileTransactions
{
    /// <summary>
    /// Operation to copy a file with rollback support.
    /// </summary>
    public sealed class CopyOperation : IFileOperation
    {
        private readonly string _source;
        private readonly string _destination;
        private readonly bool _overwrite;
        private string? _backupPath;
        private bool _existedBefore;

        public CopyOperation(string source, string destination, bool overwrite = true)
        {
            _source = source;
            _destination = destination;
            _overwrite = overwrite;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_destination);
            if (_existedBefore && _overwrite)
            {
                // Create backup of the destination if it exists
                _backupPath = _destination + ".transaction_bak";
                File.Copy(_destination, _backupPath, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_destination)!);
            File.Copy(_source, _destination, _overwrite);
            await Task.CompletedTask;
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _destination, true);
            }
            else if (!_existedBefore && File.Exists(_destination))
            {
                File.Delete(_destination);
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }
    }

    /// <summary>
    /// Operation to move a file with rollback support.
    /// </summary>
    public sealed class MoveOperation : IFileOperation
    {
        private readonly string _source;
        private readonly string _destination;
        private string? _backupPath;
        private bool _existedBefore;

        public MoveOperation(string source, string destination)
        {
            _source = source;
            _destination = destination;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_destination);
            if (_existedBefore)
            {
                _backupPath = _destination + ".transaction_bak";
                File.Move(_destination, _backupPath, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_destination)!);
            File.Move(_source, _destination, true);
            await Task.CompletedTask;
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            // Restore destination to source
            if (File.Exists(_destination))
            {
                File.Move(_destination, _source, true);
            }

            // Restore backup if it existed
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Move(_backupPath, _destination, true);
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }
    }

    /// <summary>
    /// Operation to delete a file with rollback support.
    /// </summary>
    public sealed class DeleteOperation : IFileOperation
    {
        private readonly string _path;
        private string? _backupPath;
        private bool _existedBefore;

        public DeleteOperation(string path)
        {
            _path = path;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_path);
            if (_existedBefore)
            {
                _backupPath = _path + ".transaction_bak";
                File.Copy(_path, _backupPath, true);
                File.Delete(_path);
            }
            await Task.CompletedTask;
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _path, true);
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }
    }

    /// <summary>
    /// Operation to create a directory with rollback support.
    /// </summary>
    public sealed class CreateDirectoryOperation : IFileOperation
    {
        private readonly string _path;
        private bool _created;

        public CreateDirectoryOperation(string path)
        {
            _path = path;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            if (!Directory.Exists(_path))
            {
                Directory.CreateDirectory(_path);
                _created = true;
            }
            await Task.CompletedTask;
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_created && Directory.Exists(_path))
            {
                // Note: Only deletes if empty to be safe
                if (Directory.GetFileSystemEntries(_path).Length == 0)
                {
                    Directory.Delete(_path);
                }
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            // Nothing to do for directory creation
        }
    }
}
