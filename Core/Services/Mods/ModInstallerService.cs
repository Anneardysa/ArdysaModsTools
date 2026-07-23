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
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.FileTransactions;

namespace ArdysaModsTools.Core.Services
{
    public enum PatchResult
    {
        Success,
        AlreadyPatched,
        Failed,
        Cancelled
    }



    public enum VpkOrigin
    {
        Official,
        Unofficial,
        Unreadable
    }

    public sealed class ModInstallerService : IModInstallerService
    {
        private IAppLogger? _logger;
        private readonly HttpClient _httpClient;
        private readonly ModsPackDataService _dataService;

        private static readonly Lazy<string[]> _gameInfoUrls = new(() => new[]
        {
            $"{CdnConfig.R2BaseUrl}/remote/gameinfo_branchspecific.gi",
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi"),
            $"{EnvironmentConfig.RawGitHubBase}/remote/gameinfo_branchspecific.gi"
        });
        private static string[] GameInfoUrls => _gameInfoUrls.Value;
        
        private static readonly Lazy<string[]> _disableGameInfoUrls = new(() => new[]
        {
            $"{CdnConfig.R2BaseUrl}/remote/gameinfo_branchspecific_disable.gi",
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific_disable.gi"),
            $"{EnvironmentConfig.RawGitHubBase}/remote/gameinfo_branchspecific_disable.gi"
        });
        private static string[] DisableGameInfoUrls => _disableGameInfoUrls.Value;

        private const string RequiredModFilePath = DotaPaths.ModsVpk;

        public ModInstallerService(IAppLogger? logger = null)
        {
            _logger = logger;
            _dataService = new ModsPackDataService();
            _httpClient = HttpClientProvider.Client;
            try
            {
                if (!_httpClient.DefaultRequestHeaders.UserAgent.ToString().Contains("ArdysaModsTools"))
                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArdysaModsTools/1.0");
            }
            catch {  }
        }

        public void SetLogger(IAppLogger logger)
        {
            _logger = logger;
        }

        public async Task<(bool IsValid, string ErrorMessage)> ValidateVpkAsync(string vpkFilePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vpkFilePath))
                return (false, "VPK file path is empty.");
            
            if (!File.Exists(vpkFilePath))
                return (false, "VPK file not found.");

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtractPath = Path.Combine(appPath, "HLExtract.exe");

            if (!File.Exists(hlExtractPath))
            {
                _logger?.Log($"HLExtract.exe not found at {hlExtractPath}");
                return (false, $"HLExtract.exe not found at {hlExtractPath}");
            }

            try
            {
                string? output = await TryListVpkContentsAsync(vpkFilePath, ct).ConfigureAwait(false);
                if (output == null)
                    return (false, "Could not read VPK contents.");

                bool hasMarker = ListingContainsMarker(output)
                              || ListingContainsPath(output, LegacyMarkerPath);

                if (!hasMarker)
                {
                    return (false, "VPK is invalid, please contact developer.");
                }

                return (true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return (false, "Validation canceled.");
            }
            catch (Exception ex)
            {
                _logger?.Log($"VPK validation failed: {ex.Message}");
                return (false, $"Validation failed: {ex.Message}");
            }
        }

        private const string OriginMarkerPath = @"materials\dev\deferred_light_cache.vtex_c";

        private const string LegacyMarkerPath = @"version\_ardysamods";

        private static bool ListingContainsMarker(string listing)
            => ListingContainsPath(listing, OriginMarkerPath);

        private static bool ListingContainsPath(string listing, string relativePath)
        {
            foreach (var raw in listing.Split('\n'))
            {
                var line = raw.Trim().TrimEnd('.', ' ').Replace('/', '\\');
                if (line.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
                    line.Equals(@"root\" + relativePath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private Task<string?> TryListVpkContentsAsync(string vpkFilePath, CancellationToken ct)
        {
            if (!File.Exists(vpkFilePath))
                return Task.FromResult<string?>(null);
            return RunHlExtractAsync($"-p \"{vpkFilePath}\" -l", ct);
        }

        private const int HlExtractTimeoutMinutes = 20;

        private async Task<string?> RunHlExtractAsync(string arguments, CancellationToken ct)
        {
            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtractPath))
                return null;

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

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(HlExtractTimeoutMinutes));

            using (ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }))
            {
                var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                    ct.ThrowIfCancellationRequested();
                    FallbackLogger.Log($"HLExtract timed out after {HlExtractTimeoutMinutes} minutes.");
                    return null;
                }

                if (await tcs.Task.ConfigureAwait(false) != 0)
                    return null;
            }

            return await stdoutTask.ConfigureAwait(false);
        }

        public async Task<(VpkOrigin Origin, bool NeedsRebuild)> ClassifyVpkAsync(string vpkFilePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vpkFilePath) || !File.Exists(vpkFilePath))
                return (VpkOrigin.Unreadable, false);

            string? listing;
            try
            {
                listing = await TryListVpkContentsAsync(vpkFilePath, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.Log($"VPK classification failed: {ex.Message}");
                return (VpkOrigin.Unreadable, false);
            }

            if (listing == null)
                return (VpkOrigin.Unreadable, false);

            if (!ListingContainsMarker(listing))
                return (VpkOrigin.Unofficial, false);

            bool slim = !ListingContainsPath(listing, @"scripts\items\items_game.txt");
            return (VpkOrigin.Official, slim);
        }

        public bool IsRequiredModFilePresent(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
                return false;

            string requiredFilePath = Path.Combine(targetPath, RequiredModFilePath);
            return File.Exists(requiredFilePath);
        }

        public async Task<bool> CheckForNewerModsPackAsync(
            string targetPath,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                    return false;

                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                string localHashFile = Path.Combine(modsDir, "ModsPack.hash");

                if (!File.Exists(localHashFile))
                    return false;

                string? remoteHash = await DownloadRemoteHashAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(remoteHash))
                    return false;

                string localHash = (await File.ReadAllTextAsync(localHashFile, ct).ConfigureAwait(false)).Trim();
                return !string.Equals(localHash, remoteHash.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"CheckForNewerModsPackAsync error: {ex.Message}");
                return false;
            }
        }

        private static readonly Lazy<string[]> _modsPackHashUrls = new(() => new[]
        {
            $"{CdnConfig.R2BaseUrl}/remote/ModsPack.hash",
            EnvironmentConfig.BuildRawUrl("remote/ModsPack.hash"),
            $"{EnvironmentConfig.RawGitHubBase}/remote/ModsPack.hash"
        });
        private static string[] ModsPackHashUrls => _modsPackHashUrls.Value;

        private async Task<string?> DownloadRemoteHashAsync(CancellationToken ct = default)
        {
            Exception? lastError = null;
            int urlIndex = 0;

            foreach (var url in ModsPackHashUrls)
            {
                urlIndex++;
                try
                {
                    if (urlIndex > 1)
                        _logger?.Log($"Trying hash fallback source {urlIndex}/{ModsPackHashUrls.Length}...");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    var res = await _httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
                    
                    if (!res.IsSuccessStatusCode)
                    {
                        FallbackLogger.Log($"DownloadRemoteHashAsync failed for {url}: {res.StatusCode}");
                        continue;
                    }

                    string text = await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    FallbackLogger.Log($"DownloadRemoteHashAsync exception for {url}: {ex.Message}");
                }
            }
            
            if (lastError != null)
                _logger?.Log($"Failed to fetch remote ModsPack hash: {lastError.Message}");
            return null;
        }

        private static string ComputeSHA256(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        internal static string ComputeSHA1Hex(byte[] data)
        {
            using var sha = System.Security.Cryptography.SHA1.Create();
            return Convert.ToHexString(sha.ComputeHash(data));
        }

        private async Task<byte[]?> DownloadGameInfoAsync(string[] urls, CancellationToken ct = default, string? expectedSha1 = null)
        {
            Exception? lastError = null;
            int urlIndex = 0;
            foreach (var url in urls)
            {
                urlIndex++;
                try
                {
                    if (urlIndex > 1)
                        _logger?.Log($"[PATCH] Trying fallback source {urlIndex}/{urls.Length}...");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(60));
                    var data = await _httpClient.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
                    if (data == null || data.Length == 0)
                        continue;

                    if (expectedSha1 != null)
                    {
                        string actual = ComputeSHA1Hex(data);
                        if (!string.Equals(actual, expectedSha1, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.Log($"[PATCH] Source {urlIndex}/{urls.Length} returned an out-of-date game config; trying another source.");
                            FallbackLogger.Log($"DownloadGameInfoAsync hash mismatch for {url}: expected {expectedSha1}, got {actual}");
                            continue;
                        }
                        _logger?.Log("[PATCH] Game config verified.");
                    }

                    return data;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    FallbackLogger.Log($"DownloadGameInfoAsync failed for {url}: {ex.Message}");
                }
            }

            if (lastError != null)
                _logger?.Log($"Failed to download gameinfo: {lastError.Message}");
            else if (expectedSha1 != null)
                _logger?.Log("Failed to download game config: every source returned an out-of-date file.");
            return null;
        }

        public async Task<(bool Success, bool IsUpToDate)> InstallModsAsync(
            string targetPath,
            string appPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            bool force = false,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action<string>? statusCallback = null)
        {
            _logger?.Log("Installing mods...");
            InstallReport.Begin();
            InstallReport.Step("Installation started.");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("InstallModsAsync: targetPath is empty.");
                InstallReport.Fail("Dota 2 folder is not set — run Auto or Manual Detect first.");
                return (false, false);
            }

            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch (ArgumentException ae)
            {
                _logger?.Log($"Invalid target path: {ae.Message}");
                InstallReport.Fail("The selected Dota 2 folder is not valid — re-run Auto or Manual Detect.");
                return (false, false);
            }

            string dota2ExePath = Path.Combine(targetPath, DotaPaths.Dota2Exe);
            if (!File.Exists(dota2ExePath))
            {
                _logger?.Log("Error: Dota 2 installation not found at the selected path.");
                _logger?.Log($"Expected dota2.exe at: {dota2ExePath}");
                _logger?.Log("Please click 'Auto Detect' or 'Manual Detect' to select the correct Dota 2 folder.");
                InstallReport.Fail("Dota 2 was not found at the selected folder — re-run Auto or Manual Detect.");
                return (false, false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            string tempRoot = Path.Combine(Path.GetTempPath(), $"ArdysaMods_Installer_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempRoot);

            string downloadPath = Path.Combine(tempRoot, "ModsPack.zip");
            string extractPath = Path.Combine(tempRoot, "Extracted");
            Directory.CreateDirectory(extractPath);

            bool downloadVerified = false;

            try
            {
                statusCallback?.Invoke("Checking version...");
                _logger?.Log("Checking ModsPack version...");

                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                string localHashFile = Path.Combine(modsDir, "ModsPack.hash");
                string? remoteHash = await DownloadRemoteHashAsync(cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(remoteHash) && File.Exists(localHashFile))
                {
                    string localHash = (await File.ReadAllTextAsync(localHashFile, cancellationToken).ConfigureAwait(false)).Trim();
                    string cleanRemote = remoteHash.Trim();

                    bool installIntact = File.Exists(Path.Combine(modsDir, "pak01_dir.vpk"));

                    if (string.Equals(localHash, cleanRemote, StringComparison.OrdinalIgnoreCase) && !force && installIntact)
                    {
                        _logger?.Log("ModsPack up to date.");
                        return (true, true);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                var (found, url) = await TryGetModsPackAssetUrlAsync(cancellationToken).ConfigureAwait(false);
                if (!found || string.IsNullOrWhiteSpace(url))
                {
                    _logger?.Log("Failed to locate ModsPack from server.");
                    InstallReport.Fail("Could not reach the download server — check your internet connection and try again.");
                    return (false, false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke(Loc.T("progress.downloading"));
                _logger?.Log("Downloading ModsPack...");

                if (Path.IsPathRooted(url) && File.Exists(url))
                {
                    const int bufferSize = 81920;
                    using (var source = new FileStream(url, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dest = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long total = source.Length;
                        long read = 0;
                        var buffer = new byte[bufferSize];
                        int bytesRead;
                        int lastReported = -1;

                        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await dest.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                            read += bytesRead;

                            if (total > 0 && progress != null)
                            {
                                int pct = (int)(read * 100L / total);
                                if (pct != lastReported)
                                {
                                    lastReported = pct;
                                    progress.Report(pct);
                                }
                            }
                        }
                    }
                }
                else
                {
                    try
                    {
                        void logMsg(string msg)
                        {
                            _logger?.Log(msg);
                            statusCallback?.Invoke(msg);
                        }

                        await ResumableDownloadService.Instance.DownloadAsync(
                            new[] { url },
                            downloadPath,
                            logMsg,
                            progress,
                            speedProgress,
                            cancellationToken,
                            expected: string.IsNullOrWhiteSpace(remoteHash)
                                ? null
                                : new AssetHashEntry { Sha256 = remoteHash }
                        ).ConfigureAwait(false);
                        downloadVerified = !string.IsNullOrWhiteSpace(remoteHash);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"ModsPack download failed: {ex.Message}");
                        FallbackLogger.Log($"ModsPack download exception: {ex.Message}");
                        InstallReport.Fail("Download failed — check your internet connection and try again.");
                        return (false, false);
                    }
                }

                progress?.Report(100);
                try { InstallReport.Ok($"ModsPack downloaded ({new FileInfo(downloadPath).Length / 1048576.0:F1} MB)."); }
                catch { InstallReport.Ok("ModsPack downloaded."); }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke(Loc.T("progress.verifying"));
                if (!downloadVerified && !string.IsNullOrWhiteSpace(remoteHash))
                {
                    try
                    {
                        var downloadedSha = ComputeSHA256(downloadPath);
                        if (!string.Equals(downloadedSha, remoteHash, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.Log("Downloaded ModsPack hash mismatch.");
                            FallbackLogger.Log($"ModsPack hash mismatch. expected={remoteHash} got={downloadedSha}");
                            InstallReport.Fail("The downloaded package was corrupted — please try again.");
                            return (false, false);
                        }
                        InstallReport.Ok("Download verified.");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed computing downloaded file hash: {ex.Message}");
                        InstallReport.Fail("Could not verify the download — please try again.");
                        return (false, false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke(Loc.T("progress.extracting"));
                try
                {
                    ZipFile.ExtractToDirectory(downloadPath, extractPath);
                    InstallReport.Ok("Files extracted.");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"ERROR: Failed to extract ModsPack: {ex.Message}");
                    FallbackLogger.Log($"Zip extraction error: {ex.Message}");
                    InstallReport.Fail("Could not unpack the download — check free disk space and antivirus.");
                    return (false, false);
                }

                string installedVpk = Path.Combine(modsDir, "pak01_dir.vpk");

                using var snapshot = InstallSnapshot.Capture(installedVpk);

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke(Loc.T("progress.installing"));
                Directory.CreateDirectory(modsDir);

                using (var transaction = new FileTransaction(_logger))
                {
                    foreach (string subDir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string rel = Path.GetRelativePath(extractPath, subDir);
                        string dest = Path.Combine(modsDir, rel);
                        transaction.AddOperation(new CreateDirectoryOperation(dest));
                    }

                    foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string rel = Path.GetRelativePath(extractPath, file);
                        string dest = Path.Combine(modsDir, rel);
                        transaction.AddOperation(new CopyOperation(file, dest, true));
                    }

                    await transaction.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke(Loc.T("progress.buildingData"));
                bool dataOk = await _dataService.RebuildVpkAsync(
                    targetPath,
                    installedVpk,
                    msg => statusCallback?.Invoke(msg),
                    cancellationToken,
                    percent: progress).ConfigureAwait(false);
                if (!dataOk)
                {
                    _logger?.Log("ERROR: Failed to build the ModsPack package.");
                    InstallReport.Fail("Package build failed — your previous install was restored.");
                    return (false, false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(remoteHash))
                {
                    try { await File.WriteAllTextAsync(localHashFile, remoteHash.Trim(), cancellationToken).ConfigureAwait(false); }
                    catch (Exception ex) { FallbackLogger.Log($"Failed writing local ModsPack.hash: {ex.Message}"); }
                }

                snapshot.Commit();

                ProtectedVpkStore.Clear(targetPath);

                _logger?.Log("Mod installation completed successfully.");
                InstallReport.Ok("Installation completed successfully.");
                return (true, false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("Installation canceled. Cleaning up partial downloads...");
                InstallReport.Warn("Installation canceled.");
                return (false, false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Installation failed: {ex.Message}");
                FallbackLogger.Log($"InstallModsAsync unexpected exception: {ex.Message}");
                InstallReport.Fail("Unexpected error — please try again.");
                return (false, false);
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch (Exception ex) { FallbackLogger.Log($"Temp cleanup failed: {ex.Message}"); }
            }
        }

        public async Task<bool> DisableModsAsync(string targetPath, CancellationToken cancellationToken = default)
        {
            _logger?.Log("Disabling mods...");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("DisableModsAsync: no target path provided.");
                return true;
            }

            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch
            {
            }

            try
            {
                string signaturesPath = Path.Combine(targetPath, DotaPaths.Signatures);
                string gameInfoPath = Path.Combine(targetPath, DotaPaths.GameInfo);

                if (!File.Exists(signaturesPath))
                    return true;

                string[] lines = await File.ReadAllLinesAsync(signaturesPath, cancellationToken).ConfigureAwait(false);
                string[]? trimmed = TrimAfterDigest(lines);

                var fileBytes = await DownloadGameInfoAsync(DisableGameInfoUrls, cancellationToken).ConfigureAwait(false);

                if (trimmed != null)
                {
                    await ReplaceAtomicAsync(signaturesPath,
                        (tmp, ct) => File.WriteAllLinesAsync(tmp, trimmed, ct),
                        cancellationToken).ConfigureAwait(false);
                }

                bool gameInfoRestored = false;
                if (fileBytes != null && fileBytes.Length > 0)
                {
                    try
                    {
                        await ReplaceAtomicAsync(gameInfoPath,
                            (tmp, ct) => File.WriteAllBytesAsync(tmp, fileBytes, ct),
                            cancellationToken).ConfigureAwait(false);
                        gameInfoRestored = true;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger?.Log($"Failed to write game config: {ex.Message}");
                        FallbackLogger.Log($"DisableModsAsync write config failed: {ex.Message}");
                    }
                }

                if (!gameInfoRestored)
                {
                    try
                    {
                        if (File.Exists(gameInfoPath))
                            File.Delete(gameInfoPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed to remove game config: {ex.Message}");
                        FallbackLogger.Log($"DisableModsAsync delete config failed: {ex.Message}");
                        return false;
                    }
                }

                _logger?.Log("Mods disabled successfully.");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("Disable mods canceled.");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Log($"DisableModsAsync failed: {ex.Message}");
                FallbackLogger.Log($"DisableModsAsync exception: {ex.Message}");
                return false;
            }
        }

        internal static string[]? TrimAfterDigest(string[] lines)
        {
            int digestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:"));
            return digestIndex >= 0 ? lines[..(digestIndex + 1)] : null;
        }

        internal static async Task ReplaceAtomicAsync(string path, Func<string, CancellationToken, Task> writeTmp, CancellationToken ct)
        {
            string tmp = path + ".tmp";
            try
            {
                await writeTmp(tmp, ct).ConfigureAwait(false);
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch (Exception ex) { FallbackLogger.Log($"ReplaceAtomicAsync temp cleanup failed for {tmp}: {ex.Message}"); }
            }
        }

        public async Task<PatchResult> UpdatePatcherAsync(
            string targetPath,
            Action<string>? statusCallback = null,
            CancellationToken ct = default)
        {

            _logger?.Log("[PATCH] Starting patch...");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("[PATCH] Error: targetPath empty.");
                return PatchResult.Failed;
            }

            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch
            {
            }

            string signaturesPath = Path.Combine(targetPath, "game", "bin", "win64", "dota.signatures");
            string gameInfoPath = Path.Combine(targetPath, "game", "dota", "gameinfo_branchspecific.gi");

            try
            {
                statusCallback?.Invoke("Validating files...");
                
                if (!File.Exists(signaturesPath))
                {
                    _logger?.Log($"[PATCH] Error: Core file missing at: {signaturesPath}");
                    
                    string dotaExe = Path.Combine(targetPath, DotaPaths.Dota2Exe);
                    
                    if (File.Exists(dotaExe))
                    {
                        _logger?.Log("[PATCH] Diagnostic: dota2.exe exists, but core file is missing.");
                        _logger?.Log("[PATCH] Suggestion: Please verify integrity of game files in Steam.");
                    }
                    else
                    {
                        _logger?.Log($"[PATCH] Diagnostic: dota2.exe also missing at {targetPath}.");
                        _logger?.Log("[PATCH] Suggestion: The detected Dota 2 path may be incorrect.");
                    }
                    
                    return PatchResult.Failed;
                }

                ct.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Checking patch status...");
                string[] lines = await File.ReadAllLinesAsync(signaturesPath, ct).ConfigureAwait(false);
                int lineDigestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:", StringComparison.Ordinal));

                if (lineDigestIndex < 0)
                {
                    _logger?.Log("[PATCH] Error: Core file format invalid (no DIGEST found).");
                    return PatchResult.Failed;
                }

                ct.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Updating game config...");

                var fileBytes = await DownloadGameInfoAsync(GameInfoUrls, ct, ModConstants.ModPatchSHA1).ConfigureAwait(false);

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger?.Log("[PATCH] Error: Failed to download game config. Nothing was changed.");
                    return PatchResult.Failed;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(gameInfoPath)!);

                using (var transaction = new FileTransaction(_logger))
                {
                    try
                    {
                        statusCallback?.Invoke("Patching core files...");

                        var modified = new List<string>(lines[..(lineDigestIndex + 1)])
                        {
                            ModConstants.ModPatchLine
                        };

                        string tmpSig = signaturesPath + ".tmp";
                        await File.WriteAllLinesAsync(tmpSig, modified, ct).ConfigureAwait(false);

                        transaction.AddOperation(new MoveOperation(tmpSig, signaturesPath));

                        _logger?.Log("[PATCH] Core files prepared for patching.");

                        string tmpGi = gameInfoPath + ".tmp";
                        await File.WriteAllBytesAsync(tmpGi, fileBytes, ct).ConfigureAwait(false);

                        transaction.AddOperation(new MoveOperation(tmpGi, gameInfoPath));

                        await transaction.ExecuteAsync(ct).ConfigureAwait(false);

                        _logger?.Log("[PATCH] All file operations completed successfully.");

                        transaction.Commit();

                        ProtectedVpkStore.Ensure(targetPath);


                        try
                        {
                            var versionService = new DotaVersionService(_logger);
                            await versionService.SavePatchedVersionJsonAsync(targetPath).ConfigureAwait(false);
                            _logger?.Log("[PATCH] Version info saved.");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Log($"[PATCH] Warning: Failed to save version info: {ex.Message}");
                        }
                        
                        _logger?.Log("[PATCH] Patch completed successfully!");
                        return PatchResult.Success;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger?.Log($"[PATCH] Error during patch: {ex.Message}. Rolling back...");
                        await transaction.RollbackAsync(ct).ConfigureAwait(false);
                        return PatchResult.Failed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("[PATCH] Patch cancelled.");
                return PatchResult.Cancelled;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[PATCH] Unexpected error: {ex.Message}");
                FallbackLogger.Log($"UpdatePatcherAsync exception: {ex.Message}");
                return PatchResult.Failed;
            }
        }

        public async Task<bool> ManualInstallModsAsync(
            string targetPath,
            string vpkFilePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            Action<string>? statusCallback = null,
            bool rebuild = true)
        {
            _logger?.Log("Starting manual installation...");
            InstallReport.Begin();
            InstallReport.Step("Manual installation started.");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("Error: Dota 2 path not set.");
                InstallReport.Fail("Dota 2 folder is not set — run Auto or Manual Detect first.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(vpkFilePath) || !File.Exists(vpkFilePath))
            {
                _logger?.Log("Error: VPK file not found.");
                InstallReport.Fail("The selected VPK file could not be found.");
                return false;
            }

            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch (ArgumentException ae)
            {
                _logger?.Log($"Invalid target path: {ae.Message}");
                InstallReport.Fail("The selected Dota 2 folder is not valid — re-run Auto or Manual Detect.");
                return false;
            }

            string dota2ExePath = Path.Combine(targetPath, DotaPaths.Dota2Exe);
            if (!File.Exists(dota2ExePath))
            {
                _logger?.Log("Error: Dota 2 installation not found at the selected path.");
                _logger?.Log($"Expected dota2.exe at: {dota2ExePath}");
                _logger?.Log("Please click 'Auto Detect' or 'Manual Detect' to select the correct Dota 2 folder.");
                InstallReport.Fail("Dota 2 was not found at the selected folder — re-run Auto or Manual Detect.");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                string destVpkPath = Path.Combine(modsDir, "pak01_dir.vpk");

                using var snapshot = InstallSnapshot.Capture(destVpkPath);

                progress?.Report(10);

                const int bufferSize = 81920;
                using (var source = new FileStream(vpkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var dest = new FileStream(destVpkPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long total = source.Length;
                    long read = 0;
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    int lastReported = 10;
                    var sw = Stopwatch.StartNew();
                    long lastReportedBytes = 0;

                    while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await dest.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        read += bytesRead;

                        if (sw.ElapsedMilliseconds > 500)
                        {
                            string speedStr = ArdysaModsTools.Core.Helpers.SpeedCalculator.FormatSpeed(read - lastReportedBytes, sw.Elapsed.TotalSeconds);
                            speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = speedStr });
                            lastReportedBytes = read;
                            sw.Restart();
                        }

                        if (total > 0 && progress != null)
                        {
                            int pct = 10 + (int)(read * 60L / total);
                            if (pct != lastReported)
                            {
                                lastReported = pct;
                                progress.Report(pct);
                            }
                        }
                    }
                    speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = "0.0 MB/S" });
                }

                InstallReport.Ok("VPK copied into the game folder.");
                progress?.Report(75);
                cancellationToken.ThrowIfCancellationRequested();

                if (rebuild)
                {
                    statusCallback?.Invoke(Loc.T("progress.buildingData"));
                    bool dataOk = await _dataService.RebuildVpkAsync(
                        targetPath,
                        destVpkPath,
                        msg => statusCallback?.Invoke(msg),
                        cancellationToken).ConfigureAwait(false);
                    if (!dataOk)
                    {
                        _logger?.Log("ERROR: Failed to build the ModsPack package.");
                        InstallReport.Fail("Package build failed — your previous install was restored.");
                        return false;
                    }
                }
                else
                {
                    _logger?.Log("VPK is self-contained or third-party: installing as-is (no package rebuild).");
                    InstallReport.Step("Third-party package — installed as-is (no rebuild).");
                }

                cancellationToken.ThrowIfCancellationRequested();

                string signaturesPath = Path.Combine(targetPath, DotaPaths.Signatures);
                string gameInfoPath = Path.Combine(targetPath, DotaPaths.GameInfo);

                if (!File.Exists(signaturesPath))
                {
                    _logger?.Log("Error: Missing game core file.");
                    _logger?.Log("This file is part of Dota 2's core installation.");
                    _logger?.Log("Please verify integrity of game files in Steam: Right-click Dota 2 > Properties > Local Files > Verify integrity.");
                    InstallReport.Fail("A Dota 2 core file is missing — verify game files in Steam and try again.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(gameInfoPath)!);
                progress?.Report(80);

                var giData = await DownloadGameInfoAsync(GameInfoUrls, cancellationToken, ModConstants.ModPatchSHA1).ConfigureAwait(false);
                if (giData == null)
                {
                    _logger?.Log("Error: Failed to download patch file.");
                    InstallReport.Fail("Could not download a required patch file — check your internet connection.");
                    return false;
                }

                progress?.Report(85);

                var sigLines = await File.ReadAllLinesAsync(signaturesPath, cancellationToken).ConfigureAwait(false);
                int sigDigestIndex = Array.FindIndex(sigLines, l => l.StartsWith("DIGEST:", StringComparison.Ordinal));
                if (sigDigestIndex < 0)
                {
                    _logger?.Log("Error: Invalid core file format.");
                    InstallReport.Fail("A Dota 2 core file has an unexpected format — verify game files in Steam and try again.");
                    return false;
                }

                using (var patchTx = new FileTransaction(_logger))
                {
                    try
                    {
                        string tempGi = gameInfoPath + ".tmp";
                        await File.WriteAllBytesAsync(tempGi, giData, cancellationToken).ConfigureAwait(false);
                        patchTx.AddOperation(new MoveOperation(tempGi, gameInfoPath));

                        progress?.Report(90);

                        var modified = new List<string>(sigLines[..(sigDigestIndex + 1)])
                        {
                            ModConstants.ModPatchLine
                        };

                        string tmpSig = signaturesPath + ".tmp";
                        await File.WriteAllLinesAsync(tmpSig, modified, cancellationToken).ConfigureAwait(false);
                        patchTx.AddOperation(new MoveOperation(tmpSig, signaturesPath));

                        await patchTx.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                        patchTx.Commit();

                        ProtectedVpkStore.Ensure(targetPath);
                    }
                    catch (OperationCanceledException)
                    {
                        await patchTx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log("Error: Failed to apply game patch. Rolling back...");
                        FallbackLogger.Log($"Manual patch (gameinfo/signatures) error: {ex.Message}");
                        await patchTx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        InstallReport.Fail("Could not apply the game patch — your previous game files were restored.");
                        return false;
                    }
                }

                progress?.Report(100);
                snapshot.Commit();

                ProtectedVpkStore.Clear(targetPath);

                _logger?.Log("Installation complete!");
                InstallReport.Ok("Installation completed successfully.");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("Installation canceled.");
                InstallReport.Warn("Installation canceled.");
                return false;
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"ManualInstallModsAsync unexpected exception: {ex.Message}");
                InstallReport.Fail("Unexpected error — please try again.");
                return false;
            }
        }

        internal sealed class InstallSnapshot : IDisposable
        {
            private readonly string _vpk;
            private readonly string _hash;
            private readonly string _vpkBak;
            private readonly string _hashBak;
            private readonly bool _hadVpk;
            private readonly bool _hadHash;
            private bool _vpkCaptured;
            private bool _hashCaptured;
            private bool _committed;

            private InstallSnapshot(string vpkPath)
            {
                _vpk = vpkPath;
                _hash = Path.Combine(Path.GetDirectoryName(vpkPath)!, "ModsPack.hash");
                _vpkBak = _vpk + ".bak";
                _hashBak = _hash + ".bak";
                _hadVpk = File.Exists(_vpk);
                _hadHash = File.Exists(_hash);
            }

            public static InstallSnapshot Capture(string vpkPath)
            {
                var s = new InstallSnapshot(vpkPath);
                try
                {
                    if (s._hadVpk) { TryDelete(s._vpkBak); File.Move(s._vpk, s._vpkBak); s._vpkCaptured = true; }
                    if (s._hadHash) { TryDelete(s._hashBak); File.Move(s._hash, s._hashBak); s._hashCaptured = true; }
                }
                catch (Exception ex)
                {
                    FallbackLogger.Log($"InstallSnapshot.Capture failed: {ex.Message}");
                }
                return s;
            }

            public void Commit()
            {
                _committed = true;
                TryDelete(_vpkBak);
                TryDelete(_hashBak);
            }

            public void Dispose()
            {
                if (_committed)
                    return;
                try
                {
                    if (!_hadVpk || _vpkCaptured) TryDelete(_vpk);
                    if (!_hadHash || _hashCaptured) TryDelete(_hash);
                    if (_vpkCaptured && File.Exists(_vpkBak)) File.Move(_vpkBak, _vpk);
                    if (_hashCaptured && File.Exists(_hashBak)) File.Move(_hashBak, _hash);
                }
                catch (Exception ex)
                {
                    FallbackLogger.Log($"InstallSnapshot rollback failed: {ex.Message}");
                }
            }

            private static void TryDelete(string path)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch {  }
            }
        }

        private async Task<(bool Success, string Url)> TryGetModsPackAssetUrlAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string api = EnvironmentConfig.ModsPackReleasesApi;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                using var response = await _httpClient.GetAsync(api, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    FallbackLogger.Log($"TryGetModsPackAssetUrlAsync: GitHub API returned {response.StatusCode}");
                    return (false, string.Empty);
                }

                using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);

                if (!json.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                    return (false, string.Empty);

                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var vName)) continue;
                    if (!asset.TryGetProperty("browser_download_url", out var vUrl)) continue;

                    var name = vName.GetString() ?? "";
                    var url = vUrl.GetString() ?? "";

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        return (true, url);
                }

                return (false, string.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"TryGetModsPackAssetUrlAsync exception: {ex.Message}");
                return (false, string.Empty);
            }
        }
    }
}

