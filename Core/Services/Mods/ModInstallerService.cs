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
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Result of a patch operation.
    /// </summary>
    public enum PatchResult
    {
        /// <summary>Patch applied successfully.</summary>
        Success,
        /// <summary>Already patched, no action needed.</summary>
        AlreadyPatched,
        /// <summary>Patch failed.</summary>
        Failed,
        /// <summary>Operation was cancelled.</summary>
        Cancelled
    }



    /// <summary>
    /// Service for mod installation operations.
    /// Handles VPK validation, mod installation, patching, and removal.
    /// </summary>
    public sealed class ModInstallerService : IModInstallerService
    {
        private readonly ILogger? _logger;
        private readonly HttpClient _httpClient;

        // GameInfo URLs - loaded from environment configuration
        private static string[] GameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific.gi")
        };
        
        // Disable GameInfo URLs - original/clean version to restore when disabling mods
        private static string[] DisableGameInfoUrls => new[]
        {
            EnvironmentConfig.BuildRawUrl("remote/gameinfo_branchspecific_disable.gi")
        };

        private const string RequiredModFilePath = "game/_ArdysaMods/pak01_dir.vpk";

        public ModInstallerService(ILogger? logger = null)
        {
            _logger = logger; // Logger is optional for DI compatibility
            _httpClient = HttpClientProvider.Client;
            try
            {
                if (!_httpClient.DefaultRequestHeaders.UserAgent.ToString().Contains("ArdysaModsTools"))
                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArdysaModsTools/1.0");
            }
            catch { /* non fatal */ }
        }

        /// <summary>
        /// Validate VPK file contains the required version/_ArdysaMods file.
        /// Uses HLExtract to list VPK contents without extracting.
        /// </summary>
        public async Task<(bool IsValid, string ErrorMessage)> ValidateVpkAsync(string vpkFilePath, CancellationToken ct = default)
        {
            // Fail-fast validation
            if (string.IsNullOrWhiteSpace(vpkFilePath))
                return (false, "VPK file path is empty.");
            
            if (!File.Exists(vpkFilePath))
                return (false, "VPK file not found.");

            // Use AppDomain.CurrentDomain.BaseDirectory for reliable path resolution
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtractPath = Path.Combine(appPath, "HLExtract.exe");

            if (!File.Exists(hlExtractPath))
            {
                _logger?.Log($"HLExtract.exe not found at {hlExtractPath}");
                return (false, $"HLExtract.exe not found at {hlExtractPath}");
            }

            try
            {
                // Use HLExtract to list contents of VPK
                var psi = new ProcessStartInfo
                {
                    FileName = hlExtractPath,
                    Arguments = $"-p \"{vpkFilePath}\" -l",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var tcs = new TaskCompletionSource<int>();

                proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);
                proc.Start();

                string output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                string error = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

                using (ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                }))
                {
                    await tcs.Task.ConfigureAwait(false);
                }

                // The path pattern might be: root/version/_ArdysaMods or version/_ArdysaMods
                bool hasArdysaModsFile = output.Contains("_ArdysaMods", StringComparison.OrdinalIgnoreCase) &&
                                         output.Contains("version", StringComparison.OrdinalIgnoreCase);

                if (!hasArdysaModsFile)
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
                _logger?.Log($"VPK validation failed: {ex.Message}"); // ERR_MOD_001: VPK validation exception
                return (false, $"Validation failed: {ex.Message}");
            }
        }

        public bool IsRequiredModFilePresent(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
                return false;

            string requiredFilePath = Path.Combine(targetPath, RequiredModFilePath);
            return File.Exists(requiredFilePath);
            // Note: Logging removed - caller should handle messaging to avoid duplicates
        }

        /// <summary>
        /// Check if a newer ModsPack is available by comparing local and remote hashes.
        /// Returns (hasNewer, hasLocalInstall).
        /// - hasNewer = true means remote hash differs from local (update available)
        /// - hasLocalInstall = true means local ModsPack is installed
        /// </summary>
        public async Task<(bool hasNewer, bool hasLocalInstall)> CheckForNewerModsPackAsync(
            string targetPath,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                    return (false, false);

                targetPath = PathUtility.NormalizeTargetPath(targetPath);
                
                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                string localHashFile = Path.Combine(modsDir, "ModsPack.hash");
                
                bool hasLocalInstall = File.Exists(localHashFile);
                
                if (!hasLocalInstall)
                    return (true, false); // No local install, always show as "new"
                
                // Get remote hash
                string? remoteHash = await DownloadRemoteHashAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(remoteHash))
                    return (false, true); // Can't check, assume no update
                
                // Compare with local hash
                string localHash = (await File.ReadAllTextAsync(localHashFile, ct).ConfigureAwait(false)).Trim();
                bool hasNewer = !string.Equals(localHash, remoteHash.Trim(), StringComparison.OrdinalIgnoreCase);
                
                return (hasNewer, true);
            }
            catch (Exception ex)
            {
                _logger?.Log($"CheckForNewerModsPackAsync error: {ex.Message}");
                return (false, false);
            }
        }

        private async Task<string?> DownloadRemoteHashAsync(CancellationToken ct = default)
        {
            string url = EnvironmentConfig.BuildRawUrl("remote/ModsPack.hash");
            try
            {
                // Use retry logic for transient network failures
                return await RetryHelper.ExecuteAsync(
                    async () =>
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(10));
                        var res = await _httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
                        
                        if (!res.IsSuccessStatusCode)
                        {
                            if (RetryHelper.IsTransientStatusCode(res.StatusCode))
                                throw new HttpRequestException($"Server returned {res.StatusCode}");
                            
                            _logger?.Log($"DownloadRemoteHashAsync failed: {res.StatusCode}");
                            return null;
                        }

                        string text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                    },
                    maxAttempts: 3,
                    onRetry: (attempt, ex) => _logger?.Log($"Retry {attempt}/3: {ex.Message}"),
                    ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to fetch remote ModsPack hash: {ex.Message}");
                FallbackLogger.Log($"DownloadRemoteHashAsync exception for {url}: {ex.Message}");
                return null;
            }
        }

        private static string ComputeSHA256(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Downloads gameinfo file, trying multiple URLs until one succeeds.
        /// </summary>
        private async Task<byte[]?> DownloadGameInfoAsync(string[] urls, CancellationToken ct = default)
        {
            Exception? lastError = null;
            foreach (var url in urls)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(15));
                    var data = await _httpClient.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
                    if (data != null && data.Length > 0)
                    {
                        return data;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // Re-throw if user cancelled
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    FallbackLogger.Log($"DownloadGameInfoAsync failed for {url}: {ex.Message}");
                }
            }
            
            if (lastError != null)
                _logger?.Log($"Failed to download gameinfo: {lastError.Message}");
            return null;
        }

        /// <summary>
        /// Install ModsPack.
        /// Returns (Success, IsUpToDate).
        /// - Success=true means operation completed successfully or nothing needed.
        /// - IsUpToDate=true means the local ModsPack matches remote and no install was performed (use force=true to override).
        /// </summary>
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

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("InstallModsAsync: targetPath is empty.");
                return (false, false);
            }

            // Normalize target path early
            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch (ArgumentException ae)
            {
                _logger?.Log($"Invalid target path: {ae.Message}");
                return (false, false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            string tempRoot = Path.Combine(Path.GetTempPath(), $"ArdysaMods_Installer_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempRoot);

            string downloadPath = Path.Combine(tempRoot, "ModsPack.zip");
            string extractPath = Path.Combine(tempRoot, "Extracted");
            Directory.CreateDirectory(extractPath);

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

                    if (localHash == cleanRemote && !force)
                    {
                        _logger?.Log("ModsPack up to date.");
                        return (true, true); // success but up-to-date
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Resolve ModsPack asset URL
                var (found, url) = await TryGetModsPackAssetUrlAsync(cancellationToken).ConfigureAwait(false);
                if (!found || string.IsNullOrWhiteSpace(url))
                {
                    _logger?.Log("Failed to locate ModsPack from server.");
                    return (false, false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Downloading...");
                _logger?.Log("Downloading ModsPack...");

                // Download (support local path or HTTP)
                if (Path.IsPathRooted(url) && File.Exists(url))
                {
                    // Copy with progress
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
                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger?.Log($"Download failed: {response.StatusCode}");
                            FallbackLogger.Log($"DownloadModsPack failed {response.StatusCode} from {url}");
                            return (false, false);
                        }

                        long? total = response.Content.Headers.ContentLength;
                        using (var netStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        using (var progressStream = new ArdysaModsTools.Core.Helpers.ProgressStream(netStream, speedProgress, total))
                        using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            const int bufferSize = 81920;
                            var buffer = new byte[bufferSize];
                            long totalRead = 0;
                            int bytesRead;
                            int lastReported = -1;

                            while ((bytesRead = await progressStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                                totalRead += bytesRead;

                                if (total.HasValue && progress != null)
                                {
                                    int pct = (int)(totalRead * 100L / total.Value);
                                    if (pct != lastReported)
                                    {
                                        lastReported = pct;
                                        progress.Report(pct);
                                    }
                                }
                            }
                        }
                    }
                }

                progress?.Report(100);

                cancellationToken.ThrowIfCancellationRequested();

                // Verify hash if available
                statusCallback?.Invoke("Verifying download...");
                if (!string.IsNullOrWhiteSpace(remoteHash))
                {
                    try
                    {
                        var downloadedSha = ComputeSHA256(downloadPath);
                        if (!string.Equals(downloadedSha, remoteHash, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.Log("Downloaded ModsPack hash mismatch.");
                            FallbackLogger.Log($"ModsPack hash mismatch. expected={remoteHash} got={downloadedSha}");
                            return (false, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed computing downloaded file hash: {ex.Message}");
                        return (false, false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Extracting files...");
                try
                {
                    ZipFile.ExtractToDirectory(downloadPath, extractPath);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"ERROR: Failed to extract ModsPack: {ex.Message}");
                    FallbackLogger.Log($"Zip extraction error: {ex.Message}");
                    return (false, false);
                }

                if (!string.IsNullOrWhiteSpace(remoteHash))
                {
                    try { await File.WriteAllTextAsync(localHashFile, remoteHash.Trim(), cancellationToken).ConfigureAwait(false); }
                    catch (Exception ex) { FallbackLogger.Log($"Failed writing local ModsPack.hash: {ex.Message}"); }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Copy files to modsDir
                statusCallback?.Invoke("Installing files...");
                Directory.CreateDirectory(modsDir);

                foreach (string dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string rel = Path.GetRelativePath(extractPath, dir);
                    string dest = Path.Combine(modsDir, rel);
                    try { Directory.CreateDirectory(dest); } catch (Exception ex) { FallbackLogger.Log($"Create dir failed: {dest} - {ex.Message}"); }
                }

                foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string rel = Path.GetRelativePath(extractPath, file);
                    string dest = Path.Combine(modsDir, rel);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(file, dest, true);
                    }
                    catch (Exception ex)
                    {
                        FallbackLogger.Log($"Copy file failed {file} -> {dest}: {ex.Message}");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Installation complete - patching is now handled separately via Patch Update button
                _logger?.Log("Mod installation completed successfully.");
                return (true, false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("Installation canceled. Cleaning up partial downloads...");
                return (false, false);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Installation failed: {ex.Message}");
                FallbackLogger.Log($"InstallModsAsync unexpected exception: {ex.Message}");
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
                // ignore normalization failure — continue best-effort
            }

            try
            {
                string signaturesPath = Path.Combine(targetPath, "game", "bin", "win64", "dota.signatures");
                string gameInfoPath = Path.Combine(targetPath, "game", "dota", "gameinfo_branchspecific.gi");

                if (!File.Exists(signaturesPath))
                    return true;

                string[] lines = await File.ReadAllLinesAsync(signaturesPath, cancellationToken).ConfigureAwait(false);
                int digestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:"));
                if (digestIndex >= 0)
                {
                    var trimmed = new List<string>(lines[..(digestIndex + 1)]);
                    string tmpSig = signaturesPath + ".tmp";
                    await File.WriteAllLinesAsync(tmpSig, trimmed, cancellationToken).ConfigureAwait(false);
                    File.Replace(tmpSig, signaturesPath, null);
                }

                // Download and restore original gameinfo_branchspecific.gi
                var fileBytes = await DownloadGameInfoAsync(DisableGameInfoUrls, cancellationToken).ConfigureAwait(false);
                if (fileBytes != null && fileBytes.Length > 0)
                {
                    try
                    {
                        string tmpGi = gameInfoPath + ".tmp";
                        await File.WriteAllBytesAsync(tmpGi, fileBytes, cancellationToken).ConfigureAwait(false);
                        if (File.Exists(gameInfoPath))
                            File.Replace(tmpGi, gameInfoPath, null);
                        else
                            File.Move(tmpGi, gameInfoPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed to write game config: {ex.Message}");
                        FallbackLogger.Log($"DisableModsAsync write config failed: {ex.Message}");
                    }
                }
                else
                {
                    // Fallback: delete the file so Steam can restore it
                    try
                    {
                        if (File.Exists(gameInfoPath))
                            File.Delete(gameInfoPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed to delete game config: {ex.Message}");
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

        /// <summary>
        /// Update patcher with improved logic.
        /// - Pre-validates if patch is needed
        /// - Atomic operations with rollback on failure
        /// - Status callbacks for UI feedback
        /// - Always does full patch (signatures + gameinfo)
        /// </summary>
        public async Task<PatchResult> UpdatePatcherAsync(
            string targetPath,
            Action<string>? statusCallback = null,
            CancellationToken ct = default)
        {
            // Use shared constant from ModConstants

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
                // continue best-effort
            }

            string signaturesPath = Path.Combine(targetPath, "game", "bin", "win64", "dota.signatures");
            string gameInfoPath = Path.Combine(targetPath, "game", "dota", "gameinfo_branchspecific.gi");
            string signaturesBackup = signaturesPath + ".backup";

            try
            {
                statusCallback?.Invoke("Validating files...");
                
                if (!File.Exists(signaturesPath))
                {
                    _logger?.Log("[PATCH] Error: Core file missing.");
                    return PatchResult.Failed;
                }

                ct.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Checking patch status...");
                string sigContent = await File.ReadAllTextAsync(signaturesPath, ct).ConfigureAwait(false);
                
                int digestIndex = sigContent.IndexOf("DIGEST:", StringComparison.Ordinal);
                if (digestIndex < 0)
                {
                    _logger?.Log("[PATCH] Error: Core file format invalid (no DIGEST found).");
                    return PatchResult.Failed;
                }

                string afterDigest = sigContent.Substring(digestIndex);
                bool alreadyPatched = afterDigest.Contains(
                    $"gameinfo_branchspecific.gi~SHA1:{ModConstants.ModPatchSHA1}",
                    StringComparison.OrdinalIgnoreCase);



                ct.ThrowIfCancellationRequested();

                statusCallback?.Invoke("Creating backup...");
                File.Copy(signaturesPath, signaturesBackup, overwrite: true);

                try
                {
                    statusCallback?.Invoke("Patching core files...");
                    
                    string[] lines = await File.ReadAllLinesAsync(signaturesPath, ct).ConfigureAwait(false);
                    int lineDigestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:"));
                    
                    if (lineDigestIndex < 0)
                    {
                        _logger?.Log("[PATCH] Error: DIGEST line not found.");
                        return PatchResult.Failed;
                    }

                    var modified = new List<string>(lines[..(lineDigestIndex + 1)])
                    {
                        ModConstants.ModPatchLine
                    };

                    string tmpSig = signaturesPath + ".tmp";
                    await File.WriteAllLinesAsync(tmpSig, modified, ct).ConfigureAwait(false);
                    File.Replace(tmpSig, signaturesPath, null);

                    _logger?.Log("[PATCH] Core files patched successfully.");

                    ct.ThrowIfCancellationRequested();

                    // Always update gameinfo
                        statusCallback?.Invoke("Updating game config...");
                        
                        Directory.CreateDirectory(Path.GetDirectoryName(gameInfoPath)!);
                        var fileBytes = await DownloadGameInfoAsync(GameInfoUrls, ct).ConfigureAwait(false);
                        
                        if (fileBytes == null || fileBytes.Length == 0)
                        {
                            _logger?.Log("[PATCH] Error: Failed to download game config. Rolling back...");
                            // Rollback signatures
                            File.Copy(signaturesBackup, signaturesPath, overwrite: true);
                            _logger?.Log("[PATCH] Rollback completed.");
                            return PatchResult.Failed;
                        }

                        string tmpGi = gameInfoPath + ".tmp";
                        await File.WriteAllBytesAsync(tmpGi, fileBytes, ct).ConfigureAwait(false);
                        
                        if (File.Exists(gameInfoPath))
                            File.Replace(tmpGi, gameInfoPath, null);
                        else
                            File.Move(tmpGi, gameInfoPath, true);

                        _logger?.Log("[PATCH] Game config updated successfully.");



                    try { File.Delete(signaturesBackup); } catch { }

                    _logger?.Log("[PATCH] Patch completed successfully!");
                    return PatchResult.Success;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Rollback on any failure
                    _logger?.Log($"[PATCH] Error during patch: {ex.Message}. Rolling back...");
                    try
                    {
                        if (File.Exists(signaturesBackup))
                        {
                            File.Copy(signaturesBackup, signaturesPath, overwrite: true);
                            File.Delete(signaturesBackup);
                            _logger?.Log("[PATCH] Rollback completed.");
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger?.Log($"[PATCH] Rollback failed: {rollbackEx.Message}");
                    }
                    return PatchResult.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("[PATCH] Patch cancelled.");
                // Cleanup backup if exists
                try { if (File.Exists(signaturesBackup)) File.Delete(signaturesBackup); } catch { }
                return PatchResult.Cancelled;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[PATCH] Unexpected error: {ex.Message}");
                FallbackLogger.Log($"UpdatePatcherAsync exception: {ex.Message}");
                return PatchResult.Failed;
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility. Calls new UpdatePatcherAsync.
        /// </summary>
        [Obsolete("Use UpdatePatcherAsync instead")]
        public async Task<bool> UpdatePatcherAsyncLegacy(string targetPath, CancellationToken ct = default)
        {
            var result = await UpdatePatcherAsync(targetPath, null, ct);
            return result == PatchResult.Success || result == PatchResult.AlreadyPatched;
        }

        /// <summary>
        /// Manual install: Copy user-provided VPK to _ArdysaMods/pak01_dir.vpk and patch gameinfo/signatures.
        /// </summary>
        public async Task<bool> ManualInstallModsAsync(
            string targetPath,
            string vpkFilePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            _logger?.Log("Starting manual installation...");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger?.Log("Error: Dota 2 path not set."); // ERR_MOD_010: Target path empty or null
                return false;
            }

            if (string.IsNullOrWhiteSpace(vpkFilePath) || !File.Exists(vpkFilePath))
            {
                _logger?.Log("Error: VPK file not found."); // ERR_MOD_011: User-provided VPK not found
                return false;
            }

            try
            {
                targetPath = PathUtility.NormalizeTargetPath(targetPath);
            }
            catch (ArgumentException ae)
            {
                _logger?.Log($"Invalid target path: {ae.Message}");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Create mods directory
                string modsDir = Path.Combine(targetPath, "game", "_ArdysaMods");
                Directory.CreateDirectory(modsDir);

                string destVpkPath = Path.Combine(modsDir, "pak01_dir.vpk");

                progress?.Report(10);

                // Copy VPK file with progress
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
                            // Progress from 10% to 70%
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

                progress?.Report(75);
                cancellationToken.ThrowIfCancellationRequested();

                // Patch gameinfo and signatures (same as auto-install)
                string signaturesPath = Path.Combine(targetPath, "game", "bin", "win64", "dota.signatures");
                string gameInfoPath = Path.Combine(targetPath, "game", "dota", "gameinfo_branchspecific.gi");

                if (!File.Exists(signaturesPath))
                {
                    _logger?.Log("Error: Missing game core file.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(gameInfoPath)!);
                progress?.Report(80);

                // Download clean gameinfo with fallback URLs
                var giData = await DownloadGameInfoAsync(GameInfoUrls, cancellationToken).ConfigureAwait(false);
                if (giData == null)
                {
                    _logger?.Log("Error: Failed to download patch file.");
                    return false;
                }

                progress?.Report(85);

                // Atomic write to gameInfoPath
                try
                {
                    string tempGi = gameInfoPath + ".tmp";
                    await File.WriteAllBytesAsync(tempGi, giData, cancellationToken).ConfigureAwait(false);
                    if (File.Exists(gameInfoPath))
                        File.Replace(tempGi, gameInfoPath, null);
                    else
                        File.Move(tempGi, gameInfoPath, true);
                }
                catch (Exception ex)
                {
                    _logger?.Log("Error: Failed to apply game patch.");
                    FallbackLogger.Log($"Write gameinfo error: {ex.Message}");
                    return false;
                }

                progress?.Report(90);

                // Update signatures file
                try
                {
                    var lines = await File.ReadAllLinesAsync(signaturesPath, cancellationToken).ConfigureAwait(false);
                    int digestIndex = Array.FindIndex(lines, l => l.StartsWith("DIGEST:", StringComparison.Ordinal));
                    if (digestIndex < 0)
                    {
                        _logger?.Log("Error: Invalid core file format.");
                        return false;
                    }

                    var modified = new List<string>(lines[..(digestIndex + 1)])
                    {
                        @"...\..\..\dota\gameinfo_branchspecific.gi~SHA1:1A9B91FB43FE89AD104B8001282D292EED94584D;CRC:043F604A"
                    };

                    string tmpSig = signaturesPath + ".tmp";
                    await File.WriteAllLinesAsync(tmpSig, modified, cancellationToken).ConfigureAwait(false);
                    File.Replace(tmpSig, signaturesPath, null);
                }
                catch
                {
                    _logger?.Log("Error: Failed to update signatures.");
                    return false;
                }

                progress?.Report(100);
                _logger?.Log("Installation complete!");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("Installation canceled.");
                return false;
            }
            catch
            {
                _logger?.Log("Installation failed.");
                return false;
            }
        }

        private async Task<(bool Success, string Url)> TryGetModsPackAssetUrlAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string api = EnvironmentConfig.ModsPackReleasesApi;
                using var response = await _httpClient.GetAsync(api, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    FallbackLogger.Log($"TryGetModsPackAssetUrlAsync: GitHub API returned {response.StatusCode}");
                    return (false, string.Empty);
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

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
            catch (Exception ex)
            {
                FallbackLogger.Log($"TryGetModsPackAssetUrlAsync exception: {ex.Message}");
                return (false, string.Empty);
            }
        }
    }
}

