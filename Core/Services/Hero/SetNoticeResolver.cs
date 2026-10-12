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
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Services;

public static class SetNoticeResolver
{
    public static IReadOnlyList<SetNotice>?[] Resolve(
        IReadOnlyList<KeyValuePair<string, List<string>>> orderedSets,
        IReadOnlyDictionary<string, List<SetNotice>>? notices,
        Action<string>? warn = null)
    {
        var count = orderedSets?.Count ?? 0;
        var result = new IReadOnlyList<SetNotice>?[count];
        if (count == 0 || notices == null || notices.Count == 0)
            return result;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; i++)
        {
            var stem = HeroModelMapper.ExtractArchiveStem(orderedSets![i].Value);
            if (string.IsNullOrEmpty(stem) || !notices.TryGetValue(stem!, out var list) || list == null || list.Count == 0)
                continue;
            result[i] = list;
            used.Add(stem!);
        }

        var unused = notices.Keys.Where(k => !used.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        if (unused.Count > 0 && warn != null)
            warn($"heroes.json 'notices' names {unused.Count} archive(s) this hero does not have: {string.Join(", ", unused)}");

        return result;
    }
}
