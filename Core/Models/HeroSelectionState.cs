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
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models;

/// <summary>
/// Structured per-hero selection state for the Skin Selector.
/// Tracks independently selectable layers: one set (Legacy, Custom, OR Persona),
/// multiple items, and one base hero override.
/// When the selected set is a Persona, Items and Base are blocked by the frontend.
/// </summary>
public class HeroSelectionState
{
    /// <summary>
    /// Index of the selected Legacy, Custom, or Persona set (mutually exclusive).
    /// Null means no set is selected.
    /// </summary>
    [JsonPropertyName("set")]
    public int? SetIndex { get; set; }

    /// <summary>
    /// Indices of active item entries (multi-select, slot-validated at build time).
    /// </summary>
    [JsonPropertyName("items")]
    public List<int> ItemIndices { get; set; } = new();

    /// <summary>
    /// Index of the selected Base Hero override entry.
    /// Only one base hero can be active at a time. Null means none.
    /// </summary>
    [JsonPropertyName("base")]
    public int? BaseIndex { get; set; }

    /// <summary>
    /// Returns true if any selection (set, items, or base) is active.
    /// </summary>
    [JsonIgnore]
    public bool HasAnySelection =>
        SetIndex.HasValue || ItemIndices.Count > 0 || BaseIndex.HasValue;
}
