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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public interface IVpkExtractor
    {
        Task<bool> ExtractAsync(string hlExtractPath, string vpkPath, string extractDir,
            Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            bool requireItemsGame = true);
    }

    public sealed class VpkExtractorService : IVpkExtractor
    {
        private readonly IAppLogger? _logger;

        public VpkExtractorService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> ExtractAsync(string hlExtractPath, string vpkPath, string extractDir,
            Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            bool requireItemsGame = true)
        {
            if (string.IsNullOrWhiteSpace(hlExtractPath))
            {
                log("HLExtract path not specified.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(vpkPath))
            {
                log("VPK path not specified.");
                return false;
            }

            if (!File.Exists(hlExtractPath))
            {
                var ex = new VpkException(ErrorCodes.VPK_TOOL_NOT_FOUND, 
                    $"HLExtract.exe not found at: {hlExtractPath}");
                _logger?.Log($"[{ex.ErrorCode}] {ex.Message}");
                log("Extraction tool not found.");
                return false;
            }

            if (!File.Exists(vpkPath))
            {
                var ex = new VpkException(ErrorCodes.VPK_FILE_NOT_FOUND,
                    $"VPK file not found: {vpkPath}");
                _logger?.Log($"[{ex.ErrorCode}] {ex.Message}");
                log("VPK file not found.");
                return false;
            }

            log("Extracting...");

            var psi = new ProcessStartInfo
            {
                FileName = hlExtractPath,
                Arguments = $"-p \"{vpkPath}\" -d \"{extractDir}\" -e \"root\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            int exitCode;
            string stdErr;
            try
            {
                (exitCode, stdErr) = await RunProcessAsync(psi, log, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                log("Extraction canceled.");
                throw;
            }
            catch (Exception ex)
            {
                var vpkEx = new VpkException(ErrorCodes.VPK_EXTRACT_FAILED,
                    $"VPK extraction failed: {ex.Message}", ex);
                _logger?.LogError($"[{vpkEx.ErrorCode}] {vpkEx.Message}", ex);
                log($"Extraction process failed: {ex.Message}");
                return false;
            }

            string toolDiagnostics = $"HLExtract exit code {exitCode}" +
                (string.IsNullOrWhiteSpace(stdErr) ? "" : $"; stderr: {stdErr.Trim()}");
            _logger?.Log($"HLExtract finished: {toolDiagnostics} (vpk: {vpkPath})",
                exitCode == 0 ? LogLevel.Debug : LogLevel.Warning);

            ct.ThrowIfCancellationRequested();

            if (!await ReorganizeExtractedFilesAsync(extractDir, log, ct, speedProgress).ConfigureAwait(false))
            {
                return false;
            }

            if (!Directory.EnumerateFileSystemEntries(extractDir).Any())
            {
                var ex = new VpkException(ErrorCodes.VPK_EXTRACT_FAILED,
                    $"Extraction produced no files. {toolDiagnostics}");
                _logger?.LogError($"[{ex.ErrorCode}] {ex.Message}");
                log(exitCode == 0
                    ? "Extraction produced no files — the archive may be corrupted or incomplete."
                    : $"Extraction tool failed (code {exitCode}) and produced no files.");
                return false;
            }

            if (requireItemsGame)
            {
                string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
                if (!File.Exists(itemsGamePath))
                {
                    var ex = new VpkException(ErrorCodes.VPK_INVALID_FORMAT,
                        $"items_game.txt missing after extraction - VPK may be invalid. {toolDiagnostics}");
                    _logger?.LogError($"[{ex.ErrorCode}] {ex.Message}");
                    log("Package missing after extraction — the mod archive is incomplete or corrupted.");
                    return false;
                }
            }

            log("Extraction completed.");
            return true;
        }

        private async Task<bool> ReorganizeExtractedFilesAsync(string extractDir, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            string rootDir = Path.Combine(extractDir, "root");
            if (!Directory.Exists(rootDir))
            {
                return true;
            }

            foreach (var file in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(rootDir, file);
                string dest = Path.Combine(extractDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                
                try
                {
                    var fileInfo = new FileInfo(file);
                    long size = fileInfo.Length;
                    var sw = Stopwatch.StartNew();
                    File.Move(file, dest, true);
                    sw.Stop();
                    
                    if (sw.Elapsed.TotalSeconds > 0)
                    {
                        var speed = size / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                        speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = $"{speed:F1} MB/S" });
                    }
                }
                catch (Exception ex)
                {
                    log($"Warning: moving extracted file failed: {ex.Message}");
                    _logger?.Log($"Move extracted file failed: {ex}");
                }
            }
            
            speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = "0.0 MB/S" });

            try 
            { 
                Directory.Delete(rootDir, true); 
            } 
            catch (Exception ex)
            {
                _logger?.Log($"Failed to delete root dir: {ex.Message}");
            }

            return true;
        }

        private const int ExtractTimeoutMinutes = 10;

        private async Task<(int ExitCode, string StdErr)> RunProcessAsync(
            ProcessStartInfo psi, Action<string> log, CancellationToken ct)
        {
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            proc.Start();

            var stdOutTask = proc.StandardOutput.ReadToEndAsync();
            var stdErrTask = proc.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(ExtractTimeoutMinutes));

            using (ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(); } catch {  }
            }))
            {
                var completedTask = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(Timeout.Infinite, timeoutCts.Token)
                ).ConfigureAwait(false);

                if (completedTask != tcs.Task)
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }

                    if (!ct.IsCancellationRequested)
                    {
                        log($"Extraction timed out after {ExtractTimeoutMinutes} minutes");
                        _logger?.LogError($"[{ErrorCodes.VPK_EXTRACT_FAILED}] HLExtract.exe timed out after {ExtractTimeoutMinutes} minutes");
                    }
                    throw new OperationCanceledException("Extraction timed out");
                }
            }

            int exitCode = await tcs.Task.ConfigureAwait(false);
            string stdErr = await stdErrTask.ConfigureAwait(false);
            string stdOut = await stdOutTask.ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stdOut))
                _logger?.LogDebug($"HLExtract stdout (tail): {Tail(stdOut, 1000)}");

            return (exitCode, stdErr);
        }

        private static string Tail(string text, int maxChars) =>
            text.Length <= maxChars ? text.Trim() : text[^maxChars..].Trim();
    }
}

