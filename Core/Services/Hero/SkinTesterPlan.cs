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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Core.Services
{
    public sealed class SkinTesterPlan
    {
        public List<(HeroModel Hero, string SetName)> Selections { get; } = new();
        public Dictionary<string, string> LocalZips { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Errors { get; } = new();
        public Dictionary<string, string> ErrorsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Warnings { get; } = new();
        public List<SkinTesterRow> Rows { get; } = new();

        private static readonly Regex UsedByHeroes = new("\"used_by_heroes\"\\s*\\{([^}]*)\\}", RegexOptions.Compiled);
        private static readonly Regex HeroUnit = new("\"(npc_dota_hero_[A-Za-z0-9_]+)\"", RegexOptions.Compiled);

        private void Fail(string zip, string message)
        {
            Errors.Add($"{Path.GetFileName(zip)}: {message}");
            ErrorsByPath[zip] = message;
        }

        public static SkinTesterPlan Build(IReadOnlyList<string> zipPaths, IReadOnlyList<HeroModel> heroes)
        {
            var plan = new SkinTesterPlan();
            var byId = heroes.GroupBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
                             .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var published = heroes.Distinct().ToDictionary(h => h, h => h.Sets.ToList());
            var claimed = new HashSet<HeroModel>();
            var indexes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var zip in zipPaths)
            {
                string stem = Path.GetFileNameWithoutExtension(zip);
                string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(zip))) ?? "";
                string assetPath = $"{CdnConfig.ModelsPath}/{folder}/{stem}.zip";

                if (!File.Exists(zip))
                {
                    plan.Fail(zip, "not found - moved or deleted since it was added.");
                    continue;
                }
                var index = ReadIndex(zip, assetPath, out var readError);
                if (readError != null)
                {
                    plan.Fail(zip, readError);
                    continue;
                }
                indexes[zip] = index;

                string marker = $"/{CdnConfig.ModelsPath}/{folder}/";
                bool PublishesHere(HeroModel h) => published[h].SelectMany(s => s.Value)
                    .Any(u => u.Contains(marker, StringComparison.OrdinalIgnoreCase));

                var candidates = OwnerCandidates(index);
                HeroModel? hero;
                if (candidates.Count == 1)
                {
                    if (!byId.TryGetValue(candidates[0], out hero))
                    {
                        plan.Fail(zip, $"{candidates[0]} is not in heroes.json.");
                        continue;
                    }
                }
                else if (candidates.Count > 1)
                {
                    var tied = candidates.Select(c => byId.GetValueOrDefault(c)).OfType<HeroModel>().Where(PublishesHere).ToList();
                    if (tied.Count != 1)
                    {
                        plan.Fail(zip, $"equally for {string.Join(", ", candidates)}, and its folder '{folder}' does not settle which.");
                        continue;
                    }
                    hero = tied[0];
                }
                else
                {
                    hero = heroes.FirstOrDefault(PublishesHere);
                    if (hero == null)
                    {
                        plan.Fail(zip, $"no used_by_heroes in its index.txt and no hero publishes sets from '{folder}'.");
                        continue;
                    }
                }

                string setName = published[hero]
                    .FirstOrDefault(s => s.Value.Any(u => string.Equals(CdnConfig.ExtractAssetPath(u), assetPath, StringComparison.OrdinalIgnoreCase)))
                    .Key ?? $"Test: {stem}";
                if (claimed.Contains(hero) && hero.Sets.ContainsKey(setName))
                {
                    plan.Fail(zip, $"{hero.Id} already has a zip named {stem} in this run.");
                    continue;
                }
                if (claimed.Add(hero))
                    hero.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                string url = $"{CdnConfig.R2BaseUrl}/{assetPath}";
                hero.Sets[setName] = new List<string> { url };
                hero.SetNames[stem] = $"{stem} (test)";
                plan.LocalZips[url] = zip;
                plan.Selections.Add((hero, setName));
                plan.Rows.Add(new SkinTesterRow(zip, Path.GetFileName(zip), hero.Id, hero.DisplayName,
                    HeroModelMapper.ClassifySet(hero.Sets[setName]).ToString()));
            }

            plan.ResolvePriority(indexes);
            return plan;
        }

        private void ResolvePriority(IReadOnlyDictionary<string, string?> indexes)
        {
            var anchorFree = new[]
            {
                HeroModelMapper.SkinCategory.Item, HeroModelMapper.SkinCategory.CustomSet,
                HeroModelMapper.SkinCategory.LegacySet, HeroModelMapper.SkinCategory.Persona,
            };
            var entries = Selections.Select((s, i) => (s.Hero, s.SetName, Row: i,
                Category: HeroModelMapper.ClassifySet(s.Hero.Sets, s.SetName))).ToList();

            foreach (var group in entries.GroupBy(e => e.Hero))
            {
                var hero = group.Key;
                var policy = hero.BasePriority ?? new BasePriorityPolicy();
                var baseEntry = group.FirstOrDefault(e => e.Category == HeroModelMapper.SkinCategory.BaseHero);
                bool detected = baseEntry.Hero != null
                    && indexes.GetValueOrDefault(Rows[baseEntry.Row].Path) is string baseIndex
                    && KeyValuesBlockHelper.AnyBlockHasItemSlot(baseIndex, "hero_base");

                var ordered = group.OrderByDescending(e => HeroGenerationService.LayerWeight(policy, e.Category, e.SetName, detected)).ToList();
                var verdicts = group.Where(e => anchorFree.Contains(e.Category))
                    .Select(e => policy.BaseWins(e.SetName, null, detected)).DefaultIfEmpty(policy.BaseWins(null, null, detected))
                    .Distinct().ToList();
                string mode = verdicts.Count > 1 ? "mixed" : verdicts[0] ? "base wins" : "sets win";
                for (int i = 0; i < ordered.Count; i++)
                {
                    var e = ordered[i];
                    Rows[e.Row] = Rows[e.Row] with
                    {
                        Order = i + 1,
                        Top = i == ordered.Count - 1,
                        BaseWins = policy.BaseWins(e.SetName, null, detected),
                        Mode = mode,
                    };
                }

                int bases = group.Count(e => e.Category == HeroModelMapper.SkinCategory.BaseHero);
                int personas = group.Count(e => e.Category == HeroModelMapper.SkinCategory.Persona);
                if (bases > 1)
                    Warnings.Add($"{hero.DisplayName}: {bases} Base zips - the Skin Selector allows one, and hero_base is read from the first only.");
                if (personas > 1)
                    Warnings.Add($"{hero.DisplayName}: {personas} Persona zips - the Skin Selector allows one.");
                var droppedByPersona = group.Where(e => e.Category is HeroModelMapper.SkinCategory.Item or HeroModelMapper.SkinCategory.BaseHero
                    or HeroModelMapper.SkinCategory.Prismatic or HeroModelMapper.SkinCategory.AbilityEffect).ToList();
                if (personas > 0 && droppedByPersona.Count > 0)
                    Warnings.Add($"{hero.DisplayName}: Persona with {droppedByPersona.Count} item/base/prismatic/effect zip(s) - the Skin Selector drops those beside a Persona; this build applies them all.");
            }
        }

        public static async Task<SkinTesterPlan> FromFilesAsync(
            IReadOnlyList<string> zipPaths, string heroesJsonPath, CancellationToken ct = default)
        {
            List<HeroModel> heroes;
            try
            {
                if (!File.Exists(heroesJsonPath))
                    return WithError($"heroes.json not found: {heroesJsonPath}");
                string raw = await File.ReadAllTextAsync(heroesJsonPath, ct).ConfigureAwait(false);
                heroes = HeroModelMapper.MapFromSummaries(HeroService.ParseHeroesJson(raw));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return WithError($"heroes.json unreadable: {ex.Message}");
            }
            return Build(zipPaths, heroes);

            static SkinTesterPlan WithError(string message)
            {
                var failed = new SkinTesterPlan();
                failed.Errors.Add(message);
                return failed;
            }
        }

        public IHeroGenerationService CreateGenerationService(IAppLogger? logger = null)
            => new HeroGenerationService(downloader: new LocalZipSetDownloader(LocalZips),
                                         indexProvider: new ZipIndexOnly(), logger: logger);

        private sealed class ZipIndexOnly : IHeroIndexProvider
        {
            public Task<string?> GetIndexTextAsync(string zipUrl, Action<string> log, CancellationToken ct = default)
                => Task.FromResult<string?>(null);
        }

        private static string? ReadIndex(string zip, string assetPath, out string? error)
        {
            error = null;
            byte[]? plaintext = null;
            try
            {
                Stream source;
                if (AssetCipher.IsEncrypted(zip))
                {
                    try { plaintext = AssetCipher.Decrypt(File.ReadAllBytes(zip), assetPath); }
                    catch (CryptographicException)
                    {
                        error = $"cannot be decrypted as {assetPath} - an encrypted zip must keep its original name and ModsPack folder.";
                        return null;
                    }
                    source = new MemoryStream(plaintext, writable: false);
                }
                else
                {
                    source = new FileStream(zip, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                using var owned = source;
                using var archive = new ZipArchive(source, ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("index.txt", StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            }
            catch (InvalidDataException)
            {
                error = "not a readable zip.";
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"cannot read: {ex.Message}";
                return null;
            }
            finally
            {
                if (plaintext != null) Array.Clear(plaintext);
            }
        }

        private static List<string> OwnerCandidates(string? index)
        {
            if (index == null) return new List<string>();
            var votes = UsedByHeroes.Matches(index)
                .SelectMany(block => HeroUnit.Matches(block.Groups[1].Value)
                    .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct())
                .GroupBy(u => u)
                .ToDictionary(g => g.Key, g => g.Count());
            if (votes.Count == 0) return new List<string>();
            int top = votes.Values.Max();
            return votes.Where(kv => kv.Value == top).Select(kv => kv.Key).OrderBy(u => u, StringComparer.Ordinal).ToList();
        }
    }

    public sealed record SkinTesterRow(string Path, string File, string HeroId, string HeroName, string Layer)
    {
        public int Order { get; init; }
        public bool Top { get; init; }
        public bool BaseWins { get; init; }
        public string Mode { get; init; } = "";
    }
}
