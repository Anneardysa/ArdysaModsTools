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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public interface IGameItemsGameExtractor
    {
        Task<bool> RefreshFromGameAsync(string targetPath, string extractDir,
            Action<string> log, CancellationToken ct = default);

        Task<bool> ExtractItemsGameAsync(string vpkPath, string destFilePath,
            Action<string>? log = null, CancellationToken ct = default);
    }

    public sealed class GameItemsGameExtractorService : IGameItemsGameExtractor
    {
        private const long MinItemsGameBytes = 100_000;

        private const int ExtractTimeoutMinutes = 10;

        private readonly IAppLogger? _logger;

        public GameItemsGameExtractorService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> RefreshFromGameAsync(string targetPath, string extractDir,
            Action<string> log, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                log("Dota 2 path not set — cannot read latest package.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(extractDir))
            {
                _logger?.Log("GameItemsGameExtractor: extractDir was empty.");
                return false;
            }

            string dotaRoot = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpkPath = Path.Combine(dotaRoot, DotaPaths.GameVpk.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(gameVpkPath))
            {
                log("Dota 2 game files (pak01_dir.vpk) not found — verify game files in Steam.");
                _logger?.Log($"GameItemsGameExtractor: game VPK missing at {gameVpkPath}");
                return false;
            }

            string dest = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
            if (!await ExtractItemsGameAsync(gameVpkPath, dest, log, ct).ConfigureAwait(false))
            {
                log("Could not read package from your Dota 2 installation.");
                return false;
            }

            log("Latest package loaded.");

            await ItemsGameBaselineStore.WritePendingAsync(dotaRoot, gameVpkPath, dest, ct).ConfigureAwait(false);

            return true;
        }

        public async Task<bool> ExtractItemsGameAsync(string vpkPath, string destFilePath,
            Action<string>? log = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(vpkPath) || string.IsNullOrWhiteSpace(destFilePath))
                return false;

            if (!File.Exists(vpkPath))
            {
                _logger?.Log($"GameItemsGameExtractor: VPK missing at {vpkPath}");
                return false;
            }

            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtractPath))
            {
                log?.Invoke("Extraction tool not found.");
                _logger?.Log($"GameItemsGameExtractor: HLExtract.exe missing at {hlExtractPath}");
                return false;
            }

            string tempDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaItemsGame_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string? extracted = null;
                foreach (var entry in new[] { "root/scripts/items", "scripts/items" })
                {
                    ct.ThrowIfCancellationRequested();

                    string arguments = $"-p \"{vpkPath}\" -d \"{tempDir}\" -e \"{entry}\"";
                    if (!await RunHlExtractAsync(hlExtractPath, arguments, ct).ConfigureAwait(false))
                        continue;

                    extracted = Directory
                        .EnumerateFiles(tempDir, "items_game.txt", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (extracted != null) break;
                }

                if (extracted is null)
                {
                    _logger?.Log($"GameItemsGameExtractor: items_game.txt not found in {vpkPath}");
                    return false;
                }

                long size = new FileInfo(extracted).Length;
                if (size < MinItemsGameBytes)
                {
                    log?.Invoke("Package data looks incomplete.");
                    _logger?.Log($"GameItemsGameExtractor: extracted items_game.txt too small ({size} bytes) from {vpkPath}.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                File.Copy(extracted, destFilePath, overwrite: true);

                _logger?.Log($"GameItemsGameExtractor: extracted items_game.txt ({size} bytes) from {vpkPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"GameItemsGameExtractor error: {ex}");
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch {  }
            }
        }

        private async Task<bool> RunHlExtractAsync(string hlExtractPath, string arguments, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = hlExtractPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<int>();
            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            proc.Start();

            _ = proc.StandardOutput.ReadToEndAsync();
            _ = Task.Run(async () =>
            {
                var err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(err))
                    _logger?.Log($"HLExtract: {err.Trim()}");
            });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(ExtractTimeoutMinutes));

            using (ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(); } catch {  }
            }))
            {
                var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch {  }
                    ct.ThrowIfCancellationRequested();
                    _logger?.Log($"HLExtract.exe timed out after {ExtractTimeoutMinutes} minutes (items_game).");
                    return false;
                }
            }

            int exitCode = await tcs.Task.ConfigureAwait(false);
            if (exitCode != 0)
            {
                _logger?.Log($"HLExtract.exe exited with code {exitCode} (items_game extraction).");
                return false;
            }

            return true;
        }
    }
}
