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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Helpers;
using SharpCompress.Archives;
using SharpCompress.Common;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for asset modification operations.
    /// </summary>
    public interface IAssetModifier
    {
        /// <summary>
        /// Apply modifications based on user selections.
        /// </summary>
        Task<bool> ApplyModificationsAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);

        /// <summary>
        /// Gets the installed files from the last modification operation.
        /// Key: category, Value: list of relative file paths
        /// </summary>
        Dictionary<string, List<string>> GetInstalledFiles();
    }

    /// <summary>
    /// Focused service for applying mod patches to items_game.txt and assets.
    /// Single responsibility: Download and apply mod patches based on user selections.
    /// Uses balanced brace matching for reliable KV block replacement.
    /// </summary>
    public sealed class AssetModifierService : IAssetModifier
    {
        private readonly HttpClient _httpClient;
        private readonly IAppLogger? _logger;

        // Tracks installed files from last operation (category -> file paths)
        private readonly Dictionary<string, List<string>> _installedFiles = new();

        // Previous extraction log for cleanup
        private MiscExtractionLog? _previousLog;

        // Item IDs for each mod category in items_game.txt
        private static readonly Dictionary<string, string> CategoryItemIds = new()
        {
            { "Weather", "555" },
            { "Map", "590" },
            { "Music", "588" },
            { "HUD", "587" },
            { "Versus", "12970" },
            { "RadiantCreep", "660" },
            { "DireCreep", "661" },
            { "RadiantSiege", "34462" },
            { "DireSiege", "34463" },
            { "RadiantTower", "677" },
            { "DireTower", "678" }
        };

        public AssetModifierService(HttpClient? httpClient = null, IAppLogger? logger = null)
        {
            _httpClient = httpClient ?? HttpClientProvider.Client;
            _logger = logger;
        }

        /// <inheritdoc />
        public Dictionary<string, List<string>> GetInstalledFiles() => _installedFiles;

        /// <summary>
        /// Sets the previous extraction log for cleanup operations.
        /// </summary>
        public void SetPreviousLog(MiscExtractionLog? log) => _previousLog = log;

        /// <inheritdoc />
        public async Task<bool> ApplyModificationsAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            log("Applying modifications...");

            // Clear installed files from previous run
            _installedFiles.Clear();

            string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
            if (!File.Exists(itemsGamePath))
            {
                log("items_game.txt not found.");
                return false;
            }

            string content = await File.ReadAllTextAsync(itemsGamePath, ct).ConfigureAwait(false);

            // Prettify one-liner format to multi-line format for reliable parsing
            if (KeyValuesBlockHelper.IsOneLinerFormat(content))
            {
                content = KeyValuesBlockHelper.PrettifyKvText(content);
            }

            // Apply each mod type using balanced brace matching
            foreach (var kvp in CategoryItemIds)
            {
                ct.ThrowIfCancellationRequested();
                content = await ApplyBlockModAsync(content, selections, kvp.Key, kvp.Value, log, ct, speedProgress).ConfigureAwait(false);
            }

            // Apply file-based mods (these don't modify items_game.txt)
            // Adding intervals between each for better pacing
            await ApplyRiverModAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await Task.Delay(200, ct).ConfigureAwait(false);
            
            await ApplyEmblemModAsync(extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await Task.Delay(200, ct).ConfigureAwait(false);
            
            await ApplyShaderModAsync(extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await Task.Delay(200, ct).ConfigureAwait(false);
            
            await ApplyAtkModifierAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);

            // Write modified content back
            await File.WriteAllTextAsync(itemsGamePath, content, ct).ConfigureAwait(false);
            log("Modification completed.");
            return true;
        }

        /// <summary>
        /// Applies a mod by replacing the block with the given item ID using balanced brace matching.
        /// </summary>
        private async Task<string> ApplyBlockModAsync(string content, Dictionary<string, string> selections,
            string category, string itemId, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            ct.ThrowIfCancellationRequested();
            
            if (!selections.TryGetValue(category, out var selectedKey) || string.IsNullOrEmpty(selectedKey))
                return content;

            var rawUrl = ModConfigurationData.GetUrl(category, selectedKey);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
                return content;

            log($"Fetching {category}...");
            var replacementBlock = await GetStringWithRetryAsync(url, ct).ConfigureAwait(false);
            // reporting speed for strings is negligible, but we reset it
            speedProgress?.Report(ArdysaModsTools.Core.Models.SpeedMetrics.Empty);

            if (string.IsNullOrEmpty(replacementBlock))
            {
                log($"Warning: Failed to fetch {category}.");
                return content;
            }

            // Use balanced brace matching for reliable replacement
            content = KeyValuesBlockHelper.ReplaceIdBlock(content, itemId, replacementBlock, requireItemMarkers: false, out bool didReplace);
            
            if (didReplace)
            {
                log($"{category} applied.");
            }
            else
            {
                log($"Warning: {category} not found.");
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
            return content;
        }

        private async Task ApplyEmblemModAsync(string extractDir, Dictionary<string, string> selections,
            Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Emblems";
            
            // Cleanup previous emblem files
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selEmblem) || string.IsNullOrEmpty(selEmblem))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selEmblem);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
                return;

            string emblemDir = Path.Combine(extractDir, "particles", "ui_mouseactions");
            string relativePath = Path.Combine("particles", "ui_mouseactions", "selected_ring.vpcf_c");

            if (selEmblem == "Disable Emblem")
            {
                log("Disabling Emblems...");
                if (Directory.Exists(emblemDir))
                    Directory.Delete(emblemDir, true);
            }
            else
            {
                Directory.CreateDirectory(emblemDir);
                log("Fetching Emblems...");
                var data = await GetByteArrayWithProgressAsync(url, ct, speedProgress).ConfigureAwait(false);
                if (data != null)
                {
                    await File.WriteAllBytesAsync(Path.Combine(emblemDir, "selected_ring.vpcf_c"), data, ct).ConfigureAwait(false);
                    TrackInstalledFile(category, relativePath);
                    log("Emblem applied.");
                }
                else
                {
                    log("Warning: Failed to download Emblem.");
                }
            }
        }

        private async Task ApplyShaderModAsync(string extractDir, Dictionary<string, string> selections,
            Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Shader";
            
            // Cleanup previous shader files
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selShader) || string.IsNullOrEmpty(selShader))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selShader);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
                return;

            string shaderDir = Path.Combine(extractDir, "materials", "dev");
            string relativePath = Path.Combine("materials", "dev", "deferred_post_process.vmat_c");

            if (selShader == "Disable Shader")
            {
                log("Disabling Shaders...");
                if (Directory.Exists(shaderDir))
                    Directory.Delete(shaderDir, true);
            }
            else
            {
                Directory.CreateDirectory(shaderDir);
                log("Fetching Shader...");
                var data = await GetByteArrayWithProgressAsync(url, ct, speedProgress).ConfigureAwait(false);
                if (data != null)
                {
                    await File.WriteAllBytesAsync(Path.Combine(shaderDir, "deferred_post_process.vmat_c"), data, ct).ConfigureAwait(false);
                    TrackInstalledFile(category, relativePath);
                    log("Shader applied.");
                }
                else
                {
                    log("Warning: Failed to download Shader.");
                }
            }
        }

        private async Task ApplyRiverModAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "River";
            
            // Cleanup previous river files
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selRiver) || string.IsNullOrEmpty(selRiver))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selRiver);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
                return;

            if (selRiver == "Default Vial")
            {
                log("Disabling Vial...");
            }
            else
            {
                await DownloadAndExtractRarAsync(url, extractDir, category, "River Vial", log, ct, null, speedProgress).ConfigureAwait(false);
            }
        }

        private async Task ApplyAtkModifierAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "AtkModifier";
            
            // Cleanup previous attack modifier files
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selAtk) || string.IsNullOrEmpty(selAtk))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selAtk);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
                return;

            if (selAtk == "Disable Attack Modifier")
            {
                log("Disabling Attack Modifier...");
            }
            else
            {
                await DownloadAndExtractRarAsync(url, extractDir, category, "Attack Modifier", log, ct, null, speedProgress).ConfigureAwait(false);
            }
        }

        private async Task DownloadAndExtractRarAsync(string url, string extractDir, string category,
            string modName, Action<string> log, CancellationToken ct, string? password = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            log($"Fetching {modName}...");

            using var stream = await GetStreamWithProgressAsync(url, ct, speedProgress).ConfigureAwait(false);
            if (stream == null)
            {
                log($"Warning: {modName} not available (asset not found).");
                return;
            }

            using var memoryStream = new MemoryStream();
            using (var progressStream = new ProgressStream(stream, speedProgress))
            {
                await progressStream.CopyToAsync(memoryStream, 81920, ct).ConfigureAwait(false);
            }
            memoryStream.Position = 0;

            var extractedFiles = new List<string>();
            // Archives are now expected to be unprotected - no password required
            var readerOptions = new SharpCompress.Readers.ReaderOptions();
            
            using (var archive = ArchiveFactory.Open(memoryStream, readerOptions))
            {
                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (entry.IsDirectory) continue;

                    string relativePath = entry.Key ?? string.Empty;
                    string destPath = Path.Combine(extractDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                    extractedFiles.Add(relativePath);
                }
            }

            foreach (var file in extractedFiles)
            {
                TrackInstalledFile(category, file);
            }

            log($"{modName} applied.");
        }

        private void TrackInstalledFile(string category, string relativePath)
        {
            if (!_installedFiles.ContainsKey(category))
                _installedFiles[category] = new List<string>();
            
            _installedFiles[category].Add(relativePath);
        }

        private async Task CleanupCategoryFilesAsync(string category, string extractDir, CancellationToken ct)
        {
            if (_previousLog == null) return;

            var previousFiles = _previousLog.GetFiles(category);
            if (previousFiles.Count == 0) return;

            await Task.Run(() =>
            {
                foreach (var relativePath in previousFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var fullPath = Path.Combine(extractDir, relativePath);
                    try
                    {
                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }
                    catch { /* ignore */ }
                }
            }, ct).ConfigureAwait(false);
        }

        private async Task<string?> GetStringWithRetryAsync(string url, CancellationToken ct, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    return await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
                }
                catch when (i < retries - 1)
                {
                    await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                }
            }
            return null;
        }

        private async Task<byte[]?> GetByteArrayWithProgressAsync(string url, CancellationToken ct, IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    
                    using var contentStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var ms = new MemoryStream();
                    using var progressStream = new ProgressStream(contentStream, speedProgress);
                    
                    await progressStream.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
                    return ms.ToArray();
                }
                catch when (i < retries - 1)
                {
                    await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                }
            }
            return null;
        }

        private async Task<Stream?> GetStreamWithProgressAsync(string url, CancellationToken ct, IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    
                    // Handle 404 gracefully - don't retry for "not found"
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.Log($"Asset not found: {url}");
                        return null; // Don't retry for 404
                    }
                    
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (i < retries - 1)
                {
                    await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger?.Log($"Failed to download after {retries} attempts: {ex.Message}");
                    return null;
                }
            }
            return null;
        }
    }
}

