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
            return RunAsync(parent, initialStatus, operation, hideDownloadSpeed: false);
        }

        public static async Task<OperationResult> RunAsync(
            Form parent,
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed)
        {
            using var cts = new CancellationTokenSource();
            var overlay = new ProgressOverlay
            {
                HideDownloadSpeed = hideDownloadSpeed
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
                        // Note: ProgressDetails is intentionally NOT shown in substatus to avoid overwriting log messages
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
