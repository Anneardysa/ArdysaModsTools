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
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    public sealed class HeroDatabaseService : IHeroDatabaseService
    {
        private readonly string _bundledHeroesPath;
        private readonly IAppLogger? _logger;

        public HeroDatabaseService(string? baseFolder = null, IAppLogger? logger = null)
        {
            var folder = string.IsNullOrWhiteSpace(baseFolder) ? AppContext.BaseDirectory : baseFolder!;
            _bundledHeroesPath = Path.Combine(folder, HeroService.HeroesManifestName);
            _logger = logger;
        }

        private static string HeroesUrl => EnvironmentConfig.BuildFreshUrl("Assets/heroes.json");

        public async Task<HeroDatabaseStatus> GetStatusAsync(CancellationToken ct = default)
        {
            var meta = ManifestCache.ReadMeta(HeroService.HeroesManifestName);
            if (meta != null)
            {
                return new HeroDatabaseStatus
                {
                    Source = string.IsNullOrEmpty(meta.Source) ? "cdn" : meta.Source,
                    SetCount = meta.ItemCount,
                    UpdatedUtc = meta.FetchedAtUtc,
                    Sha256 = meta.Sha256
                };
            }

            if (File.Exists(_bundledHeroesPath))
            {
                try
                {
                    var raw = await File.ReadAllTextAsync(_bundledHeroesPath, Encoding.UTF8, ct).ConfigureAwait(false);
                    return new HeroDatabaseStatus
                    {
                        Source = "bundled",
                        SetCount = CountSets(raw),
                        UpdatedUtc = File.GetLastWriteTimeUtc(_bundledHeroesPath),
                        Sha256 = null
                    };
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"[HeroDatabaseService] Bundled heroes.json read failed: {ex.Message}");
                }
            }

            return new HeroDatabaseStatus { Source = "none", SetCount = 0 };
        }

        public async Task<HeroDatabaseCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
        {
            try
            {
                var remote = await FetchRemoteAsync(ct).ConfigureAwait(false);
                if (remote == null)
                    return new HeroDatabaseCheckResult { Success = false, Message = "Couldn't reach the update server." };

                var local = await GetStatusAsync(ct).ConfigureAwait(false);

                string? localSha = await GetLocalShaAsync(ct).ConfigureAwait(false);
                bool upToDate = !string.IsNullOrEmpty(localSha) &&
                    string.Equals(localSha, remote.Value.Sha, StringComparison.OrdinalIgnoreCase);

                return new HeroDatabaseCheckResult
                {
                    Success = true,
                    UpToDate = upToDate,
                    LocalSetCount = local.SetCount,
                    RemoteSetCount = remote.Value.SetCount,
                    Message = upToDate
                        ? $"Database is up to date — {remote.Value.SetCount} sets."
                        : BuildDeltaMessage("Update available", local.SetCount, remote.Value.SetCount)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[HeroDatabaseService] Check failed: {ex.Message}", ex);
                return new HeroDatabaseCheckResult { Success = false, Message = "Update check failed." };
            }
        }

        public async Task<HeroDatabaseUpdateResult> UpdateAsync(CancellationToken ct = default)
        {
            try
            {
                var remote = await FetchRemoteAsync(ct).ConfigureAwait(false);
                if (remote == null)
                    return new HeroDatabaseUpdateResult { Success = false, Message = "Couldn't reach the update server." };

                var before = await GetStatusAsync(ct).ConfigureAwait(false);
                var meta = ManifestCache.ReadMeta(HeroService.HeroesManifestName);
                string? localSha = await GetLocalShaAsync(ct).ConfigureAwait(false);

                bool contentDiffers = string.IsNullOrEmpty(localSha) ||
                    !string.Equals(localSha, remote.Value.Sha, StringComparison.OrdinalIgnoreCase);
                bool needPersist = meta == null || contentDiffers;

                if (needPersist)
                {
                    var newMeta = new ManifestMeta
                    {
                        Sha256 = remote.Value.Sha,
                        ETag = remote.Value.ETag,
                        LastModified = remote.Value.LastModified,
                        FetchedAtUtc = DateTime.UtcNow,
                        ItemCount = remote.Value.SetCount,
                        Source = "manual"
                    };
                    await ManifestCache.WriteAsync(HeroService.HeroesManifestName, remote.Value.Json, newMeta, ct)
                        .ConfigureAwait(false);
                    _logger?.Log($"[HeroDatabaseService] Hero database persisted at {remote.Value.SetCount} sets (changed={contentDiffers}).");
                }

                return new HeroDatabaseUpdateResult
                {
                    Success = true,
                    Changed = contentDiffers,
                    SetCount = remote.Value.SetCount,
                    Message = contentDiffers
                        ? BuildDeltaMessage("Database updated", before.SetCount, remote.Value.SetCount)
                        : $"Already up to date — {remote.Value.SetCount} sets."
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[HeroDatabaseService] Update failed: {ex.Message}", ex);
                return new HeroDatabaseUpdateResult { Success = false, Message = "Update failed." };
            }
        }

        private async Task<(string Json, string Sha, string? ETag, string? LastModified, int SetCount)?> FetchRemoteAsync(CancellationToken ct)
        {
            var result = await CdnFallbackService.Instance.DownloadWithFallbackAsync(HeroesUrl, ct).ConfigureAwait(false);
            if (!result.Success || result.Data == null)
                return null;

            var bytes = result.Data;
            string json = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
                : Encoding.UTF8.GetString(bytes);

            json = ManifestCache.NormalizeJson(json);
            return (json, ManifestCache.ComputeSha256(json), result.ETag, result.LastModified, CountSets(json));
        }

        private async Task<string?> GetLocalShaAsync(CancellationToken ct)
        {
            var meta = ManifestCache.ReadMeta(HeroService.HeroesManifestName);
            if (meta != null && !string.IsNullOrEmpty(meta.Sha256))
                return meta.Sha256;

            if (File.Exists(_bundledHeroesPath))
            {
                try
                {
                    var raw = await File.ReadAllTextAsync(_bundledHeroesPath, Encoding.UTF8, ct).ConfigureAwait(false);
                    return ManifestCache.ComputeSha256(ManifestCache.NormalizeJson(raw));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"[HeroDatabaseService] Bundled heroes.json hash failed: {ex.Message}");
                }
            }
            return null;
        }

        private static string BuildDeltaMessage(string prefix, int localCount, int remoteCount)
        {
            int delta = remoteCount - localCount;
            if (localCount <= 0)
                return $"{prefix} — {remoteCount} sets.";
            if (delta > 0)
                return $"{prefix} — +{delta} sets ({localCount} → {remoteCount}).";
            if (delta < 0)
                return $"{prefix} — {-delta} sets removed ({localCount} → {remoteCount}).";
            return $"{prefix} — content refreshed ({remoteCount} sets).";
        }

        private static int CountSets(string rawJson)
        {
            try
            {
                var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                using var doc = JsonDocument.Parse(ManifestCache.NormalizeJson(rawJson), options);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return 0;

                int count = 0;
                foreach (var hero in doc.RootElement.EnumerateArray())
                {
                    if (hero.TryGetProperty("sets", out var sets) && sets.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var set in sets.EnumerateObject())
                        {
                            if (!string.Equals(set.Name, "Default Set", StringComparison.OrdinalIgnoreCase))
                                count++;
                        }
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
