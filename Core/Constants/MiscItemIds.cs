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

namespace ArdysaModsTools.Core.Constants
{
    public static class MiscItemIds
    {
        public static readonly IReadOnlySet<string> Defaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "202",
            "555",
            "586",
            "587",
            "588",
            "590",
            "595",
            "596",
            "660",
            "661",
            "677",
            "678",
            "679",
            "680",
            "801",
            "1026",
            "11173",
            "12970",
            "34462",
            "34463"
        };

        public static readonly IReadOnlyDictionary<string, (string DisplayName, string[] Ids)> SlotExclusive =
            new Dictionary<string, (string DisplayName, string[] Ids)>(StringComparer.OrdinalIgnoreCase)
            {
                ["multikill_banner"] = ("Kill Banner", new[] { "10731", "11548" }),
                ["tormentor"] = ("Tormentor", new[] { "29339", "29588", "34576" }),
            };
    }
}
