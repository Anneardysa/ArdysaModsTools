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
using System.Net.Http;
using System.Text.Json;
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
    public class UpdaterService
    {
        private readonly HttpClient _httpClient;
        private readonly Logger _logger;
        private readonly InstallationType _installationType;
        private readonly DeltaUpdateService _delta;

        private static string GitHubApiUrl => EnvironmentConfig.ToolsReleasesApi;

        public event Action<string>? OnVersionChanged;

        public string CurrentVersion { get; }

        public int CurrentBuildNumber { get; }

        public InstallationType InstallationType => _installationType;

        public DeltaUpdateService Delta => _delta;

        public UpdaterService(Logger logger)
        {
            _logger = logger;
            _httpClient = HttpClientProvider.Client;
            _delta = new DeltaUpdateService(logger);

            var version = Application.ProductVersion;
            CurrentVersion = string.IsNullOrEmpty(version) ? "1.0.0.0" : version;

            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
                CurrentBuildNumber = fvi.FilePrivatePart;
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not read build number from FileVersion: {ex.Message}");
                CurrentBuildNumber = 0;
            }

            _installationType = InstallationDetector.Detect();

            _logger.Log($"Update service initialized. Version: {CurrentVersion}, Build: {CurrentBuildNumber}, Type: {InstallationDetector.GetInstallationTypeName(_installationType)}");
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                await Task.Run(_delta.RepairInterruptedUpdate).ConfigureAwait(true);

                _logger.Log("Checking for updates...");

                var updateInfo = await GetUpdateInfoAsync();

                if (updateInfo == null)
                {
                    _logger.Log("Unable to fetch update information.");
                    return false;
                }

                OnVersionChanged?.Invoke(updateInfo.Version);

                if (updateInfo.IsUpdateAvailable)
                {
                    var currentDisplay = new AppVersion(CurrentVersion, CurrentBuildNumber);
                    var latestDisplay = new AppVersion(updateInfo.Version, updateInfo.BuildNumber);
                    _logger.Log($"Update available: {currentDisplay} → {latestDisplay}");
                    Form? parentForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                    bool applying = UI.Forms.UpdateAvailableDialogWebView.Show(
                        parentForm, updateInfo, _installationType, _delta);

                    _logger.Log(applying
                        ? "Applying update — the app is restarting."
                        : "Update dialog shown to user.");

                    return applying;
                }

                _logger.Log($"Up to date ({new AppVersion(CurrentVersion, CurrentBuildNumber)})");
            }
            catch (Exception ex)
            {
                _logger.Log($"Update check failed: {ex.Message}");
            }

            return false;
        }

        public async Task<UpdateInfo?> GetUpdateInfoAsync()
        {
            var updateInfo = await TryGetUpdateFromR2ManifestAsync();
            if (updateInfo != null)
            {
                _logger.Log($"Got update info from R2 CDN: v{updateInfo.Version}");
                return updateInfo;
            }

            return await GetUpdateFromGitHubAsync();
        }

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

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.Log("R2 manifest is empty, using GitHub API fallback");
                    return null;
                }

                content = content.TrimStart();
                if (!content.StartsWith("{") && !content.StartsWith("["))
                {
                    _logger.Log("R2 manifest is not valid JSON, using GitHub API fallback");
                    return null;
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (!root.TryGetProperty("latest", out var latestProp) || latestProp.ValueKind != JsonValueKind.String)
                    return null;

                string latestVersion = latestProp.GetString() ?? "0.0.0";

                if (!root.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Object)
                    return null;

                if (!releases.TryGetProperty(latestVersion, out var releaseInfo) || releaseInfo.ValueKind != JsonValueKind.Object)
                    return null;

                string? mirrorInstaller = null;
                string? mirrorPortable = null;
                string? releaseNotes = null;

                if (releaseInfo.TryGetProperty("installer", out var instEl) && instEl.ValueKind == JsonValueKind.String)
                    mirrorInstaller = instEl.GetString();

                if (releaseInfo.TryGetProperty("portable", out var portEl) && portEl.ValueKind == JsonValueKind.String)
                    mirrorPortable = portEl.GetString();

                if (releaseInfo.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.String)
                    releaseNotes = notesEl.GetString();

                string? installerSha = null;
                string? portableSha = null;
                if (releaseInfo.TryGetProperty("installerSha256", out var instShaEl) && instShaEl.ValueKind == JsonValueKind.String)
                    installerSha = instShaEl.GetString();
                if (releaseInfo.TryGetProperty("portableSha256", out var portShaEl) && portShaEl.ValueKind == JsonValueKind.String)
                    portableSha = portShaEl.GetString();

                string? filesManifest = null;
                if (releaseInfo.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.String)
                    filesManifest = filesEl.GetString();

                int buildNumber = 0;
                if (releaseInfo.TryGetProperty("build", out var buildEl))
                {
                    if (buildEl.ValueKind == JsonValueKind.Number)
                        buildNumber = buildEl.GetInt32();
                    else if (buildEl.ValueKind == JsonValueKind.String &&
                             int.TryParse(buildEl.GetString(), out int parsed))
                        buildNumber = parsed;
                }

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
                    InstallerSha256 = installerSha,
                    PortableSha256 = portableSha,
                    FilesManifestUrl = filesManifest,
                    ReleaseNotes = releaseNotes,
                    IsUpdateAvailable = ShouldUpdate(latestVersion, buildNumber)
                };
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.Log($"R2 CDN unavailable: {ex.Message}");
                return null;
            }
        }

        private async Task<UpdateInfo?> GetUpdateFromGitHubAsync()
        {
            try
            {
                var response = await RetryHelper.ExecuteAsync(async () =>
                {
                    var res = await _httpClient.GetAsync(GitHubApiUrl).ConfigureAwait(false);
                    
                    if (RetryHelper.IsTransientStatusCode(res.StatusCode))
                        throw new HttpRequestException($"Server returned {res.StatusCode}");
                    
                    return res;
                },
                maxAttempts: 3,
                onRetry: (attempt, ex) => _logger.Log($"Retry {attempt}/3: {ex.Message}"))
                .ConfigureAwait(false);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.Log("GitHub API rate limit reached. Try again later.");
                    return null;
                }
                
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream);

                var root = doc.RootElement;

                string latestVersion = "0.0.0";
                if (root.TryGetProperty("tag_name", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
                {
                    latestVersion = (tagEl.GetString() ?? "0.0.0").TrimStart('v');
                }

                string? releaseNotes = null;
                if (root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
                {
                    releaseNotes = bodyEl.GetString();
                }

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

                        if (asset.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                        {
                            string? name = nameEl.GetString();
                            if (name != null)
                            {
                                if (name.Contains("_Setup_", StringComparison.OrdinalIgnoreCase) && 
                                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    installerUrl = url;
                                    installerFilename = name;
                                }
                                else if (name.StartsWith("AMT-v", StringComparison.OrdinalIgnoreCase) && 
                                         name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                {
                                    portableUrl = url;
                                    portableFilename = name;
                                }
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

                if (portableUrl == null && legacyPortableUrl != null)
                {
                    portableUrl = legacyPortableUrl;
                }

                int buildNumber = 0;
                if (root.TryGetProperty("name", out var releaseNameEl) &&
                    releaseNameEl.ValueKind == JsonValueKind.String)
                {
                    buildNumber = AppVersion.ExtractBuildFromText(releaseNameEl.GetString());
                }
                if (buildNumber == 0 && !string.IsNullOrEmpty(releaseNotes))
                {
                    buildNumber = AppVersion.ExtractBuildFromText(releaseNotes);
                }

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

        private bool ShouldUpdate(string latestVersion, int latestBuild)
        {
            var current = new AppVersion(CurrentVersion, CurrentBuildNumber);
            var latest = new AppVersion(latestVersion, latestBuild);
            return current.ShouldUpdateTo(latest);
        }
    }
}
