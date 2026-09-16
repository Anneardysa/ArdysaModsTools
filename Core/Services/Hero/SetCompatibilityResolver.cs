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

public static class SetCompatibilityResolver
{
    private static readonly IReadOnlyList<int> None = Array.Empty<int>();

    public static IReadOnlyList<int>[] BuildConflictMap(
        IReadOnlyList<KeyValuePair<string, List<string>>> orderedSets,
        IReadOnlyList<SetIncompatibilityRule>? rules,
        Action<string>? warn = null)
    {
        var count = orderedSets?.Count ?? 0;
        var result = new IReadOnlyList<int>[count];
        for (var i = 0; i < count; i++) result[i] = None;

        if (count == 0 || rules == null || rules.Count == 0)
            return result;

        var byStem = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; i++)
        {
            var stem = HeroModelMapper.ExtractArchiveStem(orderedSets![i].Value);
            if (string.IsNullOrEmpty(stem))
                continue;
            if (!byStem.TryGetValue(stem!, out var list))
                byStem[stem!] = list = new List<int>();
            list.Add(i);
        }

        var pairs = new HashSet<int>[count];
        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Link(int a, int b)
        {
            if (a == b) return;
            (pairs[a] ??= new HashSet<int>()).Add(b);
            (pairs[b] ??= new HashSet<int>()).Add(a);
        }

        List<int> Resolve(string[]? stems)
        {
            var acc = new List<int>();
            if (stems == null) return acc;
            foreach (var raw in stems)
            {
                var stem = raw?.Trim();
                if (string.IsNullOrEmpty(stem)) continue;
                if (byStem.TryGetValue(stem!, out var hits)) acc.AddRange(hits);
                else unresolved.Add(stem!);
            }
            return acc;
        }

        foreach (var rule in rules)
        {
            if (rule == null) continue;
            var left = Resolve(rule.Sets);
            var right = Resolve(rule.With);

            if (right.Count == 0)
            {
                for (var i = 0; i < left.Count; i++)
                    for (var j = i + 1; j < left.Count; j++)
                        Link(left[i], left[j]);
            }
            else
            {
                foreach (var a in left)
                    foreach (var b in right)
                        Link(a, b);
            }
        }

        if (unresolved.Count > 0 && warn != null)
            warn($"heroes.json 'incompatible' names {unresolved.Count} archive(s) this hero does not have: {string.Join(", ", unresolved.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))}");

        for (var i = 0; i < count; i++)
        {
            if (pairs[i] == null || pairs[i].Count == 0) continue;
            var sorted = pairs[i].ToList();
            sorted.Sort();
            result[i] = sorted;
        }

        return result;
    }
}
