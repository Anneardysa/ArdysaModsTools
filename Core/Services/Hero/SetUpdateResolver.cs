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
using System.Linq;
using System.Text.Json.Serialization;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Services;

public sealed record SetUpdateCard(
    [property: JsonPropertyName("heroId")] string HeroId,
    [property: JsonPropertyName("heroName")] string HeroName,
    [property: JsonPropertyName("heroThumbnail")] string? HeroThumbnail,
    [property: JsonPropertyName("setName")] string SetName,
    [property: JsonPropertyName("setIndex")] int SetIndex,
    [property: JsonPropertyName("setThumbnail")] string SetThumbnail,
    [property: JsonPropertyName("addedDate")] string AddedDate,
    [property: JsonPropertyName("daysAgo")] int DaysAgo);

public static class SetUpdateResolver
{
    public static List<SetUpdateCard> Resolve(
        IReadOnlyList<HeroModel> heroes,
        IReadOnlyList<(string HeroId, string SetName, DateTime AddedDate)> updates,
        DateTime? now = null)
    {
        var clock = now ?? DateTime.Now;
        var cards = new List<SetUpdateCard>();
        if (heroes == null || updates == null)
            return cards;

        foreach (var u in updates)
        {
            var bareId = u.HeroId.Replace("npc_dota_hero_", "");
            var hero = heroes.FirstOrDefault(h =>
                string.Equals(h.Id, u.HeroId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h.Id, $"npc_dota_hero_{bareId}", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h.Id?.Replace("npc_dota_hero_", ""), bareId, StringComparison.OrdinalIgnoreCase));
            if (hero?.Sets == null)
                continue;

            var setEntry = hero.Sets
                .Select((kvp, idx) => new { Name = kvp.Key, Files = kvp.Value, Index = idx })
                .FirstOrDefault(s => string.Equals(s.Name, u.SetName, StringComparison.OrdinalIgnoreCase));

            var setThumbnail = setEntry?.Files?.FirstOrDefault(f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
            if (setEntry == null || setThumbnail == null)
                continue;

            cards.Add(new SetUpdateCard(
                HeroId: u.HeroId,
                HeroName: hero.DisplayName ?? hero.Name ?? HeroModelMapper.FormatHeroIdAsName(u.HeroId),
                HeroThumbnail: HeroPortraitUrl(hero),
                SetName: u.SetName,
                SetIndex: setEntry.Index,
                SetThumbnail: setThumbnail,
                AddedDate: u.AddedDate.ToString("yyyy-MM-dd"),
                DaysAgo: (int)(clock - u.AddedDate).TotalDays));
        }

        return cards;
    }

    public static string HeroPortraitUrl(HeroModel hero)
    {
        var heroName = hero.Name?.Replace("npc_dota_hero_", "") ?? "";
        return $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{heroName}.png";
    }
}
