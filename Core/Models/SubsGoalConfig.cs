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
namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Configuration for YouTube subscriber goal display.
    /// Fetched from R2 CDN: config/subs_goal.json
    /// </summary>
    public class SubsGoalConfig
    {
        /// <summary>Current subscriber count.</summary>
        public int CurrentSubs { get; set; }
        
        /// <summary>Goal subscriber count.</summary>
        public int GoalSubs { get; set; }
        
        /// <summary>YouTube channel URL to open when clicked.</summary>
        public string ChannelUrl { get; set; } = "https://youtube.com/@ArdysaMods";
        
        /// <summary>Whether to show the subscriber goal section.</summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Calculate progress percentage (0-100).
        /// </summary>
        public double ProgressPercent => GoalSubs > 0 
            ? Math.Min(100.0, (double)CurrentSubs / GoalSubs * 100.0) 
            : 0;
    }
}
