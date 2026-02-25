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
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Constants;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Service for checking and applying application updates.
    /// Automatically detects installation type and uses the appropriate update strategy.
    /// </summary>
    public class UpdaterService
    {
        private readonly HttpClient _httpClient;
        private readonly Logger _logger;
        private readonly IUpdateStrategy _updateStrategy;
        private readonly InstallationType _installationType;

        // Dedicated HttpClient for binary downloads — no AutomaticDecompression
        // overhead since .exe/.zip files are already compressed.
        // MUST include User-Agent + GitHub token or downloads get throttled to ~50KB/s.
        private static readonly Lazy<HttpClient> _downloadClient = new(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                UseProxy = true,
                Proxy = WebRequest.GetSystemWebProxy(),
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                MaxConnectionsPerServer = 4,
            };
            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan // Per-request CTS handles this
            };

            // Critical: GitHub throttles requests without User-Agent
            client.DefaultRequestHeaders.Add("User-Agent", "ArdysaModsTools/1.0");

            // GitHub token for higher rate limits and faster downloads
            var token = EnvironmentConfig.GitHubToken;
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");

            return client;
        });

        /// <summary>Download buffer size (256 KB for maximum throughput).</summary>
        private const int DownloadBufferSize = 256 * 1024;

        /// <summary>Seconds with zero bytes before declaring a stall and switching CDN.</summary>
        private const int StallTimeoutSeconds = 10;

        /// <summary>Per-CDN connection timeout in seconds.</summary>
        private const int PerCdnTimeoutSeconds = 15;

        // URL now loaded from environment configuration
        private static string GitHubApiUrl => EnvironmentConfig.ToolsReleasesApi;

        /// <summary>
        /// Fired when download/update progress changes (0-100).
        /// </summary>
        public Action<int>? OnProgressChanged { get; set; }

        /// <summary>
        /// Fired when status message changes during update.
        /// </summary>
        public Action<string>? OnStatusChanged { get; set; }

        /// <summary>
        /// Fired when version information is retrieved.
        /// </summary>
        public event Action<string>? OnVersionChanged;

        /// <summary>
        /// The current application version.
        /// </summary>
        public string CurrentVersion { get; }

        /// <summary>
        /// The current build number (4th segment of FileVersion).
        /// Example: For FileVersion "2.1.13.2100", this is 2100.
        /// </summary>
        public int CurrentBuildNumber { get; }

        /// <summary>
        /// The detected installation type (Installer or Portable).
        /// </summary>
        public InstallationType InstallationType => _installationType;

        public UpdaterService(Logger logger)
        {
            _logger = logger;
            _httpClient = HttpClientProvider.Client;

            // Get current version from assembly
            var version = Application.ProductVersion;
            CurrentVersion = string.IsNullOrEmpty(version) ? "1.0.0.0" : version;

            // Get build number from FileVersion (4th segment: Major.Minor.Patch.BUILD)
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
                CurrentBuildNumber = fvi.FilePrivatePart; // 4th segment of FileVersion
            }
            catch
            {
                CurrentBuildNumber = 0;
            }

            // Detect installation type and select appropriate strategy
            _installationType = InstallationDetector.Detect();
            _updateStrategy = _installationType switch
            {
                InstallationType.Installer => new InstallerUpdateStrategy(),
                InstallationType.Portable => new PortableUpdateStrategy(),
                _ => new PortableUpdateStrategy() // Default to portable for safety
            };

            _logger.Log($"Update service initialized. Version: {CurrentVersion}, Build: {CurrentBuildNumber}, Type: {InstallationDetector.GetInstallationTypeName(_installationType)}");
        }

        /// <summary>
        /// Checks for updates and prompts the user if one is available.
        /// </summary>
        public async Task CheckForUpdatesAsync()
        {
            try
            {
                _logger.Log("Checking for updates...");
                OnStatusChanged?.Invoke("Checking for updates...");

                var updateInfo = await GetUpdateInfoAsync();

                if (updateInfo == null)
                {
                    _logger.Log("Unable to fetch update information.");
                    return;
                }

                OnVersionChanged?.Invoke(updateInfo.Version);

                if (updateInfo.IsUpdateAvailable)
                {
                    var currentDisplay = new AppVersion(CurrentVersion, CurrentBuildNumber);
                    var latestDisplay = new AppVersion(updateInfo.Version, updateInfo.BuildNumber);
                    _logger.Log($"Update available: {currentDisplay} → {latestDisplay}");
                    // Show modern WebView2 update dialog with manual download links
                    // (falls back to MessageBox if WebView2 unavailable)
                    Form? parentForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                    UI.Forms.UpdateAvailableDialogWebView.Show(
                        parentForm, updateInfo, _installationType);

                    _logger.Log("Update dialog shown to user.");
                }
                else
                {
                    _logger.Log($"Up to date ({new AppVersion(CurrentVersion, CurrentBuildNumber)})");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets update information with R2 CDN priority.
        /// Tries R2 manifest first (faster), falls back to GitHub API.
        /// </summary>
        public async Task<UpdateInfo?> GetUpdateInfoAsync()
        {
            // Try R2 manifest first (faster CDN)
            var updateInfo = await TryGetUpdateFromR2ManifestAsync();
            if (updateInfo != null)
            {
                _logger.Log($"Got update info from R2 CDN: v{updateInfo.Version}");
                return updateInfo;
            }

            // Fallback to GitHub API (this is normal if R2 is slow or unreachable)
            return await GetUpdateFromGitHubAsync();
        }

        /// <summary>
        /// Try to get update info from R2 manifest (fast CDN).
        /// </summary>
        private async Task<UpdateInfo?> TryGetUpdateFromR2ManifestAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetAsync(CdnConfig.ReleaseManifestUrl, cts.Token).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"R2 manifest not available (HTTP {(int)response.StatusCode})");
                    return null;
                }

                // Read content as string first to validate
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                // Check if content is empty or not valid JSON
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.Log("R2 manifest is empty, using GitHub API fallback");
                    return null;
                }

                // Quick check for valid JSON start
                content = content.TrimStart();
                if (!content.StartsWith("{") && !content.StartsWith("["))
                {
                    _logger.Log("R2 manifest is not valid JSON, using GitHub API fallback");
                    return null;
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Get latest version
                if (!root.TryGetProperty("latest", out var latestProp) || latestProp.ValueKind != JsonValueKind.String)
                    return null;

                string latestVersion = latestProp.GetString() ?? "0.0.0";

                // Get release info for this version
                if (!root.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Object)
                    return null;

                if (!releases.TryGetProperty(latestVersion, out var releaseInfo) || releaseInfo.ValueKind != JsonValueKind.Object)
                    return null;

                // Extract URLs
                string? mirrorInstaller = null;
                string? mirrorPortable = null;
                string? releaseNotes = null;

                if (releaseInfo.TryGetProperty("installer", out var instEl) && instEl.ValueKind == JsonValueKind.String)
                    mirrorInstaller = instEl.GetString();

                if (releaseInfo.TryGetProperty("portable", out var portEl) && portEl.ValueKind == JsonValueKind.String)
                    mirrorPortable = portEl.GetString();

                if (releaseInfo.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.String)
                    releaseNotes = notesEl.GetString();

                // Extract build number (optional field — backward compatible)
                int buildNumber = 0;
                if (releaseInfo.TryGetProperty("build", out var buildEl))
                {
                    if (buildEl.ValueKind == JsonValueKind.Number)
                        buildNumber = buildEl.GetInt32();
                    else if (buildEl.ValueKind == JsonValueKind.String &&
                             int.TryParse(buildEl.GetString(), out int parsed))
                        buildNumber = parsed;
                }

                // Fallback: extract build from release notes (handles older manifests)
                if (buildNumber == 0 && !string.IsNullOrEmpty(releaseNotes))
                {
                    buildNumber = AppVersion.ExtractBuildFromText(releaseNotes);
                }

                return new UpdateInfo
                {
                    Version = latestVersion,
                    BuildNumber = buildNumber,
                    CurrentVersion = CurrentVersion,
                    CurrentBuildNumber = CurrentBuildNumber,
                    MirrorInstallerUrl = mirrorInstaller,
                    MirrorPortableUrl = mirrorPortable,
                    ReleaseNotes = releaseNotes,
                    IsUpdateAvailable = ShouldUpdate(latestVersion, buildNumber)
                };
            }
            catch (OperationCanceledException)
            {
                // R2 was slow, will use GitHub API instead (this is normal)
                return null;
            }
            catch (Exception ex)
            {
                // Only log unexpected errors, not expected fallback scenarios
                _logger.Log($"R2 CDN unavailable: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets update information from GitHub releases (fallback).
        /// </summary>
        private async Task<UpdateInfo?> GetUpdateFromGitHubAsync()
        {
            try
            {
                // Use retry logic for transient network failures
                var response = await RetryHelper.ExecuteAsync(async () =>
                {
                    var res = await _httpClient.GetAsync(GitHubApiUrl).ConfigureAwait(false);
                    
                    // Check for transient status codes that should trigger retry
                    if (RetryHelper.IsTransientStatusCode(res.StatusCode))
                        throw new HttpRequestException($"Server returned {res.StatusCode}");
                    
                    return res;
                },
                maxAttempts: 3,
                onRetry: (attempt, ex) => _logger.Log($"Retry {attempt}/3: {ex.Message}"))
                .ConfigureAwait(false);
                
                // Handle rate limiting gracefully
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.Log("GitHub API rate limit reached. Try again later.");
                    return null;
                }
                
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream);

                var root = doc.RootElement;

                // Get version
                string latestVersion = "0.0.0";
                if (root.TryGetProperty("tag_name", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
                {
                    latestVersion = (tagEl.GetString() ?? "0.0.0").TrimStart('v');
                }

                // Get release notes
                string? releaseNotes = null;
                if (root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
                {
                    releaseNotes = bodyEl.GetString();
                }

                // Find download URLs for installer and portable
                string? installerUrl = null;
                string? portableUrl = null;
                string? installerFilename = null;
                string? portableFilename = null;
                string? legacyPortableUrl = null;

                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.ValueKind != JsonValueKind.Object) continue;

                        if (!asset.TryGetProperty("browser_download_url", out var urlEl) || 
                            urlEl.ValueKind != JsonValueKind.String)
                            continue;

                        string? url = urlEl.GetString();
                        if (string.IsNullOrEmpty(url)) continue;

                        // Check asset name
                        if (asset.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                        {
                            string? name = nameEl.GetString();
                            if (name != null)
                            {
                                // Installer: *_Setup_*.exe
                                if (name.Contains("_Setup_", StringComparison.OrdinalIgnoreCase) && 
                                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    installerUrl = url;
                                    installerFilename = name;
                                }
                                // Portable (new format): AMT-v*.zip
                                else if (name.StartsWith("AMT-v", StringComparison.OrdinalIgnoreCase) && 
                                         name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                {
                                    portableUrl = url;
                                    portableFilename = name;
                                }
                                // Portable (legacy format)
                                else if (name.Contains("_Portable_", StringComparison.OrdinalIgnoreCase) &&
                                        (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                         name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                                {
                                    legacyPortableUrl = url;
                                }
                            }
                        }
                    }
                }

                // Use legacy format as fallback if new format not found
                if (portableUrl == null && legacyPortableUrl != null)
                {
                    portableUrl = legacyPortableUrl;
                }

                // Extract build number from release name or body
                int buildNumber = 0;
                if (root.TryGetProperty("name", out var releaseNameEl) &&
                    releaseNameEl.ValueKind == JsonValueKind.String)
                {
                    buildNumber = AppVersion.ExtractBuildFromText(releaseNameEl.GetString());
                }
                // Fallback: try to extract from release notes/body
                if (buildNumber == 0 && !string.IsNullOrEmpty(releaseNotes))
                {
                    buildNumber = AppVersion.ExtractBuildFromText(releaseNotes);
                }

                // Build mirror URLs if we have filenames
                string? mirrorInstaller = installerFilename != null 
                    ? CdnConfig.BuildReleaseUrl(latestVersion, installerFilename) 
                    : null;
                string? mirrorPortable = portableFilename != null 
                    ? CdnConfig.BuildReleaseUrl(latestVersion, portableFilename) 
                    : null;

                return new UpdateInfo
                {
                    Version = latestVersion,
                    BuildNumber = buildNumber,
                    CurrentVersion = CurrentVersion,
                    CurrentBuildNumber = CurrentBuildNumber,
                    InstallerDownloadUrl = installerUrl,
                    PortableDownloadUrl = portableUrl,
                    MirrorInstallerUrl = mirrorInstaller,
                    MirrorPortableUrl = mirrorPortable,
                    ReleaseNotes = releaseNotes,
                    IsUpdateAvailable = ShouldUpdate(latestVersion, buildNumber)
                };
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to retrieve release info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads and applies the update using the appropriate strategy.
        /// Shows a ProgressOverlay during download for visual feedback.
        /// Uses multi-CDN with stall detection for maximum download speed.
        /// </summary>
        private async Task DownloadAndApplyUpdateAsync(UpdateInfo updateInfo)
        {
            string? tempFilePath = null;

            try
            {
                string? validationError = _updateStrategy.ValidateCanUpdate();
                if (validationError != null)
                {
                    _logger.Log($"Cannot update: {validationError}");
                    MessageBox.Show(validationError, "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Build ordered download URLs from all available CDN sources
                var downloadUrls = BuildOrderedDownloadUrls(updateInfo);

                if (downloadUrls.Count == 0)
                {
                    string assetType = _installationType == InstallationType.Installer ? "installer" : "portable";
                    _logger.Log($"No {assetType} asset found in release.");
                    MessageBox.Show(
                        $"No {assetType} download available for this release.\n" +
                        "Please download the update manually from GitHub.",
                        "Update Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _logger.Log($"Download servers ({downloadUrls.Count}): {string.Join(", ", downloadUrls.ConvertAll(u => GetCdnLabel(u)))}");

                // Download with progress overlay and multi-CDN fallback
                tempFilePath = await DownloadWithMultiCdnAsync(downloadUrls, updateInfo.Version);

                if (string.IsNullOrEmpty(tempFilePath))
                {
                    _logger.Log("Download failed or cancelled.");
                    return;
                }

                _logger.Log("Download complete. Applying update...");
                OnStatusChanged?.Invoke("Applying update...");

                // Apply the update using the strategy
                var result = await _updateStrategy.ApplyUpdateAsync(
                    tempFilePath,
                    OnProgressChanged,
                    OnStatusChanged);

                if (result.Success)
                {
                    // Clean up temp/cache files after successful update
                    await CleanupAfterUpdateAsync(tempFilePath);

                    if (result.RequiresRestart)
                    {
                        _logger.Log("Update in progress. Closing app...");
                        await Task.Delay(1500);
                        Application.Exit();
                    }
                    else
                    {
                        _logger.Log("Update applied successfully.");
                    }
                }
                else
                {
                    _logger.Log($"Update failed: {result.ErrorMessage}");
                    MessageBox.Show(
                        $"Update failed: {result.ErrorMessage}",
                        "Update Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Update failed: {ex.Message}");
                MessageBox.Show(
                    $"Update failed: {ex.Message}",
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Build ordered download URL list from all available CDN sources.
        /// Uses SmartCdnSelector to order by speed (fastest first).
        /// </summary>
        private List<string> BuildOrderedDownloadUrls(UpdateInfo updateInfo)
        {
            var urls = new List<string>();

            // GitHub release URLs — primary (fastest, direct CDN)
            string? githubUrl = _installationType == InstallationType.Installer
                ? updateInfo.InstallerDownloadUrl
                : updateInfo.PortableDownloadUrl;
            if (!string.IsNullOrEmpty(githubUrl))
                urls.Add(githubUrl);

            // R2 mirror URLs — fallback
            string? mirrorUrl = _installationType == InstallationType.Installer
                ? updateInfo.MirrorInstallerUrl
                : updateInfo.MirrorPortableUrl;
            if (!string.IsNullOrEmpty(mirrorUrl))
                urls.Add(mirrorUrl);

            return urls;
        }

        /// <summary>
        /// Multi-CDN download with ProgressOverlay, stall detection, and server logging.
        /// Tries each CDN in speed-optimized order; auto-switches on stall or failure.
        /// </summary>
        private async Task<string?> DownloadWithMultiCdnAsync(List<string> urls, string version)
        {
            Form? parentForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

            string? resultPath = null;
            bool wasCancelled = false;
            Exception? downloadException = null;

            UI.Forms.ProgressOverlay? overlay = null;
            Panel? dimPanel = null;

            if (parentForm != null && !parentForm.InvokeRequired)
            {
                dimPanel = new Panel
                {
                    BackColor = Color.FromArgb(179, 0, 0, 0),
                    Dock = DockStyle.Fill
                };
                parentForm.Controls.Add(dimPanel);
                dimPanel.BringToFront();

                overlay = new UI.Forms.ProgressOverlay();

                try
                {
                    await overlay.InitializeAsync();
                    await overlay.UpdateStatusAsync($"Updating to v{version}...");
                }
                catch
                {
                    overlay?.Dispose();
                    overlay = null;
                    parentForm.Controls.Remove(dimPanel!);
                    dimPanel?.Dispose();
                    dimPanel = null;
                }
            }

            if (overlay != null && parentForm != null)
            {
                var cts = new CancellationTokenSource();

                overlay.CancelRequested += (s, e) =>
                {
                    wasCancelled = true;
                    try { cts.Cancel(); } catch { }
                };

                // Build server log entries
                var serverLog = new ServerLogEntry[urls.Count];
                for (int i = 0; i < urls.Count; i++)
                {
                    serverLog[i] = new ServerLogEntry
                    {
                        Name = $"Server-{(i + 1):D2}",
                        InternalLabel = GetCdnLabel(urls[i]),
                        Status = ServerStatus.Standby
                    };
                }

                var downloadTask = Task.Run(async () =>
                {
                    try
                    {
                        resultPath = await DownloadFromMultipleCdnsAsync(
                            urls, serverLog, overlay, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        wasCancelled = true;
                    }
                    catch (Exception ex)
                    {
                        downloadException = ex;
                    }
                    finally
                    {
                        // Complete() is idempotent — safe if cancel already closed the dialog
                        try { overlay.Complete(); } catch { }
                    }
                }, cts.Token);

                overlay.ShowDialog(parentForm);

                try { await downloadTask; } catch { }

                overlay.Dispose();
                parentForm.Controls.Remove(dimPanel!);
                dimPanel?.Dispose();
                cts.Dispose();

                if (wasCancelled)
                {
                    _logger.Log("Update download cancelled by user.");
                    // Clean up any temp file that may have completed during cancel
                    if (!string.IsNullOrEmpty(resultPath))
                    {
                        try { File.Delete(resultPath); } catch { }
                    }
                    return null;
                }

                if (downloadException != null)
                {
                    _logger.Log($"Download failed: {downloadException.Message}");
                    return null;
                }

                return resultPath;
            }
            else
            {
                // Fallback: no overlay — download from first available URL
                return await DownloadFileAsync(urls[0]);
            }
        }

        /// <summary>
        /// Core multi-CDN download engine with stall detection.
        /// Tries each URL in order; if download stalls (no data for 10s), switches to next CDN.
        /// Reports progress and server status to the ProgressOverlay.
        /// </summary>
        private async Task<string?> DownloadFromMultipleCdnsAsync(
            List<string> urls,
            ServerLogEntry[] serverLog,
            UI.Forms.ProgressOverlay overlay,
            CancellationToken ct)
        {
            string? lastError = null;

            for (int cdnIdx = 0; cdnIdx < urls.Count; cdnIdx++)
            {
                ct.ThrowIfCancellationRequested();

                string url = urls[cdnIdx];
                string cdnLabel = serverLog[cdnIdx].InternalLabel;

                // Update server log: mark this CDN as active
                serverLog[cdnIdx].Status = ServerStatus.Active;
                _logger.Log($"Trying {serverLog[cdnIdx].Name} ({cdnLabel}): {url}");
                UpdateServerLogOnUi(overlay, serverLog);

                try
                {
                    string? result = await DownloadFromSingleCdnAsync(
                        url, overlay, serverLog, cdnIdx, ct);

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Success
                        serverLog[cdnIdx].Status = ServerStatus.Success;
                        UpdateServerLogOnUi(overlay, serverLog);
                        _logger.Log($"Download successful from {serverLog[cdnIdx].Name} ({cdnLabel})");
                        return result;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // User cancelled — bubble up
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.Log($"{serverLog[cdnIdx].Name} ({cdnLabel}) failed: {ex.Message}");

                    // Report failure to SmartCdnSelector for future reordering
                    SmartCdnSelector.Instance.ReportFailure(url);
                }

                // Mark this CDN as failed
                serverLog[cdnIdx].Status = ServerStatus.Failed;
                UpdateServerLogOnUi(overlay, serverLog);

                if (cdnIdx < urls.Count - 1)
                {
                    _logger.Log($"Switching to {serverLog[cdnIdx + 1].Name}...");
                }
            }

            _logger.Log($"All download servers failed. Last error: {lastError}");
            return null;
        }

        /// <summary>
        /// Download from a single CDN URL with stall detection.
        /// Returns the temp file path on success, null on stall/failure.
        /// </summary>
        private async Task<string?> DownloadFromSingleCdnAsync(
            string url,
            UI.Forms.ProgressOverlay overlay,
            ServerLogEntry[] serverLog,
            int cdnIdx,
            CancellationToken ct)
        {
            var client = _downloadClient.Value;

            // Per-CDN timeout for initial connection
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(PerCdnTimeoutSeconds));

            string extension = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".exe";
            string tempPath = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Update_{Guid.NewGuid()}{extension}");

            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, connectCts.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var netStream = await response.Content.ReadAsStreamAsync(ct)
                .ConfigureAwait(false);
            await using var fileStream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, true);

            long totalRead = 0;
            long? totalBytes = response.Content.Headers.ContentLength;
            byte[] buffer = new byte[DownloadBufferSize];
            int lastProgress = -1;

            // Speed tracking
            var sw = Stopwatch.StartNew();
            long speedWindowBytes = 0;
            var speedWindowStart = sw.Elapsed;
            double lastSpeed = 0;
            bool firstSpeedSample = true; // First sample uses shorter window for fast feedback

            // Stall detection
            var lastDataTime = DateTime.UtcNow;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Check for stall
                if ((DateTime.UtcNow - lastDataTime).TotalSeconds > StallTimeoutSeconds)
                {
                    _logger.Log($"{serverLog[cdnIdx].Name} stalled (no data for {StallTimeoutSeconds}s)");
                    // Clean up partial file
                    try { fileStream.Close(); } catch { }
                    try { File.Delete(tempPath); } catch { }
                    return null; // Stall — caller will try next CDN
                }

                // Read with a short timeout to detect stalls promptly
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(TimeSpan.FromSeconds(StallTimeoutSeconds + 2));

                int bytesRead;
                try
                {
                    bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Read timeout — stall
                    _logger.Log($"{serverLog[cdnIdx].Name} read timeout");
                    try { fileStream.Close(); } catch { }
                    try { File.Delete(tempPath); } catch { }
                    return null;
                }

                if (bytesRead == 0)
                    break; // Download complete

                await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                totalRead += bytesRead;
                speedWindowBytes += bytesRead;
                lastDataTime = DateTime.UtcNow;

                // Speed tracking — first sample at 200ms, then 500ms for smoother updates
                var elapsedSinceWindow = sw.Elapsed - speedWindowStart;
                int speedWindowMs = firstSpeedSample ? 200 : 500;
                bool speedWindowTick = elapsedSinceWindow.TotalMilliseconds >= speedWindowMs;
                if (speedWindowTick)
                {
                    lastSpeed = speedWindowBytes / elapsedSinceWindow.TotalSeconds / (1024 * 1024);
                    speedWindowBytes = 0;
                    speedWindowStart = sw.Elapsed;
                    firstSpeedSample = false;
                }

                // Update progress UI
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int progress = (int)(totalRead * 100L / totalBytes.Value);
                    bool percentChanged = progress > lastProgress;

                    if (percentChanged)
                        lastProgress = progress;

                    // Push UI updates on percent change OR every speed tick
                    if (percentChanged || speedWindowTick)
                    {
                        int snapProgress = lastProgress;
                        double snapMbDown = totalRead / 1024.0 / 1024.0;
                        double snapMbTotal = totalBytes.Value / 1024.0 / 1024.0;
                        string snapSpeedText = lastSpeed > 0 ? $"{lastSpeed:F1} MB/S" : "-- MB/S";

                        try
                        {
                            overlay.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    _ = overlay.UpdateProgressAsync(snapProgress);
                                    _ = overlay.UpdateDownloadProgressAsync(snapMbDown, snapMbTotal);
                                    _ = overlay.UpdateDownloadSpeedAsync(snapSpeedText);
                                }
                                catch { /* overlay disposed or closing */ }
                            }));
                        }
                        catch { /* handle not created yet or overlay disposed */ }
                    }
                }
            }

            sw.Stop();
            double avgSpeed = totalRead / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
            _logger.Log($"Download: {totalRead / 1024.0 / 1024.0:F1} MB at {avgSpeed:F1} MB/s");

            return tempPath;
        }

        /// <summary>
        /// Update server log on the UI thread.
        /// </summary>
        private static void UpdateServerLogOnUi(UI.Forms.ProgressOverlay overlay, ServerLogEntry[] serverLog)
        {
            if (overlay.InvokeRequired)
            {
                // Clone entries for thread safety
                var snapshot = new ServerLogEntry[serverLog.Length];
                for (int i = 0; i < serverLog.Length; i++)
                {
                    snapshot[i] = new ServerLogEntry
                    {
                        Name = serverLog[i].Name,
                        InternalLabel = serverLog[i].InternalLabel,
                        Status = serverLog[i].Status
                    };
                }
                overlay.BeginInvoke(new Action(async () =>
                {
                    await overlay.UpdateServerLogAsync(snapshot);
                }));
            }
        }

        /// <summary>
        /// Get display label for a CDN URL (for internal logging only).
        /// </summary>
        private static string GetCdnLabel(string url)
        {
            if (url.Contains("ardysamods.my.id") || url.Contains("r2.dev"))
                return "R2";
            if (url.Contains("jsdelivr.net"))
                return "jsDelivr";
            if (url.Contains("github.com") || url.Contains("githubusercontent.com"))
                return "GitHub";
            return "Unknown";
        }

        /// <summary>
        /// Fallback: simple download without overlay (used when WebView2 unavailable).
        /// </summary>
        private async Task<string?> DownloadFileAsync(string url)
        {
            try
            {
                var client = _downloadClient.Value;
                string extension = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".exe";
                string tempPath = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Update_{Guid.NewGuid()}{extension}");

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                await using var netStream = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, true);

                long totalRead = 0;
                long? totalBytes = response.Content.Headers.ContentLength;
                byte[] buffer = new byte[DownloadBufferSize];
                int bytesRead;
                int lastProgress = 0;
                var sw = Stopwatch.StartNew();

                while ((bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue)
                    {
                        int progress = (int)(totalRead * 100L / totalBytes.Value);
                        if (progress > lastProgress)
                        {
                            lastProgress = progress;
                            OnProgressChanged?.Invoke(progress);

                            if (progress % 20 == 0)
                            {
                                double mbDownloaded = totalRead / 1024.0 / 1024.0;
                                double mbTotal = totalBytes.Value / 1024.0 / 1024.0;
                                OnStatusChanged?.Invoke($"Downloading... {mbDownloaded:F1}/{mbTotal:F1} MB ({progress}%)");
                            }
                        }
                    }
                }

                sw.Stop();
                double speed = totalRead / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                _logger.Log($"Download complete: {totalRead / 1024.0 / 1024.0:F1} MB at {speed:F1} MB/s");

                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Download failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines whether the latest version/build should trigger an update.
        /// Uses AppVersion for robust comparison:
        ///   1. Higher semantic version → always update
        ///   2. Same version, higher build → update (enables hotfix-style updates)
        ///   3. Same version and build → no update
        /// </summary>
        /// <param name="latestVersion">Version string from R2 manifest or GitHub.</param>
        /// <param name="latestBuild">Build number from R2 manifest or GitHub release.</param>
        /// <returns>True if an update should be offered.</returns>
        private bool ShouldUpdate(string latestVersion, int latestBuild)
        {
            var current = new AppVersion(CurrentVersion, CurrentBuildNumber);
            var latest = new AppVersion(latestVersion, latestBuild);
            return current.ShouldUpdateTo(latest);
        }

        /// <summary>
        /// Clean up all temp/cache files after a successful update.
        /// Removes: downloaded update file, leftover update temp files,
        /// WebView2 progress overlay cache, and all app temp/cache via CacheCleaningService.
        /// </summary>
        private async Task CleanupAfterUpdateAsync(string? downloadedFilePath)
        {
            _logger.Log("Cleaning up temp/cache files after update...");
            int filesDeleted = 0;

            // Run file cleanup on background thread to avoid blocking UI
            await Task.Run(() =>
            {
                // 1. Delete the downloaded update file
                if (!string.IsNullOrEmpty(downloadedFilePath))
                {
                    try
                    {
                        if (File.Exists(downloadedFilePath))
                        {
                            File.Delete(downloadedFilePath);
                            Interlocked.Increment(ref filesDeleted);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Could not delete update file: {ex.Message}");
                    }
                }

                // 2. Clean up any leftover ArdysaModsTools_Update_* temp files
                try
                {
                    string tempDir = Path.GetTempPath();
                    var leftoverFiles = Directory.GetFiles(tempDir, "ArdysaModsTools_Update_*");
                    foreach (var file in leftoverFiles)
                    {
                        try { File.Delete(file); Interlocked.Increment(ref filesDeleted); }
                        catch { /* in use or locked */ }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Could not clean leftover update files: {ex.Message}");
                }

                // 3. Clean up WebView2 temp folder used by progress overlay
                try
                {
                    string webViewTemp = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                    if (Directory.Exists(webViewTemp))
                    {
                        Directory.Delete(webViewTemp, true);
                        _logger.Log("Cleaned WebView2 temp folder");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Could not clean WebView2 temp: {ex.Message}");
                }
            }).ConfigureAwait(false);

            // 4. Full cache clean via CacheCleaningService (already uses Task.Run internally)
            try
            {
                var cacheService = new Core.Services.App.CacheCleaningService();
                var result = await cacheService.ClearAllCacheAsync().ConfigureAwait(false);
                filesDeleted += result.FilesDeleted;
                _logger.Log($"Cache cleaned: {result.FilesDeleted} files, {Core.Services.App.CacheCleaningService.FormatBytes(result.BytesFreed)} freed");
            }
            catch (Exception ex)
            {
                _logger.Log($"Cache clean failed: {ex.Message}");
            }

            _logger.Log($"Post-update cleanup complete. {filesDeleted} files removed.");
        }
    }
}
