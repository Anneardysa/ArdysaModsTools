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
    /// <summary>
    /// Manual hero-database maintenance backing Settings → Hero Database. Reports the local copy's
    /// status, compares it to the live remote copy by SHA-256, and force-updates the persisted
    /// last-known-good copy used by <see cref="HeroService"/> on impaired launches.
    /// </summary>
    /// <remarks>
    /// [AMT:OPUS] SHA-256 identity check decides whether the local hero database is the latest and
    /// whether to overwrite the persisted copy. A wrong comparison would either suppress a needed
    /// update or churn the cache — keep the hashing/normalization in step with <see cref="ManifestCache"/>.
    /// </remarks>
    public sealed class HeroDatabaseService : IHeroDatabaseService
    {
        private readonly string _bundledHeroesPath;
        private readonly IAppLogger? _logger;

        /// <param name="baseFolder">Folder holding the bundled heroes.json (defaults to the app base dir).</param>
        /// <param name="logger">Optional logger; no-op when omitted.</param>
        public HeroDatabaseService(string? baseFolder = null, IAppLogger? logger = null)
        {
            var folder = string.IsNullOrWhiteSpace(baseFolder) ? AppContext.BaseDirectory : baseFolder!;
            _bundledHeroesPath = Path.Combine(folder, HeroService.HeroesManifestName);
            _logger = logger;
        }

        private static string HeroesUrl => EnvironmentConfig.BuildFreshUrl("Assets/heroes.json");

        /// <inheritdoc />
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

            // No persisted copy yet → report the bundled snapshot shipped with the app.
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

        /// <inheritdoc />
        public async Task<HeroDatabaseCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
        {
            try
            {
                var remote = await FetchRemoteAsync(ct).ConfigureAwait(false);
                if (remote == null)
                    return new HeroDatabaseCheckResult { Success = false, Message = "Couldn't reach the update server." };

                var meta = ManifestCache.ReadMeta(HeroService.HeroesManifestName);
                bool upToDate = meta != null &&
                    string.Equals(meta.Sha256, remote.Value.Sha, StringComparison.OrdinalIgnoreCase);

                return new HeroDatabaseCheckResult
                {
                    Success = true,
                    UpToDate = upToDate,
                    RemoteSetCount = remote.Value.SetCount,
                    Message = upToDate
                        ? $"Database is up to date — {remote.Value.SetCount} sets."
                        : "An update is available."
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[HeroDatabaseService] Check failed: {ex.Message}", ex);
                return new HeroDatabaseCheckResult { Success = false, Message = "Update check failed." };
            }
        }

        /// <inheritdoc />
        public async Task<HeroDatabaseUpdateResult> UpdateAsync(CancellationToken ct = default)
        {
            try
            {
                var remote = await FetchRemoteAsync(ct).ConfigureAwait(false);
                if (remote == null)
                    return new HeroDatabaseUpdateResult { Success = false, Message = "Couldn't reach the update server." };

                var meta = ManifestCache.ReadMeta(HeroService.HeroesManifestName);
                bool changed = meta == null ||
                    !string.Equals(meta.Sha256, remote.Value.Sha, StringComparison.OrdinalIgnoreCase);

                if (changed)
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
                    _logger?.Log($"[HeroDatabaseService] Hero database updated to {remote.Value.SetCount} sets.");
                }

                return new HeroDatabaseUpdateResult
                {
                    Success = true,
                    Changed = changed,
                    SetCount = remote.Value.SetCount,
                    Message = changed
                        ? $"Database updated — {remote.Value.SetCount} sets."
                        : "Already up to date."
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[HeroDatabaseService] Update failed: {ex.Message}", ex);
                return new HeroDatabaseUpdateResult { Success = false, Message = "Update failed." };
            }
        }

        /// <summary>
        /// Download the live heroes.json through the CDN fallback chain and derive its normalized JSON,
        /// SHA-256, freshness headers and set count. Returns null when no CDN is reachable.
        /// </summary>
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

        /// <summary>Count non-default cosmetic sets in a heroes.json document (resilient to malformed input).</summary>
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
