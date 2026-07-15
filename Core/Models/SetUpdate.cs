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
    public record SetUpdateEntry
    {
        public string HeroId { get; init; } = "";
        
        public string SetName { get; init; } = "";
    }

    public record SetUpdateBatch
    {
        public DateTime Date { get; init; }
        
        public List<SetUpdateEntry> Sets { get; init; } = new();
    }

    public class SetUpdatesData
    {
        public string Version { get; set; } = "1.0.0";
        
        public DateTime LastUpdated { get; set; }
        
        public List<SetUpdateBatch> Updates { get; set; } = new();

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
