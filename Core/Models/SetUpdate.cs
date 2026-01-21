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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents a single set update entry (hero + set that was added).
    /// </summary>
    public record SetUpdateEntry
    {
        /// <summary>
        /// The hero's internal ID (e.g., "npc_dota_hero_juggernaut").
        /// </summary>
        public string HeroId { get; init; } = "";
        
        /// <summary>
        /// The name of the set that was added.
        /// </summary>
        public string SetName { get; init; } = "";
    }

    /// <summary>
    /// Represents a batch of updates on a specific date.
    /// </summary>
    public record SetUpdateBatch
    {
        /// <summary>
        /// The date when these sets were added (YYYY-MM-DD format).
        /// </summary>
        public DateTime Date { get; init; }
        
        /// <summary>
        /// List of sets added on this date.
        /// </summary>
        public List<SetUpdateEntry> Sets { get; init; } = new();
    }

    /// <summary>
    /// Root structure for set_updates.json.
    /// </summary>
    public class SetUpdatesData
    {
        /// <summary>
        /// Version of the updates file format.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Last time this file was updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// List of update batches, ordered by date (newest first).
        /// </summary>
        public List<SetUpdateBatch> Updates { get; set; } = new();

        /// <summary>
        /// Get all recent updates within the specified number of days.
        /// </summary>
        public List<(string HeroId, string SetName, DateTime AddedDate)> GetRecentUpdates(int daysBack = 30)
        {
            var cutoff = DateTime.Now.AddDays(-daysBack);
            var result = new List<(string, string, DateTime)>();
            
            foreach (var batch in Updates)
            {
                if (batch.Date >= cutoff)
                {
                    foreach (var entry in batch.Sets)
                    {
                        result.Add((entry.HeroId, entry.SetName, batch.Date));
                    }
                }
            }
            
            return result;
        }
    }
}
