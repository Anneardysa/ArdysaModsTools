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
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.UI.Forms;

namespace ArdysaModsTools.UI
{
    /// <summary>
    /// Context for a background operation running with a ProgressOverlay.
    /// Provides cancellation and multiple progress reporters for the UI.
    /// </summary>
    public class ProgressOperationRunnerContext
    {
        public CancellationToken Token { get; internal set; }
        public IProgress<SpeedMetrics> Speed { get; internal set; }
        public IProgress<string> Status { get; internal set; }
        public IProgress<string> Substatus { get; internal set; }
        public IProgress<int> Progress { get; internal set; }

        public ProgressOperationRunnerContext(
            CancellationToken token,
            IProgress<SpeedMetrics> speed,
            IProgress<string> status,
            IProgress<string> substatus,
            IProgress<int> progress)
        {
            Token = token;
            Speed = speed;
            Status = status;
            Substatus = substatus;
            Progress = progress;
        }
    }

    /// <summary>
    /// Orchestrates a background operation with a ProgressOverlay UI.
    /// Handles showing/closing the form, updating metrics, and cancellation.
    /// </summary>
    public static class ProgressOperationRunner
    {
        public static Task<OperationResult> RunAsync(
            Form parent,
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation)
        {
            return RunAsync(parent, initialStatus, operation, hideDownloadSpeed: false, showPreview: false);
        }

        public static async Task<OperationResult> RunAsync(
            Form parent,
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed,
            bool showPreview = false)
        {
            using var cts = new CancellationTokenSource();
            var overlay = new ProgressOverlay
            {
                HideDownloadSpeed = hideDownloadSpeed,
                ShowPreview = showPreview
            };
            
            // Show overlay on top of parent
            overlay.Owner = parent;
            overlay.StartPosition = FormStartPosition.CenterParent;
            overlay.Show();

            try
            {
                await overlay.InitializeAsync();
                
                await overlay.UpdateStatusAsync(initialStatus);
                await overlay.UpdateProgressAsync(0);

                // Closing the overlay cancels the operation
                overlay.FormClosing += (s, e) => {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                };

                var speedProgress = new Progress<SpeedMetrics>(async metrics =>
                {
                    try
                    {
                        if (metrics.DownloadSpeed != null) await overlay.UpdateDownloadSpeedAsync(metrics.DownloadSpeed);
                        if (metrics.WriteSpeed != null) await overlay.UpdateWriteSpeedAsync(metrics.WriteSpeed);
                        
                        // Priority: File count > Byte count > Hide
                        // File count (for Building, Installing phases)
                        if (metrics.TotalFiles > 0)
                        {
                            await overlay.UpdateFileProgressAsync(metrics.CurrentFile, metrics.TotalFiles);
                        }
                        // Byte count (for Download phase)
                        else if (metrics.TotalBytes > 0)
                        {
                            double downloadedMB = metrics.DownloadedBytes / (1024.0 * 1024.0);
                            double totalMB = metrics.TotalBytes / (1024.0 * 1024.0);
                            await overlay.UpdateDownloadProgressAsync(downloadedMB, totalMB);
                        }
                        // TotalBytes = 0 and TotalFiles = 0 signals to hide
                        else
                        {
                            await overlay.HideDownloadProgressAsync();
                        }
                    }
                    catch { }
                });

                var statusProgress = new Progress<string>(async s => await overlay.UpdateStatusAsync(s));
                var substatusProgress = new Progress<string>(async s => await overlay.UpdateSubstatusAsync(s));
                var percentProgress = new Progress<int>(async p => await overlay.UpdateProgressAsync(p));

                var context = new ProgressOperationRunnerContext(
                    cts.Token,
                    speedProgress,
                    statusProgress,
                    substatusProgress,
                    percentProgress);

                var result = await operation(context);

                if (result.Success)
                {
                    await overlay.UpdateProgressAsync(100);
                    await overlay.UpdateStatusAsync("Operation complete!");
                    await Task.Delay(500);
                    overlay.Complete();
                }
                else
                {
                    overlay.Close();
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                overlay.Close();
                return new OperationResult { Success = false, Message = "Operation cancelled by user." };
            }
            catch (Exception ex)
            {
                overlay.Close();
                return new OperationResult { Success = false, Message = $"Error: {ex.Message}", Exception = ex };
            }
            finally
            {
                if (!overlay.IsDisposed)
                {
                    try { overlay.Dispose(); } catch { }
                }
            }
        }
    }
}

