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
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Update
{
    public sealed class DeltaUpdateService
    {
        public const string UpdaterRelPath = "tools/updater/AMT.Updater.exe";

        public const string InProgressMarker = ".amt-update-in-progress";

        public const string BackupExtension = ".amtbak";

        public const string IncomingExtension = ".amtnew";

        public const string StagedOkMarker = ".staged-ok";

        public const string ApplyPlanFile = "apply.json";

        private const string ManifestFileName = "files.json";

        private const int ManifestTimeoutSeconds = 15;

        private static readonly JsonSerializerOptions PlanJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly Logger _logger;
        private readonly string _installDir;
        private readonly string _stagingRoot;

        public DeltaUpdateService(Logger logger, string? installDir = null, string? stagingRoot = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _installDir = (installDir ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
            _stagingRoot = stagingRoot ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools", "update");
        }

        #region Prepare

        public async Task<DeltaPlan?> PrepareAsync(UpdateInfo info, CancellationToken ct = default)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.FilesManifestUrl) || string.IsNullOrWhiteSpace(info.Version))
                return null;

            try
            {
                var manifest = await FetchManifestAsync(info.FilesManifestUrl!, ct).ConfigureAwait(false);
                if (manifest == null || manifest.Count == 0)
                {
                    _logger.Log("Incremental update unavailable for this release (no file manifest).");
                    return null;
                }

                if (!manifest.ContainsKey(UpdaterRelPath))
                {
                    _logger.Log("Incremental update unavailable for this release (no update applier published).");
                    return null;
                }

                var oldManifest = await FetchManifestAsync(
                    SiblingManifestUrl(info.FilesManifestUrl!, info.CurrentVersion), ct).ConfigureAwait(false);

                var plan = await BuildPlanAsync(
                    manifest, oldManifest, info.Version, _installDir,
                    Path.Combine(_stagingRoot, info.Version),
                    FilesBaseUrl(info.FilesManifestUrl!), ct).ConfigureAwait(false);

                if (plan.Files.Count <= 1 && plan.Deletions.Count == 0)
                {
                    _logger.Log("Incremental update: local files already match the new release.");
                    return null;
                }

                _logger.Log($"Incremental update ready: {plan.Files.Count} file(s), {FormatBytes(plan.TotalDownloadBytes)} to download.");
                return plan;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not prepare incremental update: {ex.Message}");
                return null;
            }
        }

        public static async Task<DeltaPlan> BuildPlanAsync(
            IReadOnlyDictionary<string, AssetHashEntry> manifest,
            IReadOnlyDictionary<string, AssetHashEntry>? oldManifest,
            string version,
            string installDir,
            string stagingDir,
            string filesBaseUrl,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            var changed = new List<DeltaFile>();

            foreach (var (relPath, expected) in manifest)
            {
                ct.ThrowIfCancellationRequested();

                string localPath = Path.Combine(installDir, relPath.Replace('/', Path.DirectorySeparatorChar));

                bool needed = relPath.Equals(UpdaterRelPath, StringComparison.OrdinalIgnoreCase)
                    || !await AssetHashVerifier.VerifyFileAsync(localPath, expected, ct).ConfigureAwait(false);

                if (needed)
                    changed.Add(new DeltaFile(relPath, expected.Sha256, expected.Size));
            }

            var deletions = oldManifest == null
                ? Array.Empty<string>()
                : oldManifest.Keys
                    .Where(k => !manifest.ContainsKey(k))
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            return new DeltaPlan
            {
                Version = version,
                TargetDir = installDir,
                StagingDir = stagingDir,
                FilesBaseUrl = filesBaseUrl,
                Files = changed.OrderBy(f => f.RelPath, StringComparer.OrdinalIgnoreCase).ToArray(),
                Deletions = deletions
            };
        }

        internal static string FilesBaseUrl(string manifestUrl) =>
            manifestUrl[..(manifestUrl.LastIndexOf('/') + 1)] + "files/";

        internal static string SiblingManifestUrl(string manifestUrl, string version)
        {
            string releaseDir = manifestUrl[..manifestUrl.LastIndexOf('/')];
            string releasesRoot = releaseDir[..(releaseDir.LastIndexOf('/') + 1)];
            return $"{releasesRoot}{version}/{ManifestFileName}";
        }

        #endregion

        #region Stage

        public async Task StageAsync(
            DeltaPlan plan,
            Action<string>? log = null,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plan);

            CleanStagingRoot(plan.StagingDir);
            Directory.CreateDirectory(plan.StagingDir);

            long total = Math.Max(1, plan.TotalDownloadBytes);
            long done = 0;
            int index = 0;

            foreach (var file in plan.Files)
            {
                ct.ThrowIfCancellationRequested();
                index++;

                string destPath = Path.Combine(
                    plan.StagingDir, "files", file.RelPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                string url = plan.FilesBaseUrl + EncodePath(file.RelPath);
                log?.Invoke($"[{index}/{plan.Files.Count}] {file.RelPath}");

                long fileStart = done;
                var fileProgress = new Progress<int>(percent =>
                    progress?.Report((int)Math.Min(100, (fileStart + file.Size * percent / 100.0) * 100 / total)));

                await ResumableDownloadService.Instance.DownloadAsync(
                    new[] { url },
                    destPath,
                    log: null,
                    progress: fileProgress,
                    speedProgress: null,
                    ct: ct,
                    expected: new AssetHashEntry { Sha256 = file.Sha256, Size = file.Size },
                    reportCdnHealth: false)
                    .ConfigureAwait(false);

                done += file.Size;
                progress?.Report((int)Math.Min(100, done * 100 / total));
            }

            await File.WriteAllTextAsync(
                Path.Combine(plan.StagingDir, ApplyPlanFile),
                JsonSerializer.Serialize(plan, PlanJsonOptions),
                Encoding.UTF8, ct).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(plan.StagingDir, StagedOkMarker), plan.Version, Encoding.UTF8, ct).ConfigureAwait(false);

            _logger.Log($"Update staged ({FormatBytes(plan.TotalDownloadBytes)}).");
        }

        #endregion

        #region Apply

        public async Task<bool> LaunchApplierAsync(DeltaPlan plan, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plan);

            string applier = Path.Combine(
                plan.StagingDir, "files", UpdaterRelPath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(applier))
            {
                _logger.Log("Update applier missing from the staged files — cannot apply.");
                return false;
            }

            var expected = plan.Files.FirstOrDefault(
                f => f.RelPath.Equals(UpdaterRelPath, StringComparison.OrdinalIgnoreCase));

            if (expected == null ||
                !await AssetHashVerifier.VerifyFileAsync(
                    applier, new AssetHashEntry { Sha256 = expected.Sha256, Size = expected.Size }, ct)
                    .ConfigureAwait(false))
            {
                _logger.Log("Update applier failed its integrity check — refusing to run it.");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = applier,
                Arguments = $"--staging \"{plan.StagingDir}\" --pid {Environment.ProcessId}",
                UseShellExecute = true,
                WorkingDirectory = plan.StagingDir
            };

            if (!AdminHelper.IsRunningAsAdmin() && AdminHelper.IsInProtectedPath())
                psi.Verb = "runas";

            try
            {
                Process.Start(psi);
                _logger.Log("Applying update — the app will restart.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not start the update applier: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Recovery

        public void RepairInterruptedUpdate()
        {
            try
            {
                string marker = Path.Combine(_installDir, InProgressMarker);
                bool interrupted = File.Exists(marker);

                var backups = Enumerate(BackupExtension);
                var incoming = Enumerate(IncomingExtension);

                if (!interrupted && backups.Count == 0 && incoming.Count == 0)
                    return;

                if (interrupted)
                    _logger.Log("Warning: a previous update did not finish — repairing the installation...");

                int restored = 0, cleaned = 0;

                foreach (var backup in backups)
                {
                    string target = backup[..^BackupExtension.Length];
                    try
                    {
                        if (interrupted && !File.Exists(target))
                        {
                            File.Move(backup, target);
                            restored++;
                        }
                        else
                        {
                            File.Delete(backup);
                            cleaned++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Cleanup could not handle {Path.GetFileName(backup)}: {ex.Message}");
                    }
                }

                foreach (var partial in incoming)
                {
                    try { File.Delete(partial); } catch {  }
                }

                if (interrupted)
                {
                    File.Delete(marker);
                    _logger.Log($"Installation repaired ({restored} file(s) restored, {cleaned} leftover(s) removed). Re-run the update to finish.");
                }
                else if (cleaned > 0 || incoming.Count > 0)
                {
                    _logger.Log($"Removed {cleaned + incoming.Count} leftover file(s) from the last update.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Cleanup failed: {ex.Message}. Reinstall from the website if the app misbehaves.");
            }
        }

        private List<string> Enumerate(string extension)
        {
            try
            {
                return Directory.EnumerateFiles(_installDir, "*" + extension, SearchOption.AllDirectories).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        #endregion

        #region Helpers

        private async Task<Dictionary<string, AssetHashEntry>?> FetchManifestAsync(string url, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(ManifestTimeoutSeconds));

            try
            {
                using var response = await HttpClientProvider.Client.GetAsync(url, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                        _logger.Log($"File manifest unavailable (HTTP {(int)response.StatusCode}): {url}");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var parsed = AssetHashManifestService.ParseManifest(json);
                if (parsed == null)
                    _logger.Log($"File manifest could not be parsed: {url}");

                return parsed;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.Log("File manifest fetch timed out — offering the full download instead.");
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Log($"File manifest fetch failed: {ex.Message}");
                return null;
            }
        }

        private void CleanStagingRoot(string currentStagingDir)
        {
            try
            {
                if (Directory.Exists(_stagingRoot))
                    Directory.Delete(_stagingRoot, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not clear the update staging folder: {ex.Message}");
                try { if (Directory.Exists(currentStagingDir)) Directory.Delete(currentStagingDir, recursive: true); } catch { }
            }
        }

        private static string EncodePath(string relPath) =>
            string.Join("/", relPath.Split('/').Select(Uri.EscapeDataString));

        private static string FormatBytes(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{bytes / 1024.0:F0} KB";

        #endregion
    }
}
