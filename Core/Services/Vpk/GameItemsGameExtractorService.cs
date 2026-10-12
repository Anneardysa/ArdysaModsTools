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
            Action<string>? log = null, CancellationToken ct = default, bool normalize = true);

        Task<bool> ExtractModItemsGameAsync(string targetPath, string destFilePath,
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
                _logger?.LogDebug("GameItemsGameExtractor: extractDir was empty.");
                return false;
            }

            string dotaRoot = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpkPath = Path.Combine(dotaRoot, DotaPaths.GameVpk.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(gameVpkPath))
            {
                log("Dota 2 game files (pak01_dir.vpk) not found — verify game files in Steam.");
                _logger?.LogDebug($"GameItemsGameExtractor: game VPK missing at {gameVpkPath}");
                return false;
            }

            string dest = Path.Combine(extractDir, "scripts", "items", "items_game.txt");

            string? detail = null;
            if (!await ExtractItemsGameAsync(gameVpkPath, dest,
                    m => { detail = m; log(m); }, ct).ConfigureAwait(false))
            {
                if (detail is null)
                    log("Could not read package from your Dota 2 installation.");
                return false;
            }

            log("Latest package loaded.");

            await ItemsGameBaselineStore.WritePendingAsync(dotaRoot, gameVpkPath, dest, ct).ConfigureAwait(false);

            return true;
        }

        public async Task<bool> ExtractItemsGameAsync(string vpkPath, string destFilePath,
            Action<string>? log = null, CancellationToken ct = default, bool normalize = true)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(vpkPath) || string.IsNullOrWhiteSpace(destFilePath))
                return false;

            if (!File.Exists(vpkPath))
            {
                _logger?.LogDebug($"GameItemsGameExtractor: VPK missing at {vpkPath}");
                return false;
            }

            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtractPath))
            {
                log?.Invoke("Extraction tool not found.");
                _logger?.LogDebug($"GameItemsGameExtractor: HLExtract.exe missing at {hlExtractPath}");
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
                    _logger?.LogDebug($"GameItemsGameExtractor: items_game.txt not found in {vpkPath}");
                    return false;
                }

                long size = new FileInfo(extracted).Length;
                if (size < MinItemsGameBytes)
                {
                    log?.Invoke("Package data looks incomplete.");
                    _logger?.LogDebug($"GameItemsGameExtractor: extracted items_game.txt too small ({size} bytes) from {vpkPath}.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                if (normalize)
                    await CopyForBuildAsync(extracted, destFilePath, ct).ConfigureAwait(false);
                else
                    File.Copy(extracted, destFilePath, overwrite: true);

                _logger?.LogDebug($"GameItemsGameExtractor: extracted items_game.txt ({size} bytes) from {vpkPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"GameItemsGameExtractor error: {ex}");
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch {  }
            }
        }

        internal static async Task CopyForBuildAsync(string source, string dest, CancellationToken ct)
        {
            const int BufferSize = 1 << 20;

            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, useAsync: true);
            using var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None,
                BufferSize, useAsync: true);

            var buffer = new byte[BufferSize];
            bool afterCr = false;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, BufferSize), ct).ConfigureAwait(false)) > 0)
            {
                int kept = 0;
                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];

                    if (b == (byte)'\r')
                    {
                        buffer[kept++] = (byte)'\n';
                        afterCr = true;
                        continue;
                    }

                    bool swallow = afterCr && b == (byte)'\n';
                    afterCr = false;
                    if (!swallow) buffer[kept++] = b;
                }

                await output.WriteAsync(buffer.AsMemory(0, kept), ct).ConfigureAwait(false);
            }
        }

        internal static async Task<bool> RepairLineEndingsInPlaceAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path) || !await ContainsCarriageReturnAsync(path, ct).ConfigureAwait(false))
                return false;

            string lf = path + ".lf";
            await CopyForBuildAsync(path, lf, ct).ConfigureAwait(false);
            File.Move(lf, path, overwrite: true);
            return true;
        }

        private static async Task<bool> ContainsCarriageReturnAsync(string path, CancellationToken ct)
        {
            var buffer = new byte[1 << 20];
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                if (Array.IndexOf(buffer, (byte)'\r', 0, read) >= 0) return true;
            return false;
        }

        public async Task<bool> ExtractModItemsGameAsync(string targetPath, string destFilePath,
            Action<string>? log = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(destFilePath))
                return false;

            string root = PathUtility.NormalizeTargetPath(targetPath);

            string looseItemsGame = Path.Combine(root, "game", "_ArdysaMods", "scripts", "items", "items_game.txt");
            if (File.Exists(looseItemsGame))
            {
                try
                {
                    long size = new FileInfo(looseItemsGame).Length;
                    if (size >= MinItemsGameBytes)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                        File.Copy(looseItemsGame, destFilePath, overwrite: true);
                        _logger?.LogDebug($"GameItemsGameExtractor: found loose items_game.txt ({size} bytes) in _ArdysaMods.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"GameItemsGameExtractor: reading loose items_game failed: {ex.Message}");
                }
            }

            string modVpk = Path.Combine(root, DotaPaths.ModsVpk.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(modVpk))
            {
                bool ok = await ExtractItemsGameAsync(modVpk, destFilePath, log, ct).ConfigureAwait(false);
                if (ok) return true;
            }

            string protVpk = ProtectedVpkStore.VpkPath(root);
            if (File.Exists(protVpk))
            {
                bool ok = await ExtractItemsGameAsync(protVpk, destFilePath, log, ct).ConfigureAwait(false);
                if (ok) return true;
            }

            return false;
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
                    _logger?.LogDebug($"HLExtract: {err.Trim()}");
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
                    _logger?.LogDebug($"HLExtract.exe timed out after {ExtractTimeoutMinutes} minutes (items_game).");
                    return false;
                }
            }

            int exitCode = await tcs.Task.ConfigureAwait(false);
            if (exitCode != 0)
            {
                _logger?.LogDebug($"HLExtract.exe exited with code {exitCode} (items_game extraction).");
                return false;
            }

            return true;
        }
    }
}
