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
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Core.Services.Config;
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
                    
                    string installTypeInfo = _installationType == InstallationType.Installer 
                        ? "(will require administrator privileges)" 
                        : "(portable update)";

                    var currentVer = new AppVersion(CurrentVersion, CurrentBuildNumber);
                    var latestVer = new AppVersion(updateInfo.Version, updateInfo.BuildNumber);

                    var result = MessageBox.Show(
                        $"A new version is available.\n\n" +
                        $"Current: {currentVer}\n" +
                        $"Latest:  {latestVer}\n" +
                        $"Update type: {InstallationDetector.GetInstallationTypeName(_installationType)} {installTypeInfo}\n\n" +
                        $"Do you want to update now?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        await DownloadAndApplyUpdateAsync(updateInfo);
                    }
                    else
                    {
                        _logger.Log("Update skipped by user.");
                    }
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

                string? downloadUrl = _installationType == InstallationType.Installer
                    ? updateInfo.InstallerDownloadUrl
                    : updateInfo.PortableDownloadUrl;

                // Try mirror URL first (R2 CDN - faster for some regions)
                string? mirrorUrl = _installationType == InstallationType.Installer
                    ? updateInfo.MirrorInstallerUrl
                    : updateInfo.MirrorPortableUrl;

                if (string.IsNullOrEmpty(downloadUrl) && string.IsNullOrEmpty(mirrorUrl))
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

                // Download the update with progress overlay - try mirror first, then GitHub
                string primaryUrl = mirrorUrl ?? downloadUrl!;
                string? fallbackUrl = !string.IsNullOrEmpty(mirrorUrl) ? downloadUrl : null;
                
                _logger.Log($"Downloading update from: {primaryUrl}");
                
                tempFilePath = await DownloadWithFallbackAsync(primaryUrl, fallbackUrl, updateInfo.Version);

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
                    if (result.RequiresRestart)
                    {
                        _logger.Log("Update in progress. Closing app...");
                        
                        // For portable: We need to close so the batch script can proceed
                        // For installer: Inno Setup with /CLOSEAPPLICATIONS will close us
                        // Either way, give user a moment to see the status, then close
                        await Task.Delay(1500);
                        
                        // Close the application so update can proceed
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
        /// Downloads update with fallback support.
        /// Tries primary URL first (R2 CDN), falls back to secondary URL (GitHub) if failed.
        /// </summary>
        /// <param name="primaryUrl">Primary download URL (typically R2 mirror)</param>
        /// <param name="fallbackUrl">Fallback URL (typically GitHub), can be null</param>
        /// <param name="version">Version being downloaded</param>
        /// <returns>Path to downloaded file, or null if all downloads failed</returns>
        private async Task<string?> DownloadWithFallbackAsync(string primaryUrl, string? fallbackUrl, string version)
        {
            // Try primary URL first (R2 CDN - faster for some regions)
            string? result = await DownloadWithProgressOverlayAsync(primaryUrl, version);
            
            if (!string.IsNullOrEmpty(result))
            {
                _logger.Log($"Download successful from primary source");
                return result;
            }

            // If primary failed and we have a fallback, try it
            if (!string.IsNullOrEmpty(fallbackUrl))
            {
                _logger.Log($"Primary download failed, trying fallback: {fallbackUrl}");
                result = await DownloadWithProgressOverlayAsync(fallbackUrl, version);
                
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.Log($"Download successful from fallback source");
                    return result;
                }
            }

            _logger.Log("All download sources failed");
            return null;
        }

        /// <summary>
        /// Downloads update file with animated ProgressOverlay.
        /// </summary>
        private async Task<string?> DownloadWithProgressOverlayAsync(string url, string version)
        {
            // Find parent form for overlay
            Form? parentForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            
            string? resultPath = null;
            bool wasCancelled = false;
            Exception? downloadException = null;

            // Create overlay on UI thread
            UI.Forms.ProgressOverlay? overlay = null;
            Panel? dimPanel = null;
            
            if (parentForm != null && !parentForm.InvokeRequired)
            {
                // Create dim panel
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
                    // WebView2 not available - fall back to classic download
                    overlay?.Dispose();
                    overlay = null;
                    parentForm.Controls.Remove(dimPanel!);
                    dimPanel?.Dispose();
                    dimPanel = null;
                }
            }

            if (overlay != null && parentForm != null)
            {
                // Download with overlay
                var cts = new CancellationTokenSource();
                
                overlay.CancelRequested += (s, e) =>
                {
                    wasCancelled = true;
                    try { cts.Cancel(); } catch { }
                };

                // Start download task
                var downloadTask = Task.Run(async () =>
                {
                    try
                    {
                        string extension = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".exe";
                        string tempPath = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Update_{Guid.NewGuid()}{extension}");

                        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                        response.EnsureSuccessStatusCode();

                        await using var netStream = await response.Content.ReadAsStreamAsync(cts.Token);
                        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                        long totalRead = 0;
                        long? totalBytes = response.Content.Headers.ContentLength;
                        byte[] buffer = new byte[81920];
                        int bytesRead;
                        int lastProgress = -1;
                        var sw = Stopwatch.StartNew();
                        long lastBytes = 0;
                        double lastSpeed = 0;
                        bool needsUiUpdate = false;

                        while ((bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                            totalRead += bytesRead;

                            // Recalculate speed and flag UI update every ~500ms
                            if (sw.ElapsedMilliseconds >= 500)
                            {
                                long deltaBytes = totalRead - lastBytes;
                                double elapsedSec = sw.ElapsedMilliseconds / 1000.0;
                                lastSpeed = elapsedSec > 0 ? deltaBytes / elapsedSec / (1024 * 1024) : 0; // MB/s
                                lastBytes = totalRead;
                                sw.Restart();
                                needsUiUpdate = true;
                            }

                            if (totalBytes.HasValue)
                            {
                                int progress = (int)(totalRead * 100L / totalBytes.Value);

                                // Trigger UI update on progress change OR timer tick
                                if (progress > lastProgress)
                                {
                                    lastProgress = progress;
                                    needsUiUpdate = true;
                                }

                                if (needsUiUpdate && overlay.InvokeRequired)
                                {
                                    needsUiUpdate = false;

                                    // Snapshot values for thread-safe UI update
                                    int snapProgress = lastProgress;
                                    double snapMbDown = totalRead / 1024.0 / 1024.0;
                                    double snapMbTotal = totalBytes.Value / 1024.0 / 1024.0;
                                    double snapSpeed = lastSpeed;

                                    overlay.BeginInvoke(new Action(async () =>
                                    {
                                        await overlay.UpdateProgressAsync(snapProgress);
                                        await overlay.UpdateDownloadProgressAsync(snapMbDown, snapMbTotal);
                                        await overlay.UpdateDownloadSpeedAsync(
                                            snapSpeed > 0 ? $"{snapSpeed:F1} MB/S" : "-- MB/S");
                                    }));
                                }
                            }
                        }

                        resultPath = tempPath;
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
                        // Close overlay
                        if (overlay.InvokeRequired)
                            overlay.BeginInvoke(new Action(() => { try { overlay.Complete(); } catch { } }));
                        else
                            try { overlay.Complete(); } catch { }
                    }
                }, cts.Token);

                // Show dialog (blocks until closed)
                overlay.ShowDialog(parentForm);
                
                // Wait for download to finish
                try { await downloadTask; } catch { }
                
                overlay.Dispose();
                parentForm.Controls.Remove(dimPanel!);
                dimPanel?.Dispose();
                cts.Dispose();

                if (wasCancelled)
                {
                    _logger.Log("Update download cancelled by user.");
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
                // Fallback: download without overlay (original method)
                return await DownloadFileAsync(url);
            }
        }

        /// <summary>
        /// Downloads a file from URL to a temporary location with progress reporting.
        /// </summary>
        private async Task<string?> DownloadFileAsync(string url)
        {
            try
            {
                string extension = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".exe";
                string tempPath = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Update_{Guid.NewGuid()}{extension}");

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var netStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                long totalRead = 0;
                long? totalBytes = response.Content.Headers.ContentLength;
                byte[] buffer = new byte[81920];
                int bytesRead;
                int lastProgress = 0;
                var sw = Stopwatch.StartNew();

                while ((bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
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
    }
}

