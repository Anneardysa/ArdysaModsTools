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

    /// <summary>
    /// Operation to write byte content to a file with rollback support.
    /// Creates a backup of the existing file if it exists.
    /// </summary>
    public sealed class WriteContentOperation : IFileOperation
    {
        private readonly string _path;
        private readonly byte[] _content;
        private string? _backupPath;
        private bool _existedBefore;

        public WriteContentOperation(string path, byte[] content)
        {
            _path = path;
            _content = content;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_path);
            if (_existedBefore)
            {
                _backupPath = _path + ".transaction_bak";
                File.Copy(_path, _backupPath, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllBytesAsync(_path, _content, ct).ConfigureAwait(false);
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _path, true);
            }
            else if (!_existedBefore && File.Exists(_path))
            {
                File.Delete(_path);
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
    /// Operation to write text content to a file with rollback support.
    /// Creates a backup of the existing file if it exists.
    /// </summary>
    public sealed class WriteTextOperation : IFileOperation
    {
        private readonly string _path;
        private readonly string _content;
        private readonly System.Text.Encoding _encoding;
        private string? _backupPath;
        private bool _existedBefore;

        public WriteTextOperation(string path, string content, System.Text.Encoding? encoding = null)
        {
            _path = path;
            _content = content;
            _encoding = encoding ?? System.Text.Encoding.UTF8;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_path);
            if (_existedBefore)
            {
                _backupPath = _path + ".transaction_bak";
                File.Copy(_path, _backupPath, true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, _content, _encoding, ct).ConfigureAwait(false);
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _path, true);
            }
            else if (!_existedBefore && File.Exists(_path))
            {
                File.Delete(_path);
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
    /// Operation to atomically replace a file using the temp-file swap pattern.
    /// Writes content to a temp file, then moves it to the destination.
    /// </summary>
    public sealed class ReplaceFileOperation : IFileOperation
    {
        private readonly string _path;
        private readonly byte[] _content;
        private string? _tempPath;
        private string? _backupPath;
        private bool _existedBefore;

        public ReplaceFileOperation(string path, byte[] content)
        {
            _path = path;
            _content = content;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            _existedBefore = File.Exists(_path);
            
            // Backup existing file
            if (_existedBefore)
            {
                _backupPath = _path + ".transaction_bak";
                File.Copy(_path, _backupPath, true);
            }

            // Write to temp file first
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            _tempPath = _path + ".tmp";
            await File.WriteAllBytesAsync(_tempPath, _content, ct).ConfigureAwait(false);

            // Atomic move
            File.Move(_tempPath, _path, true);
            _tempPath = null; // Clear since file is now moved
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            // Clean up temp file if it still exists
            if (_tempPath != null && File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { }
            }

            // Restore from backup
            if (_existedBefore && _backupPath != null && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _path, true);
            }
            else if (!_existedBefore && File.Exists(_path))
            {
                File.Delete(_path);
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            // Clean up temp file if still exists (shouldn't normally)
            if (_tempPath != null && File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { }
            }
            
            // Clean up backup
            if (_backupPath != null && File.Exists(_backupPath))
            {
                try { File.Delete(_backupPath); } catch { }
            }
        }
    }
}
