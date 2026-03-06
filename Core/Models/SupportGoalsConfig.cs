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
namespace ArdysaModsTools.Core.Models;

/// <summary>
/// Combined configuration for Support dialog goals.
/// Fetched from R2 CDN: config/support_goals.json
/// </summary>
public class SupportGoalsConfig
{
    /// <summary>Ko-fi donation goal configuration.</summary>
    public KofiGoal KofiGoal { get; set; } = new();

    /// <summary>YouTube subscriber goal configuration.</summary>
    public SubsGoal SubsGoal { get; set; } = new();
}

/// <summary>
/// Ko-fi donation goal data.
/// </summary>
public class KofiGoal
{
    /// <summary>Goal title (e.g. "Monthly Server").</summary>
    public string Title { get; set; } = "Donation Goal";

    /// <summary>Progress percentage (0-100).</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Goal target amount in dollars.</summary>
    public int GoalAmount { get; set; }

    /// <summary>Amount raised so far in dollars.</summary>
    public int RaisedAmount { get; set; }

    /// <summary>URL to the Ko-fi goal page.</summary>
    public string GoalUrl { get; set; } = "https://ko-fi.com/ardysa/goal?g=0";

    /// <summary>Whether the goal section should be displayed.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// YouTube subscriber goal data.
/// </summary>
public class SubsGoal
{
    /// <summary>Current subscriber count.</summary>
    public int CurrentSubs { get; set; }

    /// <summary>Goal subscriber count.</summary>
    public int GoalSubs { get; set; }

    /// <summary>YouTube channel URL.</summary>
    public string ChannelUrl { get; set; } = "https://youtube.com/@ArdysaMods";

    /// <summary>Whether the goal section should be displayed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Calculate progress percentage (0-100).
    /// </summary>
    public double ProgressPercent => GoalSubs > 0
        ? Math.Min(100.0, (double)CurrentSubs / GoalSubs * 100.0)
        : 0;
}
