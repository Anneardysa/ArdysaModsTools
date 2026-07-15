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
            
            overlay.Owner = parent;
            overlay.StartPosition = FormStartPosition.CenterParent;
            overlay.Show();

            try
            {
                await overlay.InitializeAsync();
                
                await overlay.UpdateStatusAsync(initialStatus);
                await overlay.UpdateProgressAsync(0);

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
                        
                        if (metrics.TotalFiles > 0)
                        {
                            await overlay.UpdateFileProgressAsync(metrics.CurrentFile, metrics.TotalFiles);
                        }
                        else if (metrics.TotalBytes > 0)
                        {
                            double downloadedMB = metrics.DownloadedBytes / (1024.0 * 1024.0);
                            double totalMB = metrics.TotalBytes / (1024.0 * 1024.0);
                            await overlay.UpdateDownloadProgressAsync(downloadedMB, totalMB);
                        }
                        else
                        {
                            await overlay.HideDownloadProgressAsync();
                        }
                    }
                    catch { }
                });

                var statusProgress = new Progress<string>(async s =>
                {
                    try { await overlay.UpdateStatusAsync(s); } catch { }
                });
                var substatusProgress = new Progress<string>(async s =>
                {
                    try { await overlay.UpdateSubstatusAsync(s); } catch { }
                });
                var percentProgress = new Progress<int>(async p =>
                {
                    try { await overlay.UpdateProgressAsync(p); } catch { }
                });

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
                return OperationResult.Canceled("Operation cancelled by user.");
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

