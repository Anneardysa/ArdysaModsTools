using System;
using System.Diagnostics;
using System.IO;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// A stream wrapper that reports download speed and progress.
    /// </summary>
    public class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly IProgress<SpeedMetrics>? _speedProgress;
        private readonly Stopwatch _stopwatch;
        private readonly long? _totalLength;
        private long _bytesRead;
        private long _lastReportedBytes;
        private DateTime _lastReportTime;

        public ProgressStream(Stream inner, IProgress<SpeedMetrics>? speedProgress, long? totalLength = null)
        {
            _inner = inner;
            _speedProgress = speedProgress;
            _totalLength = totalLength;
            _stopwatch = Stopwatch.StartNew();
            _lastReportTime = DateTime.UtcNow;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            ReportProgress(read);
            return read;
        }

        public override async System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            int read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            ReportProgress(read);
            return read;
        }

        public override async System.Threading.Tasks.ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken);
            ReportProgress(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = _inner.Read(buffer);
            ReportProgress(read);
            return read;
        }

        private void ReportProgress(int read)
        {
            if (read <= 0) return;
            
            _bytesRead += read;
            var now = DateTime.UtcNow;
            var elapsedSinceLastReport = (now - _lastReportTime).TotalSeconds;

            // Report every 500ms or so to avoid UI spam
            if (elapsedSinceLastReport >= 0.5)
            {
                long bytesDelta = _bytesRead - _lastReportedBytes;
                string speedStr = SpeedCalculator.FormatSpeed(bytesDelta, elapsedSinceLastReport);
                
                string details = _totalLength.HasValue 
                    ? $"{_bytesRead / 1024 / 1024} / {_totalLength.Value / 1024 / 1024} MB"
                    : $"{_bytesRead / 1024 / 1024} MB";

                _speedProgress?.Report(new SpeedMetrics 
                { 
                    DownloadSpeed = speedStr,
                    ProgressDetails = details
                });
                
                _lastReportedBytes = _bytesRead;
                _lastReportTime = now;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _stopwatch.Stop();
                // Reset speed to default when download stream closes
                _speedProgress?.Report(new SpeedMetrics { DownloadSpeed = "-- MB/S" });
            }
            base.Dispose(disposing);
        }
    }
}
