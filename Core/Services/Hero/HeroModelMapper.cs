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
using System.Globalization;
using System.IO;
using System.Linq;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Services;

public static class HeroModelMapper
{
    public static List<HeroModel> MapFromSummaries(List<HeroSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            return new List<HeroModel>();

        var result = new List<HeroModel>(summaries.Count);

        foreach (var hs in summaries)
        {
            var internalId = !string.IsNullOrWhiteSpace(hs.UsedByHeroes)
                ? hs.UsedByHeroes
                : hs.Name ?? "";

            var friendlyName = hs.Name ?? internalId;

            var hm = new HeroModel
            {
                HeroId = internalId,
                Name = internalId,
                LocalizedName = friendlyName,
                PrimaryAttribute = !string.IsNullOrWhiteSpace(hs.PrimaryAttr)
                    ? hs.PrimaryAttr.ToLowerInvariant()
                    : "universal",
                Method = hs.Method
            };

            if (hs.Sets != null && hs.Sets.Count > 0)
            {
                hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in hs.Sets)
                {
                    hm.Sets[kvp.Key] = kvp.Value?.ToList() ?? new List<string>();
                }
            }

            if (hs.SetStyles != null && hs.SetStyles.Count > 0)
            {
                hm.SetStyles = new Dictionary<string, SetStyleInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in hs.SetStyles)
                {
                    if (kvp.Value != null)
                        hm.SetStyles[kvp.Key] = kvp.Value;
                }
            }

            if (hs.Ids != null && hs.Ids.Length > 0)
            {
                hm.ItemIds.Clear();
                var parsedIds = hs.Ids
                    .Select(id => int.TryParse(id, out var n) ? n : (int?)null)
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value)
                    .Distinct()
                    .OrderBy(x => x);
                hm.ItemIds.AddRange(parsedIds);
            }

            result.Add(hm);
        }

        return result;
    }

    public static bool IsCustomSet(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return false;

        var archiveUrl = assetUrls.FirstOrDefault(u =>
            u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(archiveUrl))
            return false;

        try
        {
            var fileName = Path.GetFileName(new Uri(archiveUrl).LocalPath);
            return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            var lastSlash = archiveUrl.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < archiveUrl.Length - 1)
            {
                var fileName = archiveUrl.Substring(lastSlash + 1);
                return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public static bool IsCustomSet(Dictionary<string, List<string>>? sets, string skinName)
    {
        if (sets == null || string.IsNullOrWhiteSpace(skinName))
            return false;
        if (!sets.TryGetValue(skinName, out var urls))
            return false;
        return IsCustomSet(urls);
    }

    public enum SkinCategory
    {
        LegacySet,
        CustomSet,
        Persona,
        Item,
        BaseHero,
        Prismatic
    }

    public static SkinCategory ClassifySet(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return SkinCategory.LegacySet;

        var archiveFileName = ExtractArchiveFileName(assetUrls);
        if (string.IsNullOrEmpty(archiveFileName))
            return SkinCategory.LegacySet;

        if (archiveFileName.StartsWith("prismatic_", StringComparison.OrdinalIgnoreCase))
            return SkinCategory.Prismatic;
        if (archiveFileName.StartsWith("persona_", StringComparison.OrdinalIgnoreCase))
            return SkinCategory.Persona;
        if (archiveFileName.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
            return SkinCategory.Item;
        if (archiveFileName.StartsWith("base_", StringComparison.OrdinalIgnoreCase))
            return SkinCategory.BaseHero;
        if (archiveFileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase))
            return SkinCategory.CustomSet;

        return SkinCategory.LegacySet;
    }

    public static SkinCategory ClassifySet(Dictionary<string, List<string>>? sets, string setName)
    {
        if (sets == null || string.IsNullOrWhiteSpace(setName))
            return SkinCategory.LegacySet;
        if (!sets.TryGetValue(setName, out var urls))
            return SkinCategory.LegacySet;
        return ClassifySet(urls);
    }

    public static bool IsPrismaticSet(List<string>? assetUrls) => ClassifySet(assetUrls) == SkinCategory.Prismatic;

    public static string? ExtractItemTag(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return null;

        var archiveFileName = ExtractArchiveFileName(assetUrls);
        if (string.IsNullOrEmpty(archiveFileName))
            return null;

        var basename = Path.GetFileNameWithoutExtension(archiveFileName);
        if (!basename.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
            return null;

        var afterPrefix = basename.Substring(5);
        if (string.IsNullOrEmpty(afterPrefix))
            return null;

        var underscoreIdx = afterPrefix.IndexOf('_');
        return underscoreIdx > 0
            ? afterPrefix.Substring(0, underscoreIdx).ToLowerInvariant()
            : afterPrefix.ToLowerInvariant();
    }

    private static string? ExtractArchiveFileName(List<string> assetUrls)
    {
        var archiveUrl = assetUrls.FirstOrDefault(u =>
            u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(archiveUrl))
            return null;

        try
        {
            return Path.GetFileName(new Uri(archiveUrl).LocalPath);
        }
        catch
        {
            var lastSlash = archiveUrl.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < archiveUrl.Length - 1)
                return archiveUrl.Substring(lastSlash + 1);
            return null;
        }
    }

    public static string FormatHeroIdAsName(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            return string.Empty;

        var name = heroId.Replace("npc_dota_hero_", "").Replace("_", " ");
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}
