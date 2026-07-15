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

public class SupportGoalsConfig
{
    public KofiGoal KofiGoal { get; set; } = new();

    public SubsGoal SubsGoal { get; set; } = new();
}

public class KofiGoal
{
    public string Title { get; set; } = "Donation Goal";

    public int ProgressPercent { get; set; }

    public int GoalAmount { get; set; }

    public int RaisedAmount { get; set; }

    public string GoalUrl { get; set; } = "https://ko-fi.com/ardysa/goal?g=0";

    public bool Enabled { get; set; } = true;
}

public class SubsGoal
{
    public int CurrentSubs { get; set; }

    public int GoalSubs { get; set; }

    public string ChannelUrl { get; set; } = "https://youtube.com/@ArdysaMods";

    public bool Enabled { get; set; } = true;

    public double ProgressPercent => GoalSubs > 0
        ? Math.Min(100.0, (double)CurrentSubs / GoalSubs * 100.0)
        : 0;
}
