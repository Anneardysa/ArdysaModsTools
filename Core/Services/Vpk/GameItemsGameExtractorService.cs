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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Provides the latest <c>scripts/items/items_game.txt</c> by extracting it straight from the
    /// user's detected Dota 2 game VPK, so generation never relies on a bundled (stale) copy.
    /// </summary>
    public interface IGameItemsGameExtractor
    {
        /// <summary>
        /// Extracts <c>scripts/items/items_game.txt</c> from the detected Dota 2 game VPK
        /// (<c>{targetPath}/game/dota/pak01_dir.vpk</c>) and overwrites
        /// <c>{extractDir}/scripts/items/items_game.txt</c> with it.
        /// </summary>
        /// <param name="targetPath">Detected Dota 2 installation root ("...\dota 2 beta").</param>
        /// <param name="extractDir">Extracted base folder whose items_game.txt should be refreshed.</param>
        /// <returns>
        /// <c>true</c> when the latest items_game.txt was injected; <c>false</c> on any failure
        /// (missing game VPK / HLExtract.exe, extraction error, or implausibly small result).
        /// Callers treat <c>false</c> as fatal — without items_game.txt there is nothing to patch.
        /// </returns>
        Task<bool> RefreshFromGameAsync(string targetPath, string extractDir,
            Action<string> log, CancellationToken ct = default);
    }

    /// <summary>
    /// Extracts the live <c>items_game.txt</c> from the detected Dota 2 game VPK using HLExtract.exe.
    /// Single responsibility: source the latest items_game.txt from the local install.
    /// </summary>
    public sealed class GameItemsGameExtractorService : IGameItemsGameExtractor
    {
        /// <summary>
        /// Lower bound (bytes) below which an extracted items_game.txt is treated as truncated/invalid.
        /// The real file is multiple MB; this floor only guards against empty/garbage results.
        /// </summary>
        private const long MinItemsGameBytes = 100_000;

        private const int ExtractTimeoutMinutes = 10;

        private readonly IAppLogger? _logger;

        public GameItemsGameExtractorService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        // [AMT:PRO] Implements IGameItemsGameExtractor and invokes HLExtract.exe with a fixed CLI flag
        // sequence (-p/-d/-e) that mirrors the proven game-VPK extraction in AssetModifierService.
        // Flag drift silently breaks extraction — verify against Core/Services/Vpk/ before changing.
        public async Task<bool> RefreshFromGameAsync(string targetPath, string extractDir,
            Action<string> log, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                log("Dota 2 path not set — cannot read latest items_game.txt.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(extractDir))
            {
                _logger?.Log("GameItemsGameExtractor: extractDir was empty.");
                return false;
            }

            string dotaRoot = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpkPath = Path.Combine(dotaRoot, "game", "dota", "pak01_dir.vpk");
            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");

            if (!File.Exists(gameVpkPath))
            {
                log("Dota 2 game files (pak01_dir.vpk) not found. Re-run Detect and try again.");
                _logger?.Log($"GameItemsGameExtractor: game VPK missing at {gameVpkPath}");
                return false;
            }

            if (!File.Exists(hlExtractPath))
            {
                log("Extraction tool not found.");
                _logger?.Log($"GameItemsGameExtractor: HLExtract.exe missing at {hlExtractPath}");
                return false;
            }

            // Extract into an isolated temp dir; safe path keeps non-ASCII usernames working.
            string tempDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaItemsGame_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                log("Loading latest items_game.txt from your Dota 2 installation...");

                // HLExtract extracts directories, not single files (see AssetModifierService courier path),
                // so extract the whole scripts/items folder, then pick items_game.txt out of it.
                string arguments = $"-p \"{gameVpkPath}\" -d \"{tempDir}\" -e \"root/scripts/items\"";
                if (!await RunHlExtractAsync(hlExtractPath, arguments, ct).ConfigureAwait(false))
                {
                    log("Failed to read items_game.txt from your Dota 2 installation.");
                    return false;
                }

                ct.ThrowIfCancellationRequested();

                // HLExtract flattens the extract path (it keeps only the trailing folder segment,
                // e.g. "items/items_game.txt"), so search recursively rather than assuming a layout —
                // the same defensive approach AssetModifierService uses for extracted models.
                string? extracted = Directory
                    .EnumerateFiles(tempDir, "items_game.txt", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (extracted is null)
                {
                    log("items_game.txt not found inside your Dota 2 game files.");
                    _logger?.Log($"GameItemsGameExtractor: items_game.txt not found under {tempDir} after extraction.");
                    return false;
                }

                long size = new FileInfo(extracted).Length;
                if (size < MinItemsGameBytes)
                {
                    log("items_game.txt from your Dota 2 installation looks incomplete.");
                    _logger?.Log($"GameItemsGameExtractor: extracted items_game.txt too small ({size} bytes).");
                    return false;
                }

                string dest = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(extracted, dest, overwrite: true);

                log("Latest items_game.txt loaded.");
                _logger?.Log($"GameItemsGameExtractor: injected items_game.txt ({size} bytes) from {gameVpkPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw; // Let the caller handle cancellation.
            }
            catch (Exception ex)
            {
                log("Could not read items_game.txt from your Dota 2 installation.");
                _logger?.Log($"GameItemsGameExtractor error: {ex}");
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
            }
        }

        /// <summary>
        /// Runs HLExtract.exe with the given arguments, honoring cancellation and a hard timeout.
        /// Returns true only when the process exits with code 0.
        /// </summary>
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

            // Drain stdout to prevent buffer deadlock; surface stderr to the debug log only.
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
                try { if (!proc.HasExited) proc.Kill(); } catch { /* ignore */ }
            }))
            {
                var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { /* ignore */ }
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
