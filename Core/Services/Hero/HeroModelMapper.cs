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

/// <summary>
/// Single source of truth for mapping <see cref="HeroSummary"/> to <see cref="HeroModel"/>.
/// Also contains shared hero-related utility methods used across forms and presenters.
/// </summary>
public static class HeroModelMapper
{
    /// <summary>
    /// Maps a list of <see cref="HeroSummary"/> into a list of <see cref="HeroModel"/>.
    /// Replaces all duplicate mapping logic across SelectHero, HeroGalleryForm, and SelectHeroPresenter.
    /// </summary>
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

            // Copy sets (dictionary of setName → asset URLs)
            if (hs.Sets != null && hs.Sets.Count > 0)
            {
                hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in hs.Sets)
                {
                    hm.Sets[kvp.Key] = kvp.Value?.ToList() ?? new List<string>();
                }
            }

            // Copy style-grouping metadata (flat set key → group/label) for the Skin Selector.
            if (hs.SetStyles != null && hs.SetStyles.Count > 0)
            {
                hm.SetStyles = new Dictionary<string, SetStyleInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in hs.SetStyles)
                {
                    if (kvp.Value != null)
                        hm.SetStyles[kvp.Key] = kvp.Value;
                }
            }

            // Parse item IDs from string array → int list
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

    /// <summary>
    /// Determines if a set is a "Custom Set" by checking whether its archive filename starts with "mix_".
    /// Checks zip/rar URLs in the set's asset list.
    /// </summary>
    public static bool IsCustomSet(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return false;

        // Find the first archive URL
        var archiveUrl = assetUrls.FirstOrDefault(u =>
            u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(archiveUrl))
            return false;

        // Extract filename and check for "mix_" prefix
        try
        {
            var fileName = Path.GetFileName(new Uri(archiveUrl).LocalPath);
            return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Fallback: parse URL string directly
            var lastSlash = archiveUrl.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < archiveUrl.Length - 1)
            {
                var fileName = archiveUrl.Substring(lastSlash + 1);
                return fileName.StartsWith("mix_", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    /// <summary>
    /// Determines if a named set within a hero's sets dictionary is a custom set.
    /// Convenience overload used by HeroRow.
    /// </summary>
    public static bool IsCustomSet(Dictionary<string, List<string>>? sets, string skinName)
    {
        if (sets == null || string.IsNullOrWhiteSpace(skinName))
            return false;
        if (!sets.TryGetValue(skinName, out var urls))
            return false;
        return IsCustomSet(urls);
    }

    /// <summary>
    /// Classification categories for hero skin entries.
    /// Determined by archive filename prefix in the set's asset URLs.
    /// </summary>
    public enum SkinCategory
    {
        /// <summary>Default category — set name does NOT start with mix_, item_, persona_, or base_.</summary>
        LegacySet,
        /// <summary>Archive filename starts with "mix_".</summary>
        CustomSet,
        /// <summary>Archive filename starts with "persona_".</summary>
        Persona,
        /// <summary>Archive filename starts with "item_".</summary>
        Item,
        /// <summary>Archive filename starts with "base_".</summary>
        BaseHero
    }

    /// <summary>
    /// Classifies a set entry based on the archive filename prefix in its asset URLs.
    /// Rules: item_ → Item, base_ → BaseHero, mix_ → CustomSet, else → LegacySet.
    /// </summary>
    public static SkinCategory ClassifySet(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return SkinCategory.LegacySet;

        var archiveFileName = ExtractArchiveFileName(assetUrls);
        if (string.IsNullOrEmpty(archiveFileName))
            return SkinCategory.LegacySet;

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

    /// <summary>
    /// Classifies a named set within a hero's sets dictionary.
    /// </summary>
    public static SkinCategory ClassifySet(Dictionary<string, List<string>>? sets, string setName)
    {
        if (sets == null || string.IsNullOrWhiteSpace(setName))
            return SkinCategory.LegacySet;
        if (!sets.TryGetValue(setName, out var urls))
            return SkinCategory.LegacySet;
        return ClassifySet(urls);
    }

    /// <summary>Returns true if the set is a Persona (archive filename starts with "persona_").</summary>
    public static bool IsPersonaSet(List<string>? assetUrls) => ClassifySet(assetUrls) == SkinCategory.Persona;

    /// <summary>Returns true if the set is an Item (archive filename starts with "item_").</summary>
    public static bool IsItemSet(List<string>? assetUrls) => ClassifySet(assetUrls) == SkinCategory.Item;

    /// <summary>Returns true if the set is a Base Hero (archive filename starts with "base_").</summary>
    public static bool IsBaseHeroSet(List<string>? assetUrls) => ClassifySet(assetUrls) == SkinCategory.BaseHero;

    /// <summary>
    /// Extracts the slot tag from an Item set's archive filename.
    /// Format: item_{tag}_{name}[_{N}].zip → returns the tag segment.
    /// Example: "item_shoulder_pauldrons_1.zip" → "shoulder".
    /// Returns null for non-item sets or if no tag segment is found.
    /// </summary>
    public static string? ExtractItemTag(List<string>? assetUrls)
    {
        if (assetUrls == null || assetUrls.Count == 0)
            return null;

        var archiveFileName = ExtractArchiveFileName(assetUrls);
        if (string.IsNullOrEmpty(archiveFileName))
            return null;

        // Strip extension to get basename
        var basename = Path.GetFileNameWithoutExtension(archiveFileName);
        if (!basename.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
            return null;

        // Strip "item_" prefix → "shoulder_pauldrons_1"
        var afterPrefix = basename.Substring(5);
        if (string.IsNullOrEmpty(afterPrefix))
            return null;

        // First segment before '_' is the tag
        var underscoreIdx = afterPrefix.IndexOf('_');
        return underscoreIdx > 0
            ? afterPrefix.Substring(0, underscoreIdx).ToLowerInvariant()
            : afterPrefix.ToLowerInvariant();
    }

    /// <summary>
    /// Extracts the archive filename from a set's asset URL list.
    /// Looks for .zip, .rar, or .zip.001 URLs and returns just the filename.
    /// </summary>
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

    /// <summary>
    /// Formats a hero ID like "npc_dota_hero_crystal_maiden" to "Crystal Maiden".
    /// Used as fallback display name when no localized name is available.
    /// </summary>
    public static string FormatHeroIdAsName(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            return string.Empty;

        var name = heroId.Replace("npc_dota_hero_", "").Replace("_", " ");
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}
