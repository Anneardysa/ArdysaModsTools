/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading;

namespace ArdysaModsTools.Core.Models
{
    public class ProgressContext
    {
        public IProgress<int> Progress { get; init; }

        public IProgress<string> Status { get; init; }

        public IProgress<SpeedMetrics> Speed { get; init; }

        public CancellationToken Token { get; init; }

        public ProgressContext()
        {
            Progress = new Progress<int>();
            Status = new Progress<string>();
            Speed = new Progress<SpeedMetrics>();
            Token = CancellationToken.None;
        }

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

        public void ReportProgress(int percent)
        {
            Progress.Report(Math.Clamp(percent, 0, 100));
        }

        public void ReportStatus(string message)
        {
            Status.Report(message);
        }

        public void ReportSpeed(SpeedMetrics metrics)
        {
            Speed.Report(metrics);
        }

        public void ThrowIfCancellationRequested()
        {
            Token.ThrowIfCancellationRequested();
        }

        public bool IsCancellationRequested => Token.IsCancellationRequested;

        public static ProgressContext Empty => new()
        {
            Progress = new EmptyProgress<int>(),
            Status = new EmptyProgress<string>(),
            Speed = new EmptyProgress<SpeedMetrics>(),
            Token = CancellationToken.None
        };

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

    internal sealed class EmptyProgress<T> : IProgress<T>
    {
        public void Report(T value) { }
    }
}

