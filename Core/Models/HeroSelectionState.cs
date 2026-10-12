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

public class HeroSelectionState
{
    [JsonPropertyName("set")]
    public int? SetIndex { get; set; }

    [JsonPropertyName("items")]
    public List<int> ItemIndices { get; set; } = new();

    [JsonPropertyName("base")]
    public int? BaseIndex { get; set; }

    [JsonPropertyName("prismatic")]
    public int? PrismaticIndex { get; set; }

    [JsonPropertyName("effects")]
    public List<int> EffectIndices { get; set; } = new();

    [JsonPropertyName("particles")]
    public List<ParticlePick> Particles { get; set; } = new();

    [JsonPropertyName("stems")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SelectionStems? Stems { get; set; }

    [JsonIgnore]
    public bool HasAnySelection =>
        SetIndex.HasValue || ItemIndices.Count > 0 || BaseIndex.HasValue || PrismaticIndex.HasValue
        || (EffectIndices?.Count ?? 0) > 0;
}

public sealed class ParticlePick
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("host")] public string Host { get; set; } = string.Empty;
}

public class SelectionStems
{
    [JsonPropertyName("set")] public string? Set { get; set; }
    [JsonPropertyName("items")] public List<string> Items { get; set; } = new();
    [JsonPropertyName("base")] public string? Base { get; set; }
    [JsonPropertyName("prismatic")] public string? Prismatic { get; set; }
    [JsonPropertyName("effects")] public List<string> Effects { get; set; } = new();
}
