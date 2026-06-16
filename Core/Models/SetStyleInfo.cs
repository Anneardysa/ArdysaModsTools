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
namespace ArdysaModsTools.Models
{
    /// <summary>
    /// UI-only grouping metadata for a flattened style entry in a hero's <c>Sets</c> map.
    /// A "styled" set/item is authored in heroes.json as an object with a <c>styles</c> map;
    /// each style is flattened into its own normal set entry keyed <c>"{Group} ({Label})"</c>,
    /// while this record carries the original group + style label so the Skin Selector can
    /// re-group the flat entries into a single Style Card.
    ///
    /// This metadata is consumed by the WebView2 UI only. The generation/download pipeline is
    /// unaware of styles — it keys off the flat set name exactly as for non-styled sets.
    /// </summary>
    public sealed class SetStyleInfo
    {
        /// <summary>Display name of the style group (the authored set name, e.g. "Manifold Paradox").</summary>
        public string Group { get; init; } = string.Empty;

        /// <summary>Label of this individual style within the group (e.g. "Corrupted").</summary>
        public string Label { get; init; } = string.Empty;
    }
}
