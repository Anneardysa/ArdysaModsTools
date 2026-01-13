using System;
using System.Threading;

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Unified context for progress reporting during operations.
    /// Consolidates progress, status, speed metrics, and cancellation into a single object.
    /// </summary>
    public class ProgressContext
    {
        /// <summary>
        /// Progress reporter (0-100 percentage).
        /// </summary>
        public IProgress<int> Progress { get; init; }

        /// <summary>
        /// Status message reporter for current operation step.
        /// </summary>
        public IProgress<string> Status { get; init; }

        /// <summary>
        /// Speed metrics reporter for download/write speeds.
        /// </summary>
        public IProgress<SpeedMetrics> Speed { get; init; }

        /// <summary>
        /// Cancellation token to stop the operation.
        /// </summary>
        public CancellationToken Token { get; init; }

        /// <summary>
        /// Creates a new ProgressContext with default empty implementations.
        /// </summary>
        public ProgressContext()
        {
            Progress = new Progress<int>();
            Status = new Progress<string>();
            Speed = new Progress<SpeedMetrics>();
            Token = CancellationToken.None;
        }

        /// <summary>
        /// Creates a new ProgressContext with the specified reporters.
        /// </summary>
        public ProgressContext(
            IProgress<int>? progress,
            IProgress<string>? status,
            IProgress<SpeedMetrics>? speed,
            CancellationToken token = default)
        {
            Progress = progress ?? new Progress<int>();
            Status = status ?? new Progress<string>();
            Speed = speed ?? new Progress<SpeedMetrics>();
            Token = token;
        }

        /// <summary>
        /// Reports progress percentage (0-100).
        /// </summary>
        public void ReportProgress(int percent)
        {
            Progress.Report(Math.Clamp(percent, 0, 100));
        }

        /// <summary>
        /// Reports a status message.
        /// </summary>
        public void ReportStatus(string message)
        {
            Status.Report(message);
        }

        /// <summary>
        /// Reports speed metrics.
        /// </summary>
        public void ReportSpeed(SpeedMetrics metrics)
        {
            Speed.Report(metrics);
        }

        /// <summary>
        /// Throws OperationCanceledException if cancellation is requested.
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            Token.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Gets whether cancellation has been requested.
        /// </summary>
        public bool IsCancellationRequested => Token.IsCancellationRequested;

        /// <summary>
        /// Gets an empty progress context with no-op reporters.
        /// </summary>
        public static ProgressContext Empty => new()
        {
            Progress = new EmptyProgress<int>(),
            Status = new EmptyProgress<string>(),
            Speed = new EmptyProgress<SpeedMetrics>(),
            Token = CancellationToken.None
        };

        /// <summary>
        /// Creates a ProgressContext from a CancellationTokenSource.
        /// </summary>
        public static ProgressContext FromCancellation(CancellationTokenSource cts)
        {
            return new ProgressContext
            {
                Progress = new Progress<int>(),
                Status = new Progress<string>(),
                Speed = new Progress<SpeedMetrics>(),
                Token = cts.Token
            };
        }
    }

    /// <summary>
    /// A no-op progress reporter that discards all reports.
    /// </summary>
    internal sealed class EmptyProgress<T> : IProgress<T>
    {
        public void Report(T value) { }
    }
}
