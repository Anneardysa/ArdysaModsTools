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
                    : "universal"
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
