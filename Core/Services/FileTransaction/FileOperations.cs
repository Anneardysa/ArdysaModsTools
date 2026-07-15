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
                _backupPath = _destination + ".transaction_bak";
                File.Copy(_destination, _backupPath, true);

                try { File.SetAttributes(_destination, FileAttributes.Normal); } catch { }
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
            if (File.Exists(_destination))
            {
                File.Move(_destination, _source, true);
            }

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
                if (Directory.GetFileSystemEntries(_path).Length == 0)
                {
                    Directory.Delete(_path);
                }
            }
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
        }
    }

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

                try { File.SetAttributes(_path, FileAttributes.Normal); } catch { }
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

                try { File.SetAttributes(_path, FileAttributes.Normal); } catch { }
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

            if (_existedBefore)
            {
                _backupPath = _path + ".transaction_bak";
                File.Copy(_path, _backupPath, true);

                try { File.SetAttributes(_path, FileAttributes.Normal); } catch { }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            _tempPath = _path + ".tmp";
            await File.WriteAllBytesAsync(_tempPath, _content, ct).ConfigureAwait(false);

            File.Move(_tempPath, _path, true);
            _tempPath = null;
        }

        public async Task RollbackAsync(CancellationToken ct)
        {
            if (_tempPath != null && File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { }
            }

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
            if (_tempPath != null && File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { }
            }
            
            if (_backupPath != null && File.Exists(_backupPath))
            {
                try { File.Delete(_backupPath); } catch { }
            }
        }
    }
}
