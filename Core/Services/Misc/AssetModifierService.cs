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
using System.Net.Sockets;
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
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Core.Services
{
    public interface IAssetModifier
    {
        Task<bool> ApplyModificationsAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);

        Dictionary<string, List<string>> GetInstalledFiles();
    }

    public sealed class AssetModifierService : IAssetModifier
    {
        private readonly HttpClient _httpClient;
        private readonly IAppLogger? _logger;

        private readonly Dictionary<string, List<string>> _installedFiles = new();

        private readonly List<string> _warnings = new();

        private MiscExtractionLog? _previousLog;

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

        public Dictionary<string, List<string>> GetInstalledFiles() => _installedFiles;

        public List<string> GetWarnings() => _warnings;

        public void SetPreviousLog(MiscExtractionLog? log) => _previousLog = log;

        public async Task<bool> ApplyModificationsAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, 
            CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            log("Applying modifications...");

            _installedFiles.Clear();
            _warnings.Clear();

            string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
            if (!File.Exists(itemsGamePath))
            {
                log("items_game.txt not found.");
                return false;
            }

            string content = await File.ReadAllTextAsync(itemsGamePath, ct).ConfigureAwait(false);

            if (KeyValuesBlockHelper.IsOneLinerFormat(content))
            {
                content = KeyValuesBlockHelper.PrettifyKvText(content);
            }

            foreach (var kvp in CategoryItemIds)
            {
                ct.ThrowIfCancellationRequested();
                content = await ApplyBlockModAsync(content, selections, kvp.Key, kvp.Value, log, ct, speedProgress).ConfigureAwait(false);
            }

            content = await ApplyCourierModAsync(content, vpkPath, extractDir, selections, log, ct).ConfigureAwait(false);
            content = await ApplyWardModAsync(content, vpkPath, extractDir, selections, log, ct).ConfigureAwait(false);
            await ApplyRiverModAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await ApplyEmblemModAsync(extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await ApplyShaderModAsync(extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await ApplyAtkModifierAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);
            await ApplyEffectModAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);

            await ApplySpecialModAsync(vpkPath, extractDir, selections, log, ct, speedProgress).ConfigureAwait(false);

            content = await ApplyZipModAsync(content, extractDir, selections, "mega_kills", "Mega-Kills", copyToRoot: true, mergeTxt: true, log, ct, speedProgress).ConfigureAwait(false);
            content = await ApplyZipModAsync(content, extractDir, selections, "announcer", "Announcer", copyToRoot: true, mergeTxt: true, log, ct, speedProgress).ConfigureAwait(false);
            content = await ApplyZipModAsync(content, extractDir, selections, "cursor", "Cursor", copyToRoot: false, mergeTxt: true, log, ct, speedProgress).ConfigureAwait(false);
            content = await ApplyZipModAsync(content, extractDir, selections, "ancient", "Ancient", copyToRoot: true, mergeTxt: false, log, ct, speedProgress).ConfigureAwait(false);
            content = await ApplyZipModAsync(content, extractDir, selections, "roshan", "Roshan", copyToRoot: true, mergeTxt: true, log, ct, speedProgress).ConfigureAwait(false);
            content = await ApplyZipModAsync(content, extractDir, selections, "kill_streak", "Kill Streak", copyToRoot: false, mergeTxt: true, log, ct, speedProgress).ConfigureAwait(false);

            await File.WriteAllTextAsync(itemsGamePath, content, ct).ConfigureAwait(false);
            log("Modification completed.");
            return true;
        }

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
            var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
            var replacementBlock = await TryWithFallbackAsync(primary,
                u => GetStringWithRetryAsync(u, ct), fallbacks).ConfigureAwait(false);
            speedProgress?.Report(ArdysaModsTools.Core.Models.SpeedMetrics.Empty);

            if (string.IsNullOrEmpty(replacementBlock))
            {
                var warning = $"{category}: Failed to download from all CDNs (asset may be unavailable).";
                log($"Warning: {warning}");
                _warnings.Add(warning);
                return content;
            }

            content = KeyValuesBlockHelper.ReplaceIdBlock(content, itemId, replacementBlock, out bool didReplace, requireItemMarkers: true);
            
            if (didReplace)
            {
                log($"{category} applied.");
            }
            else
            {
                var warning = $"{category}: Block ID '{itemId}' not found in items_game.txt.";
                log($"Warning: {warning}");
                _warnings.Add(warning);
            }

            return content;
        }

        private async Task<string> ApplyCourierModAsync(string content, string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct)
        {
            const string category = "Courier";

            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selCourier) || string.IsNullOrEmpty(selCourier))
                return content;

            if (selCourier == "Default Courier")
            {
                log("Restoring Default Courier...");

                if (selections.TryGetValue("CourierEthereal", out var defEthereal) && !string.IsNullOrEmpty(defEthereal))
                {
                    var effectNames = defEthereal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var effectPaths = new List<string>();
                    foreach (var name in effectNames)
                    {
                        var path = Core.Data.EtherealEffects.GetParticlePath(name);
                        if (path != null) effectPaths.Add(path);
                    }

                    if (effectPaths.Count > 0)
                    {
                        string? defBlock = KeyValuesBlockHelper.ExtractBlockById(content, CourierPatcherService.DefaultCourierItemId);
                        if (!string.IsNullOrEmpty(defBlock))
                        {
                            int existingParticles = CourierPatcherService.CountExistingParticles(defBlock);
                            string? visualsBlock = CourierPatcherService.ExtractVisualsBlock(defBlock);
                            if (visualsBlock != null)
                            {
                                string updatedVisuals = CourierPatcherService.AppendEtherealEffects(
                                    visualsBlock, effectPaths, existingParticles, replaceExisting: true);
                                string updatedBlock = defBlock.Replace(visualsBlock, updatedVisuals);

                                content = KeyValuesBlockHelper.ReplaceIdBlock(content,
                                    CourierPatcherService.DefaultCourierItemId, updatedBlock, out _, requireItemMarkers: true);
                                log($"Ethereal effects applied to Default Courier ({effectPaths.Count} effect{(effectPaths.Count > 1 ? "s" : "")}).");
                            }
                        }
                    }
                }

                return content;
            }

            var itemIdRaw = ModConfigurationData.GetUrl(category, selCourier);
            if (string.IsNullOrEmpty(itemIdRaw)) return content;

            string courierId = itemIdRaw;
            int? styleIndex = null;

            if (itemIdRaw.Contains(':'))
            {
                var parts = itemIdRaw.Split(':');
                courierId = parts[0];
                if (int.TryParse(parts[1], out int parsedStyle))
                    styleIndex = parsedStyle;
            }

            log($"Applying Courier: {selCourier}...");

            string? defaultBlock = KeyValuesBlockHelper.ExtractBlockById(content, CourierPatcherService.DefaultCourierItemId);
            if (string.IsNullOrEmpty(defaultBlock))
            {
                log("Warning: Default Courier block (595) not found in items_game.txt. Mod might not work.");
                return content;
            }

            string? selectedBlock = KeyValuesBlockHelper.ExtractBlockById(content, courierId);
            if (string.IsNullOrEmpty(selectedBlock))
            {
                log($"Warning: Selected Courier block ({courierId}) not found.");
                return content;
            }

            string mergedBlock = CourierPatcherService.BuildMergedCourierBlock(defaultBlock, selectedBlock, styleIndex);
            if (string.IsNullOrEmpty(mergedBlock))
            {
                log("Warning: Failed to build merged courier block.");
                return content;
            }

            if (selections.TryGetValue("CourierEthereal", out var etherealSelection) && !string.IsNullOrEmpty(etherealSelection))
            {
                var effectNames = etherealSelection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var effectPaths = new List<string>();

                foreach (var name in effectNames)
                {
                    var path = Core.Data.EtherealEffects.GetParticlePath(name);
                    if (path != null) effectPaths.Add(path);
                }

                if (effectPaths.Count > 0)
                {
                    int existingParticles = CourierPatcherService.CountExistingParticles(mergedBlock);

                    string? visualsBlock = CourierPatcherService.ExtractVisualsBlock(mergedBlock);
                    if (visualsBlock != null)
                    {
                        string updatedVisuals = CourierPatcherService.AppendEtherealEffects(
                            visualsBlock, effectPaths, existingParticles, replaceExisting: true);

                        mergedBlock = mergedBlock.Replace(visualsBlock, updatedVisuals);
                        log($"Ethereal effects applied ({effectPaths.Count} effect{(effectPaths.Count > 1 ? "s" : "")}).");
                    }
                }
            }

            content = KeyValuesBlockHelper.ReplaceIdBlock(content, CourierPatcherService.DefaultCourierItemId, mergedBlock, out bool replaced, requireItemMarkers: true);

            if (!replaced)
            {
                log("Warning: Failed to replace Default Courier block.");
                return content;
            }

            var models = CourierPatcherService.ParseCourierVisuals(selectedBlock, styleIndex);
            var vpkExtractPaths = CourierPatcherService.GetVpkExtractionPaths(models);
            var modelMappings = CourierPatcherService.GetModelMapping(models);

            if (vpkExtractPaths.Count > 0 && modelMappings.Count > 0)
            {
                string dotaRoot = PathUtility.NormalizeTargetPath(Path.GetDirectoryName(Path.GetDirectoryName(vpkPath)) ?? "");
                string gameVpkPath = Path.Combine(dotaRoot, "game", "dota", "pak01_dir.vpk");
                string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");

                if (File.Exists(gameVpkPath) && File.Exists(hlExtractPath))
                {
                    string tempModelDir = Path.Combine(extractDir, "_temp_couriers");
                    Directory.CreateDirectory(tempModelDir);

                    var uniqueDirs = vpkExtractPaths
                        .Select(p => p.Replace('\\', '/'))
                        .Select(p => p.Contains('/') ? p.Substring(0, p.LastIndexOf('/')) : p)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var dir in uniqueDirs)
                    {
                        string hlExtractArg = $"-p \"{gameVpkPath}\" -d \"{tempModelDir}\" -e \"root/{dir}\"";
                        _logger?.Log($"HLExtract courier: {hlExtractArg}");

                        var psi = new ProcessStartInfo
                        {
                            FileName = hlExtractPath,
                            Arguments = hlExtractArg,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        };

                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                            var stderrTask = proc.StandardError.ReadToEndAsync();
                            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                            string stderr = stderrTask.Result;
                            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                            if (proc.ExitCode != 0)
                            {
                                _logger?.Log($"HLExtract exited with code {proc.ExitCode} for dir: {dir}");
                                if (!string.IsNullOrWhiteSpace(stderr))
                                    _logger?.Log($"HLExtract stderr: {stderr.Trim()}");
                            }
                        }
                    }

                    LogExtractedFiles(tempModelDir);

                    string targetModelDir = Path.Combine(extractDir, "models", "props_gameplay");
                    Directory.CreateDirectory(targetModelDir);

                    int mappedCount = 0;
                    foreach (var mapping in modelMappings)
                    {
                        string? extractedFile = FindExtractedModel(tempModelDir, mapping.SourcePath);

                        if (extractedFile != null)
                        {
                            string targetFile = Path.Combine(targetModelDir, mapping.TargetFileName);
                            File.Copy(extractedFile, targetFile, true);
                            TrackInstalledFile(category, Path.Combine("models", "props_gameplay", mapping.TargetFileName).Replace('\\', '/'));
                            mappedCount++;
                        }
                        else
                        {
                            log($"Warning: Courier model not found: {Path.GetFileName(mapping.SourcePath)}");
                            _logger?.Log($"Missing courier model: {mapping.SourcePath} (searched in {tempModelDir})");
                        }
                    }

                    try { Directory.Delete(tempModelDir, true); } catch { }

                    if (mappedCount > 0)
                    {
                        log($"Courier models mapped ({mappedCount}/{modelMappings.Count}).");
                        _logger?.Log($"Courier models mapped: {mappedCount}/{modelMappings.Count}");
                    }
                    else
                    {
                        log("Warning: No courier models could be extracted from game VPK.");
                        _logger?.Log($"No courier models found. Searched paths: {string.Join(", ", vpkExtractPaths)}");
                    }
                }
                else
                {
                    if (!File.Exists(gameVpkPath))
                        log("Warning: Dota 2 game VPK (pak01_dir.vpk) not found.");
                    if (!File.Exists(hlExtractPath))
                        log("Warning: HLExtract.exe not found.");
                }
            }

            return content;
        }

        private async Task<string> ApplyWardModAsync(string content, string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct)
        {
            const string category = "Ward";

            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selWard) || string.IsNullOrEmpty(selWard))
                return content;

            if (selWard == "Default Ward")
            {
                log("Restoring Default Ward...");
                return content;
            }

            var itemIdRaw = ModConfigurationData.GetUrl(category, selWard);
            if (string.IsNullOrEmpty(itemIdRaw)) return content;

            string wardId = itemIdRaw;
            int? styleIndex = null;

            if (itemIdRaw.Contains(':'))
            {
                var parts = itemIdRaw.Split(':');
                wardId = parts[0];
                if (int.TryParse(parts[1], out int parsedStyle))
                    styleIndex = parsedStyle;
            }

            log($"Applying Ward: {selWard}...");

            string? defaultBlock = KeyValuesBlockHelper.ExtractBlockById(content, WardPatcherService.DefaultWardItemId);
            if (string.IsNullOrEmpty(defaultBlock))
            {
                log("Warning: Default Ward block (596) not found in items_game.txt. Mod might not work.");
                return content;
            }

            string? selectedBlock = KeyValuesBlockHelper.ExtractBlockById(content, wardId);
            if (string.IsNullOrEmpty(selectedBlock))
            {
                log($"Warning: Selected Ward block ({wardId}) not found.");
                return content;
            }

            string mergedBlock = WardPatcherService.BuildMergedWardBlock(defaultBlock, selectedBlock, styleIndex);
            if (string.IsNullOrEmpty(mergedBlock))
            {
                log("Warning: Failed to build merged ward block.");
                return content;
            }

            content = KeyValuesBlockHelper.ReplaceIdBlock(content, WardPatcherService.DefaultWardItemId, mergedBlock, out bool replaced, requireItemMarkers: true);

            if (!replaced)
            {
                log("Warning: Failed to replace Default Ward block.");
                return content;
            }

            var models = WardPatcherService.ParseWardVisuals(selectedBlock, styleIndex);
            var vpkExtractPaths = WardPatcherService.GetVpkExtractionPaths(models);
            var modelMappings = WardPatcherService.GetModelMapping(models);

            if (vpkExtractPaths.Count > 0 && modelMappings.Count > 0)
            {
                string dotaRoot = PathUtility.NormalizeTargetPath(Path.GetDirectoryName(Path.GetDirectoryName(vpkPath)) ?? "");
                string gameVpkPath = Path.Combine(dotaRoot, "game", "dota", "pak01_dir.vpk");
                string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");

                if (File.Exists(gameVpkPath) && File.Exists(hlExtractPath))
                {
                    string tempModelDir = Path.Combine(extractDir, "_temp_wards");
                    Directory.CreateDirectory(tempModelDir);

                    var uniqueDirs = vpkExtractPaths
                        .Select(p => p.Replace('\\', '/'))
                        .Select(p => p.Contains('/') ? p.Substring(0, p.LastIndexOf('/')) : p)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var dir in uniqueDirs)
                    {
                        string hlExtractArg = $"-p \"{gameVpkPath}\" -d \"{tempModelDir}\" -e \"root/{dir}\"";
                        _logger?.Log($"HLExtract ward: {hlExtractArg}");

                        var psi = new ProcessStartInfo
                        {
                            FileName = hlExtractPath,
                            Arguments = hlExtractArg,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        };

                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                            var stderrTask = proc.StandardError.ReadToEndAsync();
                            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                            string stderr = stderrTask.Result;
                            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                            if (proc.ExitCode != 0)
                            {
                                _logger?.Log($"HLExtract exited with code {proc.ExitCode} for dir: {dir}");
                                if (!string.IsNullOrWhiteSpace(stderr))
                                    _logger?.Log($"HLExtract stderr: {stderr.Trim()}");
                            }
                        }
                    }

                    LogExtractedFiles(tempModelDir);

                    string targetModelDir = Path.Combine(extractDir, "models", "props_gameplay");
                    Directory.CreateDirectory(targetModelDir);

                    int mappedCount = 0;
                    foreach (var mapping in modelMappings)
                    {
                        string? extractedFile = FindExtractedModel(tempModelDir, mapping.SourcePath);

                        if (extractedFile != null)
                        {
                            string targetFile = Path.Combine(targetModelDir, mapping.TargetFileName);
                            File.Copy(extractedFile, targetFile, true);
                            TrackInstalledFile(category, Path.Combine("models", "props_gameplay", mapping.TargetFileName).Replace('\\', '/'));
                            mappedCount++;
                        }
                        else
                        {
                            log($"Warning: Ward model not found: {Path.GetFileName(mapping.SourcePath)}");
                            _logger?.Log($"Missing ward model: {mapping.SourcePath} (searched in {tempModelDir})");
                        }
                    }

                    try { Directory.Delete(tempModelDir, true); } catch { }

                    if (mappedCount > 0)
                    {
                        log($"Ward models mapped ({mappedCount}/{modelMappings.Count}).");
                        _logger?.Log($"Ward models mapped: {mappedCount}/{modelMappings.Count}");
                    }
                    else
                    {
                        log("Warning: No ward models could be extracted from game VPK.");
                        _logger?.Log($"No ward models found. Searched paths: {string.Join(", ", vpkExtractPaths)}");
                    }
                }
                else
                {
                    if (!File.Exists(gameVpkPath))
                        log("Warning: Dota 2 game VPK (pak01_dir.vpk) not found.");
                    if (!File.Exists(hlExtractPath))
                        log("Warning: HLExtract.exe not found.");
                }
            }

            return content;
        }

        private async Task ApplyEmblemModAsync(string extractDir, Dictionary<string, string> selections,
            Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Emblems";
            
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
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                var data = await TryWithFallbackAsync(primary,
                    u => GetByteArrayWithProgressAsync(u, ct, speedProgress), fallbacks).ConfigureAwait(false);
                if (data != null)
                {
                    await File.WriteAllBytesAsync(Path.Combine(emblemDir, "selected_ring.vpcf_c"), data, ct).ConfigureAwait(false);
                    TrackInstalledFile(category, relativePath);
                    log("Emblem applied.");
                }
                else
                {
                    var warning = "Emblems: Download failed — asset not available from any CDN.";
                    log($"Warning: {warning}");
                    _warnings.Add(warning);
                }
            }
        }

        private async Task ApplyShaderModAsync(string extractDir, Dictionary<string, string> selections,
            Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Shader";
            
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
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                var data = await TryWithFallbackAsync(primary,
                    u => GetByteArrayWithProgressAsync(u, ct, speedProgress), fallbacks).ConfigureAwait(false);
                if (data != null)
                {
                    await File.WriteAllBytesAsync(Path.Combine(shaderDir, "deferred_post_process.vmat_c"), data, ct).ConfigureAwait(false);
                    TrackInstalledFile(category, relativePath);
                    log("Shader applied.");
                }
                else
                {
                    var warning = "Shader: Download failed — asset not available from any CDN.";
                    log($"Warning: {warning}");
                    _warnings.Add(warning);
                }
            }
        }

        private async Task ApplyRiverModAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "River";
            
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
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                await DownloadAndExtractRarAsync(primary, extractDir, category, "River Vial", log, ct, null, speedProgress, fallbacks).ConfigureAwait(false);
            }
        }

        private async Task ApplyAtkModifierAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "AtkModifier";
            
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
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                await DownloadAndExtractRarAsync(primary, extractDir, category, "Attack Modifier", log, ct, null, speedProgress, fallbacks).ConfigureAwait(false);
            }
        }

        private async Task ApplyEffectModAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Effect";
            
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selEffect) || string.IsNullOrEmpty(selEffect))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selEffect);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            
            if (string.IsNullOrEmpty(url))
                return;

            if (selEffect == "Disable Effect")
            {
                log("Disabling Effect...");
            }
            else
            {
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                await DownloadAndExtractRarAsync(primary, extractDir, category, "Battle Effect", log, ct, null, speedProgress, fallbacks).ConfigureAwait(false);
            }
        }

        private async Task ApplySpecialModAsync(
            string vpkPath, string extractDir, Dictionary<string, string> selections,
            Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            const string category = "Special";

            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selSpecial) || string.IsNullOrEmpty(selSpecial))
                return;

            var rawUrl = ModConfigurationData.GetUrl(category, selSpecial);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);

            if (string.IsNullOrEmpty(url))
                return;

            if (selSpecial == "Disable Special")
            {
                log("Disabling Special...");
            }
            else
            {
                var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);
                await DownloadAndExtractRarAsync(primary, extractDir, category, "Special Mod", log, ct, null, speedProgress, fallbacks).ConfigureAwait(false);
            }
        }

        private async Task<string> ApplyZipModAsync(string content, string extractDir,
            Dictionary<string, string> selections, string category, string modName,
            bool copyToRoot, bool mergeTxt, Action<string> log, CancellationToken ct,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            await CleanupCategoryFilesAsync(category, extractDir, ct).ConfigureAwait(false);

            if (!selections.TryGetValue(category, out var selection) || string.IsNullOrEmpty(selection))
                return content;

            var rawUrl = ModConfigurationData.GetUrl(category, selection);
            var url = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
            {
                log($"Restoring default {modName}...");
                return content;
            }

            log($"Fetching {modName}...");
            var (primary, fallbacks) = GetSmartCdnUrls(rawUrl);

            using var stream = await TryWithFallbackAsync(primary,
                u => GetStreamWithProgressAsync(u, ct, speedProgress), fallbacks).ConfigureAwait(false);
            if (stream == null)
            {
                var warning = $"{modName}: Download failed — asset not available from any CDN.";
                log($"Warning: {warning}");
                _warnings.Add(warning);
                return content;
            }

            using var memoryStream = new MemoryStream();
            using (var progressStream = new ProgressStream(stream, speedProgress))
                await progressStream.CopyToAsync(memoryStream, 81920, ct).ConfigureAwait(false);
            memoryStream.Position = 0;

            var txtBuffer = new System.Text.StringBuilder();
            int copied = 0;

            if (TryOpenArchive(memoryStream, out var archive))
            {
                using (archive)
                {
                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (entry.IsDirectory) continue;

                        string relativePath = entry.Key ?? string.Empty;
                        bool isTxt = relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

                        if (isTxt && mergeTxt)
                        {
                            using var es = entry.OpenEntryStream();
                            using var sr = new StreamReader(es);
                            txtBuffer.AppendLine(await sr.ReadToEndAsync().ConfigureAwait(false));
                        }
                        else if (copyToRoot)
                        {
                            string destPath = Path.Combine(extractDir, relativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                            entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                            TrackInstalledFile(category, relativePath);
                            copied++;
                        }
                    }
                }
            }
            else if (mergeTxt)
            {
                memoryStream.Position = 0;
                using var sr = new StreamReader(memoryStream);
                txtBuffer.AppendLine(await sr.ReadToEndAsync().ConfigureAwait(false));
            }
            else
            {
                var warning = $"{modName}: downloaded asset is not a valid archive.";
                log($"Warning: {warning}");
                _warnings.Add(warning);
                return content;
            }

            if (mergeTxt && txtBuffer.Length > 0)
                content = MergeBlocksIntoItemsGame(content, txtBuffer.ToString(), modName, log);

            if (copied > 0 || txtBuffer.Length > 0)
                log($"{modName} applied.");

            return content;
        }

        private string MergeBlocksIntoItemsGame(string content, string txt, string modName, Action<string> log)
        {
            var blocks = KeyValuesBlockHelper.ParseKvBlocks(txt);
            if (blocks.Count == 0)
            {
                var warning = $"{modName}: no item blocks found in package patch.";
                log($"Warning: {warning}");
                _warnings.Add(warning);
                return content;
            }

            foreach (var (id, authoredBlock) in blocks)
            {
                KeyValuesBlockHelper.TryGetTopLevelValue(authoredBlock, "prefab", out var prefab);
                content = KeyValuesBlockHelper.ReplaceIdBlock(content, id, authoredBlock, out bool didReplace, requireItemMarkers: true, requirePrefab: prefab);
                if (!didReplace)
                {
                    var warning = $"{modName}: block ID '{id}' not found in package.";
                    log($"Warning: {warning}");
                    _warnings.Add(warning);
                }
            }
            return content;
        }

        private static bool TryOpenArchive(MemoryStream ms, out IArchive archive)
        {
            long pos = ms.Position;
            try
            {
                archive = ArchiveFactory.OpenArchive(ms, new SharpCompress.Readers.ReaderOptions());
                return true;
            }
            catch
            {
                ms.Position = pos;
                archive = null!;
                return false;
            }
        }

        private async Task DownloadAndExtractRarAsync(string url, string extractDir, string category,
            string modName, Action<string> log, CancellationToken ct, string? password = null,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null,
            string[]? fallbackUrls = null)
        {
            log($"Fetching {modName}...");

            using var stream = await TryWithFallbackAsync(url,
                u => GetStreamWithProgressAsync(u, ct, speedProgress), fallbackUrls).ConfigureAwait(false);
            if (stream == null)
            {
                var warning = $"{modName}: Download failed — asset not available from any CDN.";
                log($"Warning: {warning}");
                _warnings.Add(warning);
                return;
            }

            using var memoryStream = new MemoryStream();
            using (var progressStream = new ProgressStream(stream, speedProgress))
            {
                await progressStream.CopyToAsync(memoryStream, 81920, ct).ConfigureAwait(false);
            }
            memoryStream.Position = 0;

            var extractedFiles = new List<string>();
            var readerOptions = new SharpCompress.Readers.ReaderOptions();
            
            using (var archive = ArchiveFactory.OpenArchive(memoryStream, readerOptions))
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

        private string? FindExtractedModel(string tempDir, string sourcePath)
        {
            string fileName = Path.GetFileName(sourcePath);
            string fileNameC = fileName + "_c";

            string[] candidates =
            {
                Path.Combine(tempDir, "root", sourcePath + "_c"),
                Path.Combine(tempDir, "root", sourcePath),
                Path.Combine(tempDir, sourcePath + "_c"),
                Path.Combine(tempDir, sourcePath),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(tempDir, fileNameC, SearchOption.AllDirectories))
                    return file;

                foreach (var file in Directory.EnumerateFiles(tempDir, fileName, SearchOption.AllDirectories))
                    return file;
            }
            catch (Exception ex)
            {
                _logger?.Log($"FindExtractedModel search error: {ex.Message}");
            }

            return null;
        }

        private void LogExtractedFiles(string tempDir)
        {
            try
            {
                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                _logger?.Log($"HLExtract temp dir ({tempDir}): {files.Length} files found");
                foreach (var file in files)
                {
                    _logger?.Log($"  Extracted: {Path.GetRelativePath(tempDir, file)}");
                }
            }
            catch { }
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
                    catch {  }
                }
            }, ct).ConfigureAwait(false);
        }

        private static (string primary, string[] fallbacks) GetSmartCdnUrls(string? rawUrl)
        {
            if (string.IsNullOrEmpty(rawUrl))
                return (rawUrl ?? string.Empty, Array.Empty<string>());

            var fastUrl = Config.EnvironmentConfig.ConvertToFastUrl(rawUrl);
            if (string.IsNullOrEmpty(fastUrl))
                return (rawUrl, Array.Empty<string>());

            var assetPath = Constants.CdnConfig.ExtractAssetPath(fastUrl);
            if (string.IsNullOrEmpty(assetPath))
            {
                var legacyFallbacks = Config.EnvironmentConfig.BuildFallbackUrls(fastUrl);
                return (fastUrl, legacyFallbacks);
            }

            var orderedBases = SmartCdnSelector.Instance.GetOrderedCdnUrls();
            var allUrls = orderedBases
                .Select(baseUrl => $"{baseUrl.TrimEnd('/')}/{assetPath}")
                .ToArray();

            if (allUrls.Length == 0)
            {
                var legacyFallbacks = Config.EnvironmentConfig.BuildFallbackUrls(fastUrl);
                return (fastUrl, legacyFallbacks);
            }

            return (allUrls[0], allUrls.Skip(1).ToArray());
        }
        private async Task<T?> TryWithFallbackAsync<T>(
            string primaryUrl, Func<string, Task<T?>> downloadFunc,
            string[]? fallbackUrls = null) where T : class
        {
            try
            {
                var result = await downloadFunc(primaryUrl).ConfigureAwait(false);
                if (result != null) return result;
                _logger?.Log($"Primary CDN returned null: {primaryUrl}");
            }
            catch (OperationCanceledException) when (!CancellationToken.None.IsCancellationRequested)
            {
                _logger?.Log($"Primary CDN timed out: {primaryUrl}");
            }
            catch (HttpRequestException ex)
            {
                _logger?.Log($"Primary CDN failed ({ex.StatusCode}): {primaryUrl} — {ex.Message}");
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
            {
                _logger?.Log($"Primary CDN connection error: {primaryUrl} — {ex.Message}");
            }

            if (fallbackUrls != null)
            {
                foreach (var fallback in fallbackUrls)
                {
                    try
                    {
                        _logger?.Log($"Trying fallback CDN: {fallback}");
                        var result = await downloadFunc(fallback).ConfigureAwait(false);
                        if (result != null) return result;
                        _logger?.Log($"Fallback CDN returned null: {fallback}");
                    }
                    catch (OperationCanceledException) when (!CancellationToken.None.IsCancellationRequested)
                    {
                        _logger?.Log($"Fallback CDN timed out: {fallback}");
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger?.Log($"Fallback CDN failed ({ex.StatusCode}): {fallback} — {ex.Message}");
                    }
                    catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
                    {
                        _logger?.Log($"Fallback CDN connection error: {fallback} — {ex.Message}");
                    }
                }
            }

            _logger?.Log($"All CDNs exhausted for: {primaryUrl}");
            return null;
        }

        private async Task<string?> GetStringWithRetryAsync(string url, CancellationToken ct, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.Log($"Asset not found (404): {url}");
                        return null;
                    }

                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (i < retries - 1)
                {
                    await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger?.Log($"Failed to download string after {retries} attempts: {ex.Message} — {url}");
                    return null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _logger?.Log($"Download error (attempt {i + 1}/{retries}): {ex.GetType().Name}: {ex.Message} — {url}");
                    if (i < retries - 1)
                        await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                    else
                        return null;
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

                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.Log($"Asset not found (404): {url}");
                        return null;
                    }

                    resp.EnsureSuccessStatusCode();
                    
                    using var contentStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var ms = new MemoryStream();
                    using var progressStream = new ProgressStream(contentStream, speedProgress);
                    
                    await progressStream.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
                    return ms.ToArray();
                }
                catch (HttpRequestException) when (i < retries - 1)
                {
                    await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger?.Log($"Failed to download bytes after {retries} attempts: {ex.Message} — {url}");
                    return null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _logger?.Log($"Download error (attempt {i + 1}/{retries}): {ex.GetType().Name}: {ex.Message} — {url}");
                    if (i < retries - 1)
                        await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                    else
                        return null;
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
                    
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.Log($"Asset not found: {url}");
                        return null;
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
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _logger?.Log($"Download error (attempt {i + 1}/{retries}): {ex.GetType().Name}: {ex.Message} — {url}");
                    if (i < retries - 1)
                        await Task.Delay(500 * (i + 1), ct).ConfigureAwait(false);
                    else
                        return null;
                }
            }
            return null;
        }
    }
}

