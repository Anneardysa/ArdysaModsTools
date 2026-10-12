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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public class ModsPackDataService
    {
        private const string IndexRepoPath = "remote/modspack_index.txt";
        private const int ExtractTimeoutMinutes = 20;

        private readonly IGameItemsGameExtractor _itemsGameExtractor;
        private readonly IHeroSetPatcher _patcher;
        private readonly IVpkRecompiler _recompiler;
        private readonly LocalizationPatcherService _localizationPatcher;

        public ModsPackDataService(
            IGameItemsGameExtractor? itemsGameExtractor = null,
            IHeroSetPatcher? patcher = null,
            IVpkRecompiler? recompiler = null)
        {
            _itemsGameExtractor = itemsGameExtractor ?? new GameItemsGameExtractorService(FileAppLogger.Instance);
            _patcher = patcher ?? new HeroSetPatcherService(FileAppLogger.Instance);
            _recompiler = recompiler ?? new VpkRecompilerService(FileAppLogger.Instance);
            _localizationPatcher = new LocalizationPatcherService();
        }

        public virtual async Task<bool> RebuildVpkAsync(
            string targetPath,
            string vpkPath,
            Action<string> status,
            CancellationToken ct = default,
            IProgress<int>? percent = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(vpkPath) || !File.Exists(vpkPath))
                return false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
            string vpkToolPath = Path.Combine(baseDir, "vpk.exe");

            if (!File.Exists(hlExtractPath) || !File.Exists(vpkToolPath))
            {
                status("Packaging tools not found.");
                FallbackLogger.LogFileOnly($"ModsPackData: missing tool — HLExtract={File.Exists(hlExtractPath)}, vpk.exe={File.Exists(vpkToolPath)}");
                InstallReport.Fail("Packaging tools are missing — please reinstall the app.");
                return false;
            }

            string tempRoot = Path.Combine(Core.Helpers.SafeTempPathHelper.GetSafeTempPath(),
                $"ArdysaModsPack_{Guid.NewGuid():N}");
            string buildDir = Path.Combine(tempRoot, "build");
            string extractDir = Path.Combine(tempRoot, "root");

            if (!HasRoomToBuild(tempRoot, vpkPath, out string spaceError))
            {
                status("Not enough free disk space.");
                FallbackLogger.LogFileOnly($"ModsPackData: {spaceError}");
                InstallReport.Fail(spaceError);
                return false;
            }

            try
            {
                Directory.CreateDirectory(tempRoot);
                Directory.CreateDirectory(buildDir);

                ct.ThrowIfCancellationRequested();

                percent?.Report(0);
                status("Extracting ModsPack...");
                string arguments = $"-p \"{vpkPath}\" -d \"{tempRoot}\" -e \"root\"";
                if (!await RunHlExtractAsync(hlExtractPath, arguments, ct).ConfigureAwait(false)
                    || !Directory.Exists(extractDir)
                    || !Directory.EnumerateFileSystemEntries(extractDir).Any())
                {
                    status("Failed to extract ModsPack.");
                    FallbackLogger.LogFileOnly($"ModsPackData: HLExtract failed or produced empty dir for {vpkPath}");
                    InstallReport.Fail("Could not unpack the ModsPack package.");
                    return false;
                }

                ct.ThrowIfCancellationRequested();
                percent?.Report(10);

                StripBundledData(extractDir, status);

                ct.ThrowIfCancellationRequested();
                percent?.Report(15);

                string? readReason = null;
                if (!await _itemsGameExtractor.RefreshFromGameAsync(
                        targetPath, extractDir, m => { readReason = m; status(m); }, ct).ConfigureAwait(false))
                {
                    status("Could not read the package from your Dota 2 installation.");
                    InstallReport.Fail(readReason
                        ?? "Could not read game data from your Dota 2 installation — verify game files in Steam.");
                    return false;
                }
                InstallReport.Ok("Game data loaded from your Dota 2 installation.");

                ct.ThrowIfCancellationRequested();
                percent?.Report(25);

                status("Downloading ModsPack package data...");
                string? indexText = await DownloadIndexAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(indexText))
                {
                    status("Failed to download ModsPack package data.");
                    InstallReport.Fail("Could not download package data — check your internet connection and try again.");
                    return false;
                }
                InstallReport.Ok($"Package data downloaded ({indexText.Length / 1048576.0:F1} MB).");

                var mergedBlocks = await CollectBlocksAsync(indexText, status, ct).ConfigureAwait(false);
                if (mergedBlocks == null || mergedBlocks.Count == 0)
                {
                    status("ModsPack package data did not match the hero database.");
                    FallbackLogger.LogFileOnly("ModsPackData: 0 blocks matched heroes.json — index/heroes.json out of sync?");
                    InstallReport.Fail("Package data did not match the hero database — please try again later.");
                    return false;
                }

                percent?.Report(35);
                status("Applying package data...");
                int lastBlockPct = -1;
                if (!await _patcher.PatchWithMergedBlocksAsync(extractDir, mergedBlocks, status, ct,
                    onBlockDone: (done, total) =>
                    {
                        int pct = total > 0 ? done * 100 / total : 100;
                        if (pct == lastBlockPct) return;
                        lastBlockPct = pct;
                        status($"Applying package data... {pct}%");
                        percent?.Report(35 + pct * 40 / 100);
                    }).ConfigureAwait(false))
                {
                    status("Failed to apply ModsPack package data.");
                    InstallReport.Fail("Could not apply the package data.");
                    return false;
                }
                InstallReport.Ok("Package data applied.");

                ct.ThrowIfCancellationRequested();

                if (!await _localizationPatcher.PatchLocalizationAsync(extractDir, status, ct,
                    onFileDone: (done, total) =>
                        percent?.Report(75 + done * 15 / Math.Max(total, 1))
                    ).ConfigureAwait(false))
                {
                    status("Warning: some localization files failed to download.");
                    InstallReport.Warn($"Some of the {LocalizationPatcherService.FileCount} localization files failed to download — default text will be used for those languages.");
                }

                ct.ThrowIfCancellationRequested();
                percent?.Report(90);

                string protectedDir = Path.Combine(tempRoot, "protected");
                int protectedMoved = ProtectedVpkStore.Split(
                    targetPath, extractDir, protectedDir, assetPaths: null, logger: null, ct);

                status("Building VPK...");
                string? newVpkPath = await _recompiler.RecompileAsync(
                    vpkToolPath, extractDir, buildDir, tempRoot, status, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(newVpkPath))
                {
                    status("VPK build failed.");
                    InstallReport.Fail("Could not build the final package.");
                    return false;
                }
                VpkSignatureSection.TryApply(newVpkPath);

                string? newProtectedVpkPath = null;
                if (protectedMoved > 0)
                {
                    newProtectedVpkPath = await _recompiler.RecompileAsync(
                        vpkToolPath, protectedDir, buildDir, tempRoot, status, ct).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(newProtectedVpkPath) ||
                        string.Equals(newProtectedVpkPath, newVpkPath, StringComparison.OrdinalIgnoreCase))
                    {
                        status("VPK build failed.");
                        InstallReport.Fail("Could not build the protected package.");
                        return false;
                    }
                    VpkSignatureSection.TryApply(newProtectedVpkPath);
                }

                percent?.Report(98);
                status("Installing...");

                string? deployReason = null;
                if (!await ProtectedVpkStore.DeployInstallPairAsync(targetPath, vpkPath, newVpkPath, newProtectedVpkPath,
                        m => { deployReason = m; status(m); }, FileAppLogger.Instance).ConfigureAwait(false))
                {
                    InstallReport.Fail(deployReason ?? "Could not install the mod packages.");
                    return false;
                }

                await ItemsGameBaselineStore.CommitAsync(targetPath, mergedBlocks.Keys, ct).ConfigureAwait(false);

                percent?.Report(100);

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                status("Failed to build ModsPack package.");
                FallbackLogger.LogFileOnly($"ModsPackData: RebuildVpkAsync exception: {ex}");
                InstallReport.Fail(InstallErrorMessage.Describe(ex));
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
                catch (Exception ex) { FallbackLogger.LogFileOnly($"ModsPackData: temp cleanup failed: {ex.Message}"); }

                Core.Helpers.LargeWorkMemory.Release();
            }
        }

        private const int BuildSpaceMultiplier = 4;

        internal static bool HasRoomToBuild(string tempRoot, string vpkPath, out string error)
        {
            error = string.Empty;
            try
            {
                string? root = Path.GetPathRoot(tempRoot);
                if (string.IsNullOrEmpty(root)) return true;

                var drive = new DriveInfo(root);
                if (!drive.IsReady) return true;

                long needed = new FileInfo(vpkPath).Length * BuildSpaceMultiplier;
                if (drive.AvailableFreeSpace >= needed) return true;

                error = $"Not enough free disk space on drive {drive.Name} — the rebuild needs about "
                      + $"{needed / 1073741824.0:F1} GB free and only {drive.AvailableFreeSpace / 1073741824.0:F1} GB is left.";
                return false;
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ModsPackData: free-space check skipped: {ex.Message}");
                return true;
            }
        }

        private static void StripBundledData(string extractDir, Action<string> status)
        {
            try
            {
                string itemsGame = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
                if (File.Exists(itemsGame))
                {
                    status("Removing bundled package data...");
                    File.Delete(itemsGame);
                }

                string localizationDir = Path.Combine(extractDir, "resource", "localization");
                if (Directory.Exists(localizationDir))
                {
                    status("Removing bundled localization...");
                    Directory.Delete(localizationDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ModsPackData: StripBundledData failed: {ex.Message}");
            }
        }

        private static async Task<string?> DownloadIndexAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                return await Cdn.CdnFallbackService.Instance
                    .DownloadStringWithFallbackAsync(EnvironmentConfig.BuildFreshUrl(IndexRepoPath), cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ModsPackData: index download failed: {ex.Message}");
                return null;
            }
        }

        private async Task<Dictionary<string, (string block, string heroId)>?> CollectBlocksAsync(
            string indexText, Action<string> status, CancellationToken ct)
        {
            List<HeroSummary> heroes;
            try
            {
                heroes = await new HeroService(AppDomain.CurrentDomain.BaseDirectory)
                    .LoadHeroesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                status("Failed to load hero database.");
                FallbackLogger.LogFileOnly($"ModsPackData: heroes.json load failed: {ex.Message}");
                InstallReport.Fail("Could not load the hero database — please try again.");
                return null;
            }
            InstallReport.Ok($"Hero database loaded ({heroes.Count} heroes).");

            var mergedBlocks = new Dictionary<string, (string block, string heroId)>();
            foreach (var hero in heroes)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(hero.UsedByHeroes) || hero.Ids == null || hero.Ids.Length == 0)
                    continue;

                var itemIds = hero.Ids
                    .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();
                if (itemIds.Count == 0)
                    continue;

                var blocks = _patcher.ParseIndexText(indexText, hero.UsedByHeroes, itemIds);
                if (blocks == null)
                    continue;

                InstallReport.Step($"Hero: {hero.Name} (#{blocks.Count})");

                foreach (var kvp in blocks)
                    mergedBlocks[kvp.Key] = kvp.Value;
            }

            return mergedBlocks;
        }

        private static async Task<bool> RunHlExtractAsync(string hlExtractPath, string arguments, CancellationToken ct)
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
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            proc.Start();

            _ = proc.StandardOutput.ReadToEndAsync();
            _ = Task.Run(async () =>
            {
                var err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(err))
                    FallbackLogger.LogFileOnly($"ModsPackData HLExtract: {err.Trim()}");
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
                    FallbackLogger.LogFileOnly($"ModsPackData: HLExtract timed out after {ExtractTimeoutMinutes} minutes.");
                    return false;
                }
            }

            return await tcs.Task.ConfigureAwait(false) == 0;
        }
    }
}
